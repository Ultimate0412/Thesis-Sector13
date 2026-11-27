using System;
using System.Collections;
using UnityEngine;

public class PlayerCombatSystem : MonoBehaviour
{
    [Header("Combat Settings")]
    public float hitCheckRadius = 0.35f;
    public LayerMask hitLayers = ~0;

    [Header("Animation Settings")]
    public string attackTriggerName = "BatonAttack";

    [Header("Debug")]
    public bool enableCombatLogs = true;

    private PlayerEquipment equipment;
    private PlayerStats playerStats;
    private Camera playerCam;
    private AudioSource audioSource;
    private float nextAttackTime = 0f;
    private bool isSwinging = false;
    private bool attackTriggerValid;

    private void Awake()
    {
        equipment = GetComponent<PlayerEquipment>();
        playerStats = GetComponent<PlayerStats>();
        playerCam = GetComponentInChildren<Camera>();
        if (playerCam == null) playerCam = Camera.main;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f;
        }
    }

    private void Start()
    {
        Animator targetAnimator = GetActiveAnimator();
        attackTriggerValid = HasParameter(targetAnimator, attackTriggerName);
    }

    private Animator GetActiveAnimator()
    {
        if (equipment != null && equipment.playerAnimator != null)
            return equipment.playerAnimator;

        return playerStats != null ? playerStats.playerAnimator : null;
    }

    private static bool HasParameter(Animator animator, string paramName)
    {
        if (animator == null || string.IsNullOrEmpty(paramName)) return false;
        var parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
            if (parameters[i].name == paramName) return true;
        return false;
    }

    private void Update()
    {
        if (playerStats != null && playerStats.IsDead) return;
        if (InspectionTablet.Instance != null && InspectionTablet.Instance.IsTabletHeld) return;

        bool isMenuOpen = (StampUIController.Instance != null && StampUIController.Instance.IsMenuOpen) ||
                          (DailySummaryUI.Instance != null && DailySummaryUI.Instance.IsSummaryOpen);
        if (isMenuOpen) return;

        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            TryAttack();
        }
    }

    private void TryAttack()
    {
        if (Time.time < nextAttackTime || isSwinging) return;

        if (equipment == null || !equipment.IsDrawn || equipment.EquippedWeapon == null)
        {
            return;
        }

        ExecuteAttack(equipment.EquippedWeapon);
    }

    private void ExecuteAttack(MeleeWeapon weapon)
    {
        nextAttackTime = Time.time + weapon.attackCooldown;

        if (equipment != null)
        {
            equipment.PlayAttackAnimation();
        }
        else
        {
            Animator targetAnimator = GetActiveAnimator();
            if (attackTriggerValid && targetAnimator != null)
            {
                targetAnimator.SetTrigger(attackTriggerName);
            }
        }

        if (weapon.swingSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(weapon.swingSound, 0.7f);
        }

        if (equipment == null || !equipment.useRigAnimationPose)
        {
            StartCoroutine(SwingWeaponRoutine(weapon));
        }

        if (playerCam == null)
        {
            if (enableCombatLogs)
                Debug.LogWarning("[PlayerCombat] ไม่พบ Camera สำหรับยิงระยะตี");
            return;
        }

        Ray ray = new Ray(playerCam.transform.position, playerCam.transform.forward);
        RaycastHit[] hits = Physics.SphereCastAll(
            ray,
            hitCheckRadius,
            weapon.attackRange,
            hitLayers,
            QueryTriggerInteraction.Collide
        );

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        RaycastHit validHit = default;
        bool hasValidHit = false;

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == null) continue;
            if (hits[i].collider.transform.IsChildOf(transform)) continue;

            validHit = hits[i];
            hasValidHit = true;
            break;
        }

        if (!hasValidHit)
        {
            if (enableCombatLogs)
            {
                Debug.Log($"<color=#AAAAAA>[PlayerCombat]</color> ตีวืด (ระยะ {weapon.attackRange:F2}m, รัศมี {hitCheckRadius:F2}m)");
            }
            return;
        }

        EnemyBase enemy = validHit.collider.GetComponentInParent<EnemyBase>();
        if (enemy == null) enemy = validHit.collider.GetComponent<EnemyBase>();

        if (enemy != null)
        {
            if (enemy.IsDead)
            {
                if (enableCombatLogs)
                    Debug.Log($"<color=#FFAA00>[PlayerCombat]</color> ตีโดนศัตรู <b>{enemy.name}</b> แต่ตายไปแล้ว");
                return;
            }

            if (enemy.IsStunned)
            {
                if (enableCombatLogs)
                    Debug.Log($"<color=#FFAA00>[PlayerCombat]</color> ตีโดนศัตรู <b>{enemy.name}</b> แต่ติดสตั๊นอยู่แล้ว (Stun: {enemy.CurrentStunGauge:F0}/{enemy.maxStunGauge:F0})");
                return;
            }

            float beforeStun = enemy.CurrentStunGauge;
            enemy.TakeStunDamage(weapon.stunDamagePerHit, validHit.point, playerCam.transform.forward);

            if (enableCombatLogs)
            {
                Debug.Log(
                    $"<color=#00FF88>[PlayerCombat]</color> ผู้เล่นตีโดนศัตรู <b>{enemy.name}</b> ด้วย {weapon.name} | " +
                    $"Stun +{weapon.stunDamagePerHit:F0} ({beforeStun:F0} -> {enemy.CurrentStunGauge:F0}/{enemy.maxStunGauge:F0}) | " +
                    $"ระยะ {validHit.distance:F2}m"
                );
            }

            if (weapon.hitEnemySound != null && audioSource != null)
            {
                audioSource.PlayOneShot(weapon.hitEnemySound, 0.9f);
            }
            return;
        }

        MimicPackage mimic = validHit.collider.GetComponentInParent<MimicPackage>();
        if (mimic != null && mimic.enabled)
        {
            mimic.OnAttackedByWeapon(weapon);

            if (enableCombatLogs)
            {
                Debug.Log($"<color=#FF66FF>[PlayerCombat]</color> ผู้เล่นตีโดน Mimic <b>{mimic.name}</b> (ระยะ {validHit.distance:F2}m)");
            }
        }
        else if (enableCombatLogs)
        {
            string hitLayerName = LayerMask.LayerToName(validHit.collider.gameObject.layer);
            Debug.Log(
                $"<color=#FFDD55>[PlayerCombat]</color> ตีโดนวัตถุอื่น: <b>{validHit.collider.name}</b> " +
                $"(Layer: {hitLayerName}, ระยะ {validHit.distance:F2}m)"
            );
        }

        if (weapon.hitWallSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(weapon.hitWallSound, 0.6f);
        }
    }

    private IEnumerator SwingWeaponRoutine(MeleeWeapon weapon)
    {
        isSwinging = true;

        Transform weaponTransform = equipment != null ? equipment.WeaponTransform : null;
        if (weaponTransform == null)
        {
            isSwinging = false;
            yield break;
        }

        if (equipment != null) equipment.PoseControlSuspended = true;

        Vector3 startPos = equipment != null ? equipment.drawnLocalPosition : Vector3.zero;
        Quaternion startRot = equipment != null
            ? Quaternion.Euler(equipment.drawnLocalRotation)
            : Quaternion.identity;

        Vector3 targetPos = startPos + weapon.swingPositionOffset;
        Quaternion targetRot = startRot * Quaternion.Euler(weapon.swingRotationOffset);

        float halfDuration = weapon.attackCooldown * 0.35f;
        float elapsed = 0f;

        while (elapsed < halfDuration && equipment != null && equipment.IsDrawn)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            weaponTransform.localPosition = Vector3.Lerp(startPos, targetPos, t);
            weaponTransform.localRotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < halfDuration && equipment != null && equipment.IsDrawn)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            weaponTransform.localPosition = Vector3.Lerp(targetPos, startPos, t);
            weaponTransform.localRotation = Quaternion.Slerp(targetRot, startRot, t);
            yield return null;
        }

        weaponTransform.localPosition = startPos;
        weaponTransform.localRotation = startRot;

        if (equipment != null) equipment.PoseControlSuspended = false;

        isSwinging = false;
    }
}

// --- สรุปการทำงานของโค้ด ---
// 1. เพิ่มตัวแปร enableCombatLogs ใน Inspector เพื่อเปิดหรือปิดการแสดง Log การโจมตีทั้งหมดใน Console
// 2. เปลี่ยนจากการใช้ Physics.SphereCast เป็น Physics.SphereCastAll แล้วเรียงตามระยะใกล้สุดพร้อมข้าม Collider ที่เป็นลูกของ Player เพื่อไม่ให้ฟาดไปติด Collider ของตัวผู้เล่นหรืออาวุธเอง
// 3. เมื่อตีโดนศัตรู (EnemyBase) จะพิมพ์ Log สีเขียวแสดงชื่อศัตรู อาวุธ ค่า Stun Damage เกจสตั๊นก่อน-หลังตี และระยะห่าง รวมถึงแยก Log สีส้มกรณีศัตรูติดสตั๊นหรือตายอยู่แล้ว
// 4. กรณีตีโดน Mimic วัตถุทั่วไป หรือตีวืด จะพิมพ์ Log พร้อมชื่อ Collider, Layer และระยะห่าง เพื่อให้ไล่เช็กได้ทันทีหากตีศัตรูไม่เข้า
using System.Collections;
using UnityEngine;

/// <summary>
/// ระบบการโจมตีระยะประชิดของผู้เล่น
/// ผู้เล่นจะต้องชักอาวุธ (MeleeWeapon ที่ติดตัวผ่าน PlayerEquipment) อยู่จึงจะกดคลิกซ้ายโจมตีได้
/// </summary>
public class PlayerCombatSystem : MonoBehaviour
{
    [Header("Combat Settings")]
    [Tooltip("รัศมีการตรวจสอบการโดนเป้าหมาย (SphereCast radius)")]
    public float hitCheckRadius = 0.35f;

    [Tooltip("เลเยอร์ของศัตรู")]
    public LayerMask hitLayers = ~0;

    [Header("Animation Settings")]
    [Tooltip("ชื่อ Trigger สำหรับท่าโจมตี (Attack)")]
    public string attackTriggerName = "Attack";

    private PlayerEquipment equipment;
    private PlayerStats playerStats;
    private Camera playerCam;
    private AudioSource audioSource;
    private float nextAttackTime = 0f;
    private bool isSwinging = false;

    private void Awake()
    {
        equipment = GetComponent<PlayerEquipment>();
        playerStats = GetComponent<PlayerStats>();
        playerCam = GetComponentInChildren<Camera>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f; // 2D sound สำหรับผู้เล่น
        }
    }

    private void Update()
    {
        // หากผู้เล่นตายแล้ว จะโจมตีไม่ได้
        if (playerStats != null && playerStats.IsDead)
        {
            return;
        }

        // หากผู้เล่นกำลังเปิดแท็บเล็ตอยู่ จะโจมตีไม่ได้
        if (InspectionTablet.Instance != null && InspectionTablet.Instance.IsTabletHeld)
        {
            return;
        }

        // หากกำลังเปิดเมนู Stamp หรือ Daily Summary หรือเคอร์เซอร์อยู่บน UI จะไม่โจมตี
        bool isMenuOpen = (StampUIController.Instance != null && StampUIController.Instance.IsMenuOpen) ||
                          (DailySummaryUI.Instance != null && DailySummaryUI.Instance.IsSummaryOpen);
        if (isMenuOpen) return;

        if (UnityEngine.EventSystems.EventSystem.current != null && 
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        // ตรวจสอบการคลิกซ้ายโจมตี (LMB)
        if (Input.GetMouseButtonDown(0))
        {
            TryAttack();
        }
    }

    private void TryAttack()
    {
        if (Time.time < nextAttackTime || isSwinging) return;

        // ต้องชักอาวุธที่ติดตัวอยู่ก่อนจึงจะโจมตีได้
        if (equipment == null || !equipment.IsDrawn || equipment.EquippedWeapon == null)
        {
            return;
        }

        // ทำการโจมตี
        ExecuteAttack(equipment.EquippedWeapon);
    }

    private void ExecuteAttack(MeleeWeapon weapon)
    {
        nextAttackTime = Time.time + weapon.attackCooldown;

        // สั่งเล่นแอนิเมชันโจมตีหากมีการกำหนด Animator
        if (playerStats != null && playerStats.playerAnimator != null && !string.IsNullOrEmpty(attackTriggerName))
        {
            playerStats.playerAnimator.SetTrigger(attackTriggerName);
        }

        // เล่นเสียงเหวี่ยงอาวุธ
        if (weapon.swingSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(weapon.swingSound, 0.7f);
        }

        // เล่น Procedural Swing Animation ของอาวุธ
        StartCoroutine(SwingWeaponRoutine(weapon));

        // ตรวจจับการโจมตีโดนเป้าหมาย (SphereCast ข้างหน้ากล้อง)
        Ray ray = new Ray(playerCam.transform.position, playerCam.transform.forward);
        RaycastHit hit;

        if (Physics.SphereCast(ray, hitCheckRadius, out hit, weapon.attackRange, hitLayers))
        {
            // ตรวจสอบว่าโดนศัตรูหรือไม่
            EnemyBase enemy = hit.collider.GetComponentInParent<EnemyBase>();
            if (enemy == null) enemy = hit.collider.GetComponent<EnemyBase>();

            if (enemy != null && !enemy.IsDead)
            {
                enemy.TakeStunDamage(weapon.stunDamagePerHit, hit.point, playerCam.transform.forward);

                if (weapon.hitEnemySound != null && audioSource != null)
                {
                    audioSource.PlayOneShot(weapon.hitEnemySound, 0.9f);
                }
            }
            else
            {
                // ตรวจสอบว่าตีโดนกล่องที่เป็น Mimic ปลอมตัวอยู่หรือไม่
                MimicPackage mimic = hit.collider.GetComponentInParent<MimicPackage>();
                if (mimic != null && mimic.enabled)
                {
                    mimic.OnAttackedByWeapon(weapon);
                }

                // ตีโดนกำแพงหรือสิ่งของทั่วไป
                if (weapon.hitWallSound != null && audioSource != null)
                {
                    audioSource.PlayOneShot(weapon.hitWallSound, 0.6f);
                }
            }
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

        // ระหว่างแกว่ง ให้ระบบ equipment หยุดจัดท่า ไม่งั้นจะถูกดึงกลับที่เดิมทุกเฟรม
        if (equipment != null) equipment.PoseControlSuspended = true;

        Vector3 startPos = equipment != null ? equipment.drawnLocalPosition : Vector3.zero;
        Quaternion startRot = equipment != null
            ? Quaternion.Euler(equipment.drawnLocalRotation)
            : Quaternion.identity;

        Vector3 targetPos = startPos + weapon.swingPositionOffset;
        Quaternion targetRot = startRot * Quaternion.Euler(weapon.swingRotationOffset);

        float halfDuration = weapon.attackCooldown * 0.35f;
        float elapsed = 0f;

        // ฟาดออกไปข้างหน้า
        while (elapsed < halfDuration && equipment != null && equipment.IsDrawn)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            weaponTransform.localPosition = Vector3.Lerp(startPos, targetPos, t);
            weaponTransform.localRotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        elapsed = 0f;
        // ดึงกลับตำแหน่งเดิม
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

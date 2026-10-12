using System.Collections;
using UnityEngine;

/// <summary>
/// ระบบการโจมตีระยะประชิดของผู้เล่น
/// ผู้เล่นจะต้องถืออาวุธ (เช่น MeleeWeapon) อยู่ในมือจึงจะสามารถกดคลิกซ้ายโจมตีได้
/// </summary>
public class PlayerCombatSystem : MonoBehaviour
{
    [Header("Combat Settings")]
    [Tooltip("รัศมีการตรวจสอบการโดนเป้าหมาย (SphereCast radius)")]
    public float hitCheckRadius = 0.35f;

    [Tooltip("เลเยอร์ของศัตรู")]
    public LayerMask hitLayers = ~0;

    private PlayerPickupSystem pickupSystem;
    private Camera playerCam;
    private AudioSource audioSource;
    private float nextAttackTime = 0f;
    private bool isSwinging = false;

    private void Awake()
    {
        pickupSystem = GetComponent<PlayerPickupSystem>();
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

        // ตรวจสอบว่าในมือถืออาวุธ MeleeWeapon อยู่หรือไม่
        if (pickupSystem == null || pickupSystem.heldObject == null)
        {
            return; // มือเปล่า ไม่สามารถโจมตีได้
        }

        MeleeWeapon weapon = pickupSystem.heldObject.GetComponent<MeleeWeapon>();
        if (weapon == null) weapon = pickupSystem.heldObject.GetComponentInParent<MeleeWeapon>();

        if (weapon == null)
        {
            return; // สิ่งที่ถืออยู่ไม่ใช่อาวุธ (เช่น เป็นกล่องพัสดุทั่วไป)
        }

        // ทำการโจมตี
        ExecuteAttack(weapon);
    }

    private void ExecuteAttack(MeleeWeapon weapon)
    {
        nextAttackTime = Time.time + weapon.attackCooldown;

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
        Transform weaponTransform = pickupSystem.heldObject.transform;

        Vector3 startPos = Vector3.zero;
        Quaternion startRot = Quaternion.identity;

        Vector3 targetPos = weapon.swingPositionOffset;
        Quaternion targetRot = Quaternion.Euler(weapon.swingRotationOffset);

        float halfDuration = weapon.attackCooldown * 0.35f;
        float elapsed = 0f;

        // ฟาดออกไปข้างหน้า
        while (elapsed < halfDuration && pickupSystem.heldObject != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            weaponTransform.localPosition = Vector3.Lerp(startPos, targetPos, t);
            weaponTransform.localRotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        elapsed = 0f;
        // ดึงกลับตำแหน่งเดิม
        while (elapsed < halfDuration && pickupSystem.heldObject != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            weaponTransform.localPosition = Vector3.Lerp(targetPos, startPos, t);
            weaponTransform.localRotation = Quaternion.Slerp(targetRot, startRot, t);
            yield return null;
        }

        if (pickupSystem.heldObject != null)
        {
            weaponTransform.localPosition = Vector3.zero;
            weaponTransform.localRotation = Quaternion.identity;
        }

        isSwinging = false;
    }
}

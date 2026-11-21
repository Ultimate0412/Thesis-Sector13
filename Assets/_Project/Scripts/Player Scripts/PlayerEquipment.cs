using UnityEngine;

/// <summary>
/// ระบบอุปกรณ์ติดตัวผู้เล่น
/// อาวุธถูกติดตั้งไว้กับตัวผู้เล่นตั้งแต่เริ่มเกม (ไม่ต้องเดินไปหยิบในฉาก)
/// กดปุ่มเพื่อชัก/เก็บ โดยใช้ pattern เดียวกับ InspectionTablet:
/// - ถือได้ทีละอย่าง: กดปุ่มขณะถือของอยู่ จะวางของที่ถือลงพื้นแล้วชักอาวุธแทน
/// - กดปุ่มขณะถือแท็บเล็ตอยู่ จะเก็บแท็บเล็ตแล้วชักอาวุธแทน
/// - หยิบของขึ้นมือขณะชักอาวุธอยู่ จะเก็บอาวุธให้อัตโนมัติ
/// </summary>
public class PlayerEquipment : MonoBehaviour
{
    public static PlayerEquipment Instance { get; private set; }

    [Header("Input")]
    [Tooltip("ปุ่มชัก/เก็บอาวุธ")]
    public KeyCode toggleKey = KeyCode.Alpha2;

    [Header("Weapon Setup")]
    [Tooltip("Prefab อาวุธที่จะติดตั้งให้ผู้เล่นตั้งแต่เริ่มเกม")]
    public GameObject weaponPrefab;

    [Tooltip("จุดยึดอาวุธ (ถ้าไม่ใส่จะใช้ holdPosition ของ PlayerPickupSystem)")]
    public Transform weaponSlot;

    [Header("Draw / Holster Pose (local to slot)")]
    public Vector3 drawnLocalPosition = Vector3.zero;
    public Vector3 drawnLocalRotation = Vector3.zero;
    public Vector3 holsteredLocalPosition = new Vector3(0f, -0.6f, 0.1f);
    public Vector3 holsteredLocalRotation = new Vector3(-70f, 0f, 0f);
    public float transitionSpeed = 14f;

    private PlayerPickupSystem pickupSystem;
    private GameObject weaponInstance;
    private MeleeWeapon equippedWeapon;
    private bool isDrawn = false;

    public MeleeWeapon EquippedWeapon => equippedWeapon;
    public Transform WeaponTransform => weaponInstance != null ? weaponInstance.transform : null;
    public bool IsDrawn => isDrawn;
    public bool HasWeapon => equippedWeapon != null;

    /// <summary>ให้ระบบอื่น (เช่น แอนิเมชันแกว่งอาวุธ) ควบคุม transform ชั่วคราว</summary>
    public bool PoseControlSuspended { get; set; } = false;

    private void Awake()
    {
        Instance = this;

        if (pickupSystem == null) pickupSystem = GetComponent<PlayerPickupSystem>();
        if (weaponSlot == null && pickupSystem != null) weaponSlot = pickupSystem.holdPosition;

        EnsureWeapon();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        HandleInput();
        UpdateWeaponTransform();
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(toggleKey)) ToggleWeapon();

        // หยิบของขึ้นมือขณะชักอาวุธอยู่ → เก็บอาวุธอัตโนมัติ (แบบเดียวกับแท็บเล็ต)
        if (isDrawn && pickupSystem != null && pickupSystem.heldObject != null)
            ForceHolster();
    }

    public void ToggleWeapon()
    {
        if (!isDrawn)
        {
            // ถือได้ทีละอย่าง: เก็บแท็บเล็ตก่อน (ถ้าถืออยู่)
            if (InspectionTablet.Instance != null) InspectionTablet.Instance.ForceHolster();

            // ถ้ามือถือของอยู่ ให้วางของลงพื้นก่อนแล้วชักอาวุธแทน
            if (pickupSystem != null && pickupSystem.heldObject != null)
            {
                if (!pickupSystem.PutAwayHeldObject())
                {
                    Debug.LogWarning("[PlayerEquipment] วางของที่ถืออยู่ไม่สำเร็จ ไม่สามารถชักอาวุธได้");
                    return;
                }
            }

            DrawWeapon();
        }
        else
        {
            HolsterWeapon();
        }
    }

    public void DrawWeapon()
    {
        if (!EnsureWeapon()) return;

        isDrawn = true;
        weaponInstance.SetActive(true);
    }

    public void HolsterWeapon()
    {
        isDrawn = false;
    }

    public void ForceHolster()
    {
        if (isDrawn) HolsterWeapon();
    }

    /// <summary>สร้าง/ผูกอาวุธเข้ากับจุดยึดให้พร้อมใช้ (เรียกซ้ำได้ ปลอดภัย)</summary>
    private bool EnsureWeapon()
    {
        if (weaponInstance != null) return true;

        if (weaponSlot == null)
        {
            Debug.LogError("[PlayerEquipment] ไม่มีจุดยึดอาวุธ (weaponSlot/holdPosition)");
            return false;
        }

        // ใช้ของที่มีอยู่แล้วใน slot ก่อน (เช่น วางไว้ใน prefab ผู้เล่น)
        MeleeWeapon existing = weaponSlot.GetComponentInChildren<MeleeWeapon>(true);
        if (existing != null)
        {
            equippedWeapon = existing;
            weaponInstance = existing.gameObject;
        }
        else
        {
            if (weaponPrefab == null)
            {
                Debug.LogError("[PlayerEquipment] ไม่ได้กำหนด weaponPrefab");
                return false;
            }

            weaponInstance = Instantiate(weaponPrefab, weaponSlot);
            equippedWeapon = weaponInstance.GetComponent<MeleeWeapon>();
            if (equippedWeapon == null)
                equippedWeapon = weaponInstance.GetComponentInChildren<MeleeWeapon>(true);
        }

        if (equippedWeapon == null)
        {
            Debug.LogError($"[PlayerEquipment] '{weaponInstance.name}' ไม่มี MeleeWeapon");
            return false;
        }

        weaponInstance.transform.SetParent(weaponSlot, false);
        weaponInstance.transform.localPosition = holsteredLocalPosition;
        weaponInstance.transform.localRotation = Quaternion.Euler(holsteredLocalRotation);
        weaponInstance.SetActive(false);
        return true;
    }

    private void UpdateWeaponTransform()
    {
        if (PoseControlSuspended) return;
        if (weaponInstance == null || !weaponInstance.activeSelf) return;

        Vector3 targetPos = isDrawn ? drawnLocalPosition : holsteredLocalPosition;
        Quaternion targetRot = Quaternion.Euler(isDrawn ? drawnLocalRotation : holsteredLocalRotation);

        Transform t = weaponInstance.transform;
        t.localPosition = Vector3.Lerp(t.localPosition, targetPos, Time.deltaTime * transitionSpeed);
        t.localRotation = Quaternion.Slerp(t.localRotation, targetRot, Time.deltaTime * transitionSpeed);

        // เก็บจนสุดทางแล้วค่อยปิด object
        if (!isDrawn && Vector3.Distance(t.localPosition, holsteredLocalPosition) < 0.02f)
            weaponInstance.SetActive(false);
    }
}

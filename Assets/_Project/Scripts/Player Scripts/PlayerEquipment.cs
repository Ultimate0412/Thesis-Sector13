using UnityEngine;

public class PlayerEquipment : MonoBehaviour
{
    public static PlayerEquipment Instance { get; private set; }

    [Header("Input")]
    public KeyCode toggleKey = KeyCode.Alpha1;

    [Header("Weapon Setup")]
    public GameObject weaponPrefab;
    public Transform weaponSlot;

    [Header("Audio Settings")]
    public AudioSource audioSource;
    public AudioClip drawWeaponSound;

    [Header("Animator Settings")]
    public Animator playerAnimator;
    public string batonEquippedBoolParam = "IsBatonEquipped";
    public string batonAttackTriggerParam = "BatonAttack";
    public bool useRigAnimationPose = true;

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
    private bool equippedParamValid = false;
    private bool attackParamValid = false;

    public MeleeWeapon EquippedWeapon => equippedWeapon;
    public Transform WeaponTransform => weaponInstance != null ? weaponInstance.transform : null;
    public bool IsDrawn => isDrawn;
    public bool HasWeapon => equippedWeapon != null;
    public bool PoseControlSuspended { get; set; } = false;

    private void Awake()
    {
        Instance = this;

        if (pickupSystem == null) pickupSystem = GetComponent<PlayerPickupSystem>();
        if (weaponSlot == null && pickupSystem != null) weaponSlot = pickupSystem.holdPosition;

        ResolveAnimator();

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f;
        }

        EnsureWeapon();
    }

    private void Start()
    {
        ValidateAnimatorParameters();
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

    private void ResolveAnimator()
    {
        if (playerAnimator != null) return;

        PlayerStats stats = GetComponent<PlayerStats>();
        if (stats != null && stats.playerAnimator != null)
        {
            playerAnimator = stats.playerAnimator;
            return;
        }

        playerAnimator = GetComponentInChildren<Animator>();
    }

    private void ValidateAnimatorParameters()
    {
        equippedParamValid = HasParameter(playerAnimator, batonEquippedBoolParam);
        attackParamValid = HasParameter(playerAnimator, batonAttackTriggerParam);
    }

    private static bool HasParameter(Animator animator, string paramName)
    {
        if (animator == null || string.IsNullOrEmpty(paramName)) return false;
        var parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == paramName) return true;
        }
        return false;
    }

    private void HandleInput()
    {
        if (Time.timeScale <= 0f) return;

        if (Input.GetKeyDown(toggleKey)) ToggleWeapon();

        if (isDrawn && pickupSystem != null && pickupSystem.heldObject != null)
            ForceHolster();
    }

    public void ToggleWeapon()
    {
        if (!isDrawn)
        {
            if (InspectionTablet.Instance != null) InspectionTablet.Instance.ForceHolster();

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

        if (useRigAnimationPose)
        {
            weaponInstance.transform.localPosition = drawnLocalPosition;
            weaponInstance.transform.localRotation = Quaternion.Euler(drawnLocalRotation);
        }

        SetBatonEquippedAnim(true);

        if (drawWeaponSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(drawWeaponSound);
        }
    }

    public void HolsterWeapon()
    {
        isDrawn = false;
        SetBatonEquippedAnim(false);

        if (useRigAnimationPose && weaponInstance != null)
        {
            weaponInstance.SetActive(false);
        }
    }

    public void ForceHolster()
    {
        if (isDrawn) HolsterWeapon();
    }

    public void PlayAttackAnimation()
    {
        if (!isDrawn || playerAnimator == null || !attackParamValid) return;
        playerAnimator.SetTrigger(batonAttackTriggerParam);
    }

    private void SetBatonEquippedAnim(bool equipped)
    {
        if (playerAnimator == null || !equippedParamValid) return;
        playerAnimator.SetBool(batonEquippedBoolParam, equipped);
    }

    private bool EnsureWeapon()
    {
        if (weaponInstance != null) return true;

        if (weaponSlot == null)
        {
            Debug.LogError("[PlayerEquipment] ไม่มีจุดยึดอาวุธ (weaponSlot/holdPosition)");
            return false;
        }

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
        if (useRigAnimationPose || PoseControlSuspended) return;
        if (weaponInstance == null || !weaponInstance.activeSelf) return;

        Vector3 targetPos = isDrawn ? drawnLocalPosition : holsteredLocalPosition;
        Quaternion targetRot = Quaternion.Euler(isDrawn ? drawnLocalRotation : holsteredLocalRotation);

        Transform t = weaponInstance.transform;
        t.localPosition = Vector3.Lerp(t.localPosition, targetPos, Time.deltaTime * transitionSpeed);
        t.localRotation = Quaternion.Slerp(t.localRotation, targetRot, Time.deltaTime * transitionSpeed);

        if (!isDrawn && Vector3.Distance(t.localPosition, holsteredLocalPosition) < 0.02f)
            weaponInstance.SetActive(false);
    }
}
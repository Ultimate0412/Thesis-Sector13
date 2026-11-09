using System;
using UnityEngine;

public class PlayerStats : LivingEntity
{
    [Header("HP Legacy Aliases")]
    public float maxHP
    {
        get => maxHealth;
        set => maxHealth = value;
    }
    public float currentHP => currentHealth;

    [Header("Damage & Invincibility Settings (i-Frames)")]
    [Tooltip("ระยะเวลาอมตะหลังถูกโจมตี (วินาที) ป้องกันการโดนดาเมจรัวซ้ำซ้อน")]
    public float damageCooldown = 0.8f;
    private float lastDamageTime = -999f;
    public bool IsInvincible => Time.time < lastDamageTime + damageCooldown;

    [Header("Animation Settings (ช่องสำหรับใส่ Animator และชื่อท่าทางต่างๆ)")]
    [Tooltip("Animator ของผู้เล่น (หากเว้นว่างไว้จะค้นหาในตัวหรือวัตถุลูกให้อัตโนมัติ)")]
    public Animator playerAnimator;

    [Tooltip("ชื่อ Trigger เมื่อได้รับความเสียหาย (Take Damage / Hurt)")]
    public string hurtTriggerName = "TakeDamage";

    [Tooltip("ชื่อ Trigger เมื่อตาย (Die)")]
    public string dieTriggerName = "Die";

    [Tooltip("ชื่อ Parameter ชนิด Bool สถานะตาย (IsDead)")]
    public string isDeadBoolName = "IsDead";

    [Tooltip("ชื่อ Trigger เมื่อเกิดใหม่ (Respawn)")]
    public string respawnTriggerName = "Respawn";

    [Tooltip("ชื่อ Parameter ชนิด Bool เมื่อเลือดต่ำวิกฤต (Low HP / Injured)")]
    public string isLowHealthBoolName = "IsInjured";

    [Header("Audio Feedback")]
    [Tooltip("เสียงเมื่อได้รับความเสียหาย")]
    public AudioClip hurtSound;
    [Tooltip("เสียงเมื่อตาย")]
    public AudioClip deathSound;
    [Tooltip("เสียงเมื่อเกิดใหม่")]
    public AudioClip respawnSound;

    [Header("Stamina Settings")]
    public float maxStamina = 100f;
    private float currentStamina;
    public float staminaDrainRate = 20f; // อัตราการลดต่อวินาทีตอนวิ่ง
    public float staminaRegenRate = 15f; // อัตราการฟื้นฟูต่อวินาที

    [Header("Weight Capacity Settings")]
    public float maxWeightCapacity = 50f;
    private float currentWeight = 0f;

    // Events สำหรับแจ้งเตือนเมื่อค่าต่างๆ เปลี่ยนแปลง
    public event Action<float, float> OnHPChanged;          // Current, Max
    public event Action<float> OnDamageTaken;                // Damage Amount
    public event Action OnPlayerDeath;
    public event Action OnPlayerRespawned;

    public event Action<float, float> OnStaminaChanged;     // Current, Max
    public event Action<float, float> OnWeightChanged;      // Current, Max

    public bool IsExhausted { get; private set; } = false;

    private Vector3 initialSpawnPosition;
    private Quaternion initialSpawnRotation;
    private AudioSource audioSource;

    protected override void Awake()
    {
        base.Awake();
        currentStamina = maxStamina;

        if (playerAnimator == null)
        {
            playerAnimator = GetComponent<Animator>();
            if (playerAnimator == null) playerAnimator = GetComponentInChildren<Animator>();
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f; // 2D Sound สำหรับผู้เล่น
        }
    }

    private void Start()
    {
        initialSpawnPosition = transform.position;
        initialSpawnRotation = transform.rotation;

        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.OnDayStarted += HandleDayStarted;
        }

        // แจ้งเตือนค่าเริ่มต้น
        OnHPChanged?.Invoke(currentHealth, maxHealth);
    }

    private void OnDestroy()
    {
        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.OnDayStarted -= HandleDayStarted;
        }
    }

    private void HandleDayStarted(int newDay)
    {
        // เริ่มวันใหม่ ฟื้นฟูเลือดเต็ม 100 เสมอ
        Heal(maxHealth);
    }

    public void ModifyHP(float amount)
    {
        if (amount < 0f)
        {
            TakeDamage(-amount, transform.position, Vector3.zero);
        }
        else
        {
            Heal(amount);
        }
    }

    public override void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (isDead) return;

        // ตรวจสอบ i-Frames
        if (IsInvincible)
        {
            return;
        }

        lastDamageTime = Time.time;

        base.TakeDamage(amount, hitPoint, hitDirection);
        OnHPChanged?.Invoke(currentHealth, maxHealth);
        OnDamageTaken?.Invoke(amount);

        // เล่นเสียงร้อง/เจ็บ
        if (hurtSound != null && audioSource != null && !isDead)
        {
            audioSource.PlayOneShot(hurtSound);
        }

        // เล่นแอนิเมชันโดนดาเมจ
        PlayHurtAnimation();

        // อัปเดตสถานะเลือดต่ำ
        UpdateLowHealthAnimation(currentHealth <= maxHealth * 0.25f);
    }

    public override void Heal(float amount)
    {
        base.Heal(amount);
        OnHPChanged?.Invoke(currentHealth, maxHealth);
        UpdateLowHealthAnimation(currentHealth <= maxHealth * 0.25f);
    }

    public void ModifyStamina(float amount)
    {
        currentStamina = Mathf.Clamp(currentStamina + amount, 0, maxStamina);
        OnStaminaChanged?.Invoke(currentStamina, maxStamina);

        if (currentStamina <= 0) IsExhausted = true;
        if (currentStamina >= maxStamina * 0.3f) IsExhausted = false; // ปลดล็อคเมื่อฟื้นถึง 30%
    }

    public bool AddWeight(float weightToAdd)
    {
        if (currentWeight + weightToAdd > maxWeightCapacity)
        {
            Debug.LogWarning("Weight limit exceeded!");
            return false; // หยิบไม่/น้ำหนักเกิน
        }
        currentWeight += weightToAdd;
        OnWeightChanged?.Invoke(currentWeight, maxWeightCapacity);
        return true;
    }

    public void RemoveWeight(float weightToRemove)
    {
        currentWeight = Mathf.Max(0f, currentWeight - weightToRemove);
        OnWeightChanged?.Invoke(currentWeight, maxWeightCapacity);
    }

    protected override void Die()
    {
        base.Die();
        Debug.Log("<color=#FF2222>[PlayerStats]</color> ผู้เล่นเสียชีวิต!");

        // 1. ปลดล็อกเคอร์เซอร์เมาส์
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 2. ปล่อยของที่ถืออยู่ (Drop Item)
        PlayerPickupSystem pickup = GetComponent<PlayerPickupSystem>();
        if (pickup != null && pickup.heldObject != null)
        {
            pickup.ForceDropObject(pickup.heldObject);
        }

        // 3. ปิดแท็บเล็ตตรวจพัสดุ
        if (InspectionTablet.Instance != null)
        {
            InspectionTablet.Instance.ForceHolster();
        }

        // 4. เล่นเสียงและแอนิเมชันตาย
        if (deathSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(deathSound);
        }
        PlayDeathAnimation();

        // 5. แจ้งเตือนเหตุการณ์ผู้เล่นตาย
        OnPlayerDeath?.Invoke();
    }

    /// <summary>
    /// ฟื้นคืนชีพผู้เล่น (Respawn) ณ ตำแหน่งที่ระบุ (หรือจุดเริ่มต้นหากไม่ระบุ)
    /// </summary>
    public void Respawn(Vector3? targetPosition = null)
    {
        isDead = false;
        currentHealth = maxHealth;

        Vector3 spawnPos = targetPosition.HasValue ? targetPosition.Value : initialSpawnPosition;
        transform.position = spawnPos;
        transform.rotation = initialSpawnRotation;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // ล็อกเคอร์เซอร์เมาส์กลับเข้าเกม
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // เล่นเสียงและแอนิเมชันเกิดใหม่
        if (respawnSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(respawnSound);
        }
        PlayRespawnAnimation();
        UpdateLowHealthAnimation(false);

        // แจ้งเตือนเหตุการณ์
        base.Heal(0f); // เรียกเพื่อทริกเกอร์ OnHealthChanged ใน LivingEntity
        OnHPChanged?.Invoke(currentHealth, maxHealth);
        OnPlayerRespawned?.Invoke();

        Debug.Log("<color=#55FF88>[PlayerStats]</color> ผู้เล่นเกิดใหม่เรียบร้อย เลือดเต็ม 100");
    }

    #region Animation Helpers

    public void PlayHurtAnimation()
    {
        if (playerAnimator != null && !string.IsNullOrEmpty(hurtTriggerName))
        {
            playerAnimator.SetTrigger(hurtTriggerName);
        }
    }

    public void PlayDeathAnimation()
    {
        if (playerAnimator != null)
        {
            if (!string.IsNullOrEmpty(dieTriggerName)) playerAnimator.SetTrigger(dieTriggerName);
            if (!string.IsNullOrEmpty(isDeadBoolName)) playerAnimator.SetBool(isDeadBoolName, true);
        }
    }

    public void PlayRespawnAnimation()
    {
        if (playerAnimator != null)
        {
            if (!string.IsNullOrEmpty(isDeadBoolName)) playerAnimator.SetBool(isDeadBoolName, false);
            if (!string.IsNullOrEmpty(respawnTriggerName)) playerAnimator.SetTrigger(respawnTriggerName);
        }
    }

    public void UpdateLowHealthAnimation(bool isLow)
    {
        if (playerAnimator != null && !string.IsNullOrEmpty(isLowHealthBoolName))
        {
            playerAnimator.SetBool(isLowHealthBoolName, isLow);
        }
    }

    #endregion
}

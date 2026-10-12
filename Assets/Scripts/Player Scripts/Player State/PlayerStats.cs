using UnityEngine;
using System;

public class PlayerStats : LivingEntity
{
    [Header("HP Legacy Aliases")]
    public float maxHP
    {
        get => maxHealth;
        set => maxHealth = value;
    }
    public float currentHP => currentHealth;

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
    public event Action<float, float> OnStaminaChanged;     // Current, Max
    public event Action<float, float> OnWeightChanged;      // Current, Max

    public bool IsExhausted { get; private set; } = false;

    protected override void Awake()
    {
        base.Awake();
        currentStamina = maxStamina;
    }

    private void Start()
    {
        // ค่า HP และ Stamina ได้รับการตั้งต้นเรียบร้อยแล้ว
    }

    private void Update()
    {
        // เรียกใช้การฟื้นฟู Stamina ข้างนอก State ได้ หรือจะจัดการผ่าน State ก็ได้
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
        base.TakeDamage(amount, hitPoint, hitDirection);
        OnHPChanged?.Invoke(currentHealth, maxHealth);
    }

    public override void Heal(float amount)
    {
        base.Heal(amount);
        OnHPChanged?.Invoke(currentHealth, maxHealth);
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
        Debug.Log("Player Died");
        // จัดการเมื่อผู้เล่นตาย
    }
}
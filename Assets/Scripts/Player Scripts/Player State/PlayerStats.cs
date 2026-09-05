using UnityEngine;
using System;

public class PlayerStats : MonoBehaviour
{
    [Header("HP Settings")]
    public float maxHP = 100f;
    private float currentHP;

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

    private void Start()
    {
        currentHP = maxHP;
        currentStamina = maxStamina;
    }

    private void Update()
    {
        // เรียกใช้การฟื้นฟู Stamina ข้างนอก State ได้ หรือจะจัดการผ่าน State ก็ได้
    }

    public void ModifyHP(float amount)
    {
        currentHP = Mathf.Clamp(currentHP + amount, 0, maxHP);
        OnHPChanged?.Invoke(currentHP, maxHP);
        if (currentHP <= 0) { Die(); }
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

    private void Die()
    {
        Debug.Log("Player Died");
        // จัดการเมื่อผู้เล่นตาย
    }
}
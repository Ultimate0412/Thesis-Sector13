using System;
using UnityEngine;

/// <summary>
/// Base class สำหรับสิ่งมีชีวิตทั้งหมดในเกม (Player, Enemies)
/// จัดการค่า HP พื้นฐาน, การรับความเสียหาย, และการตาย
/// </summary>
public abstract class LivingEntity : MonoBehaviour
{
    [Header("Living Entity Core Stats")]
    [Tooltip("ค่าพลังชีวิตสูงสุด")]
    public float maxHealth = 100f;
    [SerializeField] protected float currentHealth;

    public float CurrentHealth => currentHealth;
    public bool IsDead => isDead;

    protected bool isDead = false;

    // Events สำหรับแจ้งเตือนการเปลี่ยนแปลง
    public event Action<float, float> OnHealthChanged; // Current, Max
    public event Action<LivingEntity> OnDeath;

    protected virtual void Awake()
    {
        currentHealth = maxHealth;
    }

    /// <summary>
    /// รับความเสียหายจากแหล่งภายนอก
    /// </summary>
    /// <param name="amount">ปริมาณความเสียหาย</param>
    /// <param name="hitPoint">จุดที่ถูกโจมตี</param>
    /// <param name="hitDirection">ทิศทางแรงกระแทก</param>
    public virtual void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (isDead) return;

        currentHealth = Mathf.Clamp(currentHealth - amount, 0f, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    /// <summary>
    /// ฟื้นฟูพลังชีวิต
    /// </summary>
    public virtual void Heal(float amount)
    {
        if (isDead) return;

        currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>
    /// ทำงานเมื่อพลังชีวิตหมด (ตาย)
    /// </summary>
    protected virtual void Die()
    {
        if (isDead) return;
        isDead = true;
        OnDeath?.Invoke(this);
    }
}

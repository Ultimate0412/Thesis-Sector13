using System;
using UnityEngine;

/// <summary>
/// คลาสพื้นฐานสำหรับศัตรูทุกประเภท
/// ศัตรูจะไม่สามารถฆ่าได้โดยตรงจากการตีธรรมดา แต่จะมีเกจสตั๊น (Stun Gauge)
/// เมื่อเกจเต็มจะติดสถานะสตั๊น 3 วินาที ซึ่งเปิดโอกาสให้ผู้เล่นยกไปทิ้งจุดทำลาย
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public abstract class EnemyBase : LivingEntity
{
    [Header("Enemy Stun Settings")]
    [Tooltip("ค่าเกจสตั๊นสูงสุด (เมื่อเต็มจะติดสตั๊น)")]
    public float maxStunGauge = 100f;
    [SerializeField] protected float currentStunGauge = 0f;

    [Tooltip("ระยะเวลาการติดสตั๊น (วินาที)")]
    public float stunDuration = 3f;

    [Tooltip("อัตราการลดของเกจสตั๊นต่อวินาทีเมื่อไม่ได้ถูกโจมตี")]
    public float stunDecayRate = 8f;

    [Tooltip("เวลารอหลังจากถูกโจมตีก่อนที่เกจสตั๊นจะเริ่มลด (วินาที)")]
    public float stunDecayDelay = 2.5f;

    [Header("Enemy Weight / Pickup Settings")]
    [Tooltip("น้ำหนักของศัตรูเมื่อผู้เล่นยกถือ")]
    public float enemyWeight = 15f;

    [Header("Visual & Feedback")]
    [Tooltip("เอฟเฟกต์หรือมาร์กเกอร์แสดงสถานะสตั๊น (เช่น ดาวหมุนรอบหัว หรือไฟช็อต)")]
    public GameObject stunVfxObject;

    // สถานะปัจจุบัน
    public bool IsStunned => isStunned;
    public bool IsBeingCarried => isBeingCarried;
    public float CurrentStunGauge => currentStunGauge;

    protected bool isStunned = false;
    protected bool isBeingCarried = false;
    protected float currentStunTimer = 0f;
    private float lastHitTime = 0f;

    // การอ้างอิงถึงผู้เล่นที่กำลังถือศัตรูตัวนี้
    protected PlayerPickupSystem currentCarrier;
    protected Rigidbody rb;
    protected Collider mainCollider;

    public event Action<float, float> OnStunGaugeChanged; // Current, Max
    public event Action OnStunEnter;
    public event Action OnStunExit;

    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody>();
        mainCollider = GetComponent<Collider>();
    }

    protected virtual void Update()
    {
        if (isDead) return;

        // หากติดสตั๊นอยู่ ให้นับเวลาถอยหลัง
        if (isStunned)
        {
            currentStunTimer -= Time.deltaTime;
            if (currentStunTimer <= 0f)
            {
                ExitStunState();
            }
        }
        else
        {
            // หากไม่ได้ติดสตั๊น ให้เกจสตั๊นค่อยๆ ลดลงเมื่อผ่านช่วง Delay
            if (currentStunGauge > 0f && Time.time - lastHitTime > stunDecayDelay)
            {
                currentStunGauge = Mathf.Max(0f, currentStunGauge - (stunDecayRate * Time.deltaTime));
                OnStunGaugeChanged?.Invoke(currentStunGauge, maxStunGauge);
            }
        }
    }

    /// <summary>
    /// รับความเสียหายสตั๊นจากการโจมตีของผู้เล่น
    /// </summary>
    public virtual void TakeStunDamage(float stunAmount, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (isDead || isStunned) return;

        lastHitTime = Time.time;
        currentStunGauge = Mathf.Clamp(currentStunGauge + stunAmount, 0f, maxStunGauge);
        OnStunGaugeChanged?.Invoke(currentStunGauge, maxStunGauge);

        Debug.Log($"[{gameObject.name}] รับความเสียหายสตั๊น +{stunAmount:F0} (ปัจจุบัน: {currentStunGauge:F0}/{maxStunGauge})");

        if (currentStunGauge >= maxStunGauge)
        {
            EnterStunState();
        }
    }

    /// <summary>
    /// เข้าสู่สถานะสตั๊น (3 วินาที) สามารถถูกยกได้
    /// </summary>
    public virtual void EnterStunState()
    {
        isStunned = true;
        currentStunTimer = stunDuration;
        currentStunGauge = maxStunGauge;

        if (stunVfxObject != null) stunVfxObject.SetActive(true);

        OnStunEnter?.Invoke();
        Debug.Log($"[{gameObject.name}] ติดสถานะ STUNNED! (นาน {stunDuration} วิ) ผู้เล่นสามารถยกไปทิ้งได้แล้ว!");
    }

    /// <summary>
    /// หลุดออกจากสถานะสตั๊น
    /// </summary>
    public virtual void ExitStunState()
    {
        isStunned = false;
        currentStunGauge = 0f;
        currentStunTimer = 0f;

        if (stunVfxObject != null) stunVfxObject.SetActive(false);

        // หากผู้เล่นกำลังถือตัวศัตรูอยู่ ให้หลุดจากมือผู้เล่นอัตโนมัติ
        if (isBeingCarried && currentCarrier != null)
        {
            BreakFreeFromPlayer();
        }

        OnStunExit?.Invoke();
        Debug.Log($"[{gameObject.name}] หลุดจากสถานะสตั๊นแล้ว! กลับมาโจมตีต่อ");
    }

    /// <summary>
    /// ทำงานเมื่อผู้เล่นยกศัตรูตัวนี้ขึ้นมาถือ
    /// </summary>
    public virtual void OnPickedUpByPlayer(PlayerPickupSystem carrier)
    {
        isBeingCarried = true;
        currentCarrier = carrier;

        if (rb != null)
        {
            rb.isKinematic = true;
        }
    }

    /// <summary>
    /// ทำงานเมื่อผู้เล่นขว้างหรือโยนศัตรูตัวนี้ออกไป
    /// </summary>
    public virtual void OnThrownByPlayer(Vector3 throwForce)
    {
        isBeingCarried = false;
        currentCarrier = null;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.AddForce(throwForce, ForceMode.Impulse);
        }
    }

    /// <summary>
    /// สลัดหลุดจากมือผู้เล่นอัตโนมัติเมื่อหมดเวลาสตั๊น
    /// </summary>
    public virtual void BreakFreeFromPlayer()
    {
        if (currentCarrier != null)
        {
            currentCarrier.ForceDropObject(gameObject);
            currentCarrier = null;
        }

        isBeingCarried = false;

        if (rb != null)
        {
            rb.isKinematic = false;
            // แรงผลักเล็กน้อยตอนสลัดหลุด
            rb.AddForce(Vector3.up * 2f + UnityEngine.Random.insideUnitSphere * 1.5f, ForceMode.Impulse);
        }

        Debug.LogWarning($"[{gameObject.name}] สลัดหลุดจากมือผู้เล่นสำเร็จ!");
    }

    /// <summary>
    /// ถูกทำลายเมื่อสัมผัสจุดทำลายทิ้ง (Disposal Zone)
    /// </summary>
    public virtual void OnDisposed()
    {
        if (isDead) return;
        isDead = true;

        if (isBeingCarried && currentCarrier != null)
        {
            currentCarrier.ForceDropObject(gameObject);
        }

        Debug.Log($"[{gameObject.name}] ถูกทำลายทิ้งใน Disposal Zone สำเร็จ!");
        Destroy(gameObject);
    }
}

using System.Collections;
using UnityEngine;

/// <summary>
/// ศัตรูชนิด Hammer:
/// พฤติกรรม: สแกนหาผู้เล่น -> ล็อคเป้าและชาร์จเตรียมพุ่ง (Windup) -> วิ่งพุ่งชนเป็นเส้นตรงด้วยความเร็วสูง
/// หากชนผู้เล่นจะทำดาเมจ หากชนกำแพงจะหยุดชะงักเปิดโอกาสให้ผู้เล่นเข้าตี
/// </summary>
public class EnemyHammer : EnemyBase
{
    public enum HammerState
    {
        Searching,  // กำลังมองหาผู้เล่น
        Windup,     // ล็อคเป้าและเตรียมพุ่งชน (Telegraph)
        Charging,   // กำลังวิ่งพุ่งชนเป็นเส้นตรง
        Staggered,  // ชะงักหลังชนกำแพง
        Cooldown,   // พักก่อนเริ่มค้นหาใหม่
        Stunned,    // ติดสตั๊น (จาก EnemyBase)
        Carried     // กำลังถูกผู้เล่นยกถือ
    }

    [Header("Hammer Detection Settings")]
    [Tooltip("ระยะการตรวจจับสายตาหาผู้เล่น")]
    public float detectionRadius = 25f;
    [Tooltip("ความเร็วในการเดินค้นหา/เข้าหาผู้เล่น")]
    public float searchMoveSpeed = 3.5f;
    [Tooltip("ระยะเริ่มล็อคเป้าเพื่อพุ่งชน (เมื่อมี Line of Sight)")]
    public float chargeTriggerDistance = 14f;
    [Tooltip("เลเยอร์ของผู้เล่น")]
    public LayerMask playerLayer;
    [Tooltip("เลเยอร์ของสิ่งกีดขวาง/กำแพง")]
    public LayerMask obstacleLayer;

    [Header("Charge Attack Settings")]
    [Tooltip("เวลาเตรียมตัวก่อนพุ่งชน (Windup / เล็งเป้า)")]
    public float windupDuration = 0.8f;
    [Tooltip("ความเร็วในการพุ่งชนเป็นเส้นตรง")]
    public float chargeSpeed = 14f;
    [Tooltip("ระยะเวลาสูงสุดที่พุ่งชนได้ก่อนหยุด")]
    public float maxChargeTime = 2.0f;
    [Tooltip("ความเสียหายที่ทำต่อผู้เล่นเมื่อพุ่งชน")]
    public float chargeDamage = 25f;
    [Tooltip("แรงกระแทกผลักผู้เล่น")]
    public float knockbackForce = 10f;
    [Tooltip("เวลาหยุดชะงักหลังชนกำแพง")]
    public float wallStaggerDuration = 1.4f;
    [Tooltip("ระยะเวลาคูลดาวน์หลังพุ่งเสร็จ")]
    public float attackCooldown = 2.0f;

    [Header("Visual Indicators")]
    [Tooltip("วัตถุโมเดลส่วนหัว/ตา ที่จะเปลี่ยนสีเวลาเล็งหรือชาร์จ")]
    public Renderer indicatorRenderer;
    public Color normalEyeColor = Color.yellow;
    public Color chargingEyeColor = Color.red;

    public HammerState CurrentState => currentState;
    [SerializeField] private HammerState currentState = HammerState.Searching;

    private Transform playerTarget;
    private Vector3 lockedChargeDirection;
    private float stateTimer = 0f;
    private Coroutine chargeCoroutine;

    protected override void Awake()
    {
        base.Awake();
        if (obstacleLayer.value == 0)
        {
            obstacleLayer = LayerMask.GetMask("Default");
        }
    }

    private void Start()
    {
        FindPlayerReference();
        SetIndicatorColor(normalEyeColor);
    }

    protected override void Update()
    {
        base.Update();

        if (isDead) return;

        // หากติดสตั๊นหรือถูกยกถือ ให้ข้าม AI
        if (isStunned || isBeingCarried)
        {
            currentState = isBeingCarried ? HammerState.Carried : HammerState.Stunned;
            return;
        }

        switch (currentState)
        {
            case HammerState.Searching:
                HandleSearching();
                break;

            case HammerState.Staggered:
                stateTimer -= Time.deltaTime;
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
                if (stateTimer <= 0f)
                {
                    currentState = HammerState.Searching;
                }
                break;

            case HammerState.Cooldown:
                stateTimer -= Time.deltaTime;
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
                if (stateTimer <= 0f)
                {
                    currentState = HammerState.Searching;
                }
                break;
        }
    }

    private void FindPlayerReference()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTarget = playerObj.transform;
        }
    }

    private void HandleSearching()
    {
        if (playerTarget == null)
        {
            FindPlayerReference();
            return;
        }

        Vector3 toPlayer = playerTarget.position - transform.position;
        toPlayer.y = 0f; // สนใจเฉพาะระนาบพื้น
        float dist = toPlayer.magnitude;

        if (dist <= detectionRadius)
        {
            Vector3 moveDir = toPlayer.normalized;

            // หันหน้าเข้าหาผู้เล่นอย่างนุ่มนวล
            if (moveDir != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(moveDir),
                    Time.deltaTime * 6f
                );
            }

            bool hasClearLOS = HasLineOfSightToPlayer();

            if (hasClearLOS && dist <= chargeTriggerDistance)
            {
                // มองเห็นผู้เล่นตรงๆ และอยู่ในระยะพุ่ง -> หยุดเดินแล้วเริ่มชาร์จ
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
                StartWindupSequence();
            }
            else
            {
                // ยังมีสิ่งกีดขวางหรือยังอยู่นอกระยะพุ่ง -> เดินรุกคืบเข้าหาผู้เล่น
                Vector3 moveVelocity = moveDir * searchMoveSpeed;
                moveVelocity.y = rb.linearVelocity.y;
                rb.linearVelocity = moveVelocity;
            }
        }
        else
        {
            // อยู่นอกระยะตรวจจับ -> หยุดนิ่ง
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }
    }

    /// <summary>
    /// ตรวจสอบว่ามีสิ่งกีดขวางบังสายตาระหว่างตาศัตรูกับตัวผู้เล่นหรือไม่
    /// ข้ามตัว Collider ของตัวเอง และนับว่ามองเห็นหาก Ray ชนโดนตัวผู้เล่น
    /// </summary>
    private bool HasLineOfSightToPlayer()
    {
        if (playerTarget == null) return false;

        Vector3 eyePos = transform.position + Vector3.up * 1.0f;
        Vector3 targetEyePos = playerTarget.position + Vector3.up * 1.0f;
        Vector3 rayDir = targetEyePos - eyePos;
        float rayDist = rayDir.magnitude;

        if (rayDist < 0.2f) return true;

        RaycastHit[] hits = Physics.RaycastAll(eyePos, rayDir.normalized, rayDist, obstacleLayer);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            // ข้ามคอลไลเดอร์ของตัวเอง
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            // หากชนตัวผู้เล่นหรือส่วนประกอบของผู้เล่น แปลว่ามองเห็นผู้เล่นโดยตรง
            if (hit.transform == playerTarget || hit.transform.IsChildOf(playerTarget) || hit.collider.GetComponentInParent<PlayerController>() != null)
            {
                return true;
            }

            // หากชนสิ่งของหรือกำแพงที่เป็นของแข็ง (ไม่ใช่ Trigger)
            if (!hit.collider.isTrigger)
            {
                return false; // โดนสิ่งกีดขวางบังสายตา
            }
        }

        return true;
    }

    private void StartWindupSequence()
    {
        if (chargeCoroutine != null) StopCoroutine(chargeCoroutine);
        chargeCoroutine = StartCoroutine(ChargeRoutine());
    }

    private IEnumerator ChargeRoutine()
    {
        // 1. ขั้นตอน WINDUP (ล็อคทิศทางและเตือนผู้เล่น)
        currentState = HammerState.Windup;
        SetIndicatorColor(chargingEyeColor);

        // ล็อคทิศทางที่จะพุ่งตรง
        Vector3 aimDir = (playerTarget.position - transform.position);
        aimDir.y = 0f;
        lockedChargeDirection = aimDir.normalized;

        if (lockedChargeDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(lockedChargeDirection);
        }

        rb.linearVelocity = Vector3.zero;

        // สั่นกระตุกเตือนระหว่างง้าง
        float elapsedWindup = 0f;
        Vector3 origPos = transform.position;
        while (elapsedWindup < windupDuration)
        {
            if (isStunned || isDead) yield break;
            elapsedWindup += Time.deltaTime;
            yield return null;
        }

        // 2. ขั้นตอน CHARGING (พุ่งชนเป็นเส้นตรง)
        currentState = HammerState.Charging;
        float chargeElapsed = 0f;

        while (chargeElapsed < maxChargeTime && currentState == HammerState.Charging)
        {
            if (isStunned || isDead) yield break;

            chargeElapsed += Time.deltaTime;

            // เคลื่อนที่ด้วยความเร็วสูงไปตาม lockedChargeDirection
            Vector3 moveVelocity = lockedChargeDirection * chargeSpeed;
            moveVelocity.y = rb.linearVelocity.y; // รักษาระนาบแนวดิ่ง/แรงโน้มถ่วง
            rb.linearVelocity = moveVelocity;

            yield return new WaitForFixedUpdate();
        }

        // หากพุ่งจนหมดเวลาโดยไม่ชนอะไร
        EndCharge(HammerState.Cooldown, attackCooldown);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (currentState != HammerState.Charging) return;

        // 1. ตรวจสอบว่าชนผู้เล่นหรือไม่
        PlayerController player = collision.gameObject.GetComponentInParent<PlayerController>();
        if (player == null) player = collision.gameObject.GetComponent<PlayerController>();

        if (player != null)
        {
            LivingEntity targetLiving = player.GetComponent<LivingEntity>();
            if (targetLiving != null)
            {
                Vector3 hitPoint = collision.contactCount > 0 ? collision.contacts[0].point : transform.position;
                Vector3 hitDir = (player.transform.position - transform.position).normalized;
                targetLiving.TakeDamage(chargeDamage, hitPoint, hitDir);
            }

            // แรงผลักผู้เล่น
            Rigidbody playerRb = player.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                playerRb.AddForce(lockedChargeDirection * knockbackForce + Vector3.up * 2f, ForceMode.Impulse);
            }

            Debug.Log($"[{gameObject.name}] พุ่งชนโดนผู้เล่น! ทำดาเมจ {chargeDamage}");
            EndCharge(HammerState.Cooldown, attackCooldown);
            return;
        }

        // 2. ตรวจสอบว่าชนกำแพงหรือสิ่งกีดขวางหรือไม่
        // ถ้าหน้าสัมผัสทำมุมชัน (เป็นกำแพง)
        if (collision.contactCount > 0)
        {
            Vector3 normal = collision.contacts[0].normal;
            if (Vector3.Dot(normal, Vector3.up) < 0.5f) // ผนังแนวตั้ง
            {
                Debug.Log($"[{gameObject.name}] พุ่งชนกำแพงอย่างจัง! ชะงัก (Staggered)");
                EndCharge(HammerState.Staggered, wallStaggerDuration);
            }
        }
    }

    private void EndCharge(HammerState nextState, float duration)
    {
        if (chargeCoroutine != null)
        {
            StopCoroutine(chargeCoroutine);
            chargeCoroutine = null;
        }

        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        currentState = nextState;
        stateTimer = duration;
        SetIndicatorColor(normalEyeColor);
    }

    public override void EnterStunState()
    {
        if (chargeCoroutine != null)
        {
            StopCoroutine(chargeCoroutine);
            chargeCoroutine = null;
        }

        rb.linearVelocity = Vector3.zero;
        currentState = HammerState.Stunned;
        SetIndicatorColor(Color.gray);

        base.EnterStunState();
    }

    public override void ExitStunState()
    {
        base.ExitStunState();
        currentState = HammerState.Cooldown;
        stateTimer = 1.0f;
        SetIndicatorColor(normalEyeColor);
    }

    private void SetIndicatorColor(Color color)
    {
        if (indicatorRenderer != null && indicatorRenderer.material != null)
        {
            indicatorRenderer.material.color = color;
        }
    }
}

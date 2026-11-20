using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyMimic : EnemyBase
{
    public enum MimicState
    {
        Spawning,       // เพิ่งแปลงร่างออกมา กำลังคำรามตั้งตัว (0.5s)
        Chasing,        // วิ่งตรงเข้าหาผู้เล่น
        Windup,         // ง้างเตือนก่อนกัด (Telegraph)
        Attacking,      // พุ่งกัดผู้เล่น
        AttackCooldown, // พักชั่วครู่ก่อนวิ่งไล่ต่อ
        Stunned,        // ติดสตั๊น (จาก EnemyBase)
        Carried         // กำลังถูกผู้เล่นยกถือ
    }

    [Header("Mimic Chase Settings")]
    [Tooltip("ความเร็วในการวิ่งไล่ผู้เล่น")]
    public float chaseSpeed = 4.8f;

    [Tooltip("ความเร็วในการหันหน้าเข้าหาผู้เล่น")]
    public float rotationSpeed = 8.0f;

    [Tooltip("ระยะการตรวจจับสายตา (ถ้าอยู่นอกระยะจะเดินสแกนหา)")]
    public float detectionRadius = 30f;

    [Header("Bite Attack Settings")]
    [Tooltip("ระยะประชิดที่เริ่มง้างกัด (เมตร)")]
    public float attackRange = 1.8f;

    [Tooltip("ความเสียหายที่ทำต่อผู้เล่นเมื่อกัดโดน")]
    public float attackDamage = 18f;

    [Tooltip("เวลาง้างก่อนพุ่งกัด (วินาที)")]
    public float windupDuration = 0.35f;

    [Tooltip("เวลารอหลังโจมตีก่อนจะเริ่มไล่ใหม่ (วินาที)")]
    public float attackCooldown = 1.2f;

    [Tooltip("ความเร็วพุ่งตัวไปข้างหน้าสั้นๆ ตอนกัด")]
    public float lungeSpeed = 3.5f;

    [Header("Visual Indicators & Animator")]
    [Tooltip("Renderer ส่วนดวงตาหรือปากของ Mimic ที่จะเปลี่ยนสีเวลาเล็งหรือกัด")]
    public Renderer indicatorRenderer;
    public Color normalEyeColor = new Color(0.8f, 0.2f, 1.0f); // ม่วงเข้ม Alien
    public Color attackEyeColor = Color.red;

    [Tooltip("Animator ของตัว Mimic (ถ้ามี)")]
    public Animator mimicAnimator;
    public string speedParamName = "Speed";
    public string attackTriggerName = "Attack";
    public string hurtTriggerName = "Hurt";
    public string isStunnedBoolName = "IsStunned";
    public string dieTriggerName = "Die";

    [Header("Audio Feedback")]
    public AudioClip spawnScreechSound;
    public AudioClip biteSound;
    public AudioClip growlSound;
    public AudioClip hurtSound;

    public MimicState CurrentState => currentState;
    [SerializeField] private MimicState currentState = MimicState.Spawning;

    private Transform playerTarget;
    private PlayerStats playerStats;
    private float stateTimer = 0f;
    private AudioSource audioSource;
    private Material indicatorMaterialInstance;

    protected override void Awake()
    {
        base.Awake();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f; // 3D sound
            audioSource.minDistance = 2.0f;
            audioSource.maxDistance = 18.0f;
        }

        if (mimicAnimator == null)
        {
            mimicAnimator = GetComponentInChildren<Animator>();
        }

        if (indicatorRenderer != null)
        {
            indicatorMaterialInstance = indicatorRenderer.material;
        }

        if (navAgent != null)
        {
            navAgent.speed = chaseSpeed;
            navAgent.angularSpeed = rotationSpeed * 50f;
            navAgent.acceleration = 16f;
            navAgent.stoppingDistance = attackRange * 0.75f;
            navAgent.autoBraking = false;
        }
    }

    private void Start()
    {
        FindPlayerReference();
        SetIndicatorColor(normalEyeColor);

        // เริ่มต้นในสถานะ Spawning สั้นๆ เพื่อให้เอฟเฟกต์แปลงร่างและเสียงร้องทำงาน
        currentState = MimicState.Spawning;
        stateTimer = 0.5f;

        if (spawnScreechSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(spawnScreechSound, 1.0f);
        }
    }

    private void FindPlayerReference()
    {
        if (PlayerPickupSystem.Instance != null)
        {
            playerTarget = PlayerPickupSystem.Instance.transform;
            playerStats = PlayerPickupSystem.Instance.GetComponent<PlayerStats>();
        }
        else
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTarget = playerObj.transform;
                playerStats = playerObj.GetComponent<PlayerStats>();
            }
        }
    }

    protected override void Update()
    {
        base.Update();

        if (isDead) return;

        // หากติดสตั๊นหรือถูกผู้เล่นยกถืออยู่ ให้หยุดการทำงานของ AI
        if (isStunned || isBeingCarried)
        {
            currentState = isBeingCarried ? MimicState.Carried : MimicState.Stunned;
            UpdateAnimator();
            return;
        }

        if (playerTarget == null)
        {
            FindPlayerReference();
            if (playerTarget == null) return;
        }

        // ป้องกันศัตรูโจมตีผู้เล่นที่ตายแล้ว
        if (playerStats != null && playerStats.IsDead)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            UpdateAnimator();
            return;
        }

        switch (currentState)
        {
            case MimicState.Spawning:
                HandleSpawning();
                break;

            case MimicState.Chasing:
                HandleChasing();
                break;

            case MimicState.Windup:
                HandleWindup();
                break;

            case MimicState.Attacking:
                // จัดการใน Coroutine หรือหลังพุ่งกัด
                break;

            case MimicState.AttackCooldown:
                HandleCooldown();
                break;
        }

        UpdateAnimator();
    }

    private void HandleSpawning()
    {
        StopMovement();
        stateTimer -= Time.deltaTime;

        // หันหน้าหาผู้เล่นตั้งแต่ตอนเกิด
        RotateTowardsPlayer();

        if (stateTimer <= 0f)
        {
            currentState = MimicState.Chasing;
        }
    }

    private void HandleChasing()
    {
        float distance = Vector3.Distance(transform.position, playerTarget.position);

        // หันหน้าเข้าหาผู้เล่น
        RotateTowardsPlayer();

        // ตรวจสอบว่าเข้าถึงระยะกัดหรือยัง
        if (distance <= attackRange)
        {
            StopMovement();

            // เริ่มต้นจังหวะง้างกัด
            currentState = MimicState.Windup;
            stateTimer = windupDuration;
            SetIndicatorColor(attackEyeColor);

            if (growlSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(growlSound, 0.8f);
            }
            return;
        }

        // วิ่งเข้าหาผู้เล่นด้วย NavMeshAgent
        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = false;
            navAgent.speed = chaseSpeed;
            navAgent.SetDestination(playerTarget.position);
        }
    }

    private void HandleWindup()
    {
        // หยุดเดินขณะง้างกัด
        StopMovement();
        RotateTowardsPlayer();

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            ExecuteBiteAttack();
        }
    }

    private void ExecuteBiteAttack()
    {
        currentState = MimicState.Attacking;

        // สั่งพุ่งตัวไปข้างหน้าสั้นๆ
        Vector3 attackDir = (playerTarget.position - transform.position);
        attackDir.y = 0f;
        if (attackDir.sqrMagnitude > 0.001f) attackDir.Normalize();
        else attackDir = transform.forward;

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = true;
            navAgent.velocity = attackDir * lungeSpeed;
        }
        else
        {
            rb.linearVelocity = attackDir * lungeSpeed + Vector3.up * 1.5f;
        }

        // สั่งเล่นแอนิเมชันโจมตี
        if (mimicAnimator != null && !string.IsNullOrEmpty(attackTriggerName))
        {
            mimicAnimator.SetTrigger(attackTriggerName);
        }

        // เล่นเสียงกัด
        if (biteSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(biteSound, 1.0f);
        }

        // ตรวจสอบการทำดาเมจต่อผู้เล่น
        float hitDistance = Vector3.Distance(transform.position, playerTarget.position);
        if (hitDistance <= attackRange + 0.8f)
        {
            if (playerStats != null && !playerStats.IsDead)
            {
                Vector3 hitPoint = (transform.position + playerTarget.position) * 0.5f;
                playerStats.TakeDamage(attackDamage, hitPoint, attackDir);
                Debug.Log($"<color=#FF0055>[EnemyMimic]</color> กัดผู้เล่นสำเร็จ! ทำดาเมจ {attackDamage}");
            }
        }

        // เข้าสู่ช่วงคูลดาวน์
        currentState = MimicState.AttackCooldown;
        stateTimer = attackCooldown;
    }

    private void HandleCooldown()
    {
        StopMovement();
        SetIndicatorColor(normalEyeColor);

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            currentState = MimicState.Chasing;
        }
    }

    private void StopMovement()
    {
        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
        }
    }

    private void RotateTowardsPlayer()
    {
        if (playerTarget == null) return;

        Vector3 targetDir = (playerTarget.position - transform.position);
        targetDir.y = 0f;
        if (targetDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(targetDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }
    }

    private void SetIndicatorColor(Color color)
    {
        if (indicatorMaterialInstance != null)
        {
            indicatorMaterialInstance.color = color;
            if (indicatorMaterialInstance.HasProperty("_EmissionColor"))
            {
                indicatorMaterialInstance.SetColor("_EmissionColor", color);
            }
        }
    }

    private void UpdateAnimator()
    {
        if (mimicAnimator == null) return;

        if (!string.IsNullOrEmpty(speedParamName))
        {
            float horizSpeed = (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
                ? navAgent.velocity.magnitude
                : 0f;
            mimicAnimator.SetFloat(speedParamName, horizSpeed);
        }

        if (!string.IsNullOrEmpty(isStunnedBoolName))
        {
            mimicAnimator.SetBool(isStunnedBoolName, isStunned);
        }
    }

    public override void TakeStunDamage(float stunAmount, Vector3 hitPoint, Vector3 hitDirection)
    {
        base.TakeStunDamage(stunAmount, hitPoint, hitDirection);

        if (hurtSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(hurtSound, 0.8f);
        }

        if (mimicAnimator != null && !string.IsNullOrEmpty(hurtTriggerName))
        {
            mimicAnimator.SetTrigger(hurtTriggerName);
        }
    }

    public override void EnterStunState()
    {
        base.EnterStunState();
        currentState = MimicState.Stunned;
        rb.linearVelocity = Vector3.zero;
        SetIndicatorColor(Color.yellow);
    }

    public override void ExitStunState()
    {
        base.ExitStunState();
        currentState = MimicState.Chasing;
        SetIndicatorColor(normalEyeColor);
    }

    protected override void Die()
    {
        base.Die();
        if (mimicAnimator != null && !string.IsNullOrEmpty(dieTriggerName))
        {
            mimicAnimator.SetTrigger(dieTriggerName);
        }
    }
}

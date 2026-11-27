using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.Playables;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyMimic : EnemyBase
{
    public enum MimicState
    {
        Spawning,
        Chasing,
        Windup,
        Attacking,
        AttackCooldown,
        Stunned,
        Carried
    }

    [Header("Mimic Chase Settings")]
    public float chaseSpeed = 4.8f;
    public float rotationSpeed = 8.0f;
    public float detectionRadius = 30f;

    [Header("Bite Attack Settings")]
    public float attackRange = 1.8f;
    public float attackDamage = 18f;
    public float windupDuration = 0.35f;
    public float attackCooldown = 1.2f;
    public float lungeSpeed = 3.5f;

    [Header("Visual Indicators")]
    public Renderer indicatorRenderer;
    public Color normalEyeColor = new Color(0.8f, 0.2f, 1.0f);
    public Color attackEyeColor = Color.red;

    [Header("Playable Animation (Single Clip)")]
    public Animator mimicAnimator;
    public AnimationClip mimicClip;
    public float idleAnimSpeed = 0.6f;
    public float chaseAnimSpeed = 1.2f;
    public float attackAnimSpeed = 1.8f;
    public bool pauseWhenStunned = true;

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

    private PlayableGraph animGraph;
    private AnimationClipPlayable clipPlayable;
    private bool animReady;

    protected override void Awake()
    {
        base.Awake();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f;
            audioSource.minDistance = 2.0f;
            audioSource.maxDistance = 18.0f;
        }

        if (mimicAnimator == null)
        {
            mimicAnimator = GetComponent<Animator>();
            if (mimicAnimator == null) mimicAnimator = GetComponentInChildren<Animator>();
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
        InitPlayableAnimation();
        SetIndicatorColor(normalEyeColor);

        currentState = MimicState.Spawning;
        stateTimer = 0.5f;

        if (spawnScreechSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(spawnScreechSound, 1.0f);
        }
    }

    private void OnDestroy()
    {
        if (animGraph.IsValid())
        {
            animGraph.Destroy();
        }
    }

    private void InitPlayableAnimation()
    {
        if (animReady || mimicClip == null) return;

        if (mimicAnimator == null)
            mimicAnimator = GetComponent<Animator>();
        if (mimicAnimator == null)
            mimicAnimator = GetComponentInChildren<Animator>();
        if (mimicAnimator == null)
            mimicAnimator = gameObject.AddComponent<Animator>();

        mimicAnimator.applyRootMotion = false;
        mimicAnimator.runtimeAnimatorController = null;

        animGraph = PlayableGraph.Create("EnemyMimic_SingleClip");
        animGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        clipPlayable = AnimationClipPlayable.Create(animGraph, mimicClip);
        clipPlayable.SetApplyFootIK(false);

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(animGraph, "MimicAnim", mimicAnimator);
        output.SetSourcePlayable(clipPlayable);

        animGraph.Play();
        animReady = true;
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

        if (isDead)
        {
            UpdatePlayableAnimation();
            return;
        }

        if (isStunned || isBeingCarried)
        {
            currentState = isBeingCarried ? MimicState.Carried : MimicState.Stunned;
            UpdatePlayableAnimation();
            return;
        }

        if (playerTarget == null)
        {
            FindPlayerReference();
            if (playerTarget == null)
            {
                UpdatePlayableAnimation();
                return;
            }
        }

        if (playerStats != null && playerStats.IsDead)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            UpdatePlayableAnimation();
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
                break;

            case MimicState.AttackCooldown:
                HandleCooldown();
                break;
        }

        UpdatePlayableAnimation();
    }

    private void HandleSpawning()
    {
        StopMovement();
        stateTimer -= Time.deltaTime;
        RotateTowardsPlayer();

        if (stateTimer <= 0f)
        {
            currentState = MimicState.Chasing;
        }
    }

    private void HandleChasing()
    {
        float distance = Vector3.Distance(transform.position, playerTarget.position);
        RotateTowardsPlayer();

        if (distance <= attackRange)
        {
            StopMovement();
            currentState = MimicState.Windup;
            stateTimer = windupDuration;
            SetIndicatorColor(attackEyeColor);

            if (growlSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(growlSound, 0.8f);
            }
            return;
        }

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = false;
            navAgent.speed = chaseSpeed;
            navAgent.SetDestination(playerTarget.position);
        }
    }

    private void HandleWindup()
    {
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

        Vector3 attackDir = playerTarget.position - transform.position;
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

        if (biteSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(biteSound, 1.0f);
        }

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

        Vector3 targetDir = playerTarget.position - transform.position;
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

    private void UpdatePlayableAnimation()
    {
        if (!animReady || !clipPlayable.IsValid()) return;

        if (!mimicClip.isLooping && mimicClip.length > 0f && clipPlayable.GetTime() >= mimicClip.length)
        {
            clipPlayable.SetTime(0d);
        }

        float targetSpeed;
        if (isDead)
        {
            targetSpeed = 0f;
        }
        else if (isStunned || isBeingCarried)
        {
            targetSpeed = pauseWhenStunned ? 0f : 0.25f;
        }
        else
        {
            switch (currentState)
            {
                case MimicState.Spawning:
                    targetSpeed = idleAnimSpeed;
                    break;
                case MimicState.Chasing:
                    targetSpeed = chaseAnimSpeed;
                    break;
                case MimicState.Windup:
                    targetSpeed = idleAnimSpeed * 0.5f;
                    break;
                case MimicState.Attacking:
                    targetSpeed = attackAnimSpeed;
                    break;
                case MimicState.AttackCooldown:
                    targetSpeed = idleAnimSpeed;
                    break;
                default:
                    targetSpeed = 1f;
                    break;
            }
        }

        clipPlayable.SetSpeed(targetSpeed);
    }

    public override void TakeStunDamage(float stunAmount, Vector3 hitPoint, Vector3 hitDirection)
    {
        base.TakeStunDamage(stunAmount, hitPoint, hitDirection);

        if (hurtSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(hurtSound, 0.8f);
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
        if (animReady && clipPlayable.IsValid())
        {
            clipPlayable.SetSpeed(0f);
        }
    }
}

// --- สรุปการทำงานของโค้ด ---
// 1. แทนที่ระบบ Animator Controller เดิมด้วย PlayableGraph ("EnemyMimic_SingleClip") ที่ต่อ AnimationClipPlayable เข้ากับ AnimationPlayableOutput โดยตรงโดยไม่ต้องใช้ Mixer
// 2. InitPlayableAnimation() ค้นหาหรือสร้าง Component Animator บนตัว Mimic พร้อมล้าง runtimeAnimatorController และปิด applyRootMotion เพื่อไม่ให้ชนกับ NavMeshAgent
// 3. UpdatePlayableAnimation() ปรับความเร็วการเล่นของคลิป (SetSpeed) ตามสถานะปัจจุบัน (วิ่งไล่เร็วขึ้น, พุ่งกัดเร่งสปีด, หยุดนิ่งเมื่อติดสตั๊นหรือตาย) และวนลูปเวลาให้อัตโนมัติแม้ไม่ได้ติ๊ก Loop Time ในไฟล์ FBX
// 4. คืนหน่วยความจำของ PlayableGraph ใน OnDestroy() เมื่อวัตถุถูกทำลาย
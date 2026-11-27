using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public class EnemyHammer : EnemyBase
{
    public enum HammerState
    {
        Spawn_Drop,     // Root|Spawn_Drop (0 - 80)
        Spawn_Slide,    // Root|Spawn_Slide (0 - 80)
        No_Animation,   // Root|No_Animation (0 - 0) : ยืนนิ่ง / คูลดาวน์
        Roar,           // Root|Roar (0 - 80) : คำรามเตือนและล็อคเป้าก่อนพุ่ง
        Run_Start,      // Root|Run_Start (0 - 40) : เริ่มออกตัวพุ่งชน
        Run_Loop,       // Root|Run_Loop (0 - 30) : วิ่งเข้าหา / พุ่งชนต่อเนื่อง
        Stunt_Start,    // Root|Stunt_Start (0 - 40) : เริ่มชะงัก / ล้มติดสตั๊น
        Stunt_Idle,     // Root|Stunt_Idle (0 - 80) : มึนค้างระหว่างติดสตั๊น
        Stunt_End,      // Root|Stunt_End (0 - 60) : ฟื้นตัวลุกขึ้นจากสตั๊น
        Carry           // Root|Carry (0 - 0) : ถูกผู้เล่นยกถือ
    }

    public enum SpawnMode
    {
        None,
        Spawn_Drop,
        Spawn_Slide
    }

    [Header("Spawn Settings")]
    [Tooltip("รูปแบบการเกิดเมื่อเริ่มเกม (จะเล่นคลิป Spawn ก่อนเริ่ม AI)")]
    public SpawnMode spawnMode = SpawnMode.Spawn_Drop;

    [Header("Hammer Detection Settings")]
    [Tooltip("ระยะการตรวจจับสายตาหาผู้เล่น")]
    public float detectionRadius = 25f;
    [Tooltip("ความเร็วในการวิ่งเข้าหาผู้เล่นระหว่างค้นหา")]
    public float searchMoveSpeed = 3.5f;
    [Tooltip("ระยะเริ่มล็อคเป้าเพื่อคำรามและพุ่งชน (เมื่อมี Line of Sight)")]
    public float chargeTriggerDistance = 14f;
    [Tooltip("ให้หันหน้าหาผู้เล่นตามทิศทางการเคลื่อนที่หรือไม่")]
    public bool facePlayer = true;
    [Tooltip("ความเร็วในการหันหน้าหาผู้เล่น")]
    public float turnSpeed = 6f;
    [Tooltip("เลเยอร์ของผู้เล่น")]
    public LayerMask playerLayer;
    [Tooltip("เลเยอร์ของสิ่งกีดขวาง/กำแพง")]
    public LayerMask obstacleLayer;
    [Tooltip("ติ๊กถูกเพื่อข้ามท่า Roar แล้วเข้าสู่ Run_Start ทันที")]
    public bool skipRoar = false;

    [Header("Charge Attack Settings")]
    [Tooltip("ความเร็วสูงสุดในการพุ่งชนเป็นเส้นตรง (Run_Loop)")]
    public float chargeSpeed = 14f;
    [Tooltip("จำนวนรอบของคลิป Run_Loop ที่ใช้คำนวณเวลาพุ่งสูงสุดเมื่อ Sync กับคลิป")]
    [Min(1)] public int runLoopChargeCycles = 2;
    [Tooltip("ความเสียหายที่ทำต่อผู้เล่นเมื่อพุ่งชน")]
    public float chargeDamage = 25f;
    [Tooltip("แรงกระแทกผลักผู้เล่น")]
    public float knockbackForce = 10f;

    [Header("Action Durations (Auto-synced with Clips)")]
    [Tooltip("อัปเดตระยะเวลาของแต่ละ Action ให้ตรงกับความยาวจริงของ Clip อัตโนมัติ")]
    public bool syncDurationWithClip = true;

    [Tooltip("ระยะเวลา Spawn_Drop (ค่าเริ่มต้น 80 เฟรม @30fps = 2.67 วิ)")]
    [Min(0f)] public float spawnDropDuration = 2.67f;
    [Tooltip("ระยะเวลา Spawn_Slide (ค่าเริ่มต้น 80 เฟรม @30fps = 2.67 วิ)")]
    [Min(0f)] public float spawnSlideDuration = 2.67f;
    [Tooltip("ระยะเวลา Roar ก่อนออกตัวพุ่ง (ค่าเริ่มต้น 80 เฟรม @30fps = 2.67 วิ)")]
    [Min(0f)] public float roarDuration = 2.67f;
    [Tooltip("ระยะเวลา Run_Start ช่วงเร่งออกตัว (ค่าเริ่มต้น 40 เฟรม @30fps = 1.33 วิ)")]
    [Min(0f)] public float runStartDuration = 1.33f;
    [Tooltip("ระยะเวลาสูงสุดของ Run_Loop ตอนพุ่งชน (ค่าเริ่มต้น 30 เฟรม x 2 รอบ @30fps = 2.0 วิ)")]
    [Min(0f)] public float runLoopMaxDuration = 2.0f;
    [Tooltip("ระยะเวลา Stunt_Start ช่วงเริ่มชะงัก/ติดสตั๊น (ค่าเริ่มต้น 40 เฟรม @30fps = 1.33 วิ)")]
    [Min(0f)] public float stuntStartDuration = 1.33f;
    [Tooltip("ระยะเวลา Stunt_Idle ช่วงมึนค้างหลังชนกำแพง (ค่าเริ่มต้น 80 เฟรม @30fps = 2.67 วิ)")]
    [Min(0f)] public float stuntIdleDuration = 2.67f;
    [Tooltip("ระยะเวลา Stunt_End ช่วงฟื้นตัวลุกขึ้น (ค่าเริ่มต้น 60 เฟรม @30fps = 2.0 วิ)")]
    [Min(0f)] public float stuntEndDuration = 2.0f;
    [Tooltip("ระยะเวลาพัก (No_Animation) หลังพุ่งชนผู้เล่นหรือหมดเวลาพุ่ง")]
    [Min(0f)] public float noAnimationCooldown = 1.5f;

    [Header("Animation Clips")]
    [Tooltip("Animator ของโมเดล (ถ้าเว้นว่าง ระบบจะหาจากลูก เช่น hammerHead3 ให้อัตโนมัติ)")]
    public Animator hammerAnimator;

    public AnimationClip spawnDropClip;    // Root|Spawn_Drop
    public AnimationClip spawnSlideClip;   // Root|Spawn_Slide
    public AnimationClip noAnimationClip;  // Root|No_Animation
    public AnimationClip roarClip;         // Root|Roar
    public AnimationClip runStartClip;     // Root|Run_Start
    public AnimationClip runLoopClip;      // Root|Run_Loop
    public AnimationClip stuntStartClip;   // Root|Stunt_Start
    public AnimationClip stuntIdleClip;    // Root|Stunt_Idle
    public AnimationClip stuntEndClip;     // Root|Stunt_End
    public AnimationClip carryClip;        // Root|Carry

    [Tooltip("ระยะเวลา crossfade ตอนสลับท่า (วินาที) — 0 = สลับทันที")]
    [Min(0f)] public float stageAnimationFade = 0.15f;

    [Header("Movement VFX")]
    [Tooltip("Particle system ฝุ่นที่พื้น (ต้องตั้ง Simulation Space = World)")]
    public ParticleSystem dustTrail;
    [Tooltip("Particle system ควันจากส่วนหัว")]
    public ParticleSystem headSmoke;
    [Tooltip("Prefab เอฟเฟกต์ตอนพุ่งชน (กำแพง/ผู้เล่น)")]
    public GameObject impactVfxPrefab;
    [Tooltip("สเกลเอฟเฟกต์ตอนชนผู้เล่น เทียบกับตอนชนกำแพง")]
    [Range(0.1f, 1f)] public float playerHitVfxScale = 0.6f;
    [Tooltip("ความแรงการปล่อยฝุ่นตอนเดินค้นหา (0 = ปิด)")]
    [Range(0f, 1f)] public float searchEmission = 0f;

    private float lastTrailMultiplier = -1f;

    public HammerState CurrentState => currentState;
    [SerializeField] private HammerState currentState = HammerState.No_Animation;

    private Transform playerTarget;
    private Vector3 lockedChargeDirection;
    private Coroutine actionCoroutine;
    private bool isCharging;

    // --- Stage animation (Playables) ---
    private PlayableGraph stageAnimGraph;
    private AnimationMixerPlayable stageAnimMixer;
    private bool stageAnimReady;
    private int stageAnimPort = -1;
    private float stageAnimFadeTimer = 1f;
    private HammerState lastAnimatedState;
    private bool hasAnimatedState;

    private void OnValidate()
    {
        if (syncDurationWithClip)
        {
            SyncDurationsFromClips();
        }
    }

    [ContextMenu("Sync Durations From Clips")]
    public void SyncDurationsFromClips()
    {
        if (spawnDropClip != null && spawnDropClip.length > 0f)
            spawnDropDuration = spawnDropClip.length;

        if (spawnSlideClip != null && spawnSlideClip.length > 0f)
            spawnSlideDuration = spawnSlideClip.length;

        if (roarClip != null && roarClip.length > 0f)
            roarDuration = roarClip.length;

        if (runStartClip != null && runStartClip.length > 0f)
            runStartDuration = runStartClip.length;

        if (runLoopClip != null && runLoopClip.length > 0f)
            runLoopMaxDuration = runLoopClip.length * Mathf.Max(1, runLoopChargeCycles);

        if (stuntStartClip != null && stuntStartClip.length > 0f)
            stuntStartDuration = stuntStartClip.length;

        if (stuntIdleClip != null && stuntIdleClip.length > 0f)
            stuntIdleDuration = stuntIdleClip.length;

        if (stuntEndClip != null && stuntEndClip.length > 0f)
            stuntEndDuration = stuntEndClip.length;
    }

    protected override void Awake()
    {
        base.Awake();

        if (obstacleLayer.value == 0)
        {
            obstacleLayer = LayerMask.GetMask("Default");
        }

        ResolveVfxReferences();

        // เตรียม pool ไว้ล่วงหน้า -> ตอนชนไม่มี Instantiate เลย (เปิด/ปิดอย่างเดียว)
        VfxPool.EnsureExists().Prewarm(impactVfxPrefab);

        if (syncDurationWithClip)
        {
            SyncDurationsFromClips();
        }
    }

    private void Start()
    {
        FindPlayerReference();
        InitStageAnimation();

        if (spawnMode == SpawnMode.Spawn_Drop && spawnDropClip != null)
        {
            actionCoroutine = StartCoroutine(SpawnRoutine(HammerState.Spawn_Drop, spawnDropDuration));
        }
        else if (spawnMode == SpawnMode.Spawn_Slide && spawnSlideClip != null)
        {
            actionCoroutine = StartCoroutine(SpawnRoutine(HammerState.Spawn_Slide, spawnSlideDuration));
        }
        else
        {
            SetState(HammerState.No_Animation);
        }

        UpdateTrailForState(currentState);
    }

    private void LateUpdate()
    {
        UpdateStageAnimationFade();
    }

    private void OnDestroy()
    {
        if (stageAnimGraph.IsValid())
        {
            stageAnimGraph.Destroy();
        }
    }

    protected override void Update()
    {
        base.Update();

        if (isDead)
        {
            StopActionCoroutine();
            return;
        }

        if (isBeingCarried)
        {
            StopActionCoroutine();
            SetState(HammerState.Carry);
            return;
        }

        if (isStunned)
        {
            // กรณีถูกวางลงพื้นแล้วยังติดสตั๊นอยู่ ให้กลับเข้าท่า Stunt_Idle
            if (actionCoroutine == null && currentState != HammerState.Stunt_Start && currentState != HammerState.Stunt_Idle)
            {
                SetState(HammerState.Stunt_Idle);
            }
            return;
        }

        // หากกำลังเล่น Action Routine (Spawn / Charge / Wall Stunt / Stunt_End / Cooldown) ให้รันจนจบก่อน
        if (actionCoroutine != null) return;

        HandleSearching();
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
            SetHorizontalVelocity(Vector3.zero);
            SetState(HammerState.No_Animation);
            return;
        }

        Vector3 toPlayer = playerTarget.position - transform.position;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;

        if (dist <= detectionRadius)
        {
            Vector3 moveDir = toPlayer.normalized;
            RotateSmoothlyTowards(moveDir);

            bool hasClearLOS = HasLineOfSightToPlayer();

            if (hasClearLOS && dist <= chargeTriggerDistance)
            {
                // มองเห็นผู้เล่นตรงๆ และอยู่ในระยะพุ่ง -> หยุดแล้วเริ่มลำดับ Roar -> Run_Start -> Run_Loop
                SetHorizontalVelocity(Vector3.zero);
                StartChargeSequence();
            }
            else
            {
                // ยังมีสิ่งกีดขวางหรืออยู่นอกระยะพุ่ง -> วิ่งเข้าหาผู้เล่นด้วยท่า Run_Loop
                SetHorizontalVelocity(moveDir * searchMoveSpeed);
                SetState(HammerState.Run_Loop);
            }
        }
        else
        {
            // อยู่นอกระยะตรวจจับ -> หยุดนิ่งด้วยท่า No_Animation
            SetHorizontalVelocity(Vector3.zero);
            SetState(HammerState.No_Animation);
        }
    }

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
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            if (hit.transform == playerTarget || hit.transform.IsChildOf(playerTarget) || hit.collider.GetComponentInParent<PlayerController>() != null)
            {
                return true;
            }

            if (!hit.collider.isTrigger)
            {
                return false;
            }
        }

        return true;
    }

    // ---------------------------------------------------------------- Action Routines

    private void StopActionCoroutine()
    {
        if (actionCoroutine != null)
        {
            StopCoroutine(actionCoroutine);
            actionCoroutine = null;
        }
        isCharging = false;
    }

    private IEnumerator SpawnRoutine(HammerState spawnState, float duration)
    {
        SetState(spawnState);
        SetHorizontalVelocity(Vector3.zero);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (isDead || isStunned || isBeingCarried) yield break;
            SetHorizontalVelocity(Vector3.zero);
            elapsed += Time.deltaTime;
            yield return null;
        }

        actionCoroutine = null;
        SetState(HammerState.No_Animation);
    }

    private void StartChargeSequence()
    {
        StopActionCoroutine();
        actionCoroutine = StartCoroutine(ChargeRoutine());
    }

    private IEnumerator ChargeRoutine()
    {
        lockedChargeDirection = Vector3.zero;
        SetHorizontalVelocity(Vector3.zero);

        if (!skipRoar && roarDuration > 0f)
        {
            SetState(HammerState.Roar);

            float elapsedRoar = 0f;
            while (elapsedRoar < roarDuration)
            {
                if (isStunned || isBeingCarried || isDead) yield break;

                UpdateChargeDirectionFromPlayer();
                RotateSmoothlyTowards(lockedChargeDirection);
                SetHorizontalVelocity(Vector3.zero);

                elapsedRoar += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            UpdateChargeDirectionFromPlayer();
        }

        if (lockedChargeDirection.sqrMagnitude < 0.001f)
        {
            lockedChargeDirection = transform.forward;
            lockedChargeDirection.y = 0f;
            lockedChargeDirection.Normalize();
        }

        SetState(HammerState.Run_Start);
        isCharging = false;

        float elapsedStart = 0f;
        while (elapsedStart < runStartDuration)
        {
            if (isStunned || isBeingCarried || isDead) yield break;

            RotateSmoothlyTowards(lockedChargeDirection);
            SetHorizontalVelocity(Vector3.zero);

            elapsedStart += Time.deltaTime;
            yield return null;
        }

        // 2. ขั้นตอนพุ่งชน (Run_Loop) — ปล่อยฝุ่นเต็มกำลัง
        SetState(HammerState.Run_Loop);
        isCharging = true;
        SetTrailEmission(1f);
        EmitChargePuff();
        float elapsedLoop = 0f;

        while (elapsedLoop < runLoopMaxDuration && isCharging)
        {
            if (isStunned || isBeingCarried || isDead) yield break;

            elapsedLoop += Time.fixedDeltaTime;
            SetHorizontalVelocity(lockedChargeDirection * chargeSpeed);
            yield return new WaitForFixedUpdate();
        }

        isCharging = false;
        yield return CooldownSequence(noAnimationCooldown);
    }

    private void UpdateChargeDirectionFromPlayer()
    {
        if (playerTarget == null) return;

        Vector3 aimDir = playerTarget.position - transform.position;
        aimDir.y = 0f;
        if (aimDir.sqrMagnitude > 0.001f)
        {
            lockedChargeDirection = aimDir.normalized;
        }
    }

    // --- สรุปการทำงานของโค้ด ---
    // 1. เช็กเงื่อนไข !skipRoar && roarDuration > 0f: ถ้าไม่ได้ข้าม จะเข้าสถานะ Roar หยุดนิ่งและอัปเดตทิศเล็งหาผู้เล่นจนครบ roarDuration
    // 2. ถ้าติ๊ก skipRoar ไว้ จะข้าม Roar แล้วดึงทิศทางผู้เล่นล่าสุดผ่าน UpdateChargeDirectionFromPlayer() ทันที เพื่อไม่ให้ทิศพุ่งเป็นศูนย์
    // 3. เข้าสถานะ Run_Start โดยยืนนิ่ง (isCharging = false) พร้อมค่อยๆ หันหน้าเข้าหาทิศที่ล็อคไว้จนครบ runStartDuration
    // 4. เข้าสถานะ Run_Loop และเปิด isCharging = true เพื่อออกตัวพุ่งชนเป็นเส้นตรงจนชนเป้าหมายหรือครบ runLoopMaxDuration แล้วจึงพักใน CooldownSequence

    private void StartWallStuntSequence()
    {
        StopActionCoroutine();
        SetHorizontalVelocity(Vector3.zero);
        actionCoroutine = StartCoroutine(WallStuntRoutine());
    }

    private IEnumerator WallStuntRoutine()
    {
        // 1. Stunt_Start
        SetState(HammerState.Stunt_Start);
        yield return WaitStationary(stuntStartDuration);
        if (isDead || isBeingCarried || isStunned) yield break;

        // 2. Stunt_Idle
        SetState(HammerState.Stunt_Idle);
        yield return WaitStationary(stuntIdleDuration);
        if (isDead || isBeingCarried || isStunned) yield break;

        // 3. Stunt_End
        SetState(HammerState.Stunt_End);
        yield return WaitStationary(stuntEndDuration);
        if (isDead || isBeingCarried || isStunned) yield break;

        actionCoroutine = null;
        SetState(HammerState.No_Animation);
    }

    private void StartCooldownSequence(float duration)
    {
        StopActionCoroutine();
        SetHorizontalVelocity(Vector3.zero);
        actionCoroutine = StartCoroutine(CooldownSequence(duration));
    }

    private IEnumerator CooldownSequence(float duration)
    {
        SetState(HammerState.No_Animation);
        yield return WaitStationary(duration);

        actionCoroutine = null;
    }

    private IEnumerator ExternalStunRoutine()
    {
        // เล่น Stunt_Start ก่อนแล้วค้างที่ Stunt_Idle จนกว่าจะออกจากสถานะ Stun
        SetState(HammerState.Stunt_Start);

        float elapsed = 0f;
        while (elapsed < stuntStartDuration)
        {
            if (isDead || isBeingCarried || !isStunned) yield break;
            SetHorizontalVelocity(Vector3.zero);
            elapsed += Time.deltaTime;
            yield return null;
        }

        SetState(HammerState.Stunt_Idle);
        actionCoroutine = null;
    }

    private IEnumerator StuntEndRoutine()
    {
        SetState(HammerState.Stunt_End);
        yield return WaitStationary(stuntEndDuration);

        actionCoroutine = null;
        SetState(HammerState.No_Animation);
    }

    private IEnumerator WaitStationary(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (isDead || isBeingCarried) yield break;
            SetHorizontalVelocity(Vector3.zero);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    // ---------------------------------------------------------------- Collisions & Stun Hooks

    private void OnCollisionEnter(Collision collision)
    {
        if (!isCharging) return;

        // 1. ตรวจสอบว่าชนผู้เล่นหรือไม่
        PlayerController player = collision.gameObject.GetComponentInParent<PlayerController>();
        if (player == null) player = collision.gameObject.GetComponent<PlayerController>();

        if (player != null)
        {
            Vector3 hitPoint = collision.contactCount > 0 ? collision.contacts[0].point : transform.position;
            Vector3 hitDir = (player.transform.position - transform.position).normalized;

            LivingEntity targetLiving = player.GetComponent<LivingEntity>();
            if (targetLiving != null)
            {
                targetLiving.TakeDamage(chargeDamage, hitPoint, hitDir);
            }

            Rigidbody playerRb = player.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                playerRb.AddForce(lockedChargeDirection * knockbackForce + Vector3.up * 2f, ForceMode.Impulse);
            }

            // เอฟเฟกต์กระแทกที่ตัวผู้เล่น (ย่อส่วน)
            SpawnImpactVfx(hitPoint, -hitDir, playerHitVfxScale);

            Debug.Log($"[{gameObject.name}] พุ่งชนโดนผู้เล่น! ทำดาเมจ {chargeDamage} และเข้าสู่ Stunt_Start -> Stunt_Idle -> Stunt_End");
            StartWallStuntSequence();
            return;
        }

        // 2. ตรวจสอบว่าชนกำแพงแนวตั้งหรือไม่ -> เข้าลำดับ Stunt_Start -> Stunt_Idle -> Stunt_End
        if (collision.contactCount > 0)
        {
            Vector3 normal = collision.contacts[0].normal;
            if (Vector3.Dot(normal, Vector3.up) < 0.5f)
            {
                SpawnImpactVfx(collision.contacts[0].point, normal, 1f);
                Debug.Log($"[{gameObject.name}] พุ่งชนกำแพง! เข้าสู่ Stunt_Start -> Stunt_Idle -> Stunt_End");
                StartWallStuntSequence();
            }
        }
    }

    public override void EnterStunState()
    {
        StopActionCoroutine();
        SetHorizontalVelocity(Vector3.zero);

        base.EnterStunState();

        if (!isDead && !isBeingCarried)
        {
            actionCoroutine = StartCoroutine(ExternalStunRoutine());
        }
    }

    public override void ExitStunState()
    {
        StopActionCoroutine();
        base.ExitStunState();

        if (!isDead && !isBeingCarried)
        {
            actionCoroutine = StartCoroutine(StuntEndRoutine());
        }
    }

    protected override void Die()
    {
        base.Die();
        SetTrailEmission(0f);
    }

    // ---------------------------------------------------------------- Movement VFX

    /// <summary>หาอ้างอิง ParticleSystem ในลูกๆ อัตโนมัติถ้ายังไม่ได้ผูกใน Inspector</summary>
    private void ResolveVfxReferences()
    {
        if (dustTrail != null && headSmoke != null) return;

        ParticleSystem[] systems = GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem ps in systems)
        {
            if (dustTrail == null && ps.name == "Dust") dustTrail = ps;
            else if (headSmoke == null && ps.name == "HeadSmoke") headSmoke = ps;
        }
    }

    /// <summary>ปรับความแรงการปล่อยฝุ่นตามสถานะ (0 = หยุดปล่อย แต่ให้หางเดิมจางหายเอง)</summary>
    private void UpdateTrailForState(HammerState state)
    {
        float multiplier;
        switch (state)
        {
            // ยืนนิ่ง/คูลดาวน์/เดินหาผู้เล่น (Run_Loop ตอนเดินถูก override เป็น 1f ตอนพุ่งจริง)
            case HammerState.No_Animation:
            case HammerState.Run_Loop:
                multiplier = searchEmission;
                break;
            case HammerState.Roar:
                multiplier = 0.15f;
                break;
            case HammerState.Run_Start:
                multiplier = 1f;
                break;
            default:
                multiplier = 0f;
                break;
        }

        SetTrailEmission(multiplier);
    }

    private void SetTrailEmission(float multiplier)
    {
        if (Mathf.Approximately(lastTrailMultiplier, multiplier)) return;
        lastTrailMultiplier = multiplier;

        SetEmissionMultiplier(dustTrail, multiplier);
        SetEmissionMultiplier(headSmoke, multiplier);
    }

    private static void SetEmissionMultiplier(ParticleSystem ps, float multiplier)
    {
        if (ps == null) return;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverDistanceMultiplier = multiplier;
    }

    /// <summary>พ่นฝุ่นก้อนแรกตอนเริ่มพุ่ง (กันหางขาดตอนออกตัว)</summary>
    private void EmitChargePuff()
    {
        if (dustTrail != null) dustTrail.Emit(10);
        if (headSmoke != null) headSmoke.Emit(4);
    }

    /// <summary>เสกเอฟเฟกต์กระแทกที่จุดชน (หยิบจาก pool: เปิด/ปิด ไม่สร้าง/ทำลาย)</summary>
    private void SpawnImpactVfx(Vector3 position, Vector3 normal, float scale)
    {
        if (impactVfxPrefab == null) return;

        Quaternion rotation = normal.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(normal)
            : Quaternion.identity;

        VfxPool.EnsureExists().Spawn(impactVfxPrefab, position, rotation, scale);
    }

    // ---------------------------------------------------------------- Movement Helpers

    private void SetHorizontalVelocity(Vector3 horizontalVelocity)
    {
        if (rb == null || rb.isKinematic) return;
        rb.linearVelocity = new Vector3(horizontalVelocity.x, rb.linearVelocity.y, horizontalVelocity.z);
    }

    private void RotateSmoothlyTowards(Vector3 direction)
    {
        if (!facePlayer || direction.sqrMagnitude < 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * turnSpeed);
    }

    // ---------------------------------------------------------------- Stage Animation (Playables)

    private void SetState(HammerState nextState)
    {
        currentState = nextState;
        PlayStageAnimation(nextState);
        UpdateTrailForState(nextState);
    }

    private bool HasAnyStageClip()
    {
        return spawnDropClip != null || spawnSlideClip != null || noAnimationClip != null ||
               roarClip != null || runStartClip != null || runLoopClip != null ||
               stuntStartClip != null || stuntIdleClip != null || stuntEndClip != null ||
               carryClip != null;
    }

    private AnimationClip GetStageClip(HammerState state)
    {
        switch (state)
        {
            case HammerState.Spawn_Drop: return spawnDropClip;
            case HammerState.Spawn_Slide: return spawnSlideClip;
            case HammerState.No_Animation: return noAnimationClip;
            case HammerState.Roar: return roarClip;
            case HammerState.Run_Start: return runStartClip;
            case HammerState.Run_Loop: return runLoopClip;
            case HammerState.Stunt_Start: return stuntStartClip;
            case HammerState.Stunt_Idle: return stuntIdleClip;
            case HammerState.Stunt_End: return stuntEndClip;
            case HammerState.Carry: return carryClip;
            default: return null;
        }
    }

    private void InitStageAnimation()
    {
        if (stageAnimReady) return;
        if (!HasAnyStageClip()) return;

        if (hammerAnimator == null)
            hammerAnimator = GetComponent<Animator>();
        if (hammerAnimator == null)
            hammerAnimator = GetComponentInChildren<Animator>();
        if (hammerAnimator == null)
            hammerAnimator = gameObject.AddComponent<Animator>();

        hammerAnimator.applyRootMotion = false;

        stageAnimGraph = PlayableGraph.Create("EnemyHammer_StageAnim");
        stageAnimGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        stageAnimMixer = AnimationMixerPlayable.Create(stageAnimGraph, 2);

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(stageAnimGraph, "StageAnim", hammerAnimator);
        output.SetSourcePlayable(stageAnimMixer);

        stageAnimGraph.Play();
        stageAnimReady = true;
    }

    private void PlayStageAnimation(HammerState state)
    {
        if (!stageAnimReady) return;
        if (hasAnimatedState && state == lastAnimatedState) return;

        AnimationClip clip = GetStageClip(state);
        if (clip == null)
        {
            hasAnimatedState = false;
            return;
        }

        hasAnimatedState = true;
        lastAnimatedState = state;

        int nextPort = stageAnimPort < 0 ? 0 : 1 - stageAnimPort;

        Playable previous = stageAnimMixer.GetInput(nextPort);
        stageAnimMixer.DisconnectInput(nextPort);
        if (previous.IsValid())
        {
            previous.Destroy();
        }

        AnimationClipPlayable playable = AnimationClipPlayable.Create(stageAnimGraph, clip);
        playable.SetApplyFootIK(false);
        stageAnimMixer.ConnectInput(nextPort, playable, 0, 0f);

        if (stageAnimPort < 0)
        {
            stageAnimPort = nextPort;
            stageAnimMixer.SetInputWeight(nextPort, 1f);
            stageAnimFadeTimer = 1f;
            return;
        }

        stageAnimPort = nextPort;
        stageAnimFadeTimer = 0f;
    }

    private void UpdateStageAnimationFade()
    {
        if (!stageAnimReady || stageAnimPort < 0) return;

        float duration = Mathf.Max(0.0001f, stageAnimationFade);
        stageAnimFadeTimer = Mathf.Min(1f, stageAnimFadeTimer + Time.deltaTime / duration);

        float weight = stageAnimFadeTimer;
        stageAnimMixer.SetInputWeight(stageAnimPort, weight);
        stageAnimMixer.SetInputWeight(1 - stageAnimPort, 1f - weight);
    }
}
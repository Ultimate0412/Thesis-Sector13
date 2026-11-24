using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

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

    [Header("Stage Animations (Optional)")]
    [Tooltip("Animator ของโมเดล (ถ้าเว้นว่าง ระบบจะหา/สร้างให้อัตโนมัติตอนเริ่มเกม)")]
    public Animator hammerAnimator;

    [Tooltip("ท่ากำลังค้นหาผู้เล่น (Searching)")]
    public AnimationClip searchingClip;
    [Tooltip("ท่าง้าง/เล็งก่อนพุ่งชน (Windup)")]
    public AnimationClip windupClip;
    [Tooltip("ท่าพุ่งชน (Charging)")]
    public AnimationClip chargingClip;
    [Tooltip("ท่าชะงักหลังชนกำแพง (Staggered)")]
    public AnimationClip staggeredClip;
    [Tooltip("ท่าพักหลังพุ่งเสร็จ (Cooldown)")]
    public AnimationClip cooldownClip;
    [Tooltip("ท่าติดสตั๊น (Stunned)")]
    public AnimationClip stunnedClip;
    [Tooltip("ท่าตอนถูกผู้เล่นยกถือ (Carried)")]
    public AnimationClip carriedClip;

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
    [SerializeField] private HammerState currentState = HammerState.Searching;

    private Transform playerTarget;
    private Vector3 lockedChargeDirection;
    private float stateTimer = 0f;
    private Coroutine chargeCoroutine;

    // --- Stage animation (Playables) ---
    private PlayableGraph stageAnimGraph;
    private AnimationMixerPlayable stageAnimMixer;
    private bool stageAnimReady;
    private int stageAnimPort = -1;
    private float stageAnimFadeTimer = 1f;
    private HammerState lastAnimatedState;
    private bool hasAnimatedState;

    protected override void Awake()
    {
        base.Awake();
        if (obstacleLayer.value == 0)
        {
            obstacleLayer = LayerMask.GetMask("Default");
        }

        ResolveVfxReferences();
    }

    private void Start()
    {
        FindPlayerReference();
        SetIndicatorColor(normalEyeColor);

        InitStageAnimation();
        PlayStageAnimation(currentState);
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

        if (isDead) return;

        // หากติดสตั๊นหรือถูกยกถือ ให้ข้าม AI
        if (isStunned || isBeingCarried)
        {
            SetState(isBeingCarried ? HammerState.Carried : HammerState.Stunned);
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
                    SetState(HammerState.Searching);
                }
                break;

            case HammerState.Cooldown:
                stateTimer -= Time.deltaTime;
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
                if (stateTimer <= 0f)
                {
                    SetState(HammerState.Searching);
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
        SetState(HammerState.Windup);
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
        SetState(HammerState.Charging);
        EmitChargePuff();
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
            Vector3 hitPoint = collision.contactCount > 0 ? collision.contacts[0].point : transform.position;
            Vector3 hitDir = (player.transform.position - transform.position).normalized;

            LivingEntity targetLiving = player.GetComponent<LivingEntity>();
            if (targetLiving != null)
            {
                targetLiving.TakeDamage(chargeDamage, hitPoint, hitDir);
            }

            // แรงผลักผู้เล่น
            Rigidbody playerRb = player.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                playerRb.AddForce(lockedChargeDirection * knockbackForce + Vector3.up * 2f, ForceMode.Impulse);
            }

            // เอฟเฟกต์กระแทกที่ตัวผู้เล่น (ย่อส่วน)
            SpawnImpactVfx(hitPoint, -hitDir, playerHitVfxScale);

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
                SpawnImpactVfx(collision.contacts[0].point, normal, 1f);
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
        SetState(nextState);
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
        SetState(HammerState.Stunned);
        SetIndicatorColor(Color.gray);

        base.EnterStunState();
    }

    public override void ExitStunState()
    {
        base.ExitStunState();
        SetState(HammerState.Cooldown);
        stateTimer = 1.0f;
        SetIndicatorColor(normalEyeColor);
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
            case HammerState.Searching: multiplier = searchEmission; break;
            case HammerState.Windup: multiplier = 0.15f; break;
            case HammerState.Charging: multiplier = 1f; break;
            default: multiplier = 0f; break;
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

    /// <summary>เสกเอฟเฟกต์กระแทกที่จุดชน</summary>
    private void SpawnImpactVfx(Vector3 position, Vector3 normal, float scale)
    {
        if (impactVfxPrefab == null) return;

        Quaternion rotation = normal.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(normal)
            : Quaternion.identity;

        GameObject vfx = Instantiate(impactVfxPrefab, position, rotation);
        if (!Mathf.Approximately(scale, 1f))
        {
            vfx.transform.localScale = Vector3.one * scale;
        }
    }

    // ---------------------------------------------------------------- Stage Animation

    /// <summary>เปลี่ยนสถานะพร้อมเล่นอนิเมชันของท่านั้น (ถ้ามีการใส่ clip ไว้)</summary>
    private void SetState(HammerState nextState)
    {
        currentState = nextState;
        PlayStageAnimation(nextState);
        UpdateTrailForState(nextState);
    }

    private bool HasAnyStageClip()
    {
        return searchingClip != null || windupClip != null || chargingClip != null ||
               staggeredClip != null || cooldownClip != null || stunnedClip != null ||
               carriedClip != null;
    }

    private AnimationClip GetStageClip(HammerState state)
    {
        switch (state)
        {
            case HammerState.Searching: return searchingClip;
            case HammerState.Windup: return windupClip;
            case HammerState.Charging: return chargingClip;
            case HammerState.Staggered: return staggeredClip;
            case HammerState.Cooldown: return cooldownClip;
            case HammerState.Stunned: return stunnedClip;
            case HammerState.Carried: return carriedClip;
            default: return null;
        }
    }

    /// <summary>
    /// เตรียม PlayableGraph สำหรับเล่น clip ต่อสเตจ
    /// (ทำงานเฉพาะเมื่อมีการใส่ clip อย่างน้อย 1 ท่า — ถ้าใช้ Animator Controller อยู่จะไม่แตะต้อง)
    /// </summary>
    private void InitStageAnimation()
    {
        if (stageAnimReady) return;
        if (!HasAnyStageClip()) return;

        if (hammerAnimator == null)
        {
            hammerAnimator = GetComponent<Animator>();
        }
        if (hammerAnimator == null)
        {
            hammerAnimator = GetComponentInChildren<Animator>();
        }
        if (hammerAnimator == null)
        {
            hammerAnimator = gameObject.AddComponent<Animator>();
        }

        hammerAnimator.applyRootMotion = false;

        stageAnimGraph = PlayableGraph.Create("EnemyHammer_StageAnim");
        stageAnimGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        stageAnimMixer = AnimationMixerPlayable.Create(stageAnimGraph, 2);

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(stageAnimGraph, "StageAnim", hammerAnimator);
        output.SetSourcePlayable(stageAnimMixer);

        stageAnimGraph.Play();
        stageAnimReady = true;
    }

    /// <summary>เล่น clip ของสถานะที่ระบุ (ถ้าไม่ได้ใส่ clip ของท่านั้น จะเล่นท่าเดิมต่อ)</summary>
    private void PlayStageAnimation(HammerState state)
    {
        if (!stageAnimReady) return;
        if (hasAnimatedState && state == lastAnimatedState) return;

        AnimationClip clip = GetStageClip(state);
        if (clip == null)
        {
            // สเตจนี้ไม่ได้ใส่ clip ไว้ — เล่นท่าเดิมต่อไป แต่ล้าง marker
            // เพื่อให้กลับมาสเตจที่มี clip อีกครั้งแล้วเล่นท่าใหม่ได้
            hasAnimatedState = false;
            return;
        }

        hasAnimatedState = true;
        lastAnimatedState = state;

        int nextPort = stageAnimPort < 0 ? 0 : 1 - stageAnimPort;

        // คืนหน่วยความจำของ playable ท่าเดิมก่อนเสียบท่าใหม่
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
            // ท่าแรก: เล่นเต็มน้ำหนักทันที
            stageAnimPort = nextPort;
            stageAnimMixer.SetInputWeight(nextPort, 1f);
            stageAnimFadeTimer = 1f;
            return;
        }

        // สลับท่า: เริ่ม crossfade จาก 0 ไป 1 ใน LateUpdate
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

    private void SetIndicatorColor(Color color)
    {
        if (indicatorRenderer != null && indicatorRenderer.material != null)
        {
            indicatorRenderer.material.color = color;
        }
    }
}

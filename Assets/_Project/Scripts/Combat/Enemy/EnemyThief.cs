using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.Playables;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyThief : EnemyBase
{
    public enum ThiefState
    {
        Spawning,
        Approaching,
        Stealing,
        Fleeing,
        Panicking,
        Escaped,
        Dead
    }

    [Header("Thief Movement Settings")]
    public float searchSpeed = 4.2f;
    public float fleeSpeed = 5.2f;
    public float panicSpeed = 6.2f;
    public float rotationSpeed = 10f;
    public float stealDistance = 1.4f;
    public float escapeDistance = 1.2f;

    [Header("Panic Settings")]
    public float panicDuration = 3f;
    public float droppedPackageIgnoreDuration = 1.5f;

    [Header("Carried Package Slot")]
    public Transform holdPosition;

    [Header("Visual & Feedback")]
    public Renderer indicatorRenderer;
    public Color searchEyeColor = new Color(1.0f, 0.7f, 0.0f);
    public Color fleeEyeColor = new Color(0.9f, 0.1f, 0.1f);
    public GameObject escapeVfxPrefab;

    [Header("Audio Feedback")]
    public AudioClip snatchSound;
    public AudioClip hurtSound;
    public AudioClip deathSound;
    public AudioClip escapeSound;

    [Header("PlayableGraph Animation Clips")]
    public Animator thiefAnimator;
    public AnimationClip spawnClip;
    public AnimationClip walkLoopClip;
    public AnimationClip walkHoldLoopClip;
    public AnimationClip stuntClip;
    public AnimationClip stuntEndClip;
    public AnimationClip carryClip;

    [Header("PlayableGraph Animation Speeds")]
    [Min(0.1f)] public float walkAnimSpeed = 1.0f;
    [Min(0.1f)] public float fleeAnimSpeed = 1.4f;
    [Min(0.1f)] public float panicAnimSpeed = 1.8f;

    [Min(0f)] public float animationFadeDuration = 0.15f;

    [Header("Status (Read-Only)")]
    [SerializeField] private ThiefState currentState = ThiefState.Spawning;
    public ThiefState CurrentState => currentState;

    [HideInInspector] public Transform designatedEscapePoint;
    public PackageBox StolenPackage => stolenPackage;

    private PlayableGraph animGraph;
    private AnimationMixerPlayable rootMixer;
    private AnimationMixerPlayable locomotionMixer;
    private AnimationClipPlayable actionPlayable;
    private bool isGraphReady;
    private float currentHoldBlend;
    private Coroutine actionAnimCoroutine;

    private PackageBox targetPackage;
    private PackageBox stolenPackage;
    private PackageBox lastDroppedPackage;
    private float lastDropTime = -999f;
    private Transform chosenEscapePoint;
    private float currentMoveSpeed;
    private float panicTimer = 0f;
    private float panicRetargetTimer = 0f;
    private Vector3 panicDestination;
    private Coroutine spawnCoroutine;
    private Coroutine stealCoroutine;
    private AudioSource audioSource;
    private Material indicatorMaterialInstance;
    private Vector3 spawnOriginPosition;

    protected override void Awake()
    {
        maxHealth = 70f;
        base.Awake();

        currentMoveSpeed = searchSpeed;
        spawnOriginPosition = transform.position;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 18f;
        }

        if (holdPosition == null)
        {
            GameObject slot = new GameObject("HoldPosition");
            slot.transform.SetParent(transform);
            slot.transform.localPosition = new Vector3(0f, 0.5f, 0.55f);
            holdPosition = slot.transform;
        }

        if (indicatorRenderer != null)
        {
            indicatorMaterialInstance = indicatorRenderer.material;
        }

        if (navAgent != null)
        {
            navAgent.speed = searchSpeed;
            navAgent.angularSpeed = rotationSpeed * 50f;
            navAgent.acceleration = 16f;
            navAgent.stoppingDistance = 0.5f;
            navAgent.autoBraking = false;
        }

        if (stunVfxObject != null)
        {
            stunVfxObject.SetActive(false);
        }
    }

    private void Start()
    {
        InitPlayableGraph();
        SetIndicatorColor(searchEyeColor);
        currentState = ThiefState.Spawning;
        spawnCoroutine = StartCoroutine(SpawningRoutine());
    }

    private void OnDestroy()
    {
        if (animGraph.IsValid())
        {
            animGraph.Destroy();
        }
    }

    private void InitPlayableGraph()
    {
        if (isGraphReady) return;

        if (thiefAnimator == null) thiefAnimator = GetComponent<Animator>();
        if (thiefAnimator == null) thiefAnimator = GetComponentInChildren<Animator>();
        if (thiefAnimator == null) thiefAnimator = gameObject.AddComponent<Animator>();

        thiefAnimator.applyRootMotion = false;
        thiefAnimator.runtimeAnimatorController = null;

        animGraph = PlayableGraph.Create($"{gameObject.name}_ThiefAnimGraph");
        animGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(animGraph, "ThiefAnim", thiefAnimator);

        rootMixer = AnimationMixerPlayable.Create(animGraph, 2);
        output.SetSourcePlayable(rootMixer);

        locomotionMixer = AnimationMixerPlayable.Create(animGraph, 2);

        if (walkLoopClip != null)
        {
            AnimationClipPlayable walkPlayable = AnimationClipPlayable.Create(animGraph, walkLoopClip);
            walkPlayable.SetApplyFootIK(false);
            animGraph.Connect(walkPlayable, 0, locomotionMixer, 0);
        }

        if (walkHoldLoopClip != null)
        {
            AnimationClipPlayable walkHoldPlayable = AnimationClipPlayable.Create(animGraph, walkHoldLoopClip);
            walkHoldPlayable.SetApplyFootIK(false);
            animGraph.Connect(walkHoldPlayable, 0, locomotionMixer, 1);
        }

        animGraph.Connect(locomotionMixer, 0, rootMixer, 0);
        rootMixer.SetInputWeight(0, 1f);
        rootMixer.SetInputWeight(1, 0f);

        locomotionMixer.SetInputWeight(0, 1f);
        locomotionMixer.SetInputWeight(1, 0f);

        animGraph.Play();
        isGraphReady = true;
    }

    private void UpdateLocomotionBlend()
    {
        if (!isGraphReady || !animGraph.IsValid()) return;

        float speed = (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
            ? navAgent.velocity.magnitude
            : 0f;

        float targetAnimSpeed = walkAnimSpeed;
        if (currentState == ThiefState.Fleeing)
        {
            targetAnimSpeed = fleeAnimSpeed;
        }
        else if (currentState == ThiefState.Panicking)
        {
            targetAnimSpeed = panicAnimSpeed;
        }

        locomotionMixer.SetSpeed(speed > 0.1f ? targetAnimSpeed : 0f);

        float targetHoldBlend = (stolenPackage != null && walkHoldLoopClip != null) ? 1f : 0f;
        currentHoldBlend = Mathf.MoveTowards(currentHoldBlend, targetHoldBlend, Time.deltaTime * 8f);

        locomotionMixer.SetInputWeight(0, 1f - currentHoldBlend);
        locomotionMixer.SetInputWeight(1, currentHoldBlend);
    }

    private void ClearActionClip()
    {
        if (actionAnimCoroutine != null)
        {
            StopCoroutine(actionAnimCoroutine);
            actionAnimCoroutine = null;
        }

        if (isGraphReady && animGraph.IsValid())
        {
            rootMixer.SetInputWeight(0, 1f);
            rootMixer.SetInputWeight(1, 0f);

            if (rootMixer.GetInput(1).IsValid())
            {
                animGraph.Disconnect(rootMixer, 1);
                actionPlayable.Destroy();
            }
        }
    }

    private void PlayActionClip(AnimationClip clip, bool lockOnFinish = false, AnimationClip followUpClip = null)
    {
        if (!isGraphReady || clip == null || !animGraph.IsValid()) return;

        if (actionAnimCoroutine != null)
        {
            StopCoroutine(actionAnimCoroutine);
        }

        actionAnimCoroutine = StartCoroutine(ActionClipRoutine(clip, lockOnFinish, followUpClip));
    }

    private IEnumerator ActionClipRoutine(AnimationClip clip, bool lockOnFinish, AnimationClip followUpClip)
    {
        yield return PlaySingleActionSegment(clip, lockOnFinish && followUpClip == null, fadeIn: true, fadeOut: followUpClip == null && !lockOnFinish);

        if (followUpClip != null && !isDead)
        {
            yield return PlaySingleActionSegment(followUpClip, lockOnFinish, fadeIn: false, fadeOut: !lockOnFinish);
        }

        actionAnimCoroutine = null;
    }

    private IEnumerator PlaySingleActionSegment(AnimationClip clip, bool lockOnFinish, bool fadeIn, bool fadeOut)
    {
        if (rootMixer.GetInput(1).IsValid())
        {
            animGraph.Disconnect(rootMixer, 1);
            actionPlayable.Destroy();
        }

        actionPlayable = AnimationClipPlayable.Create(animGraph, clip);
        actionPlayable.SetApplyFootIK(false);
        actionPlayable.SetTime(0);
        actionPlayable.SetSpeed(1);

        animGraph.Connect(actionPlayable, 0, rootMixer, 1);

        float fadeTime = Mathf.Max(0.01f, animationFadeDuration);

        if (fadeIn)
        {
            float timer = 0f;
            while (timer < fadeTime)
            {
                timer += Time.deltaTime;
                float w = Mathf.Clamp01(timer / fadeTime);
                rootMixer.SetInputWeight(0, 1f - w);
                rootMixer.SetInputWeight(1, w);
                yield return null;
            }
        }

        rootMixer.SetInputWeight(0, 0f);
        rootMixer.SetInputWeight(1, 1f);

        float waitTime = Mathf.Max(0.05f, clip.length - (fadeOut ? fadeTime : 0f));
        yield return new WaitForSeconds(waitTime);

        if (fadeOut)
        {
            float timer = 0f;
            while (timer < fadeTime)
            {
                timer += Time.deltaTime;
                float w = Mathf.Clamp01(timer / fadeTime);
                rootMixer.SetInputWeight(0, w);
                rootMixer.SetInputWeight(1, 1f - w);
                yield return null;
            }

            rootMixer.SetInputWeight(0, 1f);
            rootMixer.SetInputWeight(1, 0f);

            if (rootMixer.GetInput(1).IsValid())
            {
                animGraph.Disconnect(rootMixer, 1);
                actionPlayable.Destroy();
            }
        }
        else if (lockOnFinish)
        {
            actionPlayable.SetSpeed(0);
        }
    }

    public override void EnterStunState() { }
    public override void ExitStunState() { }

    public override void OnPickedUpByPlayer(PlayerPickupSystem carrier)
    {
        base.OnPickedUpByPlayer(carrier);
        if (carryClip != null)
        {
            PlayActionClip(carryClip, lockOnFinish: true);
        }
    }

    protected override void Update()
    {
        if (isDead) return;

        if (currentState != ThiefState.Panicking)
        {
            currentMoveSpeed = (currentState == ThiefState.Fleeing) ? fleeSpeed : searchSpeed;
        }

        switch (currentState)
        {
            case ThiefState.Approaching:
                HandleApproaching();
                break;

            case ThiefState.Fleeing:
                HandleFleeing();
                break;

            case ThiefState.Panicking:
                HandlePanicking();
                break;
        }

        UpdateLocomotionBlend();
    }

    private IEnumerator SpawningRoutine()
    {
        StopMovement();

        if (spawnClip != null)
        {
            PlayActionClip(spawnClip, lockOnFinish: false);
            yield return new WaitForSeconds(spawnClip.length);
        }
        else
        {
            yield return new WaitForSeconds(0.4f);
        }

        spawnCoroutine = null;
        if (isDead) yield break;

        FindNearestPackage();

        if (targetPackage != null)
        {
            currentState = ThiefState.Approaching;
        }
        else
        {
            ChooseEscapePoint();
            currentState = ThiefState.Fleeing;
        }
    }

    public void FindNearestPackage()
    {
        PackageBox[] allBoxes = FindObjectsByType<PackageBox>(FindObjectsSortMode.None);
        float closestDist = float.MaxValue;
        PackageBox bestBox = null;

        GameObject playerHeld = (PlayerPickupSystem.Instance != null) ? PlayerPickupSystem.Instance.heldObject : null;

        for (int i = 0; i < allBoxes.Length; i++)
        {
            PackageBox box = allBoxes[i];
            if (box == null || box.gameObject == null) continue;

            if (playerHeld != null && (playerHeld == box.gameObject || playerHeld.transform.IsChildOf(box.transform)))
            {
                continue;
            }

            if (lastDroppedPackage == box && Time.time - lastDropTime < droppedPackageIgnoreDuration)
            {
                continue;
            }

            float dist = Vector3.Distance(transform.position, box.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                bestBox = box;
            }
        }

        targetPackage = bestBox;
    }

    private void HandleApproaching()
    {
        if (targetPackage == null || targetPackage.gameObject == null)
        {
            FindNearestPackage();
            if (targetPackage == null)
            {
                ChooseEscapePoint();
                currentState = ThiefState.Fleeing;
                return;
            }
        }

        Vector3 targetPos = targetPackage.transform.position;
        float distance = Vector3.Distance(transform.position, targetPos);

        if (distance <= stealDistance)
        {
            if (stealCoroutine != null) StopCoroutine(stealCoroutine);
            stealCoroutine = StartCoroutine(StealPackageRoutine(targetPackage));
            return;
        }

        MoveTowards(targetPos, currentMoveSpeed);
    }

    private IEnumerator StealPackageRoutine(PackageBox box)
    {
        currentState = ThiefState.Stealing;
        StopMovement();

        yield return new WaitForSeconds(0.25f);

        if (box != null && box.gameObject != null && !isDead)
        {
            stolenPackage = box;

            BaseDropPoint dp = BaseDropPoint.GetDropPointHolding(box.gameObject);
            if (dp != null)
            {
                dp.currentPlacedItem = null;
            }

            StorageRoomManager storage = FindFirstObjectByType<StorageRoomManager>();
            if (storage != null)
            {
                storage.RemoveItemFromSlots(box.gameObject);
            }

            Rigidbody boxRb = box.GetComponent<Rigidbody>();
            if (boxRb != null)
            {
                boxRb.isKinematic = true;
            }

            Collider boxCol = box.GetComponent<Collider>();
            if (boxCol != null)
            {
                boxCol.enabled = false;
            }

            box.transform.SetParent(holdPosition);
            box.transform.localPosition = Vector3.zero;
            box.transform.localRotation = Quaternion.identity;

            if (snatchSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(snatchSound, 1.0f);
            }
            else
            {
                PlayProceduralChuckle();
            }

            Debug.LogWarning($"<color=#FF8800>[EnemyThief]</color> Thief ขโมยกล่อง <b>{box.name}</b> สำเร็จ! กำลังวิ่งหนี!");
        }

        stealCoroutine = null;
        ChooseEscapePoint();
        SetIndicatorColor(fleeEyeColor);
        currentState = ThiefState.Fleeing;
    }

    private void HandleFleeing()
    {
        if (chosenEscapePoint == null)
        {
            ChooseEscapePoint();
        }

        Vector3 escapePos = (chosenEscapePoint != null) ? chosenEscapePoint.position : spawnOriginPosition;
        float distance = Vector3.Distance(transform.position, escapePos);

        if (distance <= escapeDistance)
        {
            EscapeSuccessfully();
            return;
        }

        MoveTowards(escapePos, currentMoveSpeed);
    }

    private void HandlePanicking()
    {
        panicTimer -= Time.deltaTime;
        panicRetargetTimer -= Time.deltaTime;

        if (panicTimer <= 0f)
        {
            panicTimer = 0f;
            ResumeSearching();
            return;
        }

        if (panicRetargetTimer <= 0f || Vector3.Distance(transform.position, panicDestination) < 1.2f)
        {
            panicRetargetTimer = Random.Range(0.8f, 1.2f);
            panicDestination = GetRandomPanicPosition();
        }

        MoveTowards(panicDestination, panicSpeed);
    }

    private void ResumeSearching()
    {
        SetIndicatorColor(searchEyeColor);
        targetPackage = null;
        FindNearestPackage();

        if (targetPackage != null)
        {
            currentState = ThiefState.Approaching;
            Debug.Log("<color=#00FFAA>[EnemyThief]</color> Thief หายแตกตื่นแล้ว! กำลังกลับไปค้นหาและขโมยพัสดุต่อ");
        }
        else
        {
            ChooseEscapePoint();
            currentState = ThiefState.Fleeing;
        }
    }

    private Vector3 GetRandomPanicPosition()
    {
        Vector3 fleeDir = transform.forward;
        if (PlayerPickupSystem.Instance != null)
        {
            Vector3 awayFromPlayer = (transform.position - PlayerPickupSystem.Instance.transform.position);
            awayFromPlayer.y = 0f;
            if (awayFromPlayer.sqrMagnitude > 0.1f)
            {
                fleeDir = awayFromPlayer.normalized;
            }
        }

        float randomAngle = Random.Range(-75f, 75f);
        Vector3 scatterDir = Quaternion.Euler(0f, randomAngle, 0f) * fleeDir;
        float randomDist = Random.Range(4f, 8f);
        Vector3 candidatePos = transform.position + scatterDir * randomDist;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(candidatePos, out hit, 4f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return candidatePos;
    }

    private void MoveTowards(Vector3 destination, float speed)
    {
        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = false;
            navAgent.speed = speed;
            navAgent.SetDestination(destination);
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

    private void ChooseEscapePoint()
    {
        // Prefer the nearest point from ThiefManager's escape list.
        // A designated point is only a fallback (e.g. no vent list assigned).
        if (ThiefManager.Instance != null && ThiefManager.Instance.escapePoints != null && ThiefManager.Instance.escapePoints.Length > 0)
        {
            float closestDist = float.MaxValue;
            Transform bestPoint = null;

            foreach (var pt in ThiefManager.Instance.escapePoints)
            {
                if (pt == null) continue;
                float dist = Vector3.Distance(transform.position, pt.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    bestPoint = pt;
                }
            }

            if (bestPoint != null)
            {
                chosenEscapePoint = bestPoint;
                return;
            }
        }

        if (designatedEscapePoint != null)
        {
            chosenEscapePoint = designatedEscapePoint;
            return;
        }

        // Fallback: ใช้จุดเกิดเดิม
        GameObject fallbackObj = new GameObject("ThiefFallbackEscape");
        fallbackObj.transform.position = spawnOriginPosition;
        chosenEscapePoint = fallbackObj.transform;
    }

    private void EscapeSuccessfully()
    {
        if (currentState == ThiefState.Escaped || isDead) return;
        currentState = ThiefState.Escaped;

        if (stolenPackage != null)
        {
            stolenPackage.NotifyPackageDestroyed("Stolen by Thief");
            Destroy(stolenPackage.gameObject);
            stolenPackage = null;

            Debug.LogError("<color=#FF2222>[EnemyThief] หัวขโมยหนีรอดไปได้พร้อมพัสดุ! หักคะแนนผู้เล่นเรียบร้อย</color>");
        }

        if (escapeSound != null && audioSource != null)
        {
            AudioSource.PlayClipAtPoint(escapeSound, transform.position, 1.0f);
        }

        if (escapeVfxPrefab != null)
        {
            Instantiate(escapeVfxPrefab, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }

    public override void TakeStunDamage(float stunAmount, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (isDead) return;

        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        if (stealCoroutine != null)
        {
            StopCoroutine(stealCoroutine);
            stealCoroutine = null;
        }

        TakeDamage(stunAmount, hitPoint, hitDirection);

        if (hurtSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(hurtSound, 0.9f);
        }
        else
        {
            PlayProceduralHurtSound();
        }

        if (isDead) return;

        EnterPanic(panicDuration);

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
        {
            navAgent.velocity = hitDirection * 3.5f;
        }
        else if (rb != null)
        {
            rb.AddForce(hitDirection * 4f, ForceMode.Impulse);
        }
    }

    public void EnterPanic(float duration = 3f)
    {
        if (isDead) return;

        ClearActionClip();
        StopMovement();
        DropStolenPackage();

        currentState = ThiefState.Panicking;
        panicTimer = Mathf.Max(0.1f, duration);
        panicRetargetTimer = 0f;
        panicDestination = GetRandomPanicPosition();
        currentMoveSpeed = panicSpeed;
        SetIndicatorColor(fleeEyeColor);

        Debug.Log($"<color=#FFAA00>[EnemyThief]</color> Thief โดนตี! ปล่อยพัสดุและกำลังแตกตื่นวิ่งหนี {panicTimer:0.#} วินาที!");
    }

    protected override void Die()
    {
        base.Die();
        currentState = ThiefState.Dead;

        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        if (stealCoroutine != null)
        {
            StopCoroutine(stealCoroutine);
            stealCoroutine = null;
        }

        StopMovement();
        DropStolenPackage();

        if (deathSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(deathSound, 1.0f);
        }
        else
        {
            PlayProceduralDeathSound();
        }

        PlayActionClip(stuntClip, lockOnFinish: true);

        if (mainCollider != null) mainCollider.enabled = false;
        if (rb != null) { rb.isKinematic = true; }

        Debug.Log("<color=#00FF88>[EnemyThief] หัวขโมยถูกผู้เล่นกำจัดสำเร็จ! พัสดุถูกกู้คืนกลับมาแล้ว</color>");
        Destroy(gameObject, 1.5f);
    }

    public void DropStolenPackage()
    {
        if (stolenPackage != null && stolenPackage.gameObject != null)
        {
            stolenPackage.transform.SetParent(null);

            Rigidbody boxRb = stolenPackage.GetComponent<Rigidbody>();
            if (boxRb != null)
            {
                boxRb.isKinematic = false;
                boxRb.useGravity = true;
                boxRb.linearVelocity = Vector3.up * 1.5f + Random.insideUnitSphere * 0.8f;
            }

            Collider boxCol = stolenPackage.GetComponent<Collider>();
            if (boxCol != null)
            {
                boxCol.enabled = true;
            }

            lastDroppedPackage = stolenPackage;
            lastDropTime = Time.time;

            Debug.Log($"[EnemyThief] ดรอปพัสดุ '{stolenPackage.name}' คืนสู่พื้นเรียบร้อย");
            stolenPackage = null;
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

    private void PlayProceduralChuckle()
    {
        StartCoroutine(ProceduralSoundRoutine(700f, 0.08f, 2));
    }

    private void PlayProceduralHurtSound()
    {
        StartCoroutine(ProceduralSoundRoutine(350f, 0.12f, 1));
    }

    private void PlayProceduralDeathSound()
    {
        StartCoroutine(ProceduralSoundRoutine(220f, 0.25f, 1));
    }

    private IEnumerator ProceduralSoundRoutine(float frequency, float duration, int repeats)
    {
        if (audioSource == null) yield break;

        for (int i = 0; i < repeats; i++)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            AudioClip clip = AudioClip.Create("ThiefProcSound", sampleCount, 1, sampleRate, false);
            float[] samples = new float[sampleCount];

            for (int s = 0; s < sampleCount; s++)
            {
                float t = (float)s / sampleRate;
                samples[s] = Mathf.Sin(2f * Mathf.PI * frequency * t) * (1f - (float)s / sampleCount);
            }

            clip.SetData(samples, 0);
            audioSource.PlayOneShot(clip, 0.8f);

            yield return new WaitForSeconds(duration + 0.05f);
        }
    }
}

// --- สรุปการทำงานของโค้ด ---
// 1. เพิ่มตัวแปรปรับความเร็วอนิเมชันใน Inspector: walkAnimSpeed (ปกติ), fleeAnimSpeed (ตอนหนี), panicAnimSpeed (ตอนแตกตื่น)
// 2. ใน UpdateLocomotionBlend() จะเช็ก currentState เพื่อจ่ายค่าความเร็วให้ locomotionMixer.SetSpeed() ทำให้ walkLoopClip และ walkHoldLoopClip เล่นเร็วขึ้นตามสถานะ Fleeing และ Panicking
// 3. เพิ่ม ClearActionClip() และเรียกใช้ใน EnterPanic() พร้อมเอา PlayActionClip(stuntClip) ออกจาก TakeStunDamage() เพื่อให้ตอนโดนตีแล้วเข้าสู่สถานะ Panicking กลับมาเล่นท่า walkLoopClip แบบเร่งความเร็วทันทีโดยไม่ถูกท่า Stunt ทับ
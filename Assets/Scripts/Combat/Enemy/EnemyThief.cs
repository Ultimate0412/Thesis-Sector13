using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// ศัตรูประเภทใหม่: Thief (หัวขโมยพัสดุ)
/// พฤติกรรม:
/// 1. ไม่โจมตีผู้เล่น (Non-Hostile)
/// 2. ตีให้ตายได้โดยตรง (HP = 70, ฟาด 2 ครั้งตาย)
/// 3. เกิดออกมาแล้วจะมองหาพัสดุที่ใกล้ที่สุด (PackageBox) แล้ววิ่งเข้าไปขโมย
/// 4. เมื่ออุ้มพัสดุแล้ว จะวิ่งหนีไปยังจุดหลบหนี (Escape Point)
/// 5. หากหนีรอดไปได้สำเร็จ พัสดุจะถูกทำลายและผู้เล่นจะถูกหักคะแนน
/// 6. เมื่อถูกตีจะปล่อยพัสดุตกพื้น และแตกตื่นวิ่งมั่วๆ 3 วินาทีก่อนกลับมาขโมยต่อ
/// 7. หากผู้เล่นตีจนตาย จะดรอปพัสดุคืนลงบนพื้นให้เก็บกลับมาได้
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyThief : EnemyBase
{
    public enum ThiefState
    {
        Spawning,       // เพิ่งเกิด กำลังมองหาพัสดุ
        Approaching,    // กำลังวิ่งเข้าหาพัสดุเป้าหมาย
        Stealing,       // กำลังหยิบพัสดุขึ้นมาถือ
        Fleeing,        // กำลังวิ่งหนีไปยังจุด Escape Point
        Panicking,      // โดนตี ตื่นตระหนกปล่อยพัสดุแล้ววิ่งมั่วๆ 3 วินาที
        Escaped,        // หลบหนีสำเร็จ
        Dead            // ถูกผู้เล่นกำจัด
    }

    [Header("Thief Movement Settings")]
    [Tooltip("ความเร็วในการวิ่งเข้าหาพัสดุ")]
    public float searchSpeed = 4.2f;

    [Tooltip("ความเร็วในการวิ่งหนีไปยังจุด Escape Point")]
    public float fleeSpeed = 5.2f;

    [Tooltip("ความเร็วตื่นตระหนกเมื่อถูกผู้เล่นตี (Panic Sprint)")]
    public float panicSpeed = 6.2f;

    [Tooltip("ความเร็วในการหันหน้า (หมุนตัว)")]
    public float rotationSpeed = 10f;

    [Tooltip("ระยะที่ถือว่าเข้าถึงพัสดุแล้วเพื่อขโมย")]
    public float stealDistance = 1.4f;

    [Tooltip("ระยะที่ถือว่าถึงจุดหลบหนีแล้ว")]
    public float escapeDistance = 1.2f;

    [Header("Panic Settings")]
    [Tooltip("ระยะเวลาตื่นตระหนกหลังถูกตี (วินาที)")]
    public float panicDuration = 3f;

    [Tooltip("หลังทำพัสดุตก จะไม่เลือกกล่องเดิมซ้ำภายในเวลานี้ (วินาที)")]
    public float droppedPackageIgnoreDuration = 1.5f;

    [Header("Carried Package Slot")]
    [Tooltip("ตำแหน่งหน้าอก/มือของ Thief สำหรับยึดกล่องพัสดุที่ขโมยมา")]
    public Transform holdPosition;

    [Header("Visual & Feedback")]
    [Tooltip("Renderer ส่วนดวงตา/หน้ากาก สำหรับเปลี่ยนสีตามสถานะ")]
    public Renderer indicatorRenderer;
    public Color searchEyeColor = new Color(1.0f, 0.7f, 0.0f); // ส้ม
    public Color fleeEyeColor = new Color(0.9f, 0.1f, 0.1f);   // แดง
    public GameObject escapeVfxPrefab;

    [Header("Audio Feedback")]
    public AudioClip snatchSound;
    public AudioClip hurtSound;
    public AudioClip deathSound;
    public AudioClip escapeSound;

    [Header("Animator Settings")]
    public Animator thiefAnimator;
    public string speedParamName = "Speed";
    public string hurtTriggerName = "Hurt";
    public string dieTriggerName = "Die";
    public string stealTriggerName = "Steal";

    [Header("Status (Read-Only)")]
    [SerializeField] private ThiefState currentState = ThiefState.Spawning;
    public ThiefState CurrentState => currentState;

    [HideInInspector] public Transform designatedEscapePoint;
    public PackageBox StolenPackage => stolenPackage;

    private PackageBox targetPackage;
    private PackageBox stolenPackage;
    private PackageBox lastDroppedPackage;
    private float lastDropTime = -999f;
    private Transform chosenEscapePoint;
    private float currentMoveSpeed;
    private float panicTimer = 0f;
    private float panicRetargetTimer = 0f;
    private Vector3 panicDestination;
    private Coroutine stealCoroutine;
    private AudioSource audioSource;
    private Material indicatorMaterialInstance;
    private Vector3 spawnOriginPosition;

    protected override void Awake()
    {
        // กำหนดเลือด Thief = 70 (โดนไม้ 35 ดาเมจ ตี 2 ครั้งตาย)
        maxHealth = 70f;
        base.Awake();

        currentMoveSpeed = searchSpeed;
        spawnOriginPosition = transform.position;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f; // 3D Sound
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

        if (thiefAnimator == null)
        {
            thiefAnimator = GetComponentInChildren<Animator>();
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
    }

    private void Start()
    {
        SetIndicatorColor(searchEyeColor);
        currentState = ThiefState.Spawning;
        StartCoroutine(SpawningRoutine());
    }

    private IEnumerator SpawningRoutine()
    {
        yield return new WaitForSeconds(0.4f);
        FindNearestPackage();

        if (targetPackage != null)
        {
            currentState = ThiefState.Approaching;
        }
        else
        {
            // หากไม่มีพัสดุในฉากเลย ให้วิ่งหนีกลับทันที
            ChooseEscapePoint();
            currentState = ThiefState.Fleeing;
        }
    }

    protected override void Update()
    {
        base.Update();

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

        UpdateAnimator();
    }

    /// <summary>
    /// ค้นหา PackageBox ที่ใกล้ที่สุดในฉาก (ไม่รวมกล่องที่ผู้เล่นกำลังถืออยู่)
    /// </summary>
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

            // ไม่เลือกกล่องที่ผู้เล่นกำลังถืออยู่
            if (playerHeld != null && (playerHeld == box.gameObject || playerHeld.transform.IsChildOf(box.transform)))
            {
                continue;
            }

            // ไม่เลือกกล่องที่เพิ่งทำตกจากอาการตื่นตระหนกทันที (กันการขโมยกล่องเดิมซ้ำ)
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
            // ถึงตัวกล่องแล้ว ทำการขโมย!
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

        if (thiefAnimator != null && !string.IsNullOrEmpty(stealTriggerName))
        {
            thiefAnimator.SetTrigger(stealTriggerName);
        }

        // หน่วงเวลาหยิบของสั้นๆ (0.25 วินาที)
        yield return new WaitForSeconds(0.25f);

        if (box != null && box.gameObject != null && !isDead)
        {
            stolenPackage = box;

            // 1. ปลดล็อกออกจาก BaseDropPoint (เช่น ชั้นวางของใน Storage หรือ Shipping)
            BaseDropPoint dp = BaseDropPoint.GetDropPointHolding(box.gameObject);
            if (dp != null)
            {
                dp.currentPlacedItem = null;
            }

            // 2. ปลดล็อกออกจาก StorageRoomManager
            StorageRoomManager storage = FindFirstObjectByType<StorageRoomManager>();
            if (storage != null)
            {
                storage.RemoveItemFromSlots(box.gameObject);
            }

            // 3. อุ้มกล่องพัสดุขึ้นมาไว้ที่ holdPosition
            Rigidbody boxRb = box.GetComponent<Rigidbody>();
            if (boxRb != null)
            {
                boxRb.isKinematic = true;
            }

            Collider boxCol = box.GetComponent<Collider>();
            if (boxCol != null)
            {
                boxCol.enabled = false; // ปิด Collider ชั่วคราวเพื่อไม่ให้เบียดกับ Thief
            }

            box.transform.SetParent(holdPosition);
            box.transform.localPosition = Vector3.zero;
            box.transform.localRotation = Quaternion.identity;

            // 4. เล่นเสียงขโมยของ
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

    /// <summary>
    /// พฤติกรรมเมื่อโดนตี: วิ่งแตกตื่นมั่วๆ ตามระยะเวลา panicDuration หลบหลีกผู้เล่น
    /// </summary>
    private void HandlePanicking()
    {
        panicTimer -= Time.deltaTime;
        panicRetargetTimer -= Time.deltaTime;

        // เมื่อครบกำหนด ให้หยุดแตกตื่นและกลับไปมองหาพัสดุใหม่
        if (panicTimer <= 0f)
        {
            panicTimer = 0f;
            ResumeSearching();
            return;
        }

        // สุ่มทิศทางวิ่งมั่วๆ ทุกๆ 0.8 - 1.2 วินาที หรือเมื่อใกล้ถึงจุดหมายเดิม
        if (panicRetargetTimer <= 0f || Vector3.Distance(transform.position, panicDestination) < 1.2f)
        {
            panicRetargetTimer = Random.Range(0.8f, 1.2f);
            panicDestination = GetRandomPanicPosition();
        }

        MoveTowards(panicDestination, panicSpeed);
    }

    /// <summary>
    /// ออกจากอาการตื่นตระหนก กลับสู่โหมดปกติและค้นหาพัสดุใหม่
    /// </summary>
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

        // สุ่มมุมกระจายตัว -75 ถึง +75 องศาเพื่อวิ่งแตกตื่น
        float randomAngle = Random.Range(-75f, 75f);
        Vector3 scatterDir = Quaternion.Euler(0f, randomAngle, 0f) * fleeDir;
        float randomDist = Random.Range(4f, 8f);
        Vector3 candidatePos = transform.position + scatterDir * randomDist;

        // หากมี NavMesh ให้คำนวณตำแหน่งที่ถูกต้องบน NavMesh
        NavMeshHit hit;
        if (NavMesh.SamplePosition(candidatePos, out hit, 4f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return candidatePos;
    }

    /// <summary>
    /// การเคลื่อนที่ด้วย NavMeshAgent 100%
    /// </summary>
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
        if (designatedEscapePoint != null)
        {
            chosenEscapePoint = designatedEscapePoint;
            return;
        }

        // ค้นหา Escape Point จาก ThiefManager
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

        // Fallback: ใช้จุดเกิดเดิม
        GameObject fallbackObj = new GameObject("ThiefFallbackEscape");
        fallbackObj.transform.position = spawnOriginPosition;
        chosenEscapePoint = fallbackObj.transform;
    }

    /// <summary>
    /// หนีรอดไปได้สำเร็จ: ทำลายพัสดุ, หักคะแนนผู้เล่น, และ Despawn
    /// </summary>
    private void EscapeSuccessfully()
    {
        if (currentState == ThiefState.Escaped || isDead) return;
        currentState = ThiefState.Escaped;

        if (stolenPackage != null)
        {
            // หักคะแนนผู้เล่นและแจ้งเตือน
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

    /// <summary>
    /// ถูกผู้เล่นใช้อาวุธโจมตี: ลดเลือด (HP) โดยตรง และไม่โจมตีกลับ!
    /// ปล่อยพัสดุทันที และเข้าสู่โหมดแตกตื่นวิ่งมั่วๆ ตาม panicDuration
    /// </summary>
    public override void TakeStunDamage(float stunAmount, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (isDead) return;

        // 1. ยกเลิกการหยิบของที่อาจกำลังทำอยู่
        if (stealCoroutine != null)
        {
            StopCoroutine(stealCoroutine);
            stealCoroutine = null;
        }

        // 2. หักพลังชีวิตโดยตรง
        TakeDamage(stunAmount, hitPoint, hitDirection);

        if (hurtSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(hurtSound, 0.9f);
        }
        else
        {
            PlayProceduralHurtSound();
        }

        if (thiefAnimator != null && !string.IsNullOrEmpty(hurtTriggerName))
        {
            thiefAnimator.SetTrigger(hurtTriggerName);
        }

        // หากผู้เล่นฟาดจน HP หมด (Die() จะถูกเรียกจาก TakeDamage) ให้หยุดทำงานตรงนี้
        if (isDead) return;

        // 3. ปล่อยพัสดุ + เข้าสู่โหมดตื่นตระหนก (Panicking) วิ่งมั่วๆ ตาม panicDuration
        EnterPanic(panicDuration);

        // 4. แรงกระแทกถอยหลังชั่วขณะ
        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
        {
            navAgent.velocity = hitDirection * 3.5f;
        }
        else if (rb != null)
        {
            rb.AddForce(hitDirection * 4f, ForceMode.Impulse);
        }
    }

    /// <summary>
    /// เข้าสู่โหมดตื่นตระหนก: ปล่อยพัสดุที่ถืออยู่ แล้ววิ่งมั่วเป็นเวลา <paramref name="duration"/> วินาที
    /// เมื่อครบเวลาจะกลับไปค้นหาพัสดุตามปกติโดยอัตโนมัติ (ดู ResumeSearching)
    /// </summary>
    public void EnterPanic(float duration = 3f)
    {
        if (isDead) return;

        // หยุดการเคลื่อนที่เดิมก่อน เพื่อให้แรงกระแทกและทิศวิ่งใหม่ทำงานถูกต้อง
        StopMovement();

        // ปล่อยพัสดุที่กำลังถืออยู่ทันที
        DropStolenPackage();

        currentState = ThiefState.Panicking;
        panicTimer = Mathf.Max(0.1f, duration);
        panicRetargetTimer = 0f;
        panicDestination = GetRandomPanicPosition();
        currentMoveSpeed = panicSpeed;
        SetIndicatorColor(fleeEyeColor);

        Debug.Log($"<color=#FFAA00>[EnemyThief]</color> Thief โดนตี! ปล่อยพัสดุและกำลังแตกตื่นวิ่งหนี {panicTimer:0.#} วินาที!");
    }

    /// <summary>
    /// เมื่อพลังชีวิตหมด (ตาย): ดรอปพัสดุคืนลงบนพื้น และ Despawn
    /// </summary>
    protected override void Die()
    {
        base.Die();
        currentState = ThiefState.Dead;

        if (stealCoroutine != null)
        {
            StopCoroutine(stealCoroutine);
            stealCoroutine = null;
        }

        StopMovement();

        // ดรอปพัสดุที่ขโมยมาคืนสู่พื้นทันที!
        DropStolenPackage();

        if (deathSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(deathSound, 1.0f);
        }
        else
        {
            PlayProceduralDeathSound();
        }

        if (thiefAnimator != null && !string.IsNullOrEmpty(dieTriggerName))
        {
            thiefAnimator.SetTrigger(dieTriggerName);
        }

        // ปิด Collider และฟิสิกส์ เพื่อให้ผู้เล่นเดินผ่านได้สะดวก
        if (mainCollider != null) mainCollider.enabled = false;
        if (rb != null) { rb.isKinematic = true; }

        Debug.Log("<color=#00FF88>[EnemyThief] หัวขโมยถูกผู้เล่นกำจัดสำเร็จ! พัสดุถูกกู้คืนกลับมาแล้ว</color>");
        Destroy(gameObject, 1.5f);
    }

    /// <summary>
    /// ดรอปพัสดุที่ถืออยู่ลงบนพื้น
    /// </summary>
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

    private void UpdateAnimator()
    {
        if (thiefAnimator == null) return;

        if (!string.IsNullOrEmpty(speedParamName))
        {
            float speed = (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
                ? navAgent.velocity.magnitude
                : 0f;
            thiefAnimator.SetFloat(speedParamName, speed);
        }
    }

    // --- Procedural Sound Helpers (ป้องกันเกมเงียบเมื่อยังไม่ได้ใส่ AudioClip ใน Inspector) ---
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

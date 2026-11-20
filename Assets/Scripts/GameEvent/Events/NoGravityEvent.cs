using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// ข้อมูลสถานะฟิสิกส์เดิมของ Rigidbody ก่อนเข้าสู่สภาวะ Zero-G
/// </summary>
public class OriginalPhysicsState
{
    public bool originalUseGravity;
    public float originalDrag;
    public float originalAngularDrag;
    public float initialHeight;
    public float targetHoverHeight;
    public float randomPhase;
}


public class NoGravityEvent : GameEventBase
{
    [Header("Zero-G Physics Settings")]
    [Tooltip("ความเร่งในการยกตัวลอยขึ้นสู่ระดับลอยตัว (m/s^2)")]
    public float upwardAcceleration = 2.4f;

    [Tooltip("ความเร็วสูงสุดในการลอยตัวแนวตั้ง")]
    public float maxVerticalSpeed = 1.1f;

    [Tooltip("ความสูงสัมพัทธ์ในการลอยตัวเฉลี่ย (เมตรจากจุดเดิม)")]
    public float averageHoverHeight = 1.3f;

    [Tooltip("ความแรงของการหมุนคว้างช้าๆ (Micro-Torque)")]
    public float tumbleTorqueStrength = 0.45f;

    [Header("Air Resistance / Damping in Zero-G")]
    [Tooltip("ค่าความหน่วงการเคลื่อนที่ในสภาวะลอยเคว้ง")]
    public float zeroGLinearDamping = 1.2f;

    [Tooltip("ค่าความหน่วงการหมุนในสภาวะลอยเคว้ง")]
    public float zeroGAngularDamping = 0.8f;

    [Header("Target Inclusions (Option B)")]
    [Tooltip("ให้กล่องบนชั้นวาง (Storage Drop Points) หลุดลอยออกมาด้วยหรือไม่ (Option B = true)")]
    public bool affectPlacedShelfItems = true;

    [Tooltip("ให้ศัตรู (Entities: EnemyHammer, EnemyMimic ฯลฯ) ลอยเคว้งด้วยหรือไม่")]
    public bool affectEnemies = true;

    [Tooltip("ให้ผู้เล่นได้รับผลกระทบฟิสิกส์กระโดดสูง/ร่อนช้าแบบนักบินอวกาศหรือไม่")]
    public bool affectPlayer = true;

    [Tooltip("ตัวคูณความสูงในการกระโดดของผู้เล่นขณะอยู่ในสภาวะ Zero-G")]
    public float playerJumpMultiplier = 1.6f;

    [Tooltip("แรงพยุงการตกของผู้เล่น (ทำให้ร่อนลงมาช้าๆ นุ่มนวล)")]
    public float playerFallBuoyancy = 0.7f;

    [Header("Optimization Settings (การกระจายโหลดเพื่อความลื่นไหล)")]
    [Tooltip("จำนวนวัตถุที่จะทยอยปลดลอยตัวต่อ 1 Batch")]
    public int batchSize = 4;

    [Tooltip("หน่วงเวลาระหว่างแต่ละ Batch (วินาที) เพื่อให้เกิดเอฟเฟกต์ระลอกคลื่นลอยตัวและไม่กระตุก")]
    public float batchDelay = 0.05f;

    public static NoGravityEvent ActiveInstance { get; private set; }

    // รายการแคช Rigidbodies ที่กำลังลอยตัว
    private Dictionary<Rigidbody, OriginalPhysicsState> trackedRigidbodies = new Dictionary<Rigidbody, OriginalPhysicsState>();
    private List<Rigidbody> rigidbodiesList = new List<Rigidbody>();
    private Coroutine liftOffCoroutine;
    private PlayerController playerController;
    private float originalPlayerJumpHeight = 1.2f;

    public NoGravityEvent()
    {
        eventId = "NoGravity";
        eventName = "Zero Gravity Malfunction";
        eventDescription = "Sector 13 gravity generators offline! All loose freight and entities are floating.";
        eventColor = new Color(0.15f, 0.8f, 1.0f);
        duration = 28f;
    }

    private void Reset()
    {
        eventId = "NoGravity";
        eventName = "Zero Gravity Malfunction";
        eventDescription = "Sector 13 gravity generators offline! All loose freight and entities are floating.";
        eventColor = new Color(0.15f, 0.8f, 1.0f);
        duration = 28f;
    }

    protected override void Awake()
    {
        // กำหนดข้อมูลพื้นฐานของอีเวนต์
        eventId = "NoGravity";
        eventName = "Zero Gravity Malfunction";
        eventDescription = "Sector 13 gravity generators offline! All loose freight and entities are floating.";
        eventColor = new Color(0.15f, 0.8f, 1.0f); // Bright Sci-Fi Cyan
        duration = 28f;

        base.Awake();
    }

    protected override void OnEventStart()
    {
        ActiveInstance = this;
        trackedRigidbodies.Clear();
        rigidbodiesList.Clear();

        // ค้นหาผู้เล่น
        playerController = FindFirstObjectByType<PlayerController>();
        if (playerController != null)
        {
            originalPlayerJumpHeight = playerController.jumpHeight;
            if (affectPlayer)
            {
                playerController.jumpHeight = originalPlayerJumpHeight * playerJumpMultiplier;
            }
        }

        // เริ่ม Coroutine ทยอยปลดล็อกและยกตัววัตถุเป็นระลอกคลื่น (Staggered Wave)
        if (liftOffCoroutine != null) StopCoroutine(liftOffCoroutine);
        liftOffCoroutine = StartCoroutine(StaggeredWaveLiftOffRoutine());
    }

    /// <summary>
    /// Coroutine สำหรับทยอยปลดและยกตัววัตถุเป็นระลอกคลื่น
    /// </summary>
    private IEnumerator StaggeredWaveLiftOffRoutine()
    {
        // 1. จัดการกล่องบนชั้นวาง (Option B): ปลดล็อกและดีดออกมาข้างหน้านิดหน่อยอย่างนุ่มนวล
        if (affectPlacedShelfItems)
        {
            List<BaseDropPoint> dropPointsCopy = new List<BaseDropPoint>(BaseDropPoint.allDropPoints);
            StorageRoomManager storageManager = FindFirstObjectByType<StorageRoomManager>();

            int countInBatch = 0;
            for (int i = 0; i < dropPointsCopy.Count; i++)
            {
                BaseDropPoint dp = dropPointsCopy[i];
                if (dp != null && dp.currentPlacedItem != null)
                {
                    GameObject itemObj = dp.currentPlacedItem;
                    dp.currentPlacedItem = null;

                    if (storageManager != null)
                    {
                        storageManager.RemoveItemFromSlots(itemObj);
                    }

                    // ปลด Parent และขยับพ้นชั้นวางเล็กน้อย
                    itemObj.transform.SetParent(null);
                    Vector3 forwardNudge = dp.transform.forward * 0.25f + Vector3.up * 0.05f;
                    itemObj.transform.position += forwardNudge;

                    Rigidbody rb = itemObj.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.isKinematic = false;
                        RegisterRigidbody(rb);
                        // ใส่แรงผลักเบาๆ ให้ออกจากช่องชั้นวาง
                        rb.AddForce(forwardNudge * 1.5f, ForceMode.Impulse);
                    }

                    countInBatch++;
                    if (countInBatch >= batchSize)
                    {
                        countInBatch = 0;
                        yield return new WaitForSeconds(batchDelay);
                    }
                }
            }
        }

        // 2. ค้นหากล่องพัสดุและสิ่งของที่วางอยู่บนพื้นทั่วไป (ItemObject, PackageBox, MeleeWeapon)
        PackageBox[] allBoxes = FindObjectsByType<PackageBox>(FindObjectsSortMode.None);
        int itemBatchCount = 0;
        for (int i = 0; i < allBoxes.Length; i++)
        {
            if (allBoxes[i] != null)
            {
                Rigidbody rb = allBoxes[i].GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                {
                    RegisterRigidbody(rb);

                    itemBatchCount++;
                    if (itemBatchCount >= batchSize)
                    {
                        itemBatchCount = 0;
                        yield return new WaitForSeconds(batchDelay);
                    }
                }
            }
        }

        ItemObject[] allItems = FindObjectsByType<ItemObject>(FindObjectsSortMode.None);
        for (int i = 0; i < allItems.Length; i++)
        {
            if (allItems[i] != null)
            {
                Rigidbody rb = allItems[i].GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                {
                    RegisterRigidbody(rb);
                }
            }
        }

        MeleeWeapon[] allWeapons = FindObjectsByType<MeleeWeapon>(FindObjectsSortMode.None);
        for (int i = 0; i < allWeapons.Length; i++)
        {
            if (allWeapons[i] != null)
            {
                Rigidbody rb = allWeapons[i].GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                {
                    RegisterRigidbody(rb);
                }
            }
        }

        // 3. จัดการสิ่งมีชีวิตและศัตรู (Entities)
        if (affectEnemies)
        {
            EnemyBase[] allEnemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
            for (int i = 0; i < allEnemies.Length; i++)
            {
                if (allEnemies[i] != null)
                {
                    // ปิด NavMeshAgent ชั่วคราวเพื่อให้ฟิสิกส์ลอยตัวทำงานได้สมบูรณ์
                    NavMeshAgent agent = allEnemies[i].GetComponent<NavMeshAgent>();
                    if (agent != null && agent.enabled)
                    {
                        agent.enabled = false;
                    }

                    Rigidbody rb = allEnemies[i].GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        RegisterRigidbody(rb);
                    }
                }
            }
        }

        liftOffCoroutine = null;
    }

    /// <summary>
    /// บันทึกและปรับแต่ง Rigidbody ให้เข้าสู่สถานะ Zero-G
    /// </summary>
    public void RegisterRigidbody(Rigidbody rb)
    {
        if (rb == null || trackedRigidbodies.ContainsKey(rb)) return;

        // ข้ามผู้เล่น (จัดการแยกต่างหาก)
        if (playerController != null && rb == playerController.rb) return;

        OriginalPhysicsState state = new OriginalPhysicsState
        {
            originalUseGravity = rb.useGravity,
#if UNITY_6000_0_OR_NEWER
            originalDrag = rb.linearDamping,
            originalAngularDrag = rb.angularDamping,
#else
            originalDrag = rb.drag,
            originalAngularDrag = rb.angularDrag,
#endif
            initialHeight = rb.position.y,
            targetHoverHeight = rb.position.y + Random.Range(averageHoverHeight * 0.7f, averageHoverHeight * 1.4f),
            randomPhase = Random.Range(0f, 100f)
        };

        trackedRigidbodies[rb] = state;
        rigidbodiesList.Add(rb);

        // ปิดแรงโน้มถ่วง และปรับค่า Damping ให้ลอยนุ่มนวล
        rb.useGravity = false;
#if UNITY_6000_0_OR_NEWER
        rb.linearDamping = zeroGLinearDamping;
        rb.angularDamping = zeroGAngularDamping;
#else
        rb.drag = zeroGLinearDamping;
        rb.angularDrag = zeroGAngularDamping;
#endif

        // ให้แรงลอยตัวเริ่มต้นเบาๆ
        rb.AddForce(Vector3.up * Random.Range(0.8f, 1.6f) + Random.insideUnitSphere * 0.2f, ForceMode.Impulse);
    }

    protected override void OnEventFixedUpdate()
    {
        // 1. ตรวจสอบของที่ผู้เล่นเพิ่งปล่อยมือหรือสปอนใหม่เข้ามา
        if (PlayerPickupSystem.Instance != null && PlayerPickupSystem.Instance.heldObject != null)
        {
            // ของที่ถืออยู่ในมือไม่ต้องคำนวณแรง
        }

        // 2. ควบคุมการลอยตัวของ Rigidbodies ทั้งหมด
        for (int i = rigidbodiesList.Count - 1; i >= 0; i--)
        {
            Rigidbody rb = rigidbodiesList[i];
            if (rb == null)
            {
                rigidbodiesList.RemoveAt(i);
                continue;
            }

            // หากเป็นวัตถุที่กำลังถูกผู้เล่นอุ้มถืออยู่ ให้ข้ามการใส่แรง
            if (PlayerPickupSystem.Instance != null && PlayerPickupSystem.Instance.heldObject == rb.gameObject)
            {
                continue;
            }

            if (!trackedRigidbodies.TryGetValue(rb, out OriginalPhysicsState state))
            {
                continue;
            }

            // คำนวณความสูงและทิศทางแรงพยุง (Damping Buoyancy)
            float currentY = rb.position.y;
            float heightDiff = state.targetHoverHeight - currentY;

            // การแกว่งตัวนุ่มนวล (Sinusoidal Bobbing)
            float bobbing = Mathf.Sin(Time.time * 1.5f + state.randomPhase) * 0.15f;
            float desiredUpwardForce = (heightDiff + bobbing) * upwardAcceleration;

            // จำกัดความเร็วแนวตั้งไม่ให้พุ่งทะลุเพดาน
#if UNITY_6000_0_OR_NEWER
            Vector3 vel = rb.linearVelocity;
#else
            Vector3 vel = rb.velocity;
#endif
            if (vel.y > maxVerticalSpeed)
            {
                desiredUpwardForce = 0f;
            }

            // ใส่แรงยกตัว
            rb.AddForce(Vector3.up * desiredUpwardForce, ForceMode.Acceleration);

            // ใส่แรงหมุนเบาๆ (Micro-Torque & Tumble)
            Vector3 torque = new Vector3(
                Mathf.Sin(Time.time * 0.7f + state.randomPhase),
                Mathf.Cos(Time.time * 0.5f + state.randomPhase),
                Mathf.Sin(Time.time * 0.9f + state.randomPhase)
            ) * tumbleTorqueStrength;

            rb.AddTorque(torque, ForceMode.Acceleration);
        }

        // 3. ปรับฟิสิกส์การตกของผู้เล่น (Astronaut Jump Physics)
        if (affectPlayer && playerController != null && playerController.rb != null)
        {
            if (!playerController.isGrounded)
            {
#if UNITY_6000_0_OR_NEWER
                Vector3 pVel = playerController.rb.linearVelocity;
#else
                Vector3 pVel = playerController.rb.velocity;
#endif
                // เมื่อผู้เล่นกำลังตกลงมา (pVel.y < 0) ให้ช่วยชะลอแรงตก
                if (pVel.y < -0.5f)
                {
                    playerController.rb.AddForce(Vector3.up * (Mathf.Abs(Physics.gravity.y) * playerFallBuoyancy), ForceMode.Acceleration);
                }
            }
        }
    }

    protected override void OnEventEnd()
    {
        if (ActiveInstance == this) ActiveInstance = null;

        if (liftOffCoroutine != null)
        {
            StopCoroutine(liftOffCoroutine);
            liftOffCoroutine = null;
        }

        // คืนค่าฟิสิกส์เดิมให้ Rigidbodies ทั้งหมด
        foreach (var kvp in trackedRigidbodies)
        {
            Rigidbody rb = kvp.Key;
            OriginalPhysicsState state = kvp.Value;

            if (rb != null)
            {
                rb.useGravity = state.originalUseGravity;
#if UNITY_6000_0_OR_NEWER
                rb.linearDamping = state.originalDrag;
                rb.angularDamping = state.originalAngularDrag;
#else
                rb.drag = state.originalDrag;
                rb.angularDrag = state.originalAngularDrag;
#endif

                // คืนการทำงานของ NavMeshAgent ให้ศัตรูที่ยังมีชีวิตและไม่ได้ถูกอุ้ม/สตั๊น
                EnemyBase enemy = rb.GetComponent<EnemyBase>();
                if (enemy != null && !enemy.IsDead && !enemy.IsStunned && !enemy.IsBeingCarried)
                {
                    NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
                    if (agent != null)
                    {
                        agent.enabled = true;
                    }
                }
            }
        }

        trackedRigidbodies.Clear();
        rigidbodiesList.Clear();

        // คืนค่าการกระโดดของผู้เล่น
        if (playerController != null)
        {
            playerController.jumpHeight = originalPlayerJumpHeight;
        }
    }
}

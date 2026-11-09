using System.Collections;
using UnityEngine;

/// <summary>
/// สาเหตุที่ทำให้กล่อง Mimic แปลงร่าง
/// </summary>
public enum MimicTransformReason
{
    Opened,         // ผู้เล่นกดปุ่ม E เปิดกล่อง
    HeldTooLong,    // ผู้เล่นอุ้มถือกล่องต่อเนื่องเกิน 5 วินาที
    Attacked        // ถูกโจมตีด้วยอาวุธระยะประชิด
}

/// <summary>
/// คอมโพเนนต์สำหรับกล่องพัสดุ Mimic ที่ปลอมตัวเป็นพัสดุปกติ
/// หากผู้เล่นกดเปิดกล่อง หรืออุ้มถือเกิน 5 วินาที กล่องจะระเบิด VFX ลบตัวเองทิ้ง และสปอน EnemyMimic ออกมาทันที
/// </summary>
[RequireComponent(typeof(PackageBox))]
public class MimicPackage : MonoBehaviour
{
    [Header("Mimic Spawn Settings")]
    [Tooltip("Prefab ของตัวศัตรู Mimic ที่จะสปอนออกมา")]
    public GameObject enemyMimicPrefab;

    [Tooltip("Prefab เอฟเฟกต์ควัน/การระเบิดแปลงร่าง (ถ้าไม่ใส่ ระบบจะสร้าง Procedural VFX ให้เอง)")]
    public GameObject transformVfxPrefab;

    [Tooltip("เสียงเอฟเฟกต์ตอนแปลงร่าง")]
    public AudioClip transformSound;

    [Tooltip("ระยะยกตัวศัตรูขึ้นจากพื้นเล็กน้อยตอนสปอน")]
    public float spawnHeightOffset = 0.2f;

    [Header("Trigger Conditions")]
    [Tooltip("แปลงร่างเมื่อผู้เล่นกดเปิดกล่องหรือไม่")]
    public bool triggerOnOpen = true;

    [Tooltip("แปลงร่างเมื่อผู้เล่นอุ้มถือเกินเวลาที่กำหนดหรือไม่")]
    public bool triggerOnHold = true;

    [Tooltip("ระยะเวลาที่อุ้มถือได้ก่อนแปลงร่าง (วินาที)")]
    public float holdDurationThreshold = 5.0f;

    [Tooltip("แปลงร่างเมื่อถูกอาวุธระยะประชิดตีหรือไม่")]
    public bool triggerOnMeleeHit = true;

    [Header("Hold Warning & Suspense")]
    [Tooltip("ให้กล่องสั่นเตือนในมือเมื่อถือใกล้ครบกำหนดหรือไม่")]
    public bool enableHoldShakeWarning = true;

    [Tooltip("เริ่มสั่นเตือนเมื่อถือผ่านไปกี่วินาที")]
    public float warningStartTime = 3.0f;

    [Tooltip("ความแรงของการสั่นในมือ")]
    public float shakeIntensity = 0.04f;

    [Tooltip("เสียงชีพจร/หัวใจเต้นหรือเสียงครางเบาๆ ขณะใกล้ครบเวลา")]
    public AudioClip warningHeartbeatSound;

    private bool isTransforming = false;
    private float currentHeldTimer = 0f;
    private bool wasHeld = false;
    private Vector3 originalLocalPos = Vector3.zero;
    private PackageBox packageBox;
    private AudioSource audioSource;
    private float nextHeartbeatTime = 0f;

    private void Awake()
    {
        packageBox = GetComponent<PackageBox>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f; // 3D sound
        }
    }

    private void Update()
    {
        if (isTransforming || !triggerOnHold) return;

        // ตรวจสอบว่าผู้เล่นกำลังอุ้มถือกล่องนี้อยู่ในมือหรือไม่
        bool isHeldByPlayer = PlayerPickupSystem.Instance != null && PlayerPickupSystem.Instance.heldObject == gameObject;

        if (isHeldByPlayer)
        {
            if (!wasHeld)
            {
                wasHeld = true;
                currentHeldTimer = 0f;
                originalLocalPos = transform.localPosition;
            }

            currentHeldTimer += Time.deltaTime;

            // เอฟเฟกต์สั่นเตือนเมื่อถือเกิน warningStartTime (วินาทีที่ 3 - 5)
            if (enableHoldShakeWarning && currentHeldTimer >= warningStartTime)
            {
                float t = Mathf.Clamp01((currentHeldTimer - warningStartTime) / Mathf.Max(0.1f, (holdDurationThreshold - warningStartTime)));
                Vector3 shake = Random.insideUnitSphere * (shakeIntensity * Mathf.Lerp(0.5f, 1.8f, t));
                transform.localPosition = originalLocalPos + shake;

                // เล่นเสียงเตือนเบาๆ
                if (warningHeartbeatSound != null && Time.time >= nextHeartbeatTime)
                {
                    audioSource.PlayOneShot(warningHeartbeatSound, 0.7f);
                    nextHeartbeatTime = Time.time + Mathf.Lerp(0.8f, 0.3f, t);
                }
            }

            // เมื่ออุ้มถือครบเวลาที่กำหนด
            if (currentHeldTimer >= holdDurationThreshold)
            {
                TriggerTransform(MimicTransformReason.HeldTooLong);
            }
        }
        else
        {
            // หากวางหรือปล่อยกล่อง ให้รีเซ็ตเวลาและการสั่น
            if (wasHeld)
            {
                wasHeld = false;
                currentHeldTimer = 0f;
                transform.localPosition = originalLocalPos;
            }
        }
    }

    /// <summary>
    /// เรียกใช้เมื่อผู้เล่นพยายามกดปุ่ม E เปิดกล่องนี้
    /// </summary>
    public void OnAttemptOpen()
    {
        if (isTransforming || !triggerOnOpen) return;
        TriggerTransform(MimicTransformReason.Opened);
    }

    /// <summary>
    /// เรียกใช้เมื่อผู้เล่นใช้อาวุธระยะประชิดตีกล่องนี้
    /// </summary>
    public void OnAttackedByWeapon(MeleeWeapon weapon)
    {
        if (isTransforming || !triggerOnMeleeHit) return;
        TriggerTransform(MimicTransformReason.Attacked);
    }

    /// <summary>
    /// สั่งกระบวนการแปลงร่าง
    /// </summary>
    public void TriggerTransform(MimicTransformReason reason)
    {
        if (isTransforming) return;
        isTransforming = true;

        Debug.LogWarning($"<color=#FF0044>[MimicPackage] กล่องพัสดุกลายร่างเป็น Mimic!</color> สาเหตุ: <b>{reason}</b>");

        Vector3 spawnPos = transform.position + Vector3.up * spawnHeightOffset;
        Quaternion spawnRot = transform.rotation;

        // 1. หากผู้เล่นกำลังอุ้มถืออยู่ ให้ปลดของออกจากมือและคืนค่าน้ำหนักทันที
        if (PlayerPickupSystem.Instance != null && PlayerPickupSystem.Instance.heldObject == gameObject)
        {
            PlayerPickupSystem.Instance.ForceDropObject(gameObject);

            // ปรับตำแหน่งให้เกิดตรงหน้าผู้เล่นเล็กน้อยเพื่อความชัดเจน
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                spawnPos = mainCam.transform.position + mainCam.transform.forward * 1.5f + Vector3.down * 0.4f;
                spawnRot = Quaternion.LookRotation(mainCam.transform.position - spawnPos);
            }
        }

        // 2. ปลดสถานะออกจาก BaseDropPoint (เช่น ชั้นวางของใน Storage หรือ โต๊ะ Shipping)
        BaseDropPoint dropPoint = BaseDropPoint.GetDropPointHolding(gameObject);
        if (dropPoint != null)
        {
            dropPoint.currentPlacedItem = null;
        }

        // 3. ปลดสถานะออกจาก StorageRoomManager (หากอยู่ในห้องเก็บของ)
        StorageRoomManager storageManager = FindAnyObjectByType<StorageRoomManager>();
        if (storageManager != null)
        {
            storageManager.RemoveItemFromSlots(gameObject);
        }

        // 4. เล่นเอฟเฟกต์การแปลงร่าง (VFX & SFX)
        PlayTransformationEffects(spawnPos);

        // 5. สปอนตัวศัตรู EnemyMimic ออกมา
        SpawnEnemyEntity(spawnPos, spawnRot);

        // 7. ลบกล่องพัสดุทิ้งอย่างปลอดภัย
        Destroy(gameObject);
    }

    private void PlayTransformationEffects(Vector3 position)
    {
        if (transformVfxPrefab != null)
        {
            Instantiate(transformVfxPrefab, position, Quaternion.identity);
        }
        else
        {
            CreateProceduralTransformVFX(position);
        }

        if (transformSound != null)
        {
            AudioSource.PlayClipAtPoint(transformSound, position, 1.0f);
        }
    }

    private void SpawnEnemyEntity(Vector3 position, Quaternion rotation)
    {
        GameObject enemyToSpawn = enemyMimicPrefab;

        // หากยังไม่ได้ลาก Prefab ใส่ ให้พยายามค้นหาหรือดึงจาก EnemySpawner
        if (enemyToSpawn == null && EnemySpawner.Instance != null)
        {
            enemyToSpawn = EnemySpawner.Instance.enemyPrefab;
        }

        if (enemyToSpawn != null)
        {
            if (EnemySpawner.Instance != null)
            {
                EnemySpawner.Instance.SpawnEnemyAtLocation(enemyToSpawn, position, rotation);
            }
            else
            {
                Instantiate(enemyToSpawn, position, rotation);
            }
        }
        else
        {
            Debug.LogError("[MimicPackage] ไม่สามารถสปอนศัตรูได้: ยังไม่ได้กำหนด enemyMimicPrefab ใน Inspector!");
        }
    }

    /// <summary>
    /// สร้างเอฟเฟกต์ควัน แสงแฟลช และสะเก็ดระเบิดแบบ Procedural อัตโนมัติเมื่อไม่มี VFX Prefab
    /// </summary>
    private void CreateProceduralTransformVFX(Vector3 position)
    {
        GameObject vfxObj = new GameObject("Mimic_Transform_VFX");
        vfxObj.transform.position = position;

        // 1. แสงแฟลชวาบสีม่วง/แดง
        Light flashLight = vfxObj.AddComponent<Light>();
        flashLight.type = LightType.Point;
        flashLight.color = new Color(0.9f, 0.1f, 0.7f); // สีม่วง Alien สดใส
        flashLight.range = 6.0f;
        flashLight.intensity = 5.0f;

        // 2. ระบบ Particle ควันระเบิด
        ParticleSystem ps = vfxObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 1.0f;
        main.loop = false;
        main.startLifetime = 1.2f;
        main.startSpeed = 4.5f;
        main.startSize = 0.6f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.4f, 0.05f, 0.5f, 0.9f),
            new Color(0.1f, 0.8f, 0.3f, 0.8f) // ม่วงสลับเขียวเอเลี่ยน
        );

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 40) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.4f;

        ps.Play();

        // 3. ทำลายออบเจกต์ VFX เมื่อเล่นเสร็จ
        Destroy(vfxObj, 2.5f);
    }
}

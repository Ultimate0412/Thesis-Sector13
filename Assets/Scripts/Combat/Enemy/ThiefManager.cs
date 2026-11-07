using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ผู้จัดการการเกิดของ Thief (Thief Manager / Intruder System)
/// ตรวจจับการประทับตราพัสดุประจำวัน:
/// - กล่องที่ 1 - 2: ยังไม่เกิด
/// - กล่องที่ 3 - 4: สุ่มโอกาสเกิด (ค่าเริ่มต้น: 45%)
/// - กล่องที่ 5: การันตีเกิด 100%
/// มีระยะหน่วงเวลาแจ้งเตือนล่วงหน้า (Pre-Spawn Warning) พร้อมเสียงเตือน ก่อนที่ Thief จะปรากฏตัว
/// </summary>
public class ThiefManager : MonoBehaviour
{
    public static ThiefManager Instance { get; private set; }

    [Header("Thief Prefab")]
    [Tooltip("Prefab ของศัตรู Thief ที่จะสปอนออกมา")]
    public GameObject thiefPrefab;

    [Header("Stamp Trigger Settings")]
    [Tooltip("จำนวนกล่องประทับตราขั้นต่ำที่เริ่มสุ่มโอกาสเกิด")]
    public int minStampsThreshold = 3;

    [Tooltip("จำนวนกล่องประทับตราที่การันตีเกิดแน่นอน 100%")]
    public int guaranteedStampsThreshold = 5;

    [Range(0f, 1f)]
    [Tooltip("โอกาสเกิดเมื่อประทับตราถึงเกณฑ์ขั้นต่ำ (3 - 4 กล่อง)")]
    public float spawnChancePerStamp = 0.45f;

    [Tooltip("จำนวน Thief สูงสุดที่เกิดได้ต่อวัน")]
    public int maxThievesPerDay = 1;

    [Header("Pre-Spawn Warning Settings")]
    [Tooltip("ระยะเวลาหน่วงก่อนตัว Thief จะเกิดจริง (วินาที)")]
    public float warningDelay = 4.0f;

    [Tooltip("คลิปเสียงแจ้งเตือนก่อนเกิด (เช่น เสียงฝีเท้าลับๆ ล่อๆ หรือเสียงเคาะท่อ)")]
    public AudioClip warningAudioClip;

    [Tooltip("Prefab แสงหรือควันแจ้งเตือนตรงจุดที่จะเกิด")]
    public GameObject warningVfxPrefab;

    [Header("Spawn & Escape Locations")]
    [Tooltip("จุดที่ Thief จะสปอนออกมา (หากไม่ใส่ จะดึงจาก EnemySpawner)")]
    public Transform[] spawnPoints;

    [Tooltip("จุดหลบหนีที่ Thief จะวิ่งไป (หากไม่ใส่ จะใช้จุดเกิด)")]
    public Transform[] escapePoints;

    [Header("Debug Controls")]
    [Tooltip("ปุ่มสำหรับกดเรียก Thief ออกมาทันทีเพื่อทดสอบ (Default = T)")]
    public KeyCode debugSpawnThiefKey = KeyCode.T;

    [Header("Runtime Status (Read-Only)")]
    [SerializeField] private int dailyStampedCount = 0;
    [SerializeField] private int thievesSpawnedToday = 0;
    [SerializeField] private bool isWarningActive = false;

    public int DailyStampedCount => dailyStampedCount;
    public int ThievesSpawnedToday => thievesSpawnedToday;
    public bool IsWarningActive => isWarningActive;

    private HashSet<PackageBox> stampedBoxesToday = new HashSet<PackageBox>();
    private AudioSource audioSource;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f; // 2D Sound ให้ผู้เล่นได้ยินชัดเจน
        }
    }

    private void Start()
    {
        PackageBox.OnAnyPackageStamped += HandlePackageStamped;

        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.OnDayStarted += HandleDayStarted;
        }
    }

    private void OnDestroy()
    {
        PackageBox.OnAnyPackageStamped -= HandlePackageStamped;

        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.OnDayStarted -= HandleDayStarted;
        }
    }

    private void Update()
    {
        // ตรวจสอบปุ่มคีย์ลัด Debug (ปุ่ม T)
        if (debugSpawnThiefKey != KeyCode.None && Input.GetKeyDown(debugSpawnThiefKey))
        {
            TriggerThiefSpawnManually();
        }
    }

    private void HandleDayStarted(int newDay)
    {
        dailyStampedCount = 0;
        thievesSpawnedToday = 0;
        isWarningActive = false;
        stampedBoxesToday.Clear();
        Debug.Log($"[ThiefManager] รีเซ็ตสถิติ Thief สำหรับ Day {newDay}");
    }

    private void HandlePackageStamped(PackageBox box, InspectionStatus status)
    {
        if (box == null) return;

        // นับเฉพาะกล่องที่ไม่เคยประทับตราในวันนี้
        if (stampedBoxesToday.Contains(box)) return;
        stampedBoxesToday.Add(box);

        dailyStampedCount++;
        Debug.Log($"<color=#FFCC00>[ThiefManager]</color> ประทับตรากล่องที่ <b>{dailyStampedCount}</b> ของวัน");

        // หากโควต้าการเกิดวันนี้เต็มแล้ว หรือกำลังเตือนอยู่ ไม่ต้องตรวจเพิ่ม
        if (thievesSpawnedToday >= maxThievesPerDay || isWarningActive) return;

        // ตรวจสอบเงื่อนไขการเกิด
        if (dailyStampedCount >= guaranteedStampsThreshold)
        {
            // การันตีเกิด 100% ที่กล่องที่ 5
            Debug.LogWarning($"<color=#FF5500>[ThiefManager] ประทับตราครบ {guaranteedStampsThreshold} กล่อง! การันตีการเกิดของ Thief 100%</color>");
            StartCoroutine(PreSpawnWarningRoutine());
        }
        else if (dailyStampedCount >= minStampsThreshold)
        {
            // สุ่มโอกาสเกิดระหว่างกล่องที่ 3 - 4
            float roll = Random.value;
            if (roll <= spawnChancePerStamp)
            {
                Debug.LogWarning($"<color=#FF5500>[ThiefManager] ประทับตรากล่องที่ {dailyStampedCount} สุ่มติด Thief (สุ่ม {roll:F2} <= {spawnChancePerStamp})</color>");
                StartCoroutine(PreSpawnWarningRoutine());
            }
        }
    }

    /// <summary>
    /// สั่งทริกเกอร์การเกิดของ Thief ด้วยตนเอง (ผ่านปุ่ม Debug T หรือเรียกจากโค้ด)
    /// </summary>
    [ContextMenu("Trigger Thief Spawn Now")]
    public void TriggerThiefSpawnManually()
    {
        if (isWarningActive)
        {
            Debug.LogWarning("[ThiefManager] ระบบกำลังอยู่ในช่วงนับถอยหลังแจ้งเตือนอยู่แล้ว");
            return;
        }

        Debug.Log("<color=#FF5500>[ThiefManager] ทริกเกอร์การเกิด Thief ด้วยตนเอง (Debug)</color>");
        StartCoroutine(PreSpawnWarningRoutine());
    }

    private IEnumerator PreSpawnWarningRoutine()
    {
        isWarningActive = true;

        // 1. เลือกจุดเกิด
        Transform chosenSpawnPoint = PickSpawnPoint();
        Vector3 spawnPos = (chosenSpawnPoint != null) ? chosenSpawnPoint.position : transform.position;

        // 2. เล่นเสียงแจ้งเตือนล่วงหน้า (Warning Phase)
        if (warningAudioClip != null)
        {
            AudioSource.PlayClipAtPoint(warningAudioClip, spawnPos, 1.0f);
        }
        else
        {
            PlayProceduralWarningChime();
        }

        // 3. เอฟเฟกต์ไฟ/ควันเตือน
        if (warningVfxPrefab != null)
        {
            Instantiate(warningVfxPrefab, spawnPos, Quaternion.identity);
        }

        Debug.LogWarning($"<color=#FF3300>[ThiefManager] ⚠️ ระวัง! ได้ยินเสียงฝีเท้าต้องสงสัยในโกดัง... กำลังบุกรุกในอีก {warningDelay} วินาที!</color>");

        // 4. หน่วงเวลาตามที่กำหนด
        yield return new WaitForSeconds(warningDelay);

        // 5. สปอนตัว Thief ออกมา
        SpawnThief(chosenSpawnPoint);

        thievesSpawnedToday++;
        isWarningActive = false;
    }

    private void SpawnThief(Transform spawnPoint)
    {
        Vector3 spawnPos = (spawnPoint != null) ? spawnPoint.position : transform.position;
        Quaternion spawnRot = (spawnPoint != null) ? spawnPoint.rotation : Quaternion.identity;

        GameObject prefabToSpawn = thiefPrefab;
        if (prefabToSpawn == null)
        {
#if UNITY_EDITOR
            prefabToSpawn = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PrefabPrtotype/Enemy_Thief.prefab");
#endif
            if (prefabToSpawn == null)
            {
                prefabToSpawn = Resources.Load<GameObject>("PrefabPrtotype/Enemy_Thief");
            }
        }

        GameObject spawnedThief = null;
        if (prefabToSpawn != null)
        {
            spawnedThief = Instantiate(prefabToSpawn, spawnPos, spawnRot);
        }
        else
        {
            // หากยังไม่มี Prefab สร้างโมเดลตัวอย่างชั่วคราว
            spawnedThief = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            spawnedThief.name = "Enemy_Thief_AutoCreated";
            spawnedThief.transform.position = spawnPos;
            spawnedThief.transform.localScale = new Vector3(0.75f, 0.75f, 0.75f);
            spawnedThief.AddComponent<Rigidbody>();
        }

        EnemyThief thiefComp = spawnedThief.GetComponent<EnemyThief>();
        if (thiefComp == null)
        {
            thiefComp = spawnedThief.AddComponent<EnemyThief>();
        }

        // มอบหมาย Escape Point ให้ Thief
        if (escapePoints != null && escapePoints.Length > 0)
        {
            thiefComp.designatedEscapePoint = escapePoints[Random.Range(0, escapePoints.Length)];
        }
        else if (spawnPoint != null)
        {
            thiefComp.designatedEscapePoint = spawnPoint;
        }

        Debug.LogWarning($"<color=#FF0044>[ThiefManager] 🥷 Thief ปรากฏตัวแล้ว ณ {spawnPos}! กำลังมองหาพัสดุไปขโมย!</color>");
    }

    private Transform PickSpawnPoint()
    {
        // 1. ถ้ามีจุดเกิดเฉพาะใน ThiefManager
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            List<Transform> validPoints = new List<Transform>();
            foreach (var pt in spawnPoints)
            {
                if (pt != null) validPoints.Add(pt);
            }
            if (validPoints.Count > 0)
            {
                return validPoints[Random.Range(0, validPoints.Count)];
            }
        }

        // 2. ดึงจาก EnemySpawner
        if (EnemySpawner.Instance != null && EnemySpawner.Instance.spawnPoints != null && EnemySpawner.Instance.spawnPoints.Length > 0)
        {
            return EnemySpawner.Instance.spawnPoints[Random.Range(0, EnemySpawner.Instance.spawnPoints.Length)];
        }

        // 3. Fallback: สปอนห่างจากผู้เล่น 8 เมตร
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            GameObject fallbackSpawn = new GameObject("ThiefFallbackSpawn");
            Vector3 offset = mainCam.transform.forward * 8f + mainCam.transform.right * 4f;
            offset.y = 0f;
            fallbackSpawn.transform.position = mainCam.transform.position + offset;
            return fallbackSpawn.transform;
        }

        return transform;
    }

    // --- เสียงแจ้งเตือนเตือนภัย Procedural หากไม่ได้ใส่ AudioClip ---
    private void PlayProceduralWarningChime()
    {
        StartCoroutine(WarningSirenRoutine());
    }

    private IEnumerator WarningSirenRoutine()
    {
        if (audioSource == null) yield break;

        float[] pitches = { 600f, 850f, 600f, 900f };
        for (int i = 0; i < pitches.Length; i++)
        {
            int sampleRate = 44100;
            float duration = 0.15f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            AudioClip clip = AudioClip.Create("WarningSiren", sampleCount, 1, sampleRate, false);
            float[] samples = new float[sampleCount];

            for (int s = 0; s < sampleCount; s++)
            {
                float t = (float)s / sampleRate;
                samples[s] = Mathf.Sin(2f * Mathf.PI * pitches[i] * t) * (1f - (float)s / sampleCount);
            }

            clip.SetData(samples, 0);
            audioSource.PlayOneShot(clip, 0.9f);

            yield return new WaitForSeconds(0.25f);
        }
    }
}

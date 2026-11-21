using UnityEngine;
using System;
using System.Collections.Generic;

[System.Serializable]
public class DailyCategorySpawnRate
{
    [Tooltip("น้ำหนักโอกาสเกิดของของถูกกฎหมาย (Legal)")]
    [Range(0f, 100f)] public float legalWeight = 70f;

    [Tooltip("น้ำหนักโอกาสเกิดของของผิดกฎหมาย (Illegal)")]
    [Range(0f, 100f)] public float illegalWeight = 20f;

    [Tooltip("น้ำหนักโอกาสเกิดของของเอเลี่ยน (Alien)")]
    [Range(0f, 100f)] public float alienWeight = 10f;

    public float GetWeight(ItemCategory category)
    {
        switch (category)
        {
            case ItemCategory.Legal: return Mathf.Max(0f, legalWeight);
            case ItemCategory.Illegal: return Mathf.Max(0f, illegalWeight);
            case ItemCategory.Alien: return Mathf.Max(0f, alienWeight);
            default: return 0f;
        }
    }
}

[System.Serializable]
public class DailyPackageList
{
    [Tooltip("ชื่อระบุวันสำหรับอ่านง่ายใน Inspector เช่น 'Day 1'")]
    public string dayName = "Day 1";

    [Tooltip("รายการ Prefab กล่องพัสดุเฉพาะของวันนี้ (หากกำหนด ระบบจะสุ่มเฉพาะพัสดุในชุดนี้)")]
    public GameObject[] packagePrefabs;
}

public class GameDayManager : MonoBehaviour
{
    public static GameDayManager Instance;

    [Header("Day Settings")]
    public int currentDay = 1;

    [Header("Inspection / Open Box Quota")]
    [Tooltip("โควต้าการเปิดกล่องพื้นฐานต่อวัน (ใช้เมื่อยังไม่มี Day Configs)")]
    public int defaultDailyOpenQuota = 15;
    public int dailyOpenQuota = 15; // ค่า runtime ที่ถูกคำนวณจาก DayConfig
    private int remainingQuotaToday;
    public int RemainingQuotaToday => remainingQuotaToday;

    [Header("Item Spawning Quota")]
    [Tooltip("โควต้าการสร้างกล่องสินค้าพื้นฐานต่อวัน (ใช้เมื่อยังไม่มี Day Configs)")]
    public int defaultDailySpawnQuota = 10;
    private int remainingSpawnQuotaToday;
    public int RemainingSpawnQuotaToday => remainingSpawnQuotaToday;
    public int TotalSpawnQuotaToday { get; private set; }

    [Header("Category Spawn Rates (ใช้เมื่อยังไม่มี Day Configs)")]
    [Tooltip("น้ำหนักโอกาสเกิด: สินค้าถูกกฎหมาย (Legal)")]
    [Range(0f, 100f)] public float defaultLegalRate = 70f;
    [Tooltip("น้ำหนักโอกาสเกิด: สินค้าผิดกฎหมาย (Illegal)")]
    [Range(0f, 100f)] public float defaultIllegalRate = 20f;
    [Tooltip("น้ำหนักโอกาสเกิด: วัตถุเอเลี่ยน (Alien)")]
    [Range(0f, 100f)] public float defaultAlienRate = 10f;

    [Header("Day Configs (ScriptableObject)")]
    [Tooltip("การตั้งค่าของแต่ละวัน (Index 0 = Day 1, Index 1 = Day 2, ...)")]
    public DayConfig[] dayConfigs;

    [Header("Debug Key")]
    [Tooltip("ปุ่มสำหรับกดทดสอบจบวัน (ตั้งเป็น None ได้หากไม่ต้องการใช้งาน)")]
    public KeyCode debugEndDayKey = KeyCode.Alpha2;

    // ใช้ซ้ำเพื่อลดการ allocate ตอนเรียก GetCurrentCategoryRate() บ่อยๆ
    private readonly DailyCategorySpawnRate fallbackCategoryRate = new DailyCategorySpawnRate();

    // Events
    public event Action<int> OnQuotaChanged;            // remainingOpenQuota (รองรับโค้ดเดิม)
    public event Action<int, int> OnSpawnQuotaChanged;   // remainingSpawnQuota, totalSpawnQuota
    public event Action<int> OnDayChanged;              // currentDay (รองรับโค้ดเดิม)
    public event Action<int> OnDayEnded;                // completedDay
    public event Action<int> OnDayStarted;              // newDay
    public event Action<DayEndFailure> OnDayEndFailed;  // รายละเอียดเมื่อจบวันไม่สำเร็จ
    public event Action<DaySummaryResult> OnDaySummaryRequested; // ผลสรุปที่ต้องแสดงในหน้าต่างสรุปผลประจำวัน

    private bool summaryPending;
    private bool lastSummaryPassed;

    /// <summary>มีหน้าต่างสรุปผลเปิดค้างรอผู้เล่นยืนยันอยู่หรือไม่</summary>
    public bool IsSummaryPending => summaryPending;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        ResetDailyQuota();
    }

    // --- Day Config (ScriptableObject) ---

    /// <summary>มีการกำหนด dayConfigs ไว้หรือไม่</summary>
    public bool HasAnyDayConfig => dayConfigs != null && dayConfigs.Length > 0;

    /// <summary>คืนค่า DayConfig ของวันที่ระบุ (index = day - 1) หรือ null หากไม่มี</summary>
    public DayConfig GetDayConfig(int day)
    {
        int dayIndex = day - 1;
        if (dayConfigs != null && dayIndex >= 0 && dayIndex < dayConfigs.Length)
        {
            return dayConfigs[dayIndex];
        }
        return null;
    }

    /// <summary>DayConfig ของวันปัจจุบัน</summary>
    public DayConfig CurrentDayConfig => GetDayConfig(currentDay);

    // --- เงื่อนไขรายวัน (อ่านจาก DayConfig ของวันปัจจุบัน) ---

    /// <summary>จำนวนพัสดุขั้นต่ำที่ต้องส่งในวันนี้ (0 = ไม่บังคับ)</summary>
    public int RequiredShippedToday
    {
        get
        {
            DayConfig config = CurrentDayConfig;
            return config != null ? Mathf.Max(0, config.minShippedPerDay) : 0;
        }
    }

    /// <summary>จำนวนพัสดุที่ส่งออกไปแล้วในวันนี้</summary>
    public int ShippedToday
    {
        get
        {
            DailyStatisticsData stats = ScoreManager.Instance != null ? ScoreManager.Instance.CurrentDayStats : null;
            return stats != null ? stats.TotalShippedToday : 0;
        }
    }

    /// <summary>จำนวนพัสดุที่ยังขาดจากขั้นต่ำของวันนี้</summary>
    public int ShippedShortfallToday => Mathf.Max(0, RequiredShippedToday - ShippedToday);

    /// <summary>โทษที่จะถูกหักหากยืนยันจบวันทั้งที่ส่งไม่ถึงขั้นต่ำ</summary>
    public int PenaltyForShortfall
    {
        get
        {
            DayConfig config = CurrentDayConfig;
            int perBox = config != null ? Mathf.Max(0, config.shortfallPenaltyPerBox) : 0;
            return ShippedShortfallToday * perBox;
        }
    }

    /// <summary>ค่าสถานีของวันนี้ (หักจากคะแนนรวมตอนจบวัน)</summary>
    public int StationFeeToday
    {
        get
        {
            DayConfig config = CurrentDayConfig;
            return config != null ? Mathf.Max(0, config.stationFee) : 0;
        }
    }

    /// <summary>ตรวจว่ามีการตั้งค่าของวันนั้นหรือไม่</summary>
    public bool HasDayConfig(int day)
    {
        return GetDayConfig(day) != null;
    }

    /// <summary>วันสูงสุดที่มีการตั้งค่าไว้ (0 = ยังไม่มี config เลย)</summary>
    public int LastConfiguredDay
    {
        get
        {
            if (dayConfigs == null) return 0;
            for (int i = dayConfigs.Length - 1; i >= 0; i--)
            {
                if (dayConfigs[i] != null) return i + 1;
            }
            return 0;
        }
    }

    /// <summary>
    /// ตรวจว่าวันถัดไปมี config หรือไม่
    /// คืน true พร้อมเหตุผล เมื่อมีการตั้งค่า dayConfigs แล้วแต่ขาด config ของวันถัดไป
    /// </summary>
    private bool TryGetMissingNextDayConfigReason(out string failReason)
    {
        failReason = null;
        if (!HasAnyDayConfig) return false;

        int nextDay = currentDay + 1;
        if (HasDayConfig(nextDay)) return false;

        failReason = $"ไม่สามารถไปยัง Day {nextDay} ได้: ไม่มี DayConfig สำหรับวันดังกล่าว (มีสูงสุดถึง Day {LastConfiguredDay})";
        return true;
    }

    private void Update()
    {
        // รองรับการกดปุ่ม Debug เพื่อทดสอบจบวัน
        if (debugEndDayKey != KeyCode.None && Input.GetKeyDown(debugEndDayKey))
        {
            EndDay();
        }
    }

    // รีเซ็ตโควต้าทั้งหมดเมื่อขึ้นวันใหม่
    public void ResetDailyQuota()
    {
        int dayIndex = currentDay - 1;
        DayConfig dayConfig = GetDayConfig(currentDay);

        // มีการตั้งค่า dayConfigs ไว้แล้ว แต่ไม่มีของวันนี้ = หยุดและแจ้งเตือน
        if (dayConfig == null && HasAnyDayConfig)
        {
            dailyOpenQuota = 0;
            remainingQuotaToday = 0;
            TotalSpawnQuotaToday = 0;
            remainingSpawnQuotaToday = 0;
            OnQuotaChanged?.Invoke(remainingQuotaToday);
            OnSpawnQuotaChanged?.Invoke(remainingSpawnQuotaToday, TotalSpawnQuotaToday);
            Debug.LogError($"[GameDayManager] ไม่พบ DayConfig สำหรับ Day {currentDay} (index {dayIndex}) — กรุณาเพิ่ม asset ในช่อง Day Configs");
            return;
        }

        if (dayConfig == null)
        {
            Debug.LogWarning("[GameDayManager] ยังไม่ได้กำหนด Day Configs — ใช้ค่า default แทน");
        }

        // 1. คำนวณโควต้าการเปิดตรวจกล่อง (Open Quota)
        if (dayConfig != null)
        {
            dailyOpenQuota = Mathf.Max(0, dayConfig.openQuota);
        }
        else if (defaultDailyOpenQuota > 0)
        {
            dailyOpenQuota = defaultDailyOpenQuota;
        }
        remainingQuotaToday = dailyOpenQuota;
        OnQuotaChanged?.Invoke(remainingQuotaToday);

        // 2. คำนวณโควต้าการสปอนสินค้า (Spawn Quota)
        if (dayConfig != null)
        {
            TotalSpawnQuotaToday = Mathf.Max(0, dayConfig.spawnQuota);
        }
        else
        {
            TotalSpawnQuotaToday = defaultDailySpawnQuota;
        }
        remainingSpawnQuotaToday = TotalSpawnQuotaToday;
        OnSpawnQuotaChanged?.Invoke(remainingSpawnQuotaToday, TotalSpawnQuotaToday);

        DailyCategorySpawnRate currentRate = GetCurrentCategoryRate();
        string dayLabel = dayConfig != null && !string.IsNullOrWhiteSpace(dayConfig.dayName)
            ? dayConfig.dayName
            : $"Day {currentDay}";
        Debug.Log($"--- เริ่มวันใหม่ ({dayLabel}) | โควต้าเปิดตรวจ: {remainingQuotaToday} ครั้ง | โควต้าสินค้า: {remainingSpawnQuotaToday} กล่อง | สัดส่วนสินค้า (Legal:{currentRate.legalWeight}, Illegal:{currentRate.illegalWeight}, Alien:{currentRate.alienWeight}) ---");
    }

    /// <summary>
    /// ตั้งเลขวันก่อนที่ Start ของวัตถุอื่นจะทำงาน (ใช้ตอนโหลดเซฟ)
    /// โควต้าจริงจะถูกคำนวณใหม่ใน ResetDailyQuota ตอน Start
    /// </summary>
    public void SetDayBeforeStart(int day)
    {
        currentDay = Mathf.Max(1, day);
    }

    /// <summary>
    /// คืนสถานะวัน/โควต้าจากเซฟ (เรียกหลัง Start ของฉากทำงานแล้ว)
    /// ค่า remaining ที่เป็น -1 หมายถึงใช้โควต้าเต็มของวันนั้น
    /// </summary>
    public void RestoreDayState(int day, int remainingOpenQuota, int remainingSpawnQuota)
    {
        currentDay = Mathf.Max(1, day);

        DayConfig dayConfig = GetDayConfig(currentDay);
        if (dayConfig != null)
        {
            dailyOpenQuota = Mathf.Max(0, dayConfig.openQuota);
            TotalSpawnQuotaToday = Mathf.Max(0, dayConfig.spawnQuota);
        }
        else
        {
            dailyOpenQuota = Mathf.Max(0, defaultDailyOpenQuota);
            TotalSpawnQuotaToday = Mathf.Max(0, defaultDailySpawnQuota);
        }

        remainingQuotaToday = remainingOpenQuota < 0
            ? dailyOpenQuota
            : Mathf.Clamp(remainingOpenQuota, 0, dailyOpenQuota);

        remainingSpawnQuotaToday = remainingSpawnQuota < 0
            ? TotalSpawnQuotaToday
            : Mathf.Clamp(remainingSpawnQuota, 0, TotalSpawnQuotaToday);

        OnDayChanged?.Invoke(currentDay);
        OnQuotaChanged?.Invoke(remainingQuotaToday);
        OnSpawnQuotaChanged?.Invoke(remainingSpawnQuotaToday, TotalSpawnQuotaToday);

        Debug.Log($"[GameDayManager] คืนสถานะ Day {currentDay} | โควต้าเปิดตรวจ {remainingQuotaToday}/{dailyOpenQuota} | โควต้าสินค้า {remainingSpawnQuotaToday}/{TotalSpawnQuotaToday}");
    }

    // --- ตรวจสอบและหักโควต้าเปิดตรวจกล่อง ---
    public bool CanOpenBoxToday()
    {
        return remainingQuotaToday > 0;
    }

    public void ConsumeOpenQuota()
    {
        if (remainingQuotaToday > 0)
        {
            remainingQuotaToday--;
            OnQuotaChanged?.Invoke(remainingQuotaToday);
            Debug.Log($"โควต้าเปิดกล่องคงเหลือวันนี้: {remainingQuotaToday} ครั้ง");
        }
    }

    // --- ตรวจสอบและหักโควต้าสปอนสินค้า ---
    public bool CanSpawnPackageToday()
    {
        return remainingSpawnQuotaToday > 0;
    }

    public bool ConsumeSpawnQuota(int amount = 1)
    {
        if (remainingSpawnQuotaToday >= amount)
        {
            remainingSpawnQuotaToday -= amount;
            OnSpawnQuotaChanged?.Invoke(remainingSpawnQuotaToday, TotalSpawnQuotaToday);
            Debug.Log($"[GameDayManager] ใช้โควต้าสินค้าไป {amount} กล่อง (คงเหลือ: {remainingSpawnQuotaToday}/{TotalSpawnQuotaToday})");
            return true;
        }
        return false;
    }

    // --- ระบบสุ่มหมวดหมู่ประจำวัน (Category Spawn Rate) ---
    public DailyCategorySpawnRate GetCurrentCategoryRate()
    {
        // 1. ใช้ค่าจาก DayConfig ก่อน
        DayConfig dayConfig = GetDayConfig(currentDay);
        if (dayConfig != null && dayConfig.categoryRate != null)
        {
            return dayConfig.categoryRate;
        }

        // 2. Fallback: ค่า default (ใช้ instance เดิมเพื่อลด allocation)
        fallbackCategoryRate.legalWeight = defaultLegalRate;
        fallbackCategoryRate.illegalWeight = defaultIllegalRate;
        fallbackCategoryRate.alienWeight = defaultAlienRate;
        return fallbackCategoryRate;
    }

    public ItemCategory GetRandomCategoryForToday()
    {
        DailyCategorySpawnRate rate = GetCurrentCategoryRate();
        float legal = Mathf.Max(0f, rate.legalWeight);
        float illegal = Mathf.Max(0f, rate.illegalWeight);
        float alien = Mathf.Max(0f, rate.alienWeight);
        float totalWeight = legal + illegal + alien;

        if (totalWeight <= 0.0001f)
        {
            return ItemCategory.Legal; // ค่าเริ่มต้นหากไม่มีการกำหนดน้ำหนัก
        }

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        if (roll < legal)
        {
            return ItemCategory.Legal;
        }
        else if (roll < (legal + illegal))
        {
            return ItemCategory.Illegal;
        }
        else
        {
            return ItemCategory.Alien;
        }
    }

    // --- ค้นหารายการ Prefab พัสดุประจำวัน ---
    public GameObject[] GetPackagePrefabsForDay(int day)
    {
        // ใช้ค่าจาก DayConfig ก่อน
        DayConfig dayConfig = GetDayConfig(day);
        if (dayConfig != null && dayConfig.HasPackagePrefabs)
        {
            return dayConfig.packagePrefabs;
        }

        return null; // ไม่มีระบุเจาะจง ให้ใช้ค่าเริ่มต้น
    }

    // --- ตรวจสอบเงื่อนไขก่อนจบวัน: ห้ามมีสินค้าค้างตามพื้นหรือจุด Spawn ต้องเก็บใน Storage เท่านั้น ---
    public bool CanEndDay(out string failReason, bool ignoreMinShipped = false)
    {
        failReason = string.Empty;

        // ค้นหากล่องพัสดุทั้งหมดที่มีอยู่ในฉาก
        PackageBox[] allBoxes = FindObjectsByType<PackageBox>(FindObjectsSortMode.None);

        int boxesOnSpawn = 0;
        int boxesOnFloor = 0;
        int boxesInHands = 0;
        int boxesAtShipping = 0;

        PlayerPickupSystem pickupSystem = FindFirstObjectByType<PlayerPickupSystem>();
        GameObject heldObj = pickupSystem != null ? pickupSystem.GetHeldObject() : null;

        ItemSpawner[] spawners = FindObjectsByType<ItemSpawner>(FindObjectsSortMode.None);
        StorageRoomManager storage = StorageRoomManager.Instance != null ? StorageRoomManager.Instance : FindFirstObjectByType<StorageRoomManager>();
        StorageDropPoint[] storagePoints = FindObjectsByType<StorageDropPoint>(FindObjectsSortMode.None);
        ShippingDropPoint shippingPoint = FindFirstObjectByType<ShippingDropPoint>();

        foreach (PackageBox box in allBoxes)
        {
            if (box == null) continue;
            GameObject boxObj = box.gameObject;

            // 1. เช็คว่าอยู่ในมือผู้เล่นหรือไม่
            if (boxObj == heldObj || (pickupSystem != null && boxObj.transform.IsChildOf(pickupSystem.transform)))
            {
                boxesInHands++;
                continue;
            }

            // 2. เช็คว่าอยู่ที่จุด Spawn หรือไม่
            bool isAtSpawn = false;
            if (spawners != null)
            {
                foreach (ItemSpawner spawner in spawners)
                {
                    if (spawner != null && spawner.IsPackageAtSpawner(boxObj))
                    {
                        isAtSpawn = true;
                        break;
                    }
                }
            }
            if (isAtSpawn)
            {
                boxesOnSpawn++;
                continue;
            }

            // 3. เช็คว่าอยู่ใน Storage หรือไม่
            bool isInStorage = false;
            if (storage != null && storage.IsItemInStorage(boxObj))
            {
                isInStorage = true;
            }
            else if (storagePoints != null)
            {
                for (int i = 0; i < storagePoints.Length; i++)
                {
                    if (storagePoints[i] != null && storagePoints[i].currentPlacedItem == boxObj)
                    {
                        isInStorage = true;
                        break;
                    }
                }
            }

            if (isInStorage)
            {
                // กล่องอยู่ใน Storage ปลอดภัย สามารถจบวันได้
                continue;
            }

            // 4. เช็คว่าอยู่ที่จุดส่งสินค้าหรือไม่ (ต้องกดส่ง F หรือย้ายเข้า storage)
            if (shippingPoint != null && shippingPoint.currentPlacedItem == boxObj)
            {
                boxesAtShipping++;
                continue;
            }

            // 5. ไม่ได้อยู่ในที่เก็บใดๆ = ตกค้างอยู่บนพื้น
            boxesOnFloor++;
        }

        // ค้นหาสินค้าเดี่ยวทั้งหมดที่มีอยู่ในฉาก (ที่ถูกแกะออกจากกล่องแล้ว)
        ItemObject[] allItems = FindObjectsByType<ItemObject>(FindObjectsSortMode.None);
        int itemsOnFloor = 0;
        int itemsInHands = 0;
        int itemsOnSpawn = 0;

        foreach (ItemObject item in allItems)
        {
            if (item == null) continue;
            GameObject itemObj = item.gameObject;

            // ข้ามถ้าเป็นส่วนประกอบของ PackageBox เอง หรือยังอยู่ใน PackageBox
            if (itemObj.GetComponent<PackageBox>() != null) continue;
            PackageBox parentBox = itemObj.GetComponentInParent<PackageBox>();
            if (parentBox != null && !parentBox.isItemExtracted) continue;

            // 1. เช็คว่าอยู่ในมือผู้เล่นหรือไม่
            if (itemObj == heldObj || (pickupSystem != null && itemObj.transform.IsChildOf(pickupSystem.transform)))
            {
                itemsInHands++;
                continue;
            }

            // 2. เช็คว่าอยู่ที่จุด Spawn หรือไม่
            bool isAtSpawn = false;
            if (spawners != null)
            {
                foreach (ItemSpawner spawner in spawners)
                {
                    if (spawner != null && spawner.IsPackageAtSpawner(itemObj))
                    {
                        isAtSpawn = true;
                        break;
                    }
                }
            }
            if (isAtSpawn)
            {
                itemsOnSpawn++;
                continue;
            }

            // 3. เช็คว่าอยู่ใน Storage หรือไม่
            bool isInStorage = false;
            if (storage != null && storage.IsItemInStorage(itemObj))
            {
                isInStorage = true;
            }
            else if (storagePoints != null)
            {
                for (int i = 0; i < storagePoints.Length; i++)
                {
                    if (storagePoints[i] != null && storagePoints[i].currentPlacedItem == itemObj)
                    {
                        isInStorage = true;
                        break;
                    }
                }
            }

            if (isInStorage)
            {
                continue;
            }

            // 4. ไม่ได้อยู่ในที่เก็บใดๆ = ตกค้างอยู่บนพื้น
            itemsOnFloor++;
        }

        // รวมเหตุผลที่ทำให้ไม่สามารถจบวันได้
        List<string> issues = new List<string>();
        if (boxesOnSpawn > 0)
        {
            issues.Add($"{boxesOnSpawn} parcel(s) still on the delivery shelf");
        }
        if (boxesOnFloor > 0)
        {
            issues.Add($"{boxesOnFloor} parcel(s) left on the floor");
        }
        if (boxesInHands > 0)
        {
            issues.Add($"{boxesInHands} parcel(s) still in your hands");
        }
        if (boxesAtShipping > 0)
        {
            issues.Add($"{boxesAtShipping} parcel(s) waiting at the shipping bay");
        }
        if (itemsOnSpawn > 0)
        {
            issues.Add($"{itemsOnSpawn} loose item(s) on the delivery shelf");
        }
        if (itemsOnFloor > 0)
        {
            issues.Add($"{itemsOnFloor} loose item(s) left on the floor");
        }
        if (itemsInHands > 0)
        {
            issues.Add($"{itemsInHands} loose item(s) still in your hands");
        }

        // ต้องใช้โควต้าสปอนของวันนี้ให้หมดก่อน (โควต้าที่เหลือ = ยังมีพัสดุที่ยังไม่ถูกสร้าง)
        if (remainingSpawnQuotaToday > 0)
        {
            issues.Add($"{remainingSpawnQuotaToday} parcel(s) still waiting to be called in — press E at the shelf");
        }

        // ส่งพัสดุไม่ถึงขั้นต่ำ: บล็อกไว้ก่อน แต่ผู้เล่นยืนยันรับโทษเพื่อข้ามได้
        if (!ignoreMinShipped)
        {
            int required = RequiredShippedToday;
            int shipped = ShippedToday;
            if (required > 0 && shipped < required)
            {
                issues.Add($"shipped {shipped}/{required} parcels (short by {required - shipped})");
            }
        }

        // Forcing the day (END DAY ANYWAY) means the player accepts the penalty, so
        // leftover parcels must not hard-block the day. Without this, a full storage
        // room or a parcel stuck on the floor soft-locks the run.
        if (ignoreMinShipped) issues.Clear();

        if (issues.Count > 0)
        {
            failReason = "CANNOT END THE DAY:\n• " + string.Join("\n• ", issues);
            return false;
        }

        return true;
    }

    // ฟังก์ชันสั่งจบวันแบบตรวจสอบเงื่อนไข
    public bool TryEndDay(out string failReason)
    {
        if (TryGetMissingNextDayConfigReason(out failReason))
        {
            Debug.LogWarning($"[GameDayManager] {failReason}");
            OnDayEndFailed?.Invoke(new DayEndFailure { reason = failReason });
            return false;
        }

        if (!CanEndDay(out failReason))
        {
            Debug.LogWarning($"[GameDayManager] {failReason}");
            OnDayEndFailed?.Invoke(new DayEndFailure
            {
                reason = failReason,
                canOverrideWithPenalty = CanEndDay(out _, true),
                penalty = PenaltyForShortfall,
                shortfall = ShippedShortfallToday
            });
            return false;
        }

        ExecuteEndDay();
        return true;
    }

    /// <summary>
    /// ขอเปิดหน้าต่างสรุปผลรายวัน (ตรวจเงื่อนไขก่อน หากผ่านจะส่ง Event เปิด DailySummaryUI)
    /// ignoreMinShipped = true ใช้เมื่อผู้เล่นยืนยันรับโทษส่งไม่ถึงขั้นต่ำแล้ว
    /// </summary>
    public bool RequestEndDay(bool ignoreMinShipped = false)
    {
        if (summaryPending) return false;
        if (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsOpen) return false;
        if (EndDayBlockedDialog.Instance != null && EndDayBlockedDialog.Instance.IsOpen) return false;

        if (TryGetMissingNextDayConfigReason(out string failReason))
        {
            Debug.LogWarning($"[GameDayManager] {failReason}");
            OnDayEndFailed?.Invoke(new DayEndFailure { reason = failReason });
            return false;
        }

        if (!CanEndDay(out failReason, ignoreMinShipped))
        {
            Debug.LogWarning($"[GameDayManager] {failReason}");

            // ถ้าติดแค่เงื่อนไขขั้นต่ำ ให้ UI เปิด dialog ยืนยันรับโทษได้
            bool minShippedOnly = !ignoreMinShipped && CanEndDay(out _, true);
            OnDayEndFailed?.Invoke(new DayEndFailure
            {
                reason = failReason,
                canOverrideWithPenalty = minShippedOnly,
                penalty = PenaltyForShortfall,
                shortfall = ShippedShortfallToday
            });
            return false;
        }

        ScoreManager score = ScoreManager.Instance;
        int fee = StationFeeToday;
        int totalBefore = score != null ? score.totalScore : 0;
        int creditsShortfall = Mathf.Max(0, fee - totalBefore);

        // หักค่าสถานีครั้งเดียวตอนเปิดสรุปผล
        if (score != null)
        {
            score.ApplyStationFee(fee);
        }

        DailyStatisticsData stats = score != null ? score.CurrentDayStats : null;
        bool passed = creditsShortfall == 0;

        summaryPending = true;
        lastSummaryPassed = passed;

        OnDaySummaryRequested?.Invoke(new DaySummaryResult
        {
            day = currentDay,
            stats = stats,
            stationFee = fee,
            requiredShipped = RequiredShippedToday,
            shippedToday = ShippedToday,
            shortfallPenalty = stats != null ? stats.dailyShortfallPenalty : 0,
            creditsShortfall = creditsShortfall,
            passed = passed
        });
        return true;
    }

    /// <summary>
    /// ผู้เล่นยืนยันจบวันทั้งที่ส่งพัสดุไม่ถึงขั้นต่ำ: หักโทษแล้วเปิดสรุปผลต่อ
    /// (ยังคงถูกบล็อกด้วยเงื่อนไขแข็ง เช่น ของตกค้าง/โควต้าสปอนเหลือ)
    /// </summary>
    public bool ConfirmEndDayWithPenalty()
    {
        if (summaryPending) return false;

        if (TryGetMissingNextDayConfigReason(out string configReason))
        {
            OnDayEndFailed?.Invoke(new DayEndFailure { reason = configReason });
            return false;
        }

        if (!CanEndDay(out string hardReason, true))
        {
            OnDayEndFailed?.Invoke(new DayEndFailure { reason = hardReason });
            return false;
        }


        int penalty = PenaltyForShortfall;
        if (penalty > 0 && ScoreManager.Instance != null)
        {
            ScoreManager.Instance.ApplyPenalty(penalty, "ส่งพัสดุไม่ถึงขั้นต่ำ");
        }

        return RequestEndDay(true);
    }

    /// <summary>ยืนยันเริ่มวันถัดไปจากหน้าสรุปผล (เฉพาะวันที่ผ่านเงื่อนไข)</summary>
    public void ConfirmEndDayAndProceed()
    {
        if (!summaryPending || !lastSummaryPassed) return;

        summaryPending = false;
        lastSummaryPassed = false;
        ExecuteEndDay();
    }

    // ฟังก์ชันจบวัน (เรียกจากปุ่มจบวัน หรือระบบกะ)
    public void EndDay()
    {
        RequestEndDay();
    }

    // บังคับจบวันทันทีโดยไม่ตรวจเงื่อนไข (สำหรับโหมดทดสอบ / Debug)
    public void ForceEndDay()
    {
        summaryPending = false;
        lastSummaryPassed = false;
        ExecuteEndDay(true);
    }

    private void ExecuteEndDay(bool ignoreDayConfigCheck = false)
    {
        // ห้ามไปต่อถ้าไม่มี DayConfig ของวันถัดไป (ยกเว้นโหมด Debug)
        if (!ignoreDayConfigCheck && TryGetMissingNextDayConfigReason(out string configFailReason))
        {
            Debug.LogWarning($"[GameDayManager] {configFailReason}");
            OnDayEndFailed?.Invoke(new DayEndFailure { reason = configFailReason });
            return;
        }

        summaryPending = false;
        lastSummaryPassed = false;

        // 1. ล้างขยะที่จุดทิ้งขยะ (TrashDropPoint) หากมี
        TrashDropPoint trash = FindFirstObjectByType<TrashDropPoint>();
        if (trash != null)
        {
            trash.ClearTrashAtEndOfDay();
        }

        int completedDay = currentDay;
        Debug.Log($"--- จบวัน (Day {completedDay}) เรียบร้อย! สินค้าทั้งหมดใน Storage จะคงอยู่ข้ามวัน ---");
        OnDayEnded?.Invoke(completedDay);

        currentDay++;
        OnDayChanged?.Invoke(currentDay);
        ResetDailyQuota();
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.StartNewDayStats(currentDay);
        }
        OnDayStarted?.Invoke(currentDay);

        // Auto Save หลังจบวัน (เขียนทับช่องเซฟที่กำลังเล่นอยู่)
        SaveSystem.AutoSave();
    }
}

/// <summary>ข้อมูลแจ้งเตือนเมื่อจบวันไม่สำเร็จ</summary>
public class DayEndFailure
{
    public string reason;

    /// <summary>true = ผู้เล่นยืนยันรับโทษเพื่อข้ามเงื่อนไขขั้นต่ำได้</summary>
    public bool canOverrideWithPenalty;

    /// <summary>โทษที่จะถูกหักหากยืนยัน</summary>
    public int penalty;

    /// <summary>จำนวนพัสดุที่ยังขาดจากขั้นต่ำ</summary>
    public int shortfall;
}

/// <summary>ผลสรุปที่ส่งให้หน้าต่าง DailySummaryUI แสดง</summary>
public class DaySummaryResult
{
    public int day;
    public DailyStatisticsData stats;
    public int stationFee;
    public int requiredShipped;
    public int shippedToday;
    public int shortfallPenalty;
    public int creditsShortfall;
    public bool passed;
}
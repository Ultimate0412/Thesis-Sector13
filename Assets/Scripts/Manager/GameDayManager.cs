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
    public event Action<string> OnDayEndFailed;         // failReason (ส่งแจ้งเตือนเมื่อจบวันไม่สำเร็จ)
    public event Action<int, DailyStatisticsData> OnDaySummaryRequested; // day, stats (เปิดหน้าต่างสรุปผลประจำวัน)

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
    public bool CanEndDay(out string failReason)
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
            issues.Add($"มีกล่องค้างที่จุด Spawn ({boxesOnSpawn} กล่อง)");
        }
        if (boxesOnFloor > 0)
        {
            issues.Add($"มีกล่องตกค้างอยู่บนพื้น ({boxesOnFloor} กล่อง)");
        }
        if (boxesInHands > 0)
        {
            issues.Add($"มีกล่องอยู่ในมือผู้เล่น ({boxesInHands} กล่อง)");
        }
        if (boxesAtShipping > 0)
        {
            issues.Add($"มีกล่องค้างที่จุดส่งสินค้า ({boxesAtShipping} กล่อง)");
        }
        if (itemsOnSpawn > 0)
        {
            issues.Add($"มีสินค้าเดี่ยวค้างที่จุด Spawn ({itemsOnSpawn} ชิ้น)");
        }
        if (itemsOnFloor > 0)
        {
            issues.Add($"มีสินค้าเดี่ยวตกค้างอยู่บนพื้น ({itemsOnFloor} ชิ้น)");
        }
        if (itemsInHands > 0)
        {
            issues.Add($"มีสินค้าเดี่ยวอยู่ในมือผู้เล่น ({itemsInHands} ชิ้น)");
        }

        if (issues.Count > 0)
        {
            failReason = "ไม่สามารถจบวันได้: " + string.Join(", ", issues) + " | กรุณานำไปจัดเก็บใน Storage หรือส่งออกให้เรียบร้อย";
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
            OnDayEndFailed?.Invoke(failReason);
            return false;
        }

        if (!CanEndDay(out failReason))
        {
            Debug.LogWarning($"[GameDayManager] {failReason}");
            OnDayEndFailed?.Invoke(failReason);
            return false;
        }

        ExecuteEndDay();
        return true;
    }

    /// <summary>
    /// ขอเปิดหน้าต่างสรุปผลรายวัน (ตรวจเงื่อนไขก่อน หากผ่านจะส่ง Event เปิด DailySummaryUI)
    /// </summary>
    public bool RequestEndDay()
    {
        if (TryGetMissingNextDayConfigReason(out string failReason))
        {
            Debug.LogWarning($"[GameDayManager] {failReason}");
            OnDayEndFailed?.Invoke(failReason);
            return false;
        }

        if (!CanEndDay(out failReason))
        {
            Debug.LogWarning($"[GameDayManager] {failReason}");
            OnDayEndFailed?.Invoke(failReason);
            return false;
        }

        DailyStatisticsData stats = ScoreManager.Instance != null ? ScoreManager.Instance.CurrentDayStats : null;
        OnDaySummaryRequested?.Invoke(currentDay, stats);
        return true;
    }


    public void ConfirmEndDayAndProceed()
    {
        ExecuteEndDay();
    }

    // ฟังก์ชันจบวัน (เรียกจากปุ่มจบวัน หรือระบบกะ)
    public void EndDay()
    {
        
        if (!RequestEndDay())
        {
            
            return;
        }
    }

    // บังคับจบวันทันทีโดยไม่ตรวจเงื่อนไข (สำหรับโหมดทดสอบ / Debug)
    public void ForceEndDay()
    {
        ExecuteEndDay(true);
    }

    private void ExecuteEndDay(bool ignoreDayConfigCheck = false)
    {
        // ห้ามไปต่อถ้าไม่มี DayConfig ของวันถัดไป (ยกเว้นโหมด Debug)
        if (!ignoreDayConfigCheck && TryGetMissingNextDayConfigReason(out string configFailReason))
        {
            Debug.LogWarning($"[GameDayManager] {configFailReason}");
            OnDayEndFailed?.Invoke(configFailReason);
            return;
        }

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
    }
}
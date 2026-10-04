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

public class GameDayManager : MonoBehaviour
{
    public static GameDayManager Instance;

    [Header("Day Settings")]
    public int currentDay = 1;

    [Header("Inspection / Open Box Quota")]
    [Tooltip("โควต้าการเปิดกล่องพื้นฐานต่อวัน")]
    public int defaultDailyOpenQuota = 15;
    [Tooltip("โควต้าการเปิดกล่องตรวจแยกตามวัน (Index 0 = Day 1, Index 1 = Day 2, ...)")]
    public int[] dailyOpenQuotasPerDay;
    public int dailyOpenQuota = 15; // เพื่อรองรับโค้ดเดิมที่อาจเรียกใช้โดยตรง
    private int remainingQuotaToday;
    public int RemainingQuotaToday => remainingQuotaToday;

    [Header("Item Spawning Quota")]
    [Tooltip("โควต้าการสร้างกล่องสินค้าพื้นฐานต่อวัน")]
    public int defaultDailySpawnQuota = 10;
    [Tooltip("โควต้าการสร้างกล่องสินค้าแยกตามวัน (Index 0 = Day 1, Index 1 = Day 2, ...)")]
    public int[] dailySpawnQuotasPerDay;
    private int remainingSpawnQuotaToday;
    public int RemainingSpawnQuotaToday => remainingSpawnQuotaToday;
    public int TotalSpawnQuotaToday { get; private set; }

    [Header("Category Spawn Rates (ปรับอัตราการเกิดใน Inspector)")]
    [Tooltip("น้ำหนักโอกาสเกิด: สินค้าถูกกฎหมาย (Legal)")]
    [Range(0f, 100f)] public float defaultLegalRate = 70f;
    [Tooltip("น้ำหนักโอกาสเกิด: สินค้าผิดกฎหมาย (Illegal)")]
    [Range(0f, 100f)] public float defaultIllegalRate = 20f;
    [Tooltip("น้ำหนักโอกาสเกิด: วัตถุเอเลี่ยน (Alien)")]
    [Range(0f, 100f)] public float defaultAlienRate = 10f;

    [Header("Daily Rates Progression (สัดส่วนแยกตามวัน - ตัวเลือกเสริม)")]
    [Tooltip("สัดส่วนโอกาสเกิดของแต่ละหมวดหมู่แยกตามวัน (Index 0 = Day 1, Index 1 = Day 2, ...)")]
    public DailyCategorySpawnRate[] categoryRatesPerDay;

    [Header("Debug Key")]
    [Tooltip("ปุ่มสำหรับกดทดสอบจบวัน (ตั้งเป็น None ได้หากไม่ต้องการใช้งาน)")]
    public KeyCode debugEndDayKey = KeyCode.None;

    // Events
    public event Action<int> OnQuotaChanged;            // remainingOpenQuota (รองรับโค้ดเดิม)
    public event Action<int, int> OnSpawnQuotaChanged;   // remainingSpawnQuota, totalSpawnQuota
    public event Action<int> OnDayChanged;              // currentDay (รองรับโค้ดเดิม)
    public event Action<int> OnDayEnded;                // completedDay
    public event Action<int> OnDayStarted;              // newDay
    public event Action<string> OnDayEndFailed;         // failReason (ส่งแจ้งเตือนเมื่อจบวันไม่สำเร็จ)

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

        // 1. คำนวณโควต้าการเปิดตรวจกล่อง (Open Quota)
        if (dailyOpenQuotasPerDay != null && dayIndex >= 0 && dayIndex < dailyOpenQuotasPerDay.Length && dailyOpenQuotasPerDay[dayIndex] > 0)
        {
            dailyOpenQuota = dailyOpenQuotasPerDay[dayIndex];
        }
        else if (defaultDailyOpenQuota > 0)
        {
            dailyOpenQuota = defaultDailyOpenQuota;
        }
        remainingQuotaToday = dailyOpenQuota;
        OnQuotaChanged?.Invoke(remainingQuotaToday);

        // 2. คำนวณโควต้าการสปอนสินค้า (Spawn Quota)
        if (dailySpawnQuotasPerDay != null && dayIndex >= 0 && dayIndex < dailySpawnQuotasPerDay.Length && dailySpawnQuotasPerDay[dayIndex] > 0)
        {
            TotalSpawnQuotaToday = dailySpawnQuotasPerDay[dayIndex];
        }
        else
        {
            TotalSpawnQuotaToday = defaultDailySpawnQuota;
        }
        remainingSpawnQuotaToday = TotalSpawnQuotaToday;
        OnSpawnQuotaChanged?.Invoke(remainingSpawnQuotaToday, TotalSpawnQuotaToday);

        DailyCategorySpawnRate currentRate = GetCurrentCategoryRate();
        Debug.Log($"--- เริ่มวันใหม่ (Day {currentDay}) | โควต้าเปิดตรวจ: {remainingQuotaToday} ครั้ง | โควต้าสินค้า: {remainingSpawnQuotaToday} กล่อง | สัดส่วนสินค้า (Legal:{currentRate.legalWeight}, Illegal:{currentRate.illegalWeight}, Alien:{currentRate.alienWeight}) ---");
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
        int dayIndex = currentDay - 1;
        if (categoryRatesPerDay != null && dayIndex >= 0 && dayIndex < categoryRatesPerDay.Length && categoryRatesPerDay[dayIndex] != null)
        {
            return categoryRatesPerDay[dayIndex];
        }

        return new DailyCategorySpawnRate
        {
            legalWeight = defaultLegalRate,
            illegalWeight = defaultIllegalRate,
            alienWeight = defaultAlienRate
        };
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
        if (!CanEndDay(out failReason))
        {
            Debug.LogWarning($"[GameDayManager] {failReason}");
            OnDayEndFailed?.Invoke(failReason);
            return false;
        }

        ExecuteEndDay();
        return true;
    }

    // ฟังก์ชันจบวัน (เรียกจากปุ่มจบวัน หรือระบบกะ)
    public void EndDay()
    {
        if (!TryEndDay(out string failReason))
        {
            // ล็อกข้อความเตือนเมื่อเงื่อนไขไม่ผ่าน
            return;
        }
    }

    // บังคับจบวันทันทีโดยไม่ตรวจเงื่อนไข (สำหรับโหมดทดสอบ / Debug)
    public void ForceEndDay()
    {
        ExecuteEndDay();
    }

    private void ExecuteEndDay()
    {
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
        OnDayStarted?.Invoke(currentDay);
    }
}
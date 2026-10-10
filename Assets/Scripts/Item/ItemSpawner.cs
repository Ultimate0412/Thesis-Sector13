using UnityEngine;
using System.Collections.Generic;

public class ItemSpawner : MonoBehaviour, IInteractable
{
    [Header("Spawner Settings")]
    [Tooltip("รายการจุด Slot ทั้งหมดบนชั้นวาง")]
    public Transform[] spawnSlots;

    [Tooltip("รายการ Prefab กล่องพัสดุทั้งหมด (ระบบจะจัดกลุ่มหมวดหมู่ให้อัตโนมัติจาก ItemObject ภายในกล่อง)")]
    public GameObject[] packagePrefabs;

    [Header("Category Spawn Rates (ปรับอัตราการเกิดใน Inspector)")]
    [Tooltip("น้ำหนักโอกาสเกิด: สินค้าถูกกฎหมาย (Legal)")]
    [Range(0f, 100f)] public float legalRate = 70f;

    [Tooltip("น้ำหนักโอกาสเกิด: สินค้าผิดกฎหมาย (Illegal)")]
    [Range(0f, 100f)] public float illegalRate = 20f;

    [Tooltip("น้ำหนักโอกาสเกิด: วัตถุเอเลี่ยน (Alien)")]
    [Range(0f, 100f)] public float alienRate = 10f;

    [Header("Advanced / Daily Options (ตัวเลือกเสริม)")]
    [Tooltip("หากเปิดใช้งาน จะดึงเรทจาก GameDayManager แทนเรทใน Inspector ด้านบน")]
    public bool useGameDayManagerRates = false;

    [Tooltip("กำหนดเรทสุ่มหมวดหมู่แยกตามวัน (Index 0 = Day 1, Index 1 = Day 2, ...) หากเว้นว่างไว้จะใช้เรทด้านบน")]
    public DailyCategorySpawnRate[] dailyRatesPerDay;

    // เก็บอ้างอิงกล่องที่อยู่ในแต่ละ Slot
    private GameObject[] spawnedPackages;

    // แคชเก็บรายการ Prefab แยกตามประเภท (Legal, Illegal, Alien)
    private Dictionary<ItemCategory, List<GameObject>> categorizedPrefabs = new Dictionary<ItemCategory, List<GameObject>>();

    private void Awake()
    {
        InitializeCategorizedPrefabs();

        if (spawnSlots != null && spawnSlots.Length > 0)
        {
            spawnedPackages = new GameObject[spawnSlots.Length];
        }
    }

    // --- ระบบจัดกลุ่ม Prefab อัตโนมัติจาก innerItemPrefab.category ---
    public void InitializeCategorizedPrefabs()
    {
        categorizedPrefabs.Clear();
        categorizedPrefabs[ItemCategory.Legal] = new List<GameObject>();
        categorizedPrefabs[ItemCategory.Illegal] = new List<GameObject>();
        categorizedPrefabs[ItemCategory.Alien] = new List<GameObject>();

        if (packagePrefabs == null || packagePrefabs.Length == 0) return;

        foreach (GameObject prefab in packagePrefabs)
        {
            if (prefab == null) continue;

            ItemCategory cat = DetectCategoryFromPrefab(prefab);
            categorizedPrefabs[cat].Add(prefab);
        }

        Debug.Log($"[ItemSpawner] จัดกลุ่มพัสดุสำเร็จ: Legal={categorizedPrefabs[ItemCategory.Legal].Count} | Illegal={categorizedPrefabs[ItemCategory.Illegal].Count} | Alien={categorizedPrefabs[ItemCategory.Alien].Count}");
    }

    private ItemCategory DetectCategoryFromPrefab(GameObject prefab)
    {
        PackageBox box = prefab.GetComponent<PackageBox>();
        if (box != null)
        {
            if (box.innerItemPrefab != null)
            {
                ItemObject item = box.innerItemPrefab.GetComponent<ItemObject>();
                if (item != null)
                {
                    return item.category;
                }
            }
            else if (box.possibleInnerItemPrefabs != null && box.possibleInnerItemPrefabs.Length > 0 && box.possibleInnerItemPrefabs[0] != null)
            {
                ItemObject item = box.possibleInnerItemPrefabs[0].GetComponent<ItemObject>();
                if (item != null)
                {
                    return item.category;
                }
            }
        }
        return ItemCategory.Legal; // ค่าเริ่มต้นหากไม่มี ItemObject
    }

    // --- Implement จาก IInteractable ---
    public string GetInteractPrompt()
    {
        // 1. ถ้าโควต้าสินค้าวันนี้หมด
        if (GameDayManager.Instance != null && !GameDayManager.Instance.CanSpawnPackageToday())
        {
            return "No Quata Remain (Wait the next day)";
        }

        // 2. ถ้าชั้นวางเต็มทุก Slot
        if (IsShelfFull())
        {
            return "Full slot (Grab some Box)";
        }

        // 3. ถ้ายังมีโควต้าและมีที่ว่าง
        if (GameDayManager.Instance != null)
        {
            return $"Press E Fill the Box (Quata Remaining: {GameDayManager.Instance.RemainingSpawnQuotaToday}/{GameDayManager.Instance.TotalSpawnQuotaToday})";
        }

        return "Press E For Fill Box";
    }

    public void Interact(PlayerInteractor interactor)
    {
        SpawnPackagesUntilFull();
    }
    // ------------------------------------

    // ฟังก์ชันสั่งเติมสินค้าจนกว่าจะเต็มทุก Slot หรือโควต้าหมด
    public void SpawnPackagesUntilFull()
    {
        if (spawnSlots == null || spawnSlots.Length == 0 || packagePrefabs == null || packagePrefabs.Length == 0)
        {
            Debug.LogWarning("[ItemSpawner] ยังไม่ได้กำหนด Slot หรือ Prefab พัสดุใน Inspector!");
            return;
        }

        // ตรวจสอบโควต้ากับ GameDayManager ก่อน
        if (GameDayManager.Instance != null && !GameDayManager.Instance.CanSpawnPackageToday())
        {
            Debug.Log("[ItemSpawner] โควต้าสินค้าของวันนี้หมดแล้ว! ไม่สามารถเติมสินค้าได้");
            return;
        }

        int spawnedCount = 0;

        // วนลูปเช็คทุก Slot บนชั้น
        for (int i = 0; i < spawnSlots.Length; i++)
        {
            // ตรวจสอบโควต้าก่อนสร้างแต่ละชิ้น
            if (GameDayManager.Instance != null && !GameDayManager.Instance.CanSpawnPackageToday())
            {
                Debug.Log("[ItemSpawner] โควต้าสินค้าของวันนี้หมดลงระหว่างการเติมของ!");
                break;
            }

            // ถ้า Slot นั้นว่างอยู่ (ไม่มีกล่อง หรือกล่องถูกหยิบออกไปแล้ว)
            if (!HasPackageAtSlot(i))
            {
                GameObject selectedPrefab = GetPackagePrefabToSpawn();
                if (selectedPrefab == null)
                {
                    Debug.LogWarning("[ItemSpawner] ไม่พบ Prefab ที่สามารถสร้างได้!");
                    break;
                }

                // สร้างกล่องขึ้นมาที่ตำแหน่ง Slot นั้นๆ
                GameObject newPackage = Instantiate(selectedPrefab, spawnSlots[i].position, spawnSlots[i].rotation);
                newPackage.transform.SetParent(spawnSlots[i]);

                // บันทึกเก็บไว้ใน Array ประจำ Slot
                spawnedPackages[i] = newPackage;
                spawnedCount++;

                // หักโควต้าสินค้าผ่าน GameDayManager
                if (GameDayManager.Instance != null)
                {
                    GameDayManager.Instance.ConsumeSpawnQuota(1);
                }
            }
        }

        if (spawnedCount > 0)
        {
            int remaining = GameDayManager.Instance != null ? GameDayManager.Instance.RemainingSpawnQuotaToday : -1;
            string quotaMsg = remaining >= 0 ? $" (โควต้าวันนี้คงเหลือ: {remaining} กล่อง)" : "";
            Debug.Log($"[ItemSpawner] เติมกล่องพัสดุสำเร็จ {spawnedCount} กล่อง!{quotaMsg}");
        }
        else
        {
            if (IsShelfFull())
            {
                Debug.Log("[ItemSpawner] ชั้นวางเต็มแล้ว! ไม่มี Slot ว่างให้เติม");
            }
        }
    }

    // สุ่มเลือก Prefab ตามอัตราหมวดหมู่
    private GameObject GetPackagePrefabToSpawn()
    {
        if (packagePrefabs == null || packagePrefabs.Length == 0) return null;

        ItemCategory targetCategory = GetTargetCategory();

        // ค้นหา Prefab จากหมวดหมู่ที่สุ่มได้
        if (categorizedPrefabs.TryGetValue(targetCategory, out List<GameObject> list) && list.Count > 0)
        {
            return list[Random.Range(0, list.Count)];
        }

        // Fallback: หากหมวดหมู่นั้นไม่มี Prefab ให้สุ่มจาก Prefab ทั้งหมดที่มี
        List<GameObject> validPrefabs = new List<GameObject>();
        for (int i = 0; i < packagePrefabs.Length; i++)
        {
            if (packagePrefabs[i] != null) validPrefabs.Add(packagePrefabs[i]);
        }

        if (validPrefabs.Count > 0)
        {
            return validPrefabs[Random.Range(0, validPrefabs.Count)];
        }

        return null;
    }

    private ItemCategory GetTargetCategory()
    {
        // 1. ถ้าต้องการดึงเรทจาก GameDayManager
        if (useGameDayManagerRates && GameDayManager.Instance != null)
        {
            return GameDayManager.Instance.GetRandomCategoryForToday();
        }

        // 2. ถ้ามีการกำหนด Daily Rates แยกตามวันใน Spawner นี้
        int currentDay = GameDayManager.Instance != null ? GameDayManager.Instance.currentDay : 1;
        int dayIndex = currentDay - 1;
        if (dailyRatesPerDay != null && dayIndex >= 0 && dayIndex < dailyRatesPerDay.Length && dailyRatesPerDay[dayIndex] != null)
        {
            DailyCategorySpawnRate dayRate = dailyRatesPerDay[dayIndex];
            return RollCategoryFromWeights(dayRate.legalWeight, dayRate.illegalWeight, dayRate.alienWeight);
        }

        // 3. ใช้เรทที่ตั้งค่าใน Inspector ของ Spawner นี้โดยตรง
        return RollCategoryFromWeights(legalRate, illegalRate, alienRate);
    }

    private ItemCategory RollCategoryFromWeights(float legal, float illegal, float alien)
    {
        legal = Mathf.Max(0f, legal);
        illegal = Mathf.Max(0f, illegal);
        alien = Mathf.Max(0f, alien);
        float total = legal + illegal + alien;

        if (total <= 0.0001f) return ItemCategory.Legal;

        float roll = Random.Range(0f, total);
        if (roll < legal) return ItemCategory.Legal;
        if (roll < legal + illegal) return ItemCategory.Illegal;
        return ItemCategory.Alien;
    }

    // --- Helper Utilities สำหรับการตรวจสอบสถานะของ Slot และกล่อง ---

    // ตรวจสอบว่ากล่องเป้าหมายยังอยู่ที่แท่นสปอนนี้หรือไม่
    public bool IsPackageAtSpawner(GameObject pkg)
    {
        if (pkg == null || spawnSlots == null) return false;
        for (int i = 0; i < spawnSlots.Length; i++)
        {
            if (spawnSlots[i] != null && pkg.transform.IsChildOf(spawnSlots[i]))
            {
                return true;
            }
        }
        return false;
    }

    // ตรวจสอบว่า Slot ที่กำหนดมีกล่องวางอยู่จริงหรือไม่ (หากถูกหยิบออกไปแล้ว จะเคลียร์อ้างอิงอัตโนมัติ)
    public bool HasPackageAtSlot(int index)
    {
        if (spawnedPackages == null || spawnSlots == null || index < 0 || index >= spawnSlots.Length) return false;
        GameObject pkg = spawnedPackages[index];
        if (pkg == null) return false;

        // ถ้ากล่องถูกหยิบไป (parent ไม่ใช่ spawnSlot นี้แล้ว)
        if (pkg.transform.parent != spawnSlots[index])
        {
            spawnedPackages[index] = null; // เคลียร์อ้างอิง เพื่อให้ Slot ว่างสำหรับเติมใหม่
            return false;
        }

        return true;
    }

    // นับจำนวนกล่องที่ยังวางอยู่บนชั้นวาง
    public int GetPackagesOnShelfCount()
    {
        if (spawnSlots == null) return 0;
        int count = 0;
        for (int i = 0; i < spawnSlots.Length; i++)
        {
            if (HasPackageAtSlot(i)) count++;
        }
        return count;
    }

    public bool HasAnyPackageOnShelf()
    {
        return GetPackagesOnShelfCount() > 0;
    }

    public bool IsShelfFull()
    {
        if (spawnSlots == null || spawnSlots.Length == 0) return true;
        for (int i = 0; i < spawnSlots.Length; i++)
        {
            if (!HasPackageAtSlot(i)) return false;
        }
        return true;
    }

    public int GetEmptySlotCount()
    {
        if (spawnSlots == null) return 0;
        int count = 0;
        for (int i = 0; i < spawnSlots.Length; i++)
        {
            if (!HasPackageAtSlot(i)) count++;
        }
        return count;
    }
}
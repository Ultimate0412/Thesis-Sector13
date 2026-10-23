using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public enum SpawnLocationMode
{
    AtStorageSlots,      // สปอนตรงหน้าชั้นวางของ ณ ตำแหน่งกล่องที่เกิดปฏิกิริยา
    AtEnemySpawnerPoints // สปอนที่จุดเกิดของ EnemySpawner ในห้อง
}

[System.Serializable]
public class StorageReactionRule
{
    public string ruleName = "Alien Medicine + Illegal Bio-hazard";

    [Header("Item A Conditions")]
    public ItemCategory categoryA = ItemCategory.Alien;
    public ItemSubCategory subCategoryA = ItemSubCategory.Medical;
    public bool matchAnySubCategoryA = false;

    [Header("Item B Conditions")]
    public ItemCategory categoryB = ItemCategory.Illegal;
    public ItemSubCategory subCategoryB = ItemSubCategory.Biological;
    public bool matchAnySubCategoryB = false;

    [Header("Countdown & Spawn Settings")]
    [Tooltip("ระยะเวลานับถอยหลังก่อนเกิดเหตุการณ์ (วินาที)")]
    public float countdownDuration = 20f;

    [Tooltip("Prefab ศัตรูที่จะสปอน (ถ้าไม่ระบุ จะดึงจาก EnemySpawner.Instance)")]
    public GameObject enemyPrefabOverride;

    [Tooltip("ตำแหน่งที่ศัตรูจะเกิด")]
    public SpawnLocationMode spawnLocation = SpawnLocationMode.AtStorageSlots;

    [Tooltip("ทำลายกล่องพัสดุทั้งสองชิ้นทันทีเมื่อมอนสเตอร์เกิด")]
    public bool destroyReactingBoxes = true;

    [Header("Audio & Visual Feedback")]
    [Tooltip("Prefab เอฟเฟกต์ควัน/แสงเตือนขณะกำลังนับถอยหลัง")]
    public GameObject warningVfxPrefab;

    [Tooltip("คลิปเสียงแจ้งเตือนขณะนับถอยหลัง (เล่นวนซ้ำ)")]
    public AudioClip warningAudioClip;

    [Tooltip("Prefab เอฟเฟกต์ระเบิด/ควันกระจายเมื่อศัตรูสปอนออกมา")]
    public GameObject spawnExplosionVfx;

    [Tooltip("คลิปเสียงระเบิดเมื่อศัตรูสปอนออกมา")]
    public AudioClip spawnAudioClip;

    public bool Matches(ItemCategory cat1, ItemSubCategory sub1, ItemCategory cat2, ItemSubCategory sub2)
    {
        bool matchForward = (cat1 == categoryA && (matchAnySubCategoryA || sub1 == subCategoryA)) &&
                            (cat2 == categoryB && (matchAnySubCategoryB || sub2 == subCategoryB));

        bool matchReverse = (cat2 == categoryA && (matchAnySubCategoryA || sub2 == subCategoryA)) &&
                            (cat1 == categoryB && (matchAnySubCategoryB || sub1 == subCategoryB));

        return matchForward || matchReverse;
    }
}

public class ActiveHazardReaction
{
    public StorageReactionRule rule;
    public GameObject itemA;
    public GameObject itemB;
    public int slotIndexA;
    public int slotIndexB;
    public float remainingTimer;
    public GameObject vfxInstance;
    public AudioSource audioSourceInstance;
    public TextMeshPro worldTextInstance;
    public Light proceduralLightInstance;
}

/// <summary>
/// ระบบจัดการปฏิกิริยาอันตรายใน Storage (Hazardous Reaction & Enemy Spawner)
/// ตรวจจับเมื่อมีสิ่งของที่ไม่เข้ากันวางไว้ชิดกันบนชั้นวาง เริ่มนับถอยหลัง และสปอนศัตรูเมื่อเวลาหมด
/// </summary>
public class StorageHazardManager : MonoBehaviour
{
    public static StorageHazardManager Instance { get; private set; }

    [Header("Storage Reaction Rules")]
    [Tooltip("รายการกฎปฏิกิริยาอันตราย (เพิ่มได้หลายคู่ใน Inspector)")]
    public List<StorageReactionRule> reactionRules = new List<StorageReactionRule>();

    [Header("Adjacency & Proximity Settings")]
    [Tooltip("เช็คว่าช่อง Slot ใน Storage อยู่ติดกันใน Index หรือไม่ (|slotA - slotB| <= maxIndexDiff)")]
    public bool checkSlotIndexAdjacency = true;

    [Tooltip("เช็คระยะห่างทางกายภาพจริง (World Position) ระหว่าง Slot หรือไม่")]
    public bool checkPhysicalDistance = true;

    [Tooltip("ระยะห่างสูงสุด (เมตร) ที่ถือว่าสิ่งของวางอยู่ใกล้ชิดกันจนทำปฏิกิริยา")]
    public float maxReactionDistance = 2.0f;

    [Header("Spawn Position Settings")]
    [Tooltip("ระยะขยับเยื้องออกจากชั้นวางของ เพื่อไม่ให้ศัตรูเกิดแล้วติดอยู่ในชั้นวาง (X, Y, Z ข้างหน้า)")]
    public Vector3 spawnOffset = new Vector3(0f, 0.1f, 0.8f);

    [Header("Feedback Settings")]
    [Tooltip("แสดงข้อความแจ้งเตือนและตัวเลขนับถอยหลังลอยอยู่เหนือกล่องที่ทำปฏิกิริยาหรือไม่")]
    public bool showFloatingWarningText = true;

    private StorageRoomManager storageRoom;
    private List<ActiveHazardReaction> activeReactions = new List<ActiveHazardReaction>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }

        storageRoom = GetComponent<StorageRoomManager>();
        if (storageRoom == null) storageRoom = StorageRoomManager.Instance;

        // หากยังไม่มี Rule ใดๆ ให้ตั้งค่าเริ่มต้น: Alien (Medical) + Illegal (Biological)
        if (reactionRules == null || reactionRules.Count == 0)
        {
            reactionRules = new List<StorageReactionRule>
            {
                new StorageReactionRule
                {
                    ruleName = "Alien Medical + Illegal Bio-hazard",
                    categoryA = ItemCategory.Alien,
                    subCategoryA = ItemSubCategory.Medical,
                    matchAnySubCategoryA = false,

                    categoryB = ItemCategory.Illegal,
                    subCategoryB = ItemSubCategory.Biological,
                    matchAnySubCategoryB = false,

                    countdownDuration = 20f,
                    spawnLocation = SpawnLocationMode.AtStorageSlots,
                    destroyReactingBoxes = true
                }
            };
        }
    }

    private void Start()
    {
        if (storageRoom == null) storageRoom = StorageRoomManager.Instance;

        if (storageRoom != null)
        {
            storageRoom.OnItemPlacedInStorage += HandleItemPlaced;
            storageRoom.OnItemRemovedFromStorage += HandleItemRemoved;
        }

        // สแกนสิ่งของที่อาจมีอยู่เดิมในชั้นวาง
        EvaluateStorageHazards();
    }

    private void OnDestroy()
    {
        if (storageRoom != null)
        {
            storageRoom.OnItemPlacedInStorage -= HandleItemPlaced;
            storageRoom.OnItemRemovedFromStorage -= HandleItemRemoved;
        }

        ClearAllActiveReactions();
    }

    private void HandleItemPlaced(GameObject item, int startIndex, int size)
    {
        EvaluateStorageHazards();
    }

    private void HandleItemRemoved(GameObject item)
    {
        EvaluateStorageHazards();
    }

    private void Update()
    {
        if (activeReactions.Count == 0) return;

        // วนลูปอัปเดตตัวนับถอยหลังของปฏิกิริยาที่กำลังดำเนินอยู่
        for (int i = activeReactions.Count - 1; i >= 0; i--)
        {
            ActiveHazardReaction reaction = activeReactions[i];

            // ตรวจสอบความถูกต้องของวัตถุทั้งสอง
            if (reaction.itemA == null || reaction.itemB == null)
            {
                CleanUpReactionFeedback(reaction);
                activeReactions.RemoveAt(i);
                continue;
            }

            // ลดเวลา
            reaction.remainingTimer -= Time.deltaTime;

            // อัปเดตเอฟเฟกต์ไฟกะพริบและข้อความเตือนลอย
            UpdateReactionVisuals(reaction);

            // เมื่อหมดเวลา -> สปอนศัตรู!
            if (reaction.remainingTimer <= 0f)
            {
                TriggerEnemySpawn(reaction);
                CleanUpReactionFeedback(reaction);
                activeReactions.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// สแกนช่องเก็บของทั้งหมดใน Storage เพื่อหาคู่สิ่งของที่เข้าเกณฑ์ปฏิกิริยา
    /// </summary>
    public void EvaluateStorageHazards()
    {
        if (storageRoom == null || storageRoom.ItemsInSlots == null) return;

        GameObject[] items = storageRoom.ItemsInSlots;
        int total = items.Length;

        // บันทึกคู่ปฏิกิริยาที่พบในการสแกนรอบนี้
        HashSet<KeyValuePair<GameObject, GameObject>> currentDetectedPairs = new HashSet<KeyValuePair<GameObject, GameObject>>();

        for (int i = 0; i < total; i++)
        {
            GameObject objA = items[i];
            if (objA == null) continue;

            if (!TryGetItemMetadata(objA, out ItemCategory catA, out ItemSubCategory subA)) continue;

            for (int j = i + 1; j < total; j++)
            {
                GameObject objB = items[j];
                if (objB == null || objB == objA) continue;

                if (!TryGetItemMetadata(objB, out ItemCategory catB, out ItemSubCategory subB)) continue;

                // ตรวจสอบความใกล้ชิด (Adjacency)
                if (AreItemsAdjacent(i, objA, j, objB))
                {
                    // ตรวจสอบว่าตรงกับกฎข้อใด
                    StorageReactionRule matchedRule = FindMatchingRule(catA, subA, catB, subB);
                    if (matchedRule != null)
                    {
                        var pairKey = MakePairKey(objA, objB);
                        currentDetectedPairs.Add(pairKey);

                        // หากยังไม่ได้เริ่มทำปฏิกิริยา ให้เริ่มทันที
                        if (!IsPairAlreadyReacting(objA, objB))
                        {
                            StartHazardReaction(matchedRule, objA, i, objB, j);
                        }
                    }
                }
            }
        }

        // ตรวจสอบว่ามีปฏิกิริยาใดที่ถูกแก้ (Defused) โดยผู้เล่นหยิบแยกกล่องออกหรือไม่
        for (int k = activeReactions.Count - 1; k >= 0; k--)
        {
            var r = activeReactions[k];
            var key = MakePairKey(r.itemA, r.itemB);

            if (!currentDetectedPairs.Contains(key))
            {
                // แยกกล่องสำเร็จ! ปฏิกิริยาสงบลง
                Debug.Log($"<color=#55FF88>[StorageHazardManager]</color> ปฏิกิริยาสงบลง! ผู้เล่นแยกวัตถุอันตรายออกจากกันเรียบร้อย ({r.itemA.name} & {r.itemB.name})");
                CleanUpReactionFeedback(r);
                activeReactions.RemoveAt(k);
            }
        }
    }

    private bool AreItemsAdjacent(int slotA, GameObject objA, int slotB, GameObject objB)
    {
        // 1. เช็ค Slot Index Adjacency
        if (checkSlotIndexAdjacency)
        {
            ItemObject compA = objA.GetComponent<ItemObject>();
            int sizeA = compA != null ? compA.sizeInSlots : 1;

            ItemObject compB = objB.GetComponent<ItemObject>();
            int sizeB = compB != null ? compB.sizeInSlots : 1;

            // วางติดกันแบบ Slot A ต่อด้วย Slot B หรือ Slot B ต่อด้วย Slot A
            if (slotA + sizeA == slotB || slotB + sizeB == slotA)
            {
                return true;
            }

            // ในกรณีช่อง Index ถัดกันตรงๆ
            if (Mathf.Abs(slotA - slotB) == 1)
            {
                return true;
            }
        }

        // 2. เช็ค World Physical Distance
        if (checkPhysicalDistance && storageRoom.slotPositions != null)
        {
            Vector3 posA = (slotA < storageRoom.slotPositions.Length && storageRoom.slotPositions[slotA] != null)
                ? storageRoom.slotPositions[slotA].position : objA.transform.position;

            Vector3 posB = (slotB < storageRoom.slotPositions.Length && storageRoom.slotPositions[slotB] != null)
                ? storageRoom.slotPositions[slotB].position : objB.transform.position;

            float dist = Vector3.Distance(posA, posB);
            if (dist <= maxReactionDistance)
            {
                return true;
            }
        }

        return false;
    }

    private StorageReactionRule FindMatchingRule(ItemCategory catA, ItemSubCategory subA, ItemCategory catB, ItemSubCategory subB)
    {
        if (reactionRules == null) return null;

        foreach (var rule in reactionRules)
        {
            if (rule != null && rule.Matches(catA, subA, catB, subB))
            {
                return rule;
            }
        }
        return null;
    }

    private void StartHazardReaction(StorageReactionRule rule, GameObject itemA, int slotA, GameObject itemB, int slotB)
    {
        ActiveHazardReaction newReaction = new ActiveHazardReaction
        {
            rule = rule,
            itemA = itemA,
            itemB = itemB,
            slotIndexA = slotA,
            slotIndexB = slotB,
            remainingTimer = rule.countdownDuration
        };

        Vector3 midpoint = (itemA.transform.position + itemB.transform.position) * 0.5f;

        // 1. สร้าง VFX แจ้งเตือน
        if (rule.warningVfxPrefab != null)
        {
            newReaction.vfxInstance = Instantiate(rule.warningVfxPrefab, midpoint, Quaternion.identity);
        }
        else
        {
            // สร้างแสงแจ้งเตือนแบบ Procedural (ไฟกะพริบสีแดงสลับส้ม)
            GameObject lightObj = new GameObject("HazardWarningLight");
            lightObj.transform.position = midpoint + Vector3.up * 0.3f;
            Light pl = lightObj.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = new Color(1f, 0.2f, 0.1f);
            pl.intensity = 2.5f;
            pl.range = 3.5f;
            newReaction.proceduralLightInstance = pl;
            newReaction.vfxInstance = lightObj;
        }

        // 2. สร้างเสียงเตือน (Looping Audio)
        if (rule.warningAudioClip != null)
        {
            GameObject audioObj = newReaction.vfxInstance != null ? newReaction.vfxInstance : new GameObject("HazardAudioSource");
            AudioSource src = audioObj.AddComponent<AudioSource>();
            src.clip = rule.warningAudioClip;
            src.loop = true;
            src.spatialBlend = 1f; // 3D Sound
            src.minDistance = 2f;
            src.maxDistance = 15f;
            src.Play();
            newReaction.audioSourceInstance = src;
        }

        // 3. สร้าง Text แจ้งเตือนลอยใน World Space
        if (showFloatingWarningText)
        {
            GameObject textObj = new GameObject("HazardWarningText");
            textObj.transform.position = midpoint + Vector3.up * 0.85f;
            TextMeshPro tm = textObj.AddComponent<TextMeshPro>();
            tm.fontSize = 3.5f;
            tm.alignment = TextAlignmentOptions.Center;
            tm.color = new Color(1f, 0.3f, 0.2f);
            tm.text = $"⚠️ HAZARD REACTION!\n{Mathf.CeilToInt(rule.countdownDuration)}s";
            newReaction.worldTextInstance = tm;
        }

        activeReactions.Add(newReaction);

        Debug.LogWarning($"<color=#FF3333>[StorageHazardManager] คำเตือนปฏิกิริยาอันตราย!</color> วัตถุ <b>{itemA.name}</b> และ <b>{itemB.name}</b> เริ่มทำปฏิกิริยา! ศัตรูจะเกิดใน {rule.countdownDuration} วินาที!");
    }

    private void UpdateReactionVisuals(ActiveHazardReaction reaction)
    {
        int secondsLeft = Mathf.Max(0, Mathf.CeilToInt(reaction.remainingTimer));

        // อัปเดตข้อความลอยให้หันเข้าหากล้องผู้เล่นเสมอ
        if (reaction.worldTextInstance != null)
        {
            reaction.worldTextInstance.text = $"⚠️ HAZARD MUTATION!\n<size=120%><color=#FF2222>{secondsLeft}s</color></size>";
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                reaction.worldTextInstance.transform.rotation = Quaternion.LookRotation(reaction.worldTextInstance.transform.position - mainCam.transform.position);
            }
        }

        // ไฟกะพริบเร็วขึ้นตามเวลาที่ใกล้หมด
        if (reaction.proceduralLightInstance != null)
        {
            float flashSpeed = Mathf.Lerp(12f, 3f, reaction.remainingTimer / reaction.rule.countdownDuration);
            reaction.proceduralLightInstance.intensity = 1.5f + Mathf.PingPong(Time.time * flashSpeed, 2.5f);
        }
    }

    private void TriggerEnemySpawn(ActiveHazardReaction reaction)
    {
        Debug.LogError($"<color=#FF0044>[StorageHazardManager] ปฏิกิริยาปะทุสมบูรณ์!</color> สปอนศัตรูจากการกลายพันธุ์ของ {reaction.itemA.name} และ {reaction.itemB.name}!");

        Vector3 midpoint = (reaction.itemA.transform.position + reaction.itemB.transform.position) * 0.5f;
        Quaternion spawnRotation = Quaternion.identity;

        // คำนวณตำแหน่งเกิดด้านหน้าชั้นวางของ
        Transform slotTrans = null;
        if (storageRoom.slotPositions != null && reaction.slotIndexA < storageRoom.slotPositions.Length)
        {
            slotTrans = storageRoom.slotPositions[reaction.slotIndexA];
        }

        Vector3 spawnPos = midpoint;
        if (slotTrans != null)
        {
            spawnPos = midpoint + slotTrans.forward * spawnOffset.z + slotTrans.up * spawnOffset.y + slotTrans.right * spawnOffset.x;
            spawnRotation = slotTrans.rotation;
        }

        // 1. สปอนศัตรู
        if (reaction.rule.spawnLocation == SpawnLocationMode.AtEnemySpawnerPoints && EnemySpawner.Instance != null)
        {
            EnemySpawner.Instance.SpawnEnemy();
        }
        else
        {
            // สปอนตรงชั้นวางของ
            if (EnemySpawner.Instance != null)
            {
                EnemySpawner.Instance.SpawnEnemyAtLocation(reaction.rule.enemyPrefabOverride, spawnPos, spawnRotation);
            }
            else
            {
                GameObject prefab = reaction.rule.enemyPrefabOverride;
                if (prefab != null)
                {
                    Instantiate(prefab, spawnPos, spawnRotation);
                }
            }
        }

        // 2. เอฟเฟกต์การระเบิดเกิดศัตรู
        if (reaction.rule.spawnExplosionVfx != null)
        {
            Instantiate(reaction.rule.spawnExplosionVfx, midpoint, Quaternion.identity);
        }
        if (reaction.rule.spawnAudioClip != null)
        {
            AudioSource.PlayClipAtPoint(reaction.rule.spawnAudioClip, midpoint);
        }

        // 3. ทำลายกล่องพัสดุทั้ง 2 กล่องทิ้งทันที
        if (reaction.rule.destroyReactingBoxes)
        {
            GameObject boxA = reaction.itemA;
            GameObject boxB = reaction.itemB;

            if (boxA != null)
            {
                BaseDropPoint dpA = BaseDropPoint.GetDropPointHolding(boxA);
                if (dpA != null) dpA.currentPlacedItem = null;

                storageRoom.RemoveItemFromSlots(boxA);
                Destroy(boxA);
            }
            if (boxB != null)
            {
                BaseDropPoint dpB = BaseDropPoint.GetDropPointHolding(boxB);
                if (dpB != null) dpB.currentPlacedItem = null;

                storageRoom.RemoveItemFromSlots(boxB);
                Destroy(boxB);
            }
        }
    }

    private void CleanUpReactionFeedback(ActiveHazardReaction reaction)
    {
        if (reaction.worldTextInstance != null)
        {
            Destroy(reaction.worldTextInstance.gameObject);
        }
        if (reaction.vfxInstance != null)
        {
            Destroy(reaction.vfxInstance);
        }
    }

    private void ClearAllActiveReactions()
    {
        foreach (var r in activeReactions)
        {
            CleanUpReactionFeedback(r);
        }
        activeReactions.Clear();
    }

    private bool IsPairAlreadyReacting(GameObject a, GameObject b)
    {
        foreach (var r in activeReactions)
        {
            if ((r.itemA == a && r.itemB == b) || (r.itemA == b && r.itemB == a))
            {
                return true;
            }
        }
        return false;
    }

    private KeyValuePair<GameObject, GameObject> MakePairKey(GameObject a, GameObject b)
    {
        if (a.GetHashCode() < b.GetHashCode()) return new KeyValuePair<GameObject, GameObject>(a, b);
        else return new KeyValuePair<GameObject, GameObject>(b, a);
    }

    /// <summary>
    /// สกัดข้อมูลหมวดหมู่ (Category & SubCategory) จากวัตถุ ไม่ว่าจะเป็น PackageBox หรือ ItemObject
    /// </summary>
    public static bool TryGetItemMetadata(GameObject obj, out ItemCategory category, out ItemSubCategory subCategory)
    {
        category = ItemCategory.Legal;
        subCategory = ItemSubCategory.Medical;
        if (obj == null) return false;

        // 1. ตรวจสอบจาก PackageBox
        PackageBox box = obj.GetComponent<PackageBox>();
        if (box == null) box = obj.GetComponentInChildren<PackageBox>();
        if (box == null) box = obj.GetComponentInParent<PackageBox>();

        if (box != null)
        {
            // ก. ตรวจสอบจาก innerItemPrefab
            if (box.innerItemPrefab != null)
            {
                ItemObject innerItem = box.innerItemPrefab.GetComponent<ItemObject>();
                if (innerItem != null)
                {
                    category = innerItem.category;
                    subCategory = innerItem.subCategory;
                    return true;
                }
            }

            // ข. ตรวจสอบจาก possibleInnerItemPrefabs
            if (box.possibleInnerItemPrefabs != null && box.possibleInnerItemPrefabs.Length > 0 && box.possibleInnerItemPrefabs[0] != null)
            {
                ItemObject innerItem = box.possibleInnerItemPrefabs[0].GetComponent<ItemObject>();
                if (innerItem != null)
                {
                    category = innerItem.category;
                    subCategory = innerItem.subCategory;
                    return true;
                }
            }

            // ค. ตรวจสอบจากข้อมูลใบปะหน้าทางการ (officialDatabaseData)
            if (box.officialDatabaseData != null)
            {
                category = box.officialDatabaseData.declaredCategory;
                subCategory = box.officialDatabaseData.declaredSubCategory;
                return true;
            }
        }

        // 2. ตรวจสอบจาก ItemObject โดยตรง
        ItemObject item = obj.GetComponent<ItemObject>();
        if (item == null) item = obj.GetComponentInChildren<ItemObject>();
        if (item == null) item = obj.GetComponentInParent<ItemObject>();

        if (item != null)
        {
            category = item.category;
            subCategory = item.subCategory;
            return true;
        }

        return false;
    }
}

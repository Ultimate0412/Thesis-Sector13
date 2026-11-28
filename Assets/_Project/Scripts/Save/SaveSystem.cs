using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ระบบเซฟ/โหลดแบบไฟล์ JSON แยกช่อง
/// - Auto Save: เรียกจาก GameDayManager ตอนจบวัน
/// - Load: ตั้ง pending load แล้วโหลดฉาก GamePlay, ให้ GameplaySaveLoader เป็นคน apply
/// </summary>
public static class SaveSystem
{
    private const string SaveFolderName = "Saves";
    private const string FilePrefix = "slot_";
    private const string FileExtension = ".json";
    private const string MainMenuSceneName = "MainMenu";
    private const string GameplaySceneName = "GamePlay";

    // --- สถานะรอโหลดข้ามฉาก ---
    private static int pendingLoadSlot = -1;
    private static bool pendingNewGame = false;
    private static int pendingNewGameSlot = -1;

    public static bool HasPendingLoad => pendingLoadSlot > 0;
    public static int PendingLoadSlot => pendingLoadSlot;
    public static bool HasPendingNewGame => pendingNewGame;

    public static string SaveDirectory => Path.Combine(Application.persistentDataPath, SaveFolderName);

    public static string GetSlotPath(int slot)
    {
        slot = Mathf.Clamp(slot, 1, GameSession.SlotCount);
        return Path.Combine(SaveDirectory, $"{FilePrefix}{slot}{FileExtension}");
    }

    public static bool HasSlot(int slot)
    {
        return File.Exists(GetSlotPath(slot));
    }

    // ---------------------------------------------------------------- Menu API

    /// <summary>เริ่มเกมใหม่ในช่องที่ระบุ (ลบเซฟเดิมของช่องนั้น)</summary>
    public static void StartNewGame(int slot)
    {
        slot = Mathf.Clamp(slot, 1, GameSession.SlotCount);

        GameSession.CurrentSlot = slot;
        GameSession.AutoSaveEnabled = true;
        GameSession.SessionActive = true;

        DeleteSlot(slot);

        pendingLoadSlot = -1;
        pendingNewGame = true;
        pendingNewGameSlot = slot;

        Time.timeScale = 1f;
        SceneManager.LoadScene(GameplaySceneName);
    }

    /// <summary>ขอโหลดเซฟของช่องที่ระบุ</summary>
    public static void RequestLoad(int slot)
    {
        slot = Mathf.Clamp(slot, 1, GameSession.SlotCount);
        if (!HasSlot(slot))
        {
            Debug.LogWarning($"[SaveSystem] Slot {slot} ไม่มีไฟล์เซฟ");
            return;
        }

        GameSession.CurrentSlot = slot;
        GameSession.AutoSaveEnabled = true;
        GameSession.SessionActive = true;

        pendingNewGame = false;
        pendingNewGameSlot = -1;
        pendingLoadSlot = slot;

        Time.timeScale = 1f;
        SceneManager.LoadScene(GameplaySceneName);
    }

    public static void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(MainMenuSceneName);
    }

    /// <summary>
    /// เริ่มวันปัจจุบันใหม่จากเซฟต้นวัน (ถ้าไม่มีเซฟ/ยังไม่เริ่มจาก Main Menu จะ reload ฉากแทน)
    /// </summary>
    public static void RestartCurrentDay()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        int day = GameDayManager.Instance != null ? GameDayManager.Instance.currentDay : 1;
        int slot = GameSession.CurrentSlot;

        if (GameSession.SessionActive && HasSlot(slot))
        {
            // ใช้เซฟต้นวันได้เฉพาะเมื่อเซฟเป็นวันเดียวกับวันที่กำลังเล่นอยู่
            // ไม่งั้น Day จะเด้งกลับไปวันที่เซฟไว้
            SaveSlotInfo info = GetSlotInfo(slot);
            if (!info.corrupted && info.day == day)
            {
                RequestLoad(slot);
                return;
            }

            Debug.LogWarning($"[SaveSystem] เซฟช่อง {slot} เป็น Day {info.day} แต่ปัจจุบัน Day {day} — จะ reload ฉากและคง Day {day}");
        }

        // ไม่มีเซฟที่ตรงวัน (เช่นกด Play ตรง ๆ ใน GamePlay) ให้ reload ฉาก
        // แต่คงเลขวันปัจจุบันไว้ ไม่ให้กลับไป Day 1
        GameDayManager.SetPendingStartDay(day);

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>ดึง pending load (คืน null ถ้าไม่มี/อ่านไฟล์ไม่ได้)</summary>
    public static SaveGameData ConsumePendingLoad()
    {
        if (pendingLoadSlot <= 0) return null;

        int slot = pendingLoadSlot;
        pendingLoadSlot = -1;

        SaveGameData data = ReadSlot(slot);
        if (data != null)
        {
            data.slotIndex = slot;
            GameSession.CurrentSlot = slot;
        }
        return data;
    }

    /// <summary>ดึง pending new game (คืน true พร้อมหมายเลขช่อง)</summary>
    public static bool ConsumePendingNewGame(out int slot)
    {
        slot = pendingNewGameSlot;
        bool result = pendingNewGame;
        pendingNewGame = false;
        pendingNewGameSlot = -1;
        return result;
    }

    // ---------------------------------------------------------------- File IO

    /// <summary>อ่านข้อมูลสรุปของช่อง (สำหรับการ์ดใน Main Menu)</summary>
    public static SaveSlotInfo GetSlotInfo(int slot)
    {
        slot = Mathf.Clamp(slot, 1, GameSession.SlotCount);

        SaveSlotInfo info = new SaveSlotInfo
        {
            slotIndex = slot,
            exists = HasSlot(slot)
        };

        if (!info.exists) return info;

        SaveGameData data = ReadSlot(slot);
        if (data == null)
        {
            info.corrupted = true;
            return info;
        }

        info.day = data.day;
        info.totalScore = data.totalScore;
        info.savedAtUtc = data.savedAtUtc;
        return info;
    }

    public static SaveGameData ReadSlot(int slot)
    {
        string path = GetSlotPath(slot);
        if (!File.Exists(path)) return null;

        try
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            SaveGameData data = JsonUtility.FromJson<SaveGameData>(json);
            if (data == null) return null;

            if (data.items == null) data.items = new SaveItemEntry[0];
            if (data.player == null) data.player = new SavePlayerState();
            data.slotIndex = Mathf.Clamp(slot, 1, GameSession.SlotCount);
            return data;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] อ่านไฟล์เซฟช่อง {slot} ไม่ได้: {e.Message}");
            return null;
        }
    }

    public static void DeleteSlot(int slot)
    {
        try
        {
            string path = GetSlotPath(slot);
            if (File.Exists(path))
            {
                File.Delete(path);
                Debug.Log($"[SaveSystem] ลบเซฟช่อง {slot} แล้ว");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] ลบเซฟช่อง {slot} ไม่ได้: {e.Message}");
        }
    }

    /// <summary>เซฟช่องปัจจุบัน (เรียกจาก Auto Save ตอนจบวัน)</summary>
    public static bool AutoSave()
    {
        if (!GameSession.AutoSaveEnabled) return false;

        if (!GameSession.SessionActive)
        {
            Debug.Log("[SaveSystem] ข้าม Auto Save เพราะยังไม่ได้เริ่มเกมจาก Main Menu (กำลังเล่นฉาก GamePlay ตรง ๆ)");
            return false;
        }

        return SaveToSlot(GameSession.CurrentSlot);
    }

    /// <summary>เก็บ state จากฉากปัจจุบันแล้วเขียนลงไฟล์</summary>
    public static bool SaveToSlot(int slot)
    {
        slot = Mathf.Clamp(slot, 1, GameSession.SlotCount);

        try
        {
            SaveGameData data = CaptureState(slot);

            Directory.CreateDirectory(SaveDirectory);

            string path = GetSlotPath(slot);
            string tempPath = path + ".tmp";
            string json = JsonUtility.ToJson(data, true);

            File.WriteAllText(tempPath, json, new UTF8Encoding(false));

            // เขียนทับแบบ atomic: เขียน temp ก่อนแล้วค่อยแทนไฟล์จริง
            if (File.Exists(path)) File.Delete(path);
            File.Move(tempPath, path);

            Debug.Log($"[SaveSystem] Auto-saved slot {slot} (Day {data.day}, score {data.totalScore}, items {data.items.Length}) → {path}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] เซฟช่อง {slot} ไม่สำเร็จ: {e}");
            return false;
        }
    }

    // ---------------------------------------------------------------- Capture

    private static SaveGameData CaptureState(int slot)
    {
        SaveGameData data = new SaveGameData
        {
            slotIndex = slot,
            savedAtUtc = DateTime.UtcNow.ToString("o")
        };

        GameDayManager dayManager = GameDayManager.Instance != null
            ? GameDayManager.Instance
            : UnityEngine.Object.FindAnyObjectByType<GameDayManager>();

        if (dayManager != null)
        {
            data.day = dayManager.currentDay;
            data.remainingOpenQuota = dayManager.RemainingQuotaToday;
            data.remainingSpawnQuota = dayManager.RemainingSpawnQuotaToday;
        }

        ScoreManager scoreManager = ScoreManager.Instance != null
            ? ScoreManager.Instance
            : UnityEngine.Object.FindAnyObjectByType<ScoreManager>();

        if (scoreManager != null)
        {
            data.totalScore = scoreManager.totalScore;
            data.rejectedPackagesCount = scoreManager.rejectedPackagesCount;
        }

        PlayerController player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            Vector3 pos = player.transform.position;
            data.player.x = pos.x;
            data.player.y = pos.y;
            data.player.z = pos.z;
            data.player.yaw = player.rotationY;

            if (player.playerCamera != null)
            {
                data.player.pitch = NormalizeAngle(player.playerCamera.localEulerAngles.x);
            }
        }

        StorageRoomManager storage = StorageRoomManager.Instance != null
            ? StorageRoomManager.Instance
            : UnityEngine.Object.FindAnyObjectByType<StorageRoomManager>();

        List<SaveItemEntry> entries = new List<SaveItemEntry>();
        if (storage != null && storage.ItemsInSlots != null)
        {
            for (int i = 0; i < storage.ItemsInSlots.Length; i++)
            {
                GameObject item = storage.ItemsInSlots[i];
                if (item == null) continue;

                SaveItemEntry entry = CreateItemEntry(item, i);
                if (entry != null) entries.Add(entry);
            }
        }

        data.items = entries.ToArray();
        return data;
    }

    private static SaveItemEntry CreateItemEntry(GameObject item, int slotIndex)
    {
        if (item == null) return null;

        string prefabName = item.name.Replace("(Clone)", string.Empty).Trim();
        if (string.IsNullOrEmpty(prefabName))
        {
            Debug.LogWarning($"[SaveSystem] ข้ามของที่ไม่มีชื่อ prefab: {item.name}");
            return null;
        }

        SaveItemEntry entry = new SaveItemEntry
        {
            prefabName = prefabName,
            slotIndex = slotIndex,
            sizeInSlots = 1
        };

        ItemObject itemObject = item.GetComponent<ItemObject>();
        if (itemObject != null)
        {
            entry.sizeInSlots = Mathf.Max(1, itemObject.sizeInSlots);
        }

        PackageBox box = item.GetComponent<PackageBox>();
        if (box != null)
        {
            entry.isPackageBox = true;
            entry.stamp = (int)box.currentStamp;
            entry.hasBeenOpened = box.hasBeenOpened;
            entry.isItemExtracted = box.isItemExtracted;
            entry.discrepancyType = (int)box.discrepancyType;

            // กล่อง Mimic ที่ถูกเพิ่มคอมโพเนนต์ตอน runtime ให้บันทึกเป็น prefab Mimic_Package
            // เพื่อให้โหลดกลับมาแล้วยังแปลงร่างได้พร้อมค่า enemyMimicPrefab ครบ
            MimicPackage mimic = box.GetComponent<MimicPackage>();
            entry.isMimic = mimic != null;
            if (mimic != null)
            {
                entry.prefabName = "Mimic_Package";
            }

            if (box.innerItemPrefab != null)
            {
                entry.innerItemName = box.innerItemPrefab.name;
            }

            if (box.officialDatabaseData != null)
            {
                entry.officialManifest = box.officialDatabaseData.Clone();
            }

            if (box.physicalLabelData != null)
            {
                entry.physicalManifest = box.physicalLabelData.Clone();
            }
        }

        return entry;
    }

    // ---------------------------------------------------------------- Apply

    /// <summary>นำข้อมูลเซฟมาใช้กับฉากที่เพิ่งโหลด (เรียกจาก GameplaySaveLoader)</summary>
    public static void ApplyLoadedGame(SaveGameData data)
    {
        if (data == null) return;

        GameDayManager dayManager = GameDayManager.Instance != null
            ? GameDayManager.Instance
            : UnityEngine.Object.FindAnyObjectByType<GameDayManager>();

        if (dayManager != null)
        {
            dayManager.RestoreDayState(data.day, data.remainingOpenQuota, data.remainingSpawnQuota);
        }

        ScoreManager scoreManager = ScoreManager.Instance != null
            ? ScoreManager.Instance
            : UnityEngine.Object.FindAnyObjectByType<ScoreManager>();

        if (scoreManager != null)
        {
            scoreManager.RestoreState(data.totalScore, data.rejectedPackagesCount, data.day);
        }

        RestoreStorage(data);
        RestorePlayer(data);
    }

    private static void RestoreStorage(SaveGameData data)
    {
        StorageRoomManager storage = StorageRoomManager.Instance != null
            ? StorageRoomManager.Instance
            : UnityEngine.Object.FindAnyObjectByType<StorageRoomManager>();

        if (storage == null)
        {
            Debug.LogWarning("[SaveSystem] ไม่พบ StorageRoomManager — ข้ามการคืนของใน Storage");
            return;
        }

        SavePrefabDatabase database = SavePrefabDatabase.Load();
        if (database == null)
        {
            Debug.LogError("[SaveSystem] ไม่พบ SavePrefabDatabase — ไม่สามารถคืนของใน Storage ได้");
            return;
        }

        if (data.items == null) return;

        int restored = 0;
        foreach (SaveItemEntry entry in data.items)
        {
            if (entry == null) continue;

            GameObject prefab = database.Get(entry.prefabName);
            if (prefab == null)
            {
                Debug.LogWarning($"[SaveSystem] ไม่พบ prefab '{entry.prefabName}' ใน SavePrefabDatabase — ข้ามชิ้นนี้");
                continue;
            }

            int slotIndex = Mathf.Clamp(entry.slotIndex, 0, Mathf.Max(0, storage.totalSlots - 1));
            int size = Mathf.Max(1, entry.sizeInSlots);

            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            instance.name = entry.prefabName;

            Rigidbody rb = instance.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            Collider col = instance.GetComponent<Collider>();
            if (col != null) col.enabled = true;

            RestorePackageBoxState(instance, entry, database);

            storage.PlaceItemToSlots(instance, slotIndex, size);
            restored++;
        }

        Debug.Log($"[SaveSystem] คืนของเข้า Storage สำเร็จ {restored}/{data.items.Length} ชิ้น");
    }

    private static void RestorePackageBoxState(GameObject instance, SaveItemEntry entry, SavePrefabDatabase database)
    {
        PackageBox box = instance.GetComponent<PackageBox>();
        if (box == null) return;

        // คืน prefab สินค้าข้างใน (ก่อนเขียน manifest ทับ)
        if (!string.IsNullOrEmpty(entry.innerItemName))
        {
            GameObject innerPrefab = database.Get(entry.innerItemName);
            if (innerPrefab != null)
            {
                box.innerItemPrefab = innerPrefab;
            }
        }

        box.isOpen = false;
        box.hasBeenOpened = entry.hasBeenOpened;
        box.isItemExtracted = entry.isItemExtracted;
        box.currentStamp = (InspectionStatus)entry.stamp;
        box.discrepancyType = (DiscrepancyType)entry.discrepancyType;

        if (entry.officialManifest != null)
        {
            box.officialDatabaseData = entry.officialManifest;
        }

        if (entry.physicalManifest != null)
        {
            box.physicalLabelData = entry.physicalManifest;
        }

        if (entry.isMimic && box.GetComponent<MimicPackage>() == null)
        {
            MimicPackage mimic = box.gameObject.AddComponent<MimicPackage>();
            if (EnemySpawner.Instance != null)
            {
                mimic.enemyMimicPrefab = EnemySpawner.Instance.enemyMimicPrefab;
            }
        }

        box.SetOpenAnimation(false);

        PackageLabelView label = box.GetComponent<PackageLabelView>();
        if (label != null)
        {
            if (box.physicalLabelData != null) label.SetLabelData(box.physicalLabelData);
            label.UpdateStampVisual(box.currentStamp);
        }
    }

    private static void RestorePlayer(SaveGameData data)
    {
        PlayerController player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        if (player == null || data.player == null) return;

        Vector3 position = new Vector3(data.player.x, data.player.y, data.player.z);

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = position;
        }

        player.transform.position = position;
        player.rotationY = data.player.yaw;
        player.transform.rotation = Quaternion.Euler(0f, data.player.yaw, 0f);

        if (player.playerCamera != null)
        {
            player.playerCamera.localRotation = Quaternion.Euler(data.player.pitch, 0f, 0f);
        }
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        return angle;
    }
}

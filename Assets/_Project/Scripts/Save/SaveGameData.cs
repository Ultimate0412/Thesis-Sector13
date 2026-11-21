using System;
using UnityEngine;

/// <summary>ตำแหน่งและทิศทางของผู้เล่น ณ ตอนเซฟ</summary>
[Serializable]
public class SavePlayerState
{
    public float x;
    public float y;
    public float z;
    public float yaw;
    public float pitch;
}

/// <summary>
/// ข้อมูลของวัตถุหนึ่งชิ้นที่อยู่ใน Storage
/// ใช้ชื่อ prefab เป็นตัวระบุ แล้วหา prefab จริงจาก SavePrefabDatabase ตอนโหลด
/// </summary>
[Serializable]
public class SaveItemEntry
{
    public string prefabName;
    public int slotIndex;
    public int sizeInSlots = 1;

    // สถานะของ PackageBox (ถ้าเป็นกล่อง)
    public bool isPackageBox;
    public string innerItemName;
    public int stamp;
    public bool hasBeenOpened;
    public bool isItemExtracted;
    public bool isMimic;
    public int discrepancyType;

    // Manifest ที่สุ่มไว้ตอนสร้างกล่อง (ต้องคงเดิมหลังโหลด)
    public PackageManifestData officialManifest;
    public PackageManifestData physicalManifest;
}

/// <summary>ข้อมูลเซฟทั้งช่อง</summary>
[Serializable]
public class SaveGameData
{
    public int version = 1;
    public string savedAtUtc;
    public int slotIndex;

    public int day = 1;
    public int totalScore;
    public int rejectedPackagesCount;
    public int remainingOpenQuota;
    public int remainingSpawnQuota;

    public SavePlayerState player = new SavePlayerState();
    public SaveItemEntry[] items = new SaveItemEntry[0];
}

/// <summary>ข้อมูลสรุปสำหรับแสดงในการ์ดเลือกช่องเซฟ</summary>
[Serializable]
public class SaveSlotInfo
{
    public int slotIndex;
    public bool exists;
    public bool corrupted;
    public int day;
    public int totalScore;
    public string savedAtUtc;
}

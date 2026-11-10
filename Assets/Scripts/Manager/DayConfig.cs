using System;
using UnityEngine;

[Serializable]
public class DayEventSettings
{
    [Tooltip("เปิดให้สุ่มเกิดอีเวนต์อัตโนมัติในวันนี้")]
    public bool enableRandomEvents = false;

    [Tooltip("ระยะเวลารอขั้นต่ำก่อนสุ่มอีเวนต์ถัดไป (วินาที)")]
    [Min(0f)] public float minInterval = 60f;

    [Tooltip("ระยะเวลารอสูงสุดก่อนสุ่มอีเวนต์ถัดไป (วินาที)")]
    [Min(0f)] public float maxInterval = 120f;

    [Tooltip("eventId ที่อนุญาตให้เกิดในวันนี้ — หากเว้นว่างจะไม่เกิดอีเวนต์เลย")]
    public string[] allowedEventIds;
}

[Serializable]
public class DayThiefSettings
{
    [Tooltip("เปิดให้ Thief เกิดในวันนี้")]
    public bool enableThief = true;

    [Tooltip("จำนวนกล่องประทับตราขั้นต่ำที่เริ่มสุ่มโอกาสเกิด")]
    [Min(0)] public int minStampsThreshold = 3;

    [Tooltip("จำนวนกล่องประทับตราที่การันตีเกิดแน่นอน 100%")]
    [Min(0)] public int guaranteedStampsThreshold = 5;

    [Tooltip("โอกาสเกิดเมื่อประทับตราถึงเกณฑ์ขั้นต่ำ")]
    [Range(0f, 1f)] public float spawnChancePerStamp = 0.45f;

    [Tooltip("จำนวน Thief สูงสุดที่เกิดได้ต่อวัน")]
    [Min(0)] public int maxThievesPerDay = 1;

    [Tooltip("ระยะเวลาหน่วงก่อนตัว Thief จะเกิดจริง (วินาที)")]
    [Min(0f)] public float warningDelay = 4f;
}

/// <summary>
/// การตั้งค่าของแต่ละวัน (1 asset = 1 วัน)
/// ใช้กับ GameDayManager โดยเรียงตาม index: dayConfigs[0] = Day 1, dayConfigs[1] = Day 2, ...
/// </summary>
[CreateAssetMenu(fileName = "Day1Config", menuName = "Sector13/Day Config", order = 0)]
public class DayConfig : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("ชื่อวันที่ใช้แสดงใน Log/Inspector เช่น 'Day 1'")]
    public string dayName = "Day 1";

    [Header("Quotas")]
    [Tooltip("จำนวนครั้งที่ผู้เล่นเปิดตรวจกล่องได้ในวันนี้ (0 = เปิดไม่ได้เลย)")]
    [Min(0)] public int openQuota = 15;

    [Tooltip("จำนวนกล่องพัสดุที่สปอนได้ในวันนี้ (0 = สปอนไม่ได้เลย)")]
    [Min(0)] public int spawnQuota = 10;

    [Header("Category Spawn Weights")]
    [Tooltip("น้ำหนักโอกาสเกิดของแต่ละหมวดหมู่สินค้าในวันนี้")]
    public DailyCategorySpawnRate categoryRate = new DailyCategorySpawnRate();

    [Header("Package Prefabs")]
    [Tooltip("Prefab พัสดุเฉพาะของวันนี้ หากเว้นว่างจะใช้ packagePrefabs ของ ItemSpawner")]
    public GameObject[] packagePrefabs;

    [Header("Game Events (รายวัน)")]
    [Tooltip("การเกิดอีเวนต์ของวันนี้")]
    public DayEventSettings events = new DayEventSettings();

    [Header("Thief Spawning (รายวัน)")]
    [Tooltip("การเกิด Thief ของวันนี้")]
    public DayThiefSettings thief = new DayThiefSettings();

    /// <summary>มีรายการ Prefab เฉพาะวันนี้หรือไม่</summary>
    public bool HasPackagePrefabs => packagePrefabs != null && packagePrefabs.Length > 0;

    private void OnValidate()
    {
        openQuota = Mathf.Max(0, openQuota);
        spawnQuota = Mathf.Max(0, spawnQuota);

        if (events != null)
        {
            events.minInterval = Mathf.Max(0f, events.minInterval);
            events.maxInterval = Mathf.Max(events.minInterval, events.maxInterval);
        }

        if (thief != null)
        {
            thief.minStampsThreshold = Mathf.Max(0, thief.minStampsThreshold);
            thief.guaranteedStampsThreshold = Mathf.Max(thief.minStampsThreshold, thief.guaranteedStampsThreshold);
            thief.spawnChancePerStamp = Mathf.Clamp01(thief.spawnChancePerStamp);
            thief.maxThievesPerDay = Mathf.Max(0, thief.maxThievesPerDay);
            thief.warningDelay = Mathf.Max(0f, thief.warningDelay);
        }
    }
}

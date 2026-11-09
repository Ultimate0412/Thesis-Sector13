using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// รูปแบบเงื่อนไขเวลาในการเริ่มอีเวนต์ประจำวัน
/// </summary>
public enum EventTriggerTiming
{
    AtDayStart,            // เกิดทันทีเมื่อเริ่มวันใหม่
    AfterDelay,            // เกิดหลังจากเริ่มวันไปแล้ว X วินาที
    AfterBoxesStamped,     // เกิดหลังจากประทับตรากล่องครบ X กล่อง
    ManualOnly             // เกิดเมื่อถูกสั่งการด้วยโค้ดหรือปุ่ม Debug เท่านั้น
}

/// <summary>
/// ข้อมูลการตั้งค่าอีเวนต์ที่จะเกิดขึ้นในวันนั้น
/// </summary>
[System.Serializable]
public class ScheduledDailyEvent
{
    [Tooltip("รหัส Event ID เช่น 'NoGravity'")]
    public string eventId = "NoGravity";

    [Tooltip("เงื่อนไขเวลาในการเริ่มอีเวนต์")]
    public EventTriggerTiming timing = EventTriggerTiming.AfterDelay;

    [Tooltip("เวลารอหลังจากเริ่มวัน (วินาที) สำหรับโหมด AfterDelay")]
    public float delaySeconds = 60f;

    [Tooltip("จำนวนกล่องที่ประทับตราแล้วสำหรับโหมด AfterBoxesStamped")]
    public int triggerAfterStampedBoxes = 3;

    [Range(0f, 1f)]
    [Tooltip("โอกาสเกิดอีเวนต์นี้ (1 = 100%)")]
    public float triggerProbability = 1f;

    [HideInInspector] public bool hasTriggered = false;
}

/// <summary>
/// ScriptableObject สำหรับกำหนดค่าและเหตุการณ์ทั้งหมดที่จะเกิดขึ้นใน 1 วัน
/// ครอบคลุมทั้งโควต้า, พัสดุ, เอเลี่ยน/มิมิก, หัวขโมย, และอีเวนต์ประจำวัน
/// </summary>
[CreateAssetMenu(fileName = "Day_01", menuName = "Thesis/Day Configuration")]
public class DayConfigurationSO : ScriptableObject
{
    [Header("1. Day Information")]
    [Tooltip("ลำดับวันที่")]
    public int dayNumber = 1;

    [Tooltip("ชื่อหัวข้อประจำวัน เช่น 'Day 1: Rookie Inspection'")]
    public string dayTitle = "Day 1";

    [TextArea(2, 4)]
    [Tooltip("คำอธิบายหรือเนื้อเรื่องสรุปประจำวัน")]
    public string dayDescription = "Regular freight inspection shift at Sector 13.";

    [Header("2. Daily Quotas")]
    [Tooltip("โควต้าการเปิดกล่องตรวจในวันนี้")]
    public int openQuota = 15;

    [Tooltip("โควต้าการปล่อยกล่องพัสดุทั้งหมดในวันนี้")]
    public int spawnQuota = 10;

    [Header("3. Item & Cargo Distribution")]
    [Tooltip("สัดส่วนโอกาสเกิดสินค้าแต่ละหมวดหมู่")]
    public DailyCategorySpawnRate categoryRates = new DailyCategorySpawnRate { legalWeight = 70f, illegalWeight = 20f, alienWeight = 10f };

    [Tooltip("รายการ Prefab กล่องเฉพาะของวันนี้ (หากเว้นว่างจะใช้พูลตั้งต้นของ Spawner)")]
    public GameObject[] packagePrefabs;

    [Range(0f, 100f)]
    [Tooltip("โอกาสที่กล่อง Alien จะกลายเป็น Mimic ปลอมตัวมา (0-100%)")]
    public float alienMimicChance = 0f;

    [Header("4. Thief Intruder Settings")]
    [Tooltip("เปิดให้หัวขโมยเกิดในวันนี้หรือไม่")]
    public bool enableThief = false;

    [Tooltip("จำนวนกล่องประทับตราขั้นต่ำที่เริ่มสุ่มเกิด Thief")]
    public int minStampsForThief = 3;

    [Tooltip("จำนวนกล่องประทับตราที่การันตีเกิด Thief 100%")]
    public int guaranteedStampsForThief = 5;

    [Range(0f, 1f)]
    [Tooltip("โอกาสเกิดต่อการประทับตราแต่ละกล่อง")]
    public float thiefSpawnChancePerStamp = 0.45f;

    [Tooltip("จำนวน Thief สูงสุดในวันนี้")]
    public int maxThievesToday = 1;

    [Header("5. Scheduled Daily Events")]
    [Tooltip("รายการอีเวนต์พิเศษที่จะเกิดขึ้นในวันนี้")]
    public List<ScheduledDailyEvent> scheduledEvents = new List<ScheduledDailyEvent>();
}

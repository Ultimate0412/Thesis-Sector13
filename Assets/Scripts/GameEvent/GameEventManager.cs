using System;
using System.Collections.Generic;
using UnityEngine;


public class GameEventManager : MonoBehaviour
{
    public static GameEventManager Instance { get; private set; }

    [Header("Registered Events")]
    [Tooltip("รายการอีเวนต์ทั้งหมดที่พร้อมใช้งานในระบบ")]
    public List<GameEventBase> availableEvents = new List<GameEventBase>();

    [Header("Debug Controls")]
    [Tooltip("ปุ่มสำหรับเปิด/ปิดสลับสถานะ Zero Gravity Event ทันทีเพื่อทดสอบ (Default = G)")]
    public KeyCode debugToggleZeroGKey = KeyCode.G;

    [Tooltip("เปิดใช้งานปุ่มคีย์ลัด Debug หรือไม่")]
    public bool enableDebugHotkeys = true;

    [Header("Random Progression (ตัวเลือกเสริมสำหรับเกมปกติ)")]
    [Tooltip("เปิดให้สุ่มเกิดอีเวนต์ระหว่างวันโดยอัตโนมัติหรือไม่")]
    public bool enableRandomDailyEvents = false;

    [Tooltip("ระยะเวลารอขั้นต่ำก่อนสุ่มเกิดอีเวนต์ถัดไป (วินาที)")]
    public float minEventInterval = 60f;

    [Tooltip("ระยะเวลารอสูงสุดก่อนสุ่มเกิดอีเวนต์ถัดไป (วินาที)")]
    public float maxEventInterval = 120f;

    // รายการอีเวนต์ที่กำลังทำงานอยู่ในปัจจุบัน
    private List<GameEventBase> activeEvents = new List<GameEventBase>();
    public IReadOnlyList<GameEventBase> ActiveEvents => activeEvents;

    // pool ของอีเวนต์ที่อนุญาตให้เกิดในวันนี้ (จาก DayConfig)
    private readonly List<GameEventBase> dailyEventPool = new List<GameEventBase>();
    public IReadOnlyList<GameEventBase> DailyEventPool => dailyEventPool;

    // true = มีการ apply DayConfig แล้ว (แม้ allowedEventIds จะว่าง ก็ถือว่า "ห้ามเกิด")
    private bool hasDayEventConfig = false;

    // Callbacks แจ้งเตือนภายนอก
    public event Action<GameEventBase> OnEventTriggered;
    public event Action<GameEventBase> OnEventEnded;

    private float nextRandomEventTime = 0f;

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

        // ค้นหาอีเวนต์ทั้งหมดที่แนบอยู่กับ GameObject นี้หรือวัตถุลูก
        DiscoverAndRegisterEvents();
    }

    private void Start()
    {
        // เชื่อมโยงกับ GameDayManager เพื่อล้างอีเวนต์เมื่อจบวัน
        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.OnDayEnded += HandleDayEnded;
            GameDayManager.Instance.OnDayStarted += HandleDayStarted;
            ApplyDayConfig(GameDayManager.Instance.CurrentDayConfig);
        }

        ScheduleNextRandomEvent();
    }

    private void OnDestroy()
    {
        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.OnDayEnded -= HandleDayEnded;
            GameDayManager.Instance.OnDayStarted -= HandleDayStarted;
        }
    }

    private void Update()
    {
        // 1. ตรวจสอบปุ่มคีย์ลัด Debug
        if (enableDebugHotkeys && debugToggleZeroGKey != KeyCode.None && Input.GetKeyDown(debugToggleZeroGKey))
        {
            ToggleZeroGravityEvent();
        }

        // 2. ตรวจสอบการสุ่มเกิดอีเวนต์อัตโนมัติ
        int candidateCount = hasDayEventConfig ? dailyEventPool.Count : availableEvents.Count;
        if (enableRandomDailyEvents && candidateCount > 0 && Time.time >= nextRandomEventTime && activeEvents.Count == 0)
        {
            TriggerRandomEvent();
            ScheduleNextRandomEvent();
        }

        // 3. อัปเดตเวลานับถอยหลังของอีเวนต์ที่กำลังทำงาน
        for (int i = activeEvents.Count - 1; i >= 0; i--)
        {
            if (activeEvents[i] != null && activeEvents[i].IsActive)
            {
                activeEvents[i].UpdateEvent();
            }
        }
    }

    private void FixedUpdate()
    {
        // อัปเดตการคำนวณฟิสิกส์ของอีเวนต์
        for (int i = 0; i < activeEvents.Count; i++)
        {
            if (activeEvents[i] != null && activeEvents[i].IsActive)
            {
                activeEvents[i].FixedUpdateEvent();
            }
        }
    }

    /// <summary>
    /// ค้นหาและลงทะเบียนอีเวนต์ทั้งหมดในฉาก
    /// </summary>
    public void DiscoverAndRegisterEvents()
    {
        GameEventBase[] foundEvents = GetComponentsInChildren<GameEventBase>(true);
        foreach (var ev in foundEvents)
        {
            if (!availableEvents.Contains(ev))
            {
                availableEvents.Add(ev);
            }
        }

        // ตรวจสอบว่ามี NoGravityEvent แล้วหรือยัง ถ้ายังไม่มีให้สร้างให้อัตโนมัติ
        if (GetEvent("NoGravity") == null)
        {
            GameObject zeroGObj = new GameObject("NoGravityEvent");
            zeroGObj.transform.SetParent(transform);
            NoGravityEvent noGrav = zeroGObj.AddComponent<NoGravityEvent>();
            availableEvents.Add(noGrav);
        }
    }

    /// <summary>
    /// สั่งเริ่มอีเวนต์ตามรหัส Event ID
    /// </summary>
    public bool TriggerEvent(string eventId)
    {
        GameEventBase targetEvent = GetEvent(eventId);
        if (targetEvent == null)
        {
            Debug.LogWarning($"[GameEventManager] ไม่พบอีเวนต์ที่มีรหัส: '{eventId}'");
            return false;
        }

        return TriggerEvent(targetEvent);
    }

    /// <summary>
    /// สั่งเริ่มอีเวนต์ตาม Reference
    /// </summary>
    public bool TriggerEvent(GameEventBase ev)
    {
        if (ev == null) return false;

        if (!ev.CanTrigger())
        {
            Debug.LogWarning($"[GameEventManager] อีเวนต์ '{ev.eventName}' ไม่พร้อมทำงานในขณะนี้");
            return false;
        }

        if (!activeEvents.Contains(ev))
        {
            activeEvents.Add(ev);
        }

        // ผูก Callback เพื่อนำออกจากรายการ activeEvents เมื่อสิ้นสุด
        ev.OnEventEnded -= HandleEventEndedCallback;
        ev.OnEventEnded += HandleEventEndedCallback;

        ev.StartEvent();
        OnEventTriggered?.Invoke(ev);

        // แจ้งเตือนไปยัง UI Controller
        if (EventUIController.Instance != null)
        {
            EventUIController.Instance.ShowEventBanner(ev);
        }

        return true;
    }

    /// <summary>
    /// สั่งหยุดอีเวนต์ตามรหัส Event ID
    /// </summary>
    public void StopEvent(string eventId)
    {
        GameEventBase targetEvent = GetEvent(eventId);
        if (targetEvent != null && targetEvent.IsActive)
        {
            targetEvent.StopEvent();
        }
    }

    /// <summary>
    /// สั่งหยุดอีเวนต์ทั้งหมดที่กำลังทำงานอยู่ทันที
    /// </summary>
    public void StopAllEvents()
    {
        for (int i = activeEvents.Count - 1; i >= 0; i--)
        {
            if (activeEvents[i] != null && activeEvents[i].IsActive)
            {
                activeEvents[i].StopEvent();
            }
        }
        activeEvents.Clear();
    }

    /// <summary>
    /// สลับเปิด/ปิด No Gravity Event สำหรับการทดสอบ
    /// </summary>
    [ContextMenu("Toggle Zero Gravity Event")]
    public void ToggleZeroGravityEvent()
    {
        GameEventBase zeroG = GetEvent("NoGravity");
        if (zeroG != null)
        {
            if (zeroG.IsActive)
            {
                zeroG.StopEvent();
            }
            else
            {
                TriggerEvent(zeroG);
            }
        }
    }

    /// <summary>
    /// สุ่มทริกเกอร์ 1 อีเวนต์จากรายการที่มี
    /// </summary>
    public void TriggerRandomEvent()
    {
        // ถ้ามี DayConfig ให้สุ่มจาก pool ของวันนั้นเท่านั้น (pool ว่าง = ไม่เกิดอีเวนต์)
        List<GameEventBase> source = hasDayEventConfig ? dailyEventPool : availableEvents;
        List<GameEventBase> readyEvents = source.FindAll(e => e != null && e.CanTrigger());
        if (readyEvents.Count > 0)
        {
            int randIndex = UnityEngine.Random.Range(0, readyEvents.Count);
            TriggerEvent(readyEvents[randIndex]);
        }
    }

    public GameEventBase GetEvent(string eventId)
    {
        return availableEvents.Find(e => e != null && string.Equals(e.eventId, eventId, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsEventActive(string eventId)
    {
        GameEventBase ev = GetEvent(eventId);
        return ev != null && ev.IsActive;
    }

    private void HandleEventEndedCallback(GameEventBase ev)
    {
        ev.OnEventEnded -= HandleEventEndedCallback;
        activeEvents.Remove(ev);
        OnEventEnded?.Invoke(ev);

        if (EventUIController.Instance != null)
        {
            EventUIController.Instance.HideEventBanner(ev);
        }
    }

    private void HandleDayEnded(int day)
    {
        StopAllEvents();
    }

    private void HandleDayStarted(int newDay)
    {
        if (GameDayManager.Instance != null)
        {
            ApplyDayConfig(GameDayManager.Instance.CurrentDayConfig);
        }
        ScheduleNextRandomEvent();
    }

    /// <summary>
    /// นำค่าการเกิดอีเวนต์ของวันนั้นจาก DayConfig มาใช้
    /// เรียกซ้ำได้ทุกวัน (Start / OnDayStarted)
    /// </summary>
    public void ApplyDayConfig(DayConfig config)
    {
        // มีระบบวันแต่ไม่มี config ของวันนี้ → ปิดการเกิด
        if (config == null || config.events == null)
        {
            hasDayEventConfig = true;
            enableRandomDailyEvents = false;
            dailyEventPool.Clear();
            return;
        }

        hasDayEventConfig = true;

        DayEventSettings settings = config.events;
        enableRandomDailyEvents = settings.enableRandomEvents;
        minEventInterval = Mathf.Max(0f, settings.minInterval);
        maxEventInterval = Mathf.Max(minEventInterval, settings.maxInterval);

        dailyEventPool.Clear();
        if (settings.allowedEventIds != null)
        {
            foreach (string rawId in settings.allowedEventIds)
            {
                if (string.IsNullOrWhiteSpace(rawId)) continue;

                string eventId = rawId.Trim();
                GameEventBase ev = GetEvent(eventId);
                if (ev != null)
                {
                    dailyEventPool.Add(ev);
                }
                else
                {
                    Debug.LogWarning($"[GameEventManager] DayConfig อ้าง eventId ที่ไม่มีใน Scene: '{eventId}'");
                }
            }
        }
    }

    private void ScheduleNextRandomEvent()
    {
        nextRandomEventTime = Time.time + UnityEngine.Random.Range(minEventInterval, maxEventInterval);
    }
}

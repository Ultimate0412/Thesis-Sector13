using System;
using UnityEngine;

/// <summary>
/// คลาสฐาน (Abstract Base Class) สำหรับทุก Event ในเกม
/// สามารถสืบทอดเพื่อสร้าง Event ใหม่ๆ ได้อย่างยืดหยุ่น เช่น No Gravity, ไฟดับ (Power Outage), หรือการรั่วไหลของสารพิษ
/// </summary>
public abstract class GameEventBase : MonoBehaviour
{
    [Header("Event Identification")]
    [Tooltip("รหัสระบุเฉพาะของอีเวนต์ (ใช้เรียกผ่านโค้ด)")]
    public string eventId = "BaseEvent";

    [Tooltip("ชื่อของอีเวนต์ที่จะแสดงผลบนหน้าจอ UI")]
    public string eventName = "Anomaly Detected";

    [TextArea(2, 4)]
    [Tooltip("คำอธิบายรายละเอียดของอีเวนต์")]
    public string eventDescription = "An environmental anomaly has occurred in the sector.";

    [Tooltip("สีธีมประจำอีเวนต์สำหรับแสดงผลบนแถบ UI")]
    public Color eventColor = new Color(1f, 0.87058824f, 0.34901962f, 1.0f); // #ffde59 // Default: Sci-Fi Cyan

    [Header("Timing Settings")]
    [Tooltip("ระยะเวลาของอีเวนต์ (วินาที) - หากกำหนดเป็น 0 หรือน้อยกว่า จะเป็นอีเวนต์ถาวรจนกว่าจะสั่งหยุดเอง")]
    public float duration = 25f;

    [Header("Audio Settings")]
    [Tooltip("เสียงประกาศเตือนเมื่อเริ่มอีเวนต์")]
    public AudioClip startAudioClip;

    [Tooltip("เสียงบรรยากาศ (Ambient Sound) เล่นวนซ้ำตลอดช่วงที่เกิดอีเวนต์")]
    public AudioClip ambientAudioClip;

    [Tooltip("เสียงแจ้งเตือนเมื่ออีเวนต์สิ้นสุด")]
    public AudioClip endAudioClip;

    // สถานะของอีเวนต์
    public bool IsActive { get; protected set; } = false;
    public float RemainingTime { get; protected set; } = 0f;
    public float ElapsedTime { get; protected set; } = 0f;

    // Callbacks สำหรับระบบภายนอก (เช่น UI หรือ Manager)
    public event Action<GameEventBase> OnEventStarted;
    public event Action<GameEventBase> OnEventEnded;

    protected AudioSource ambientAudioSource;

    protected virtual void Awake()
    {
        if (ambientAudioClip != null)
        {
            ambientAudioSource = gameObject.AddComponent<AudioSource>();
            ambientAudioSource.clip = ambientAudioClip;
            ambientAudioSource.loop = true;
            ambientAudioSource.playOnAwake = false;
            ambientAudioSource.spatialBlend = 0f; // 2D Sound
            ambientAudioSource.volume = 0.5f;
        }
    }

    /// <summary>
    /// ตรวจสอบว่าอีเวนต์นี้พร้อมทริกเกอร์หรือไม่
    /// </summary>
    public virtual bool CanTrigger()
    {
        return !IsActive;
    }

    /// <summary>
    /// สั่งเริ่มอีเวนต์
    /// </summary>
    public virtual void StartEvent()
    {
        if (IsActive) return;

        IsActive = true;
        RemainingTime = duration;
        ElapsedTime = 0f;

        if (ambientAudioSource != null && ambientAudioClip != null)
        {
            ambientAudioSource.Play();
        }

        if (startAudioClip != null)
        {
            AudioSource.PlayClipAtPoint(startAudioClip, Camera.main != null ? Camera.main.transform.position : transform.position, 1.0f);
        }

        Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(eventColor)}>[GameEvent]</color> <b>{eventName}</b> ({eventId}) เริ่มต้นขึ้นแล้ว! (ระยะเวลา: {duration}s)");

        OnEventStart();
        OnEventStarted?.Invoke(this);
    }

    /// <summary>
    /// อัปเดตเวลานับถอยหลัง (เรียกโดย GameEventManager ใน Update)
    /// </summary>
    public virtual void UpdateEvent()
    {
        if (!IsActive) return;

        ElapsedTime += Time.deltaTime;

        if (duration > 0f)
        {
            RemainingTime = Mathf.Max(0f, RemainingTime - Time.deltaTime);

            if (RemainingTime <= 0f)
            {
                StopEvent();
                return;
            }
        }

        OnEventUpdate();
    }

    /// <summary>
    /// อัปเดตฟิสิกส์ (เรียกโดย GameEventManager ใน FixedUpdate)
    /// </summary>
    public virtual void FixedUpdateEvent()
    {
        if (!IsActive) return;
        OnEventFixedUpdate();
    }

    /// <summary>
    /// สั่งหยุดอีเวนต์
    /// </summary>
    public virtual void StopEvent()
    {
        if (!IsActive) return;

        IsActive = false;
        RemainingTime = 0f;

        if (ambientAudioSource != null && ambientAudioSource.isPlaying)
        {
            ambientAudioSource.Stop();
        }

        if (endAudioClip != null)
        {
            AudioSource.PlayClipAtPoint(endAudioClip, Camera.main != null ? Camera.main.transform.position : transform.position, 1.0f);
        }

        Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(eventColor)}>[GameEvent]</color> <b>{eventName}</b> ({eventId}) สิ้นสุดลงแล้ว");

        OnEventEnd();
        OnEventEnded?.Invoke(this);
    }

    // --- Hooks สำหรับคลาสลูกนำไป Override ---
    protected abstract void OnEventStart();
    protected virtual void OnEventUpdate() { }
    protected virtual void OnEventFixedUpdate() { }
    protected abstract void OnEventEnd();
}

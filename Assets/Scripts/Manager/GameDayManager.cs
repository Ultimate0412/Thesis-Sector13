using UnityEngine;
using System;

public class GameDayManager : MonoBehaviour
{
    public static GameDayManager Instance;

    [Header("Day & Quota Settings")]
    public int currentDay = 1;
    public int dailyOpenQuota = 15; // โควต้าเปิดกล่องรวมทั้งหมดใน 1 วัน (เช่น เปิดตรวจได้รวมกัน 15 ครั้งต่อวัน)
    private int remainingQuotaToday;

    public event Action<int> OnQuotaChanged;
    public event Action<int> OnDayChanged;


    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        ResetDailyQuota();
    }

    // รีเซ็ตโควต้าเมื่อขึ้นวันใหม่
    public void ResetDailyQuota()
    {
        remainingQuotaToday = dailyOpenQuota;
        OnQuotaChanged?.Invoke(remainingQuotaToday);
        Debug.Log($"--- เริ่มวันใหม่ (Day {currentDay}) | โควต้าเปิดกล่องรีเซ็ตเป็น {remainingQuotaToday} ครั้ง ---");
    }

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

    // ฟังก์ชันสำหรับจบวันและขึ้นวันถัดไป
    public void EndDay()
    {
        currentDay++;
        OnDayChanged?.Invoke(currentDay);
        ResetDailyQuota();
    }
}
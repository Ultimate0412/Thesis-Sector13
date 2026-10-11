using UnityEngine;
using System;

[System.Serializable]
public class DailyStatisticsData
{
    public int dayNumber = 1;
    public int dailyScoreEarned = 0;
    public int totalScoreSnapshot = 0;
    public int dailyRejectedCount = 0;
    public int dailyApprovedCount = 0;
    public int legalShippedCount = 0;
    public int illegalShippedCount = 0;
    public int alienShippedCount = 0;

    public int TotalShippedToday => legalShippedCount + illegalShippedCount + alienShippedCount;

    public void Reset(int day, int currentTotalScore)
    {
        dayNumber = day;
        dailyScoreEarned = 0;
        totalScoreSnapshot = currentTotalScore;
        dailyRejectedCount = 0;
        dailyApprovedCount = 0;
        legalShippedCount = 0;
        illegalShippedCount = 0;
        alienShippedCount = 0;
    }
}

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    public int totalScore = 0;
    [Header("Shipping Stats")]
    public int rejectedPackagesCount = 0;

    [Header("Daily Statistics")]
    [SerializeField]
    private DailyStatisticsData currentDayStats = new DailyStatisticsData();
    public DailyStatisticsData CurrentDayStats => currentDayStats;

    public event Action<int> OnScoreChanged;
    public event Action<int> rejectedPackagesCountChanged;
    public event Action<DailyStatisticsData> OnDailyStatsChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            if (currentDayStats == null)
            {
                currentDayStats = new DailyStatisticsData();
            }
            currentDayStats.Reset(1, totalScore);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void AddScore(int amount)
    {
        totalScore += amount;
        if (currentDayStats != null)
        {
            currentDayStats.dailyScoreEarned += amount;
            currentDayStats.totalScoreSnapshot = totalScore;
        }
        OnScoreChanged?.Invoke(totalScore);
        OnDailyStatsChanged?.Invoke(currentDayStats);
        Debug.Log($"Total Score: {totalScore} (Change: {amount})");
    }

    public void AddRejectedCount()
    {
        rejectedPackagesCount++;
        if (currentDayStats != null)
        {
            currentDayStats.dailyRejectedCount++;
        }
        rejectedPackagesCountChanged?.Invoke(rejectedPackagesCount);
        OnDailyStatsChanged?.Invoke(currentDayStats);
        Debug.Log($"จำนวนพัสดุตีกลับสะสม: {rejectedPackagesCount}");
    }

    /// <summary>
    /// บันทึกสถิติเมื่อมีการส่งสินค้าออกจาก ShippingDropPoint
    /// </summary>
    public void RecordShippedPackage(ItemCategory category, InspectionStatus stamp, int scoreChange)
    {
        // 1. ปรับปรุงคะแนน
        totalScore += scoreChange;
        if (currentDayStats != null)
        {
            currentDayStats.dailyScoreEarned += scoreChange;
            currentDayStats.totalScoreSnapshot = totalScore;

            // 2. ปรับปรุงตราประทับ
            if (stamp == InspectionStatus.Rejected)
            {
                rejectedPackagesCount++;
                currentDayStats.dailyRejectedCount++;
                rejectedPackagesCountChanged?.Invoke(rejectedPackagesCount);
            }
            else if (stamp == InspectionStatus.Approved)
            {
                currentDayStats.dailyApprovedCount++;
            }

            // 3. ปรับปรุงจำนวนแยกตามประเภท
            switch (category)
            {
                case ItemCategory.Legal:
                    currentDayStats.legalShippedCount++;
                    break;
                case ItemCategory.Illegal:
                    currentDayStats.illegalShippedCount++;
                    break;
                case ItemCategory.Alien:
                    currentDayStats.alienShippedCount++;
                    break;
            }
        }

        OnScoreChanged?.Invoke(totalScore);
        OnDailyStatsChanged?.Invoke(currentDayStats);
        Debug.Log($"[ScoreManager] บันทึกสินค้า {category} (Stamp: {stamp}) | คะแนนวันนี้: {currentDayStats?.dailyScoreEarned} | คะแนนรวม: {totalScore}");
    }

    /// <summary>
    /// รีเซ็ตสถิติรายวันเมื่อเริ่มวันใหม่
    /// </summary>
    public void StartNewDayStats(int newDay)
    {
        if (currentDayStats == null)
        {
            currentDayStats = new DailyStatisticsData();
        }
        currentDayStats.Reset(newDay, totalScore);
        OnDailyStatsChanged?.Invoke(currentDayStats);
        Debug.Log($"[ScoreManager] รีเซ็ตสถิติรายวันสำหรับ Day {newDay} เรียบร้อย (คะแนนตั้งต้น: {totalScore})");
    }
}
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
    public int dailyDestroyedCount = 0;
    public int legalShippedCount = 0;
    public int illegalShippedCount = 0;
    public int alienShippedCount = 0;
    public int dailyStationFee = 0;
    public int dailyShortfallPenalty = 0;

    public int TotalShippedToday => legalShippedCount + illegalShippedCount + alienShippedCount;

    /// <summary>คะแนนสุทธิของวันนี้ (หักค่าสถานีและโทษส่งไม่ถึงขั้นต่ำแล้ว)</summary>
    public int NetDailyCredits => dailyScoreEarned - dailyStationFee - dailyShortfallPenalty;

    public void Reset(int day, int currentTotalScore)
    {
        dayNumber = day;
        dailyScoreEarned = 0;
        totalScoreSnapshot = currentTotalScore;
        dailyRejectedCount = 0;
        dailyApprovedCount = 0;
        dailyDestroyedCount = 0;
        legalShippedCount = 0;
        illegalShippedCount = 0;
        alienShippedCount = 0;
        dailyStationFee = 0;
        dailyShortfallPenalty = 0;
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

    /// <summary>
    /// หักคะแนนเมื่อพัสดุถูกทำลายหรือถูกขโมย
    /// </summary>
    public void DeductScore(int amount, string reason = "Parcel Destroyed")
    {
        int penalty = Mathf.Abs(amount);
        totalScore = Mathf.Max(0, totalScore - penalty);
        if (currentDayStats != null)
        {
            currentDayStats.dailyScoreEarned -= penalty;
            currentDayStats.dailyDestroyedCount++;
            currentDayStats.totalScoreSnapshot = totalScore;
        }
        OnScoreChanged?.Invoke(totalScore);
        OnDailyStatsChanged?.Invoke(currentDayStats);
        Debug.LogWarning($"<color=#FF3333>[ScoreManager] หักคะแนน -{penalty}!</color> สาเหตุ: {reason} (คะแนนรวมคงเหลือ: {totalScore})");
    }

    /// <summary>
    /// หักคะแนนโทษกรณีส่งพัสดุไม่ถึงขั้นต่ำ (ไม่นับเป็นพัสดุถูกทำลาย)
    /// </summary>
    public void ApplyPenalty(int amount, string reason = "Below shipping quota")
    {
        int penalty = Mathf.Abs(amount);
        if (penalty <= 0) return;

        totalScore = Mathf.Max(0, totalScore - penalty);
        if (currentDayStats != null)
        {
            currentDayStats.dailyScoreEarned -= penalty;
            currentDayStats.dailyShortfallPenalty += penalty;
            currentDayStats.totalScoreSnapshot = totalScore;
        }

        OnScoreChanged?.Invoke(totalScore);
        OnDailyStatsChanged?.Invoke(currentDayStats);
        Debug.LogWarning($"<color=#FF3333>[ScoreManager] หักโทษ -{penalty}!</color> สาเหตุ: {reason} (คะแนนรวมคงเหลือ: {totalScore})");
    }

    /// <summary>
    /// หักค่าสถานีรายวันออกจากคะแนนรวม (บันทึกแยกไว้แสดงในสรุปผล)
    /// </summary>
    public void ApplyStationFee(int fee)
    {
        int cost = Mathf.Max(0, fee);

        if (currentDayStats != null)
        {
            currentDayStats.dailyStationFee = cost;
        }

        if (cost > 0)
        {
            totalScore = Mathf.Max(0, totalScore - cost);
            if (currentDayStats != null)
            {
                currentDayStats.totalScoreSnapshot = totalScore;
            }
            OnScoreChanged?.Invoke(totalScore);
            Debug.Log($"[ScoreManager] หักค่าสถานี -{cost} (คะแนนรวมคงเหลือ: {totalScore})");
        }

        OnDailyStatsChanged?.Invoke(currentDayStats);
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
    /// คืนคะแนน/สถิติจากเซฟ (เรียกหลังโหลดฉาก)
    /// สถิติรายวันของวันที่โหลดจะเริ่มที่ศูนย์ เพราะเซฟถูกเขียนตอนขึ้นวันใหม่
    /// </summary>
    public void RestoreState(int total, int rejected, int day)
    {
        totalScore = Mathf.Max(0, total);
        rejectedPackagesCount = Mathf.Max(0, rejected);

        if (currentDayStats == null)
        {
            currentDayStats = new DailyStatisticsData();
        }
        currentDayStats.Reset(day, totalScore);

        OnScoreChanged?.Invoke(totalScore);
        rejectedPackagesCountChanged?.Invoke(rejectedPackagesCount);
        OnDailyStatsChanged?.Invoke(currentDayStats);

        Debug.Log($"[ScoreManager] คืนสถานะคะแนนรวม {totalScore} | พัสดุตีกลับสะสม {rejectedPackagesCount} | Day {day}");
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
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DailySummaryUI : MonoBehaviour
{
    public static DailySummaryUI Instance { get; private set; }

    [Header("UI Panels")]
    [Tooltip("หน้าต่างป๊อปอัปสรุปผลประจำวัน")]
    public GameObject summaryPanel;

    [Tooltip("ปุ่มกดจบวันบนหน้าจอ HUD")]
    public Button endDayHUDButton;

    [Tooltip("ปุ่มเริ่มวันถัดไปในหน้าต่างสรุปผล")]
    public Button nextDayButton;

    [Header("Summary Text Displays")]
    public TextMeshProUGUI dayTitleText;
    public TextMeshProUGUI dailyScoreText;
    public TextMeshProUGUI totalScoreText;
    public TextMeshProUGUI rejectedCountText;
    public TextMeshProUGUI approvedCountText;
    public TextMeshProUGUI legalCountText;
    public TextMeshProUGUI illegalCountText;
    public TextMeshProUGUI alienCountText;
    public TextMeshProUGUI totalShippedText;

    [Header("Warning Notification Banner")]
    [Tooltip("แผงหรือข้อความเตือนเมื่อจบวันไม่สำเร็จ (เช่น มีของตกค้าง)")]
    public GameObject warningBannerObject;
    public TextMeshProUGUI warningBannerText;
    public float warningDuration = 3.5f;

    private Coroutine warningCoroutine;

    public bool IsSummaryOpen => summaryPanel != null && summaryPanel.activeSelf;

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

        if (summaryPanel != null)
        {
            summaryPanel.SetActive(false);
        }

        if (warningBannerObject != null)
        {
            warningBannerObject.SetActive(false);
        }

        if (endDayHUDButton != null)
        {
            endDayHUDButton.onClick.AddListener(OnEndDayButtonClicked);
        }

        if (nextDayButton != null)
        {
            nextDayButton.onClick.AddListener(OnNextDayClicked);
        }
    }

    private void Start()
    {
        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.OnDaySummaryRequested += ShowSummary;
            GameDayManager.Instance.OnDayEndFailed += ShowWarningBanner;
        }
    }

    private void OnDestroy()
    {
        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.OnDaySummaryRequested -= ShowSummary;
            GameDayManager.Instance.OnDayEndFailed -= ShowWarningBanner;
        }
    }

    /// <summary>
    /// แสดงหน้าต่างสรุปผลประจำวัน พร้อมจัดรูปแบบข้อความสถิติ
    /// </summary>
    public void ShowSummary(int day, DailyStatisticsData stats)
    {
        if (stats == null && ScoreManager.Instance != null)
        {
            stats = ScoreManager.Instance.CurrentDayStats;
        }

        if (dayTitleText != null)
        {
            dayTitleText.text = $"DAILY WORK SUMMARY - DAY {day}";
        }

        if (stats != null)
        {
            if (dailyScoreText != null)
            {
                string sign = stats.dailyScoreEarned >= 0 ? "+" : "";
                string colorHex = stats.dailyScoreEarned >= 0 ? "#4EFA7A" : "#FA4E4E";
                dailyScoreText.text = $"<color={colorHex}>{sign}{stats.dailyScoreEarned}</color>";
            }

            if (totalScoreText != null)
            {
                totalScoreText.text = $"{stats.totalScoreSnapshot:N0} Credits";
            }

            if (rejectedCountText != null)
            {
                rejectedCountText.text = $"{stats.dailyRejectedCount} กล่อง";
            }

            if (approvedCountText != null)
            {
                approvedCountText.text = $"{stats.dailyApprovedCount} กล่อง";
            }

            if (legalCountText != null)
            {
                legalCountText.text = $"<color=#5AFFA5>{stats.legalShippedCount} กล่อง</color>";
            }

            if (illegalCountText != null)
            {
                illegalCountText.text = $"<color=#FFD64E>{stats.illegalShippedCount} กล่อง</color>";
            }

            if (alienCountText != null)
            {
                alienCountText.text = $"<color=#FF5AA5>{stats.alienShippedCount} กล่อง</color>";
            }

            if (totalShippedText != null)
            {
                totalShippedText.text = $"{stats.TotalShippedToday} กล่อง";
            }
        }

        if (summaryPanel != null)
        {
            summaryPanel.SetActive(true);
        }

        // หยุดเวลาในเกมและเปิดเคอร์เซอร์เมาส์เพื่อคลิกปุ่ม
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// ปิดหน้าต่างสรุปผลและคืนค่าเวลาและเคอร์เซอร์
    /// </summary>
    public void HideSummary()
    {
        if (summaryPanel != null)
        {
            summaryPanel.SetActive(false);
        }

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>
    /// เมื่อผู้เล่นกดปุ่ม [ END DAY / จบวัน ] บนหน้าจอ HUD
    /// </summary>
    public void OnEndDayButtonClicked()
    {
        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.RequestEndDay();
        }
    }

    /// <summary>
    /// เมื่อผู้เล่นกดปุ่ม [ START NEXT DAY / เริ่มวันถัดไป ] ในหน้าต่างสรุปผล
    /// </summary>
    public void OnNextDayClicked()
    {
        HideSummary();

        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.ConfirmEndDayAndProceed();
        }
    }

    /// <summary>
    /// แสดงแบนเนอร์แจ้งเตือนเมื่อจบวันไม่สำเร็จ
    /// </summary>
    public void ShowWarningBanner(string message)
    {
        if (warningBannerObject == null) return;

        if (warningBannerText != null)
        {
            warningBannerText.text = message;
        }

        if (warningCoroutine != null)
        {
            StopCoroutine(warningCoroutine);
        }

        warningCoroutine = StartCoroutine(WarningBannerRoutine());
    }

    private IEnumerator WarningBannerRoutine()
    {
        warningBannerObject.SetActive(true);
        yield return new WaitForSecondsRealtime(warningDuration);
        warningBannerObject.SetActive(false);
        warningCoroutine = null;
    }
}

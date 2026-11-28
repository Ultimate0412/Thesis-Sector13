using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// หน้าต่างสรุปผลประจำวัน
/// - วันที่ผ่านเงื่อนไข: แสดงปุ่ม NEXT DAY
/// - วันที่คะแนนไม่พอจ่ายค่าสถานี: แสดงแค่ RESTART / MAIN MENU
/// UI อยู่ใน scene — ลาก reference ใน Inspector
/// </summary>
public class DailySummaryUI : MonoBehaviour
{
    public static DailySummaryUI Instance { get; private set; }

    [Header("UI Panels")]
    [Tooltip("หน้าต่างป๊อปอัปสรุปผลประจำวัน")]
    [SerializeField] private GameObject summaryPanel;

    [Tooltip("ปุ่มกดจบวันบนหน้าจอ HUD")]
    [SerializeField] private Button endDayHUDButton;

    [Header("Summary Text Displays")]
    [SerializeField] private TextMeshProUGUI dayTitleText;
    [SerializeField] private TextMeshProUGUI dailyScoreText;
    [SerializeField] private TextMeshProUGUI totalScoreText;
    [SerializeField] private TextMeshProUGUI rejectedCountText;
    [SerializeField] private TextMeshProUGUI approvedCountText;
    [SerializeField] private TextMeshProUGUI legalCountText;
    [SerializeField] private TextMeshProUGUI illegalCountText;
    [SerializeField] private TextMeshProUGUI alienCountText;
    [SerializeField] private TextMeshProUGUI totalShippedText;

    [Header("Daily Requirements")]
    [SerializeField] private TextMeshProUGUI shippedRequirementText;
    [SerializeField] private TextMeshProUGUI stationFeeText;
    [SerializeField] private GameObject penaltyRow;
    [SerializeField] private TextMeshProUGUI penaltyText;
    [SerializeField] private TextMeshProUGUI resultText;

    [Header("Result Buttons")]
    [SerializeField] private Button nextDayButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;

    public bool IsSummaryOpen => summaryPanel != null && summaryPanel.activeSelf;
    private bool summaryActionHandled;

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

        // UI ถูกย้าย/เปลี่ยนชื่อบ่อยหลัง merge — เติม ref ที่ว่างจากชื่อใน panel ก่อน
        ResolveReferences();

        // ผูกปุ่มทุกตัวเสมอ: ปุ่มหนึ่งอ้างอิงหาย ไม่ควรทำให้ปุ่มอื่นกดไม่ได้
        WireButtons();

        LogMissingReferences();

        if (summaryPanel != null)
        {
            summaryPanel.SetActive(false);
        }
    }

    private void Start()
    {
        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.OnDaySummaryRequested += ShowSummary;
        }
    }

    private void OnDestroy()
    {
        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.OnDaySummaryRequested -= ShowSummary;
        }
    }

    // ------------------------------------------------------------- Wiring

    private void WireButtons()
    {
        Bind(endDayHUDButton, OnEndDayButtonClicked);
        Bind(nextDayButton, OnNextDayClicked);
        Bind(restartButton, OnRestartClicked);
        Bind(mainMenuButton, OnMainMenuClicked);
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    /// <summary>
    /// เติม reference ที่ยังว่างอยู่โดยค้นหาจากชื่อใน panel
    /// (ชื่อเดียวกับที่ MenuReferenceAssigner ใช้) เพื่อให้ UI ยังทำงานได้
    /// แม้ลำดับชั้น/ชื่อ object จะถูกแก้ไปหลัง merge
    /// </summary>
    private void ResolveReferences()
    {
        if (summaryPanel == null)
        {
            Transform canvas = transform.parent;
            Transform found = canvas != null ? canvas.Find("DailySummaryPanel") : null;
            if (found != null) summaryPanel = found.gameObject;
        }

        if (summaryPanel == null) return;
        Transform root = summaryPanel.transform;

        if (dayTitleText == null) dayTitleText = FindText(root, "HeaderBar/TitleText", "TitleText");
        if (dailyScoreText == null) dailyScoreText = FindText(root, "DailyScoreValue");
        if (totalScoreText == null) totalScoreText = FindText(root, "TotalScoreValue");
        if (rejectedCountText == null) rejectedCountText = FindText(root, "RejectValue");
        if (approvedCountText == null) approvedCountText = FindText(root, "ApproveValue");
        if (legalCountText == null) legalCountText = FindText(root, "LegalValue");
        if (illegalCountText == null) illegalCountText = FindText(root, "IllegalValue");
        if (alienCountText == null) alienCountText = FindText(root, "AlienValue");
        if (totalShippedText == null) totalShippedText = FindText(root, "TotalValue");
        if (shippedRequirementText == null) shippedRequirementText = FindText(root, "ShippedValue");
        if (stationFeeText == null) stationFeeText = FindText(root, "StationFeeValue");
        if (penaltyRow == null) penaltyRow = FindObject(root, "PenaltyRow");
        if (penaltyText == null) penaltyText = FindText(root, "PenaltyRow/PenaltyValue", "PenaltyValue");
        if (resultText == null) resultText = FindText(root, "ResultText");

        if (nextDayButton == null) nextDayButton = FindButton(root, "NextDayButton");
        if (restartButton == null) restartButton = FindButton(root, "RestartButton");
        if (mainMenuButton == null) mainMenuButton = FindButton(root, "MainMenuButton");
    }

    /// <summary>รายงาน ref ที่ยังว่างเป็น warning (ไม่บล็อกการผูกปุ่ม)</summary>
    private void LogMissingReferences()
    {
        List<string> missing = new List<string>();

        // endDayHUDButton ไม่นับว่าจำเป็น — ปุ่มจบวันบน HUD ถูกยกเลิกใช้แล้ว
        // (ปัจจุบันจบวันผ่าน EndDayInteractable ในฉาก)
        if (summaryPanel == null) missing.Add(nameof(summaryPanel));
        if (dayTitleText == null) missing.Add(nameof(dayTitleText));
        if (dailyScoreText == null) missing.Add(nameof(dailyScoreText));
        if (totalScoreText == null) missing.Add(nameof(totalScoreText));
        if (rejectedCountText == null) missing.Add(nameof(rejectedCountText));
        if (approvedCountText == null) missing.Add(nameof(approvedCountText));
        if (legalCountText == null) missing.Add(nameof(legalCountText));
        if (illegalCountText == null) missing.Add(nameof(illegalCountText));
        if (alienCountText == null) missing.Add(nameof(alienCountText));
        if (totalShippedText == null) missing.Add(nameof(totalShippedText));
        if (shippedRequirementText == null) missing.Add(nameof(shippedRequirementText));
        if (stationFeeText == null) missing.Add(nameof(stationFeeText));
        if (penaltyRow == null) missing.Add(nameof(penaltyRow));
        if (penaltyText == null) missing.Add(nameof(penaltyText));
        if (resultText == null) missing.Add(nameof(resultText));
        if (nextDayButton == null) missing.Add(nameof(nextDayButton));
        if (restartButton == null) missing.Add(nameof(restartButton));
        if (mainMenuButton == null) missing.Add(nameof(mainMenuButton));

        if (missing.Count > 0)
        {
            Debug.LogWarning(
                $"[DailySummaryUI] reference บางตัวว่าง จะข้ามการอัปเดตส่วนนั้น — {string.Join(", ", missing)}", this);
        }
    }

    private static TextMeshProUGUI FindText(Transform root, params string[] paths)
    {
        foreach (string path in paths)
        {
            Transform found = root.Find(path);
            if (found == null) continue;

            TextMeshProUGUI text = found.GetComponent<TextMeshProUGUI>();
            if (text != null) return text;
        }

        return null;
    }

    private static Button FindButton(Transform root, params string[] paths)
    {
        foreach (string path in paths)
        {
            Transform found = root.Find(path);
            if (found == null) continue;

            Button button = found.GetComponent<Button>();
            if (button != null) return button;
        }

        return null;
    }

    private static GameObject FindObject(Transform root, params string[] paths)
    {
        foreach (string path in paths)
        {
            Transform found = root.Find(path);
            if (found != null) return found.gameObject;
        }

        return null;
    }

    // ------------------------------------------------------------- Summary

    /// <summary>แสดงหน้าต่างสรุปผลประจำวัน พร้อมจัดรูปแบบข้อความสถิติ</summary>
    public void ShowSummary(DaySummaryResult result)
    {
        if (result == null) return;
        summaryActionHandled = false;

        DailyStatisticsData stats = result.stats;
        if (stats == null && ScoreManager.Instance != null)
        {
            stats = ScoreManager.Instance.CurrentDayStats;
        }

        if (dayTitleText != null)
        {
            dayTitleText.text = result.isFinalDay
                ? $"FINAL WORK SUMMARY - DAY {result.day}"
                : $"DAILY WORK SUMMARY - DAY {result.day}";
        }

        if (stats != null)
        {
            if (dailyScoreText != null)
            {
                int net = stats.NetDailyCredits;
                string sign = net >= 0 ? "+" : "";
                string colorHex = net >= 0 ? "#4EFA7A" : "#FA4E4E";
                dailyScoreText.text = $"<color={colorHex}>{sign}{net}</color>";
            }

            if (totalScoreText != null)
            {
                totalScoreText.text = $"{stats.totalScoreSnapshot:N0} Credits";
            }

            if (rejectedCountText != null)
            {
                rejectedCountText.text = $"{stats.dailyRejectedCount} boxes";
            }

            if (approvedCountText != null)
            {
                approvedCountText.text = $"{stats.dailyApprovedCount} boxes";
            }

            if (legalCountText != null)
            {
                legalCountText.text = $"<color=#5AFFA5>{stats.legalShippedCount} boxes</color>";
            }

            if (illegalCountText != null)
            {
                illegalCountText.text = $"<color=#FFD64E>{stats.illegalShippedCount} boxes</color>";
            }

            if (alienCountText != null)
            {
                alienCountText.text = $"<color=#FF5AA5>{stats.alienShippedCount} boxes</color>";
            }

            if (totalShippedText != null)
            {
                totalShippedText.text = $"{stats.TotalShippedToday} boxes";
            }
        }

        if (shippedRequirementText != null)
        {
            int required = result.requiredShipped;
            string color = required > 0 && result.shippedToday < required ? "#FA4E4E" : "#4EFA7A";
            shippedRequirementText.text = required > 0
                ? $"<color={color}>{result.shippedToday} / {required}</color>"
                : $"{result.shippedToday}";
        }

        if (stationFeeText != null)
        {
            stationFeeText.text = result.stationFee > 0
                ? $"<color=#FA4E4E>-{result.stationFee}</color>"
                : "-";
        }

        if (penaltyRow != null)
        {
            penaltyRow.SetActive(result.shortfallPenalty > 0);
        }

        if (penaltyText != null)
        {
            penaltyText.text = $"<color=#FA4E4E>-{result.shortfallPenalty}</color>";
        }

        if (resultText != null)
        {
            if (!result.passed)
            {
                resultText.text = $"<color=#FA4E4E>INSUFFICIENT CREDITS — SHORT {result.creditsShortfall}</color>";
            }
            else if (result.runComplete)
            {
                resultText.text = result.shortfallPenalty > 0
                    ? "<color=#FFD64E>RUN COMPLETE — PENALTY APPLIED</color>"
                    : "<color=#4EFA7A>SECTOR CLEARED — RUN COMPLETE</color>";
            }
            else if (result.shortfallPenalty > 0)
            {
                // Day was forced through with leftover parcels: be honest about it
                // instead of printing a clean "APPROVED".
                resultText.text = "<color=#FFD64E>SHIFT CLOSED — PENALTY APPLIED</color>";
            }
            else
            {
                resultText.text = "<color=#4EFA7A>OPERATION APPROVED</color>";
            }
        }

        bool showNextDay = result.passed && !result.runComplete;
        bool showRestart = !result.passed;
        bool showMainMenu = !result.passed || result.runComplete;

        if (nextDayButton != null) nextDayButton.gameObject.SetActive(showNextDay);
        if (restartButton != null) restartButton.gameObject.SetActive(showRestart);
        if (mainMenuButton != null)
        {
            mainMenuButton.gameObject.SetActive(showMainMenu);
            TextMeshProUGUI menuLabel = mainMenuButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (menuLabel != null) menuLabel.text = result.runComplete ? "FINISH RUN" : "MAIN MENU";
        }

        if (summaryPanel != null)
        {
            summaryPanel.SetActive(true);
        }

        // หยุดเวลาในเกมและเปิดเคอร์เซอร์เมาส์เพื่อคลิกปุ่ม
        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>ปิดหน้าต่างสรุปผลและคืนค่าเวลาและเคอร์เซอร์</summary>
    public void HideSummary()
    {
        if (summaryPanel != null)
        {
            summaryPanel.SetActive(false);
        }

        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ------------------------------------------------------------- Actions

    private void OnEndDayButtonClicked()
    {
        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.RequestEndDay();
        }
    }

    private void OnNextDayClicked()
    {
        if (summaryActionHandled) return;
        summaryActionHandled = true;

        HideSummary();

        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.ConfirmEndDayAndProceed();
        }
    }

    private void OnRestartClicked()
    {
        if (summaryActionHandled) return;
        summaryActionHandled = true;

        HideSummary();
        SaveSystem.RestartCurrentDay();
    }

    private void OnMainMenuClicked()
    {
        if (summaryActionHandled) return;
        summaryActionHandled = true;

        // Final successful day: mark the run complete before leaving so
        // listeners get OnDayEnded once. Failed summaries are a no-op.
        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.ConfirmEndDayAndProceed();
        }

        HideSummary();
        SaveSystem.ReturnToMainMenu();
    }
}

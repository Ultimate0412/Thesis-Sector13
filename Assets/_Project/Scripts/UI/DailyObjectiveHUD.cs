using TMPro;
using UnityEngine;

/// <summary>
/// HUD แสดงเป้าหมายของวันปัจจุบัน (พัสดุขั้นต่ำ + ค่าสถานี) ที่มุมซ้ายบน
/// อัปเดตอัตโนมัติเมื่อส่งพัสดุ / ขึ้นวันใหม่ และซ่อนตัวเองระหว่างมีบทสนทนา
/// UI อยู่ใน scene — ลาก reference ใน Inspector
/// </summary>
public class DailyObjectiveHUD : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("CanvasGroup ของ panel (ใช้ซ่อน/แสดงโดยไม่ปิด GameObject)")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI dayTitleText;
    [SerializeField] private TextMeshProUGUI shippedValueText;
    [SerializeField] private TextMeshProUGUI stationFeeValueText;

    private const string GreenHex = "#4EFA7A";
    private const string AmberHex = "#FFD64E";
    private const string RedHex = "#FA4E4E";
    private const string DimHex = "#8C8C8C";

    private void Start()
    {
        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.OnDayStarted += HandleDayStarted;
            GameDayManager.Instance.OnDayChanged += HandleDayChanged;
        }
        else
        {
            Debug.LogError("[DailyObjectiveHUD] ไม่พบ GameDayManager.Instance ในฉาก!", this);
        }

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnDailyStatsChanged += HandleStatsChanged;
        }

        Refresh();
    }

    private void OnDestroy()
    {
        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.OnDayStarted -= HandleDayStarted;
            GameDayManager.Instance.OnDayChanged -= HandleDayChanged;
        }

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnDailyStatsChanged -= HandleStatsChanged;
        }
    }

    private void LateUpdate()
    {
        if (canvasGroup == null) return;

        // ซ่อนระหว่างมีบทสนทนา เพื่อไม่ให้ทับ speaker plate ของ DialoguePanel
        bool dialogueVisible =
            DialogueHUD.Instance != null &&
            DialogueHUD.Instance.dialoguePanel != null &&
            DialogueHUD.Instance.dialoguePanel.activeSelf;

        canvasGroup.alpha = dialogueVisible ? 0f : 1f;
    }

    private void HandleDayStarted(int newDay) => Refresh();
    private void HandleDayChanged(int newDay) => Refresh();
    private void HandleStatsChanged(DailyStatisticsData stats) => Refresh();

    /// <summary>อ่านค่าจาก GameDayManager แล้วอัปเดตข้อความบน HUD</summary>
    public void Refresh()
    {
        GameDayManager gm = GameDayManager.Instance;
        if (gm == null) return;

        int day = gm.currentDay;
        int required = gm.RequiredShippedToday;
        int shipped = gm.ShippedToday;
        int fee = gm.StationFeeToday;

        if (dayTitleText != null)
        {
            dayTitleText.text = $"DAY {day} OBJECTIVES";
        }

        if (shippedValueText != null)
        {
            if (required > 0)
            {
                string color = shipped >= required ? GreenHex : AmberHex;
                shippedValueText.text = $"<color={color}>{shipped} / {required}</color>";
            }
            else
            {
                // ไม่มีขั้นต่ำของวันนี้ — แสดงจำนวนที่ส่งไปแล้วเฉย ๆ
                shippedValueText.text = $"<color={DimHex}>{shipped}</color>";
            }
        }

        if (stationFeeValueText != null)
        {
            stationFeeValueText.text = fee > 0 ? $"<color={RedHex}>-{fee}</color>" : "-";
        }
    }
}

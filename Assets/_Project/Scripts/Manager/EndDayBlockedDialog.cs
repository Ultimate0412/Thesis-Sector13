using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dialog แจ้งเตือนเมื่อกดจบวันไม่ได้
/// - โหมดบล็อกปกติ: ปุ่ม OK อย่างเดียว
/// - โหมดส่งพัสดุไม่ถึงขั้นต่ำ: ปุ่ม BACK / END DAY ANYWAY (หักคะแนนแล้วจบวันได้)
/// UI อยู่ใน scene — ลาก reference ใน Inspector
/// </summary>
public class EndDayBlockedDialog : MonoBehaviour
{
    public static EndDayBlockedDialog Instance { get; private set; }

    [Header("Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("Buttons")]
    [SerializeField] private Button backButton;
    [SerializeField] private Button confirmButton;

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (ValidateReferences())
        {
            WireButtons();
        }

        Hide();
    }

    private void Start()
    {
        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.OnDayEndFailed += HandleDayEndFailed;
        }
    }

    private void OnDestroy()
    {
        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.OnDayEndFailed -= HandleDayEndFailed;
        }

        if (Instance == this) Instance = null;
    }

    private void HandleDayEndFailed(DayEndFailure failure)
    {
        if (failure == null) return;

        if (failure.canOverrideWithPenalty)
        {
            ShowPenaltyConfirm(failure);
        }
        else
        {
            ShowBlocked(failure.reason);
        }
    }

    // ------------------------------------------------------------- Wiring

    private void WireButtons()
    {
        backButton.onClick.RemoveAllListeners();
        backButton.onClick.AddListener(Hide);

        confirmButton.onClick.RemoveAllListeners();
        confirmButton.onClick.AddListener(OnConfirmClicked);
    }

    private bool ValidateReferences()
    {
        List<string> missing = new List<string>();

        if (root == null) missing.Add(nameof(root));
        if (titleText == null) missing.Add(nameof(titleText));
        if (messageText == null) missing.Add(nameof(messageText));
        if (backButton == null) missing.Add(nameof(backButton));
        if (confirmButton == null) missing.Add(nameof(confirmButton));

        if (missing.Count == 0) return true;

        Debug.LogError(
            $"[EndDayBlockedDialog] ลาก reference ไม่ครบ — ขาด: {string.Join(", ", missing)}", this);
        return false;
    }

    // ------------------------------------------------------------- Show / Hide

    public void ShowBlocked(string reason)
    {
        titleText.text = "CANNOT END DAY";
        messageText.text = reason;
        SetButtonLabel(backButton, "OK");
        confirmButton.gameObject.SetActive(false);
        SetVisible(true);
    }

    public void ShowPenaltyConfirm(DayEndFailure failure)
    {
        titleText.text = "BELOW SHIPPING QUOTA";

        string message = failure.reason;
        if (failure.penalty > 0)
        {
            message += $"\n\nยืนยันจบวันจะถูกหัก {failure.penalty} คะแนน";
        }

        messageText.text = message;
        SetButtonLabel(backButton, "BACK");
        SetButtonLabel(confirmButton, failure.penalty > 0 ? $"END DAY ANYWAY (-{failure.penalty})" : "END DAY ANYWAY");
        confirmButton.gameObject.SetActive(true);
        SetVisible(true);
    }

    public void Hide()
    {
        SetVisible(false);
    }

    private void OnConfirmClicked()
    {
        Hide();

        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.ConfirmEndDayWithPenalty();
        }
    }

    private void SetVisible(bool visible)
    {
        if (root != null) root.SetActive(visible);

        if (visible)
        {
            Time.timeScale = 0f;
            AudioListener.pause = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        Time.timeScale = 1f;
        AudioListener.pause = false;

        bool blockingMenuOpen =
            (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsOpen) ||
            (DailySummaryUI.Instance != null && DailySummaryUI.Instance.IsSummaryOpen);

        if (!blockingMenuOpen)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private static void SetButtonLabel(Button button, string label)
    {
        if (button == null) return;

        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null) text.text = label;
    }
}

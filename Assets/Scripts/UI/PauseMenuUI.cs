using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// เมนูหยุดเกม (ESC): RESUME / RESET DAY / RETURN TO MAIN MENU
/// UI อยู่ใน scene ทั้งหมด (ไม่มีการสร้างอัตโนมัติ) — ลาก reference ใน Inspector
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    public static PauseMenuUI Instance { get; private set; }

    [Header("Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private TextMeshProUGUI infoLabel;

    [Header("Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button resetDayButton;
    [SerializeField] private Button returnButton;

    [Header("Confirm Dialog")]
    [SerializeField] private GameObject confirmScreen;
    [SerializeField] private TextMeshProUGUI confirmTitle;
    [SerializeField] private TextMeshProUGUI confirmMessage;
    [SerializeField] private Button confirmYesButton;
    [SerializeField] private Button confirmNoButton;

    [Header("Feedback")]
    [SerializeField] private TextMeshProUGUI toastLabel;

    private Action confirmAction;
    private bool isOpen;

    public bool IsOpen => isOpen;

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

        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        // กันเวลา/เสียงค้างหยุดเมื่อฉากถูกเปลี่ยน
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (confirmScreen != null && confirmScreen.activeSelf)
        {
            HideConfirm();
            return;
        }

        if (isOpen)
        {
            Close();
            return;
        }

        if (IsBlockingMenuOpen()) return;
        Open();
    }

    private static bool IsBlockingMenuOpen()
    {
        if (GameOverUI.Instance != null && GameOverUI.Instance.IsGameOverOpen) return true;
        if (StampUIController.Instance != null && StampUIController.Instance.IsMenuOpen) return true;
        if (DailySummaryUI.Instance != null && DailySummaryUI.Instance.IsSummaryOpen) return true;
        if (EndDayBlockedDialog.Instance != null && EndDayBlockedDialog.Instance.IsOpen) return true;
        return false;
    }

    // ------------------------------------------------------------- Wiring

    private void WireButtons()
    {
        Bind(resumeButton, Close);
        Bind(resetDayButton, OnResetDayClicked);
        Bind(returnButton, OnReturnToMenuClicked);
        Bind(confirmYesButton, OnConfirmYes);
        Bind(confirmNoButton, HideConfirm);
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private bool ValidateReferences()
    {
        List<string> missing = new List<string>();

        if (root == null) missing.Add(nameof(root));
        if (infoLabel == null) missing.Add(nameof(infoLabel));
        if (resumeButton == null) missing.Add(nameof(resumeButton));
        if (resetDayButton == null) missing.Add(nameof(resetDayButton));
        if (returnButton == null) missing.Add(nameof(returnButton));
        if (confirmScreen == null) missing.Add(nameof(confirmScreen));
        if (confirmTitle == null) missing.Add(nameof(confirmTitle));
        if (confirmMessage == null) missing.Add(nameof(confirmMessage));
        if (confirmYesButton == null) missing.Add(nameof(confirmYesButton));
        if (confirmNoButton == null) missing.Add(nameof(confirmNoButton));
        if (toastLabel == null) missing.Add(nameof(toastLabel));

        if (missing.Count == 0) return true;

        Debug.LogError(
            $"[PauseMenuUI] ลาก reference ไม่ครบ — ขาด: {string.Join(", ", missing)}", this);
        return false;
    }

    // ------------------------------------------------------------- Open/Close

    private void SetVisible(bool visible)
    {
        isOpen = visible;

        if (root != null) root.SetActive(visible);

        if (visible)
        {
            Time.timeScale = 0f;
            AudioListener.pause = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;

            if (confirmScreen != null) confirmScreen.SetActive(false);
            confirmAction = null;

            if (!IsBlockingMenuOpen())
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    public void Open()
    {
        int day = GameDayManager.Instance != null ? GameDayManager.Instance.currentDay : 1;
        int score = ScoreManager.Instance != null ? ScoreManager.Instance.totalScore : 0;

        if (infoLabel != null) infoLabel.text = $"DAY {day}  ·  {score:N0} PTS";

        SetVisible(true);
    }

    public void Close()
    {
        SetVisible(false);
    }

    // ------------------------------------------------------------- Actions

    private void OnResetDayClicked()
    {
        int day = GameDayManager.Instance != null ? GameDayManager.Instance.currentDay : 1;

        ShowConfirm(
            "RESET DAY?",
            $"Reload the auto-save from the start of Day {day}.\nAll progress made today will be lost.",
            "RESET",
            SaveSystem.RestartCurrentDay);
    }

    private void OnReturnToMenuClicked()
    {
        ShowConfirm(
            "RETURN TO MAIN MENU?",
            "Progress made during the current day is not saved.\nAuto-save only happens at the end of a day.",
            "RETURN",
            () => SaveSystem.ReturnToMainMenu());
    }

    private void ShowConfirm(string title, string message, string yesLabel, Action action)
    {
        confirmAction = action;
        confirmTitle.text = title;
        confirmMessage.text = message;
        SetButtonLabel(confirmYesButton, yesLabel);
        confirmScreen.SetActive(true);
    }

    private void HideConfirm()
    {
        confirmAction = null;
        if (confirmScreen != null) confirmScreen.SetActive(false);
    }

    private void OnConfirmYes()
    {
        Action action = confirmAction;
        confirmAction = null;
        if (confirmScreen != null) confirmScreen.SetActive(false);
        action?.Invoke();
    }

    private static void SetButtonLabel(Button button, string label)
    {
        if (button == null) return;

        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null) text.text = label;
    }
}

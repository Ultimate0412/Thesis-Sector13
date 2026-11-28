using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// หน้า Main Menu: NEW GAME / LOAD GAME / EXIT + หน้าเลือกช่องเซฟ 3 ช่อง
/// UI อยู่ใน scene ทั้งหมด (ไม่มีการสร้างอัตโนมัติ) — ลาก reference ใน Inspector
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Serializable]
    public class SlotCard
    {
        public TextMeshProUGUI statusLabel;
        public TextMeshProUGUI metaLabel;
        public Button primaryButton;
        public Button deleteButton;
    }

    private enum MenuMode
    {
        NewGame,
        LoadGame
    }

    private enum ScreenState
    {
        Root,
        Slots,
        Confirm
    }

    private static readonly Color Accent = new Color(0.208f, 0.878f, 1f, 1f);
    private static readonly Color TextDim = new Color(0.490f, 0.639f, 0.706f, 1f);
    private static readonly Color Danger = new Color(1f, 0.353f, 0.431f, 1f);

    [Header("Screens")]
    [SerializeField] private GameObject rootScreen;
    [SerializeField] private GameObject slotScreen;
    [SerializeField] private GameObject confirmScreen;

    [Header("Root Buttons")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button exitButton;

    [Header("Slot Selection")]
    [SerializeField] private TextMeshProUGUI slotHeader;
    [SerializeField] private TextMeshProUGUI slotSubHeader;
    [SerializeField] private Button backButton;
    [SerializeField] private TextMeshProUGUI toastLabel;
    [SerializeField] private SlotCard[] cards = new SlotCard[GameSession.SlotCount];

    [Header("Confirm Dialog")]
    [SerializeField] private TextMeshProUGUI confirmTitle;
    [SerializeField] private TextMeshProUGUI confirmMessage;
    [SerializeField] private Button confirmYesButton;
    [SerializeField] private Button confirmNoButton;

    private MenuMode mode = MenuMode.NewGame;
    private ScreenState state = ScreenState.Root;
    private Action confirmAction;
    private Coroutine toastRoutine;

    private void Awake()
    {
        if (!ValidateReferences()) return;

        WireButtons();

        // Back must stay on top of the slot screen so nothing can steal its clicks.
        if (backButton != null) backButton.transform.SetAsLastSibling();

        ShowRoot();
    }

    private void Start()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (state == ScreenState.Confirm)
        {
            HideConfirm();
        }
        else if (state == ScreenState.Slots)
        {
            ShowRoot();
        }
    }

    // ------------------------------------------------------------- Wiring

    private void WireButtons()
    {
        Bind(newGameButton, OnNewGameClicked);
        Bind(loadGameButton, OnLoadGameClicked);
        Bind(exitButton, OnExitClicked);
        Bind(backButton, ShowRoot);
        Bind(confirmYesButton, OnConfirmYes);
        Bind(confirmNoButton, HideConfirm);

        for (int i = 0; i < cards.Length; i++)
        {
            SlotCard card = cards[i];
            if (card == null) continue;

            int slot = i + 1;
            Bind(card.primaryButton, () => OnSlotPrimaryClicked(slot));
            Bind(card.deleteButton, () => OnDeleteSlotClicked(slot));
        }
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

        if (rootScreen == null) missing.Add(nameof(rootScreen));
        if (slotScreen == null) missing.Add(nameof(slotScreen));
        if (confirmScreen == null) missing.Add(nameof(confirmScreen));
        if (newGameButton == null) missing.Add(nameof(newGameButton));
        if (loadGameButton == null) missing.Add(nameof(loadGameButton));
        if (exitButton == null) missing.Add(nameof(exitButton));
        if (slotHeader == null) missing.Add(nameof(slotHeader));
        if (slotSubHeader == null) missing.Add(nameof(slotSubHeader));
        if (backButton == null) missing.Add(nameof(backButton));
        if (toastLabel == null) missing.Add(nameof(toastLabel));
        if (confirmTitle == null) missing.Add(nameof(confirmTitle));
        if (confirmMessage == null) missing.Add(nameof(confirmMessage));
        if (confirmYesButton == null) missing.Add(nameof(confirmYesButton));
        if (confirmNoButton == null) missing.Add(nameof(confirmNoButton));

        if (cards == null || cards.Length != GameSession.SlotCount)
        {
            missing.Add($"cards (ต้องมี {GameSession.SlotCount} ช่อง)");
        }
        else
        {
            for (int i = 0; i < cards.Length; i++)
            {
                SlotCard card = cards[i];
                if (card == null)
                {
                    missing.Add($"cards[{i}]");
                    continue;
                }

                if (card.statusLabel == null) missing.Add($"cards[{i}].statusLabel");
                if (card.metaLabel == null) missing.Add($"cards[{i}].metaLabel");
                if (card.primaryButton == null) missing.Add($"cards[{i}].primaryButton");
                if (card.deleteButton == null) missing.Add($"cards[{i}].deleteButton");
            }
        }

        if (missing.Count == 0) return true;

        Debug.LogError(
            $"[MainMenuController] ลาก reference ไม่ครบ — ขาด: {string.Join(", ", missing)}", this);
        return false;
    }

    // ------------------------------------------------------------- Navigation

    public void ShowRoot()
    {
        state = ScreenState.Root;
        confirmAction = null;
        rootScreen.SetActive(true);
        slotScreen.SetActive(false);
        confirmScreen.SetActive(false);
    }

    private void ShowSlots(MenuMode newMode)
    {
        mode = newMode;
        state = ScreenState.Slots;
        confirmAction = null;

        slotHeader.text = newMode == MenuMode.NewGame ? "SELECT SAVE SLOT" : "LOAD SAVE";
        slotSubHeader.text = newMode == MenuMode.NewGame
            ? "CHOOSE A SLOT FOR THIS NEW RUN"
            : "PICK A SAVE TO CONTINUE";

        rootScreen.SetActive(false);
        slotScreen.SetActive(true);
        confirmScreen.SetActive(false);

        if (backButton != null) backButton.transform.SetAsLastSibling();

        RefreshSlots();
    }

    private void RefreshSlots()
    {
        for (int i = 0; i < cards.Length; i++)
        {
            int slot = i + 1;
            SaveSlotInfo info = SaveSystem.GetSlotInfo(slot);
            SlotCard card = cards[i];
            if (card == null) continue;

            bool hasData = info.exists && !info.corrupted;

            if (info.corrupted)
            {
                card.statusLabel.text = "CORRUPTED";
                card.statusLabel.color = Danger;
                card.metaLabel.text = "SAVE FILE UNREADABLE";
            }
            else if (info.exists)
            {
                card.statusLabel.text = $"DAY {info.day}";
                card.statusLabel.color = Accent;
                card.metaLabel.text = $"{info.totalScore:N0} PTS  ·  {FormatDate(info.savedAtUtc)}";
            }
            else
            {
                card.statusLabel.text = "EMPTY SLOT";
                card.statusLabel.color = TextDim;
                card.metaLabel.text = "NO DATA";
            }

            bool canUse = mode == MenuMode.NewGame || hasData;
            card.primaryButton.interactable = canUse;

            string primaryLabel;
            if (mode == MenuMode.NewGame)
            {
                primaryLabel = info.exists ? "OVERWRITE" : "START";
            }
            else
            {
                primaryLabel = hasData ? "LOAD" : "EMPTY";
            }
            SetButtonLabel(card.primaryButton, primaryLabel);

            card.deleteButton.gameObject.SetActive(info.exists);
        }
    }

    private void ShowConfirm(string title, string message, string yesLabel, bool danger, Action action)
    {
        state = ScreenState.Confirm;
        confirmAction = action;

        confirmTitle.text = title;
        confirmMessage.text = message;
        SetButtonLabel(confirmYesButton, yesLabel);

        Graphic target = confirmYesButton.targetGraphic;
        if (target != null)
        {
            target.color = danger
                ? new Color(0.169f, 0.043f, 0.067f, 0.96f)
                : new Color(0.047f, 0.098f, 0.137f, 0.96f);
        }

        Transform accent = confirmYesButton.transform.Find("AccentBar");
        if (accent != null)
        {
            Image accentImage = accent.GetComponent<Image>();
            if (accentImage != null) accentImage.color = danger ? Danger : Accent;
        }

        confirmScreen.SetActive(true);
    }

    private void HideConfirm()
    {
        if (state == ScreenState.Confirm)
        {
            state = ScreenState.Slots;
        }
        confirmAction = null;
        confirmScreen.SetActive(false);
    }

    private void OnConfirmYes()
    {
        Action action = confirmAction;
        confirmAction = null;
        confirmScreen.SetActive(false);
        state = ScreenState.Slots;
        action?.Invoke();
    }

    // ------------------------------------------------------------- Actions

    private void OnNewGameClicked()
    {
        ShowSlots(MenuMode.NewGame);
    }

    private void OnLoadGameClicked()
    {
        ShowSlots(MenuMode.LoadGame);
    }

    private void OnExitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnSlotPrimaryClicked(int slot)
    {
        SaveSlotInfo info = SaveSystem.GetSlotInfo(slot);

        if (mode == MenuMode.LoadGame)
        {
            if (!info.exists || info.corrupted)
            {
                ShowToast("THIS SLOT CANNOT BE LOADED");
                return;
            }

            SaveSystem.RequestLoad(slot);
            return;
        }

        if (info.exists)
        {
            ShowConfirm(
                $"OVERWRITE SLOT {slot}?",
                $"Slot {slot} already contains Day {info.day}. Starting a new game will permanently erase it.",
                "OVERWRITE",
                true,
                () => SaveSystem.StartNewGame(slot));
        }
        else
        {
            SaveSystem.StartNewGame(slot);
        }
    }

    private void OnDeleteSlotClicked(int slot)
    {
        ShowConfirm(
            $"DELETE SLOT {slot}?",
            $"The save in slot {slot} will be permanently deleted. This cannot be undone.",
            "DELETE",
            true,
            () =>
            {
                SaveSystem.DeleteSlot(slot);
                RefreshSlots();
                ShowToast($"SLOT {slot} DELETED");
            });
    }

    // ------------------------------------------------------------- Helpers

    private static void SetButtonLabel(Button button, string label)
    {
        if (button == null) return;

        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null) text.text = label;
    }

    private void ShowToast(string message)
    {
        if (toastLabel == null) return;

        toastLabel.text = message;
        toastLabel.gameObject.SetActive(true);

        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(HideToastAfterDelay());
    }

    private IEnumerator HideToastAfterDelay()
    {
        yield return new WaitForSecondsRealtime(2.5f);
        if (toastLabel != null) toastLabel.text = string.Empty;
        toastRoutine = null;
    }

    private static string FormatDate(string isoUtc)
    {
        if (string.IsNullOrEmpty(isoUtc)) return "-";

        if (DateTime.TryParse(isoUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsed))
        {
            return parsed.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        }

        return "-";
    }
}

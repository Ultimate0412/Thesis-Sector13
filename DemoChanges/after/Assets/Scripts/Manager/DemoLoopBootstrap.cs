using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DemoLoopBootstrap : MonoBehaviour
{
    [Header("Demo Settings")]
    public int demoDayLimit = 1;

    private GameDayManager dayManager;
    private Canvas uiCanvas;
    private bool demoFinished;

    private TMP_Text dayLabel;
    private TMP_Text openLabel;
    private TMP_Text spawnLabel;

    private GameObject toastRoot;
    private TMP_Text toastLabel;
    private float toastHideAt;

    private GameObject summaryRoot;
    private TMP_Text summaryTitle;
    private TMP_Text summaryBody;

    private void Awake()
    {
        EnsureEventSystem();
    }

    private void Start()
    {
        dayManager = GameDayManager.Instance != null
            ? GameDayManager.Instance
            : FindAnyObjectByType<GameDayManager>();

        uiCanvas = FindAnyObjectByType<Canvas>();

        FixStampButtons();
        BuildEndDayButton();
        FixScoreLayout();
        BuildHud();
        BuildToast();

        if (dayManager != null)
        {
            dayManager.OnDayChanged += HandleDayChanged;
            dayManager.OnQuotaChanged += HandleQuotaChanged;
            dayManager.OnSpawnQuotaChanged += HandleSpawnQuotaChanged;
            dayManager.OnDayEndFailed += HandleDayEndFailed;
            dayManager.OnDayEnded += HandleDayEnded;
        }

        RefreshHud();
        Debug.Log("[DemoLoopBootstrap] Demo loop ready");
    }

    private void OnDestroy()
    {
        if (dayManager != null)
        {
            dayManager.OnDayChanged -= HandleDayChanged;
            dayManager.OnQuotaChanged -= HandleQuotaChanged;
            dayManager.OnSpawnQuotaChanged -= HandleSpawnQuotaChanged;
            dayManager.OnDayEndFailed -= HandleDayEndFailed;
            dayManager.OnDayEnded -= HandleDayEnded;
        }
    }

    private void Update()
    {
        if (toastRoot != null && toastRoot.activeSelf && Time.unscaledTime >= toastHideAt)
        {
            toastRoot.SetActive(false);
        }
    }

    private void EnsureEventSystem()
    {
        EventSystem existing = FindAnyObjectByType<EventSystem>();
        if (existing == null)
        {
            GameObject go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
            Debug.Log("[DemoLoopBootstrap] EventSystem created (scene had none, UI was not clickable)");
            return;
        }

        if (existing.GetComponent<BaseInputModule>() == null)
        {
            existing.gameObject.AddComponent<StandaloneInputModule>();
            Debug.Log("[DemoLoopBootstrap] Added BaseInputModule to existing EventSystem");
        }
    }

    private void FixStampButtons()
    {
        StampUIController stamp = StampUIController.Instance != null
            ? StampUIController.Instance
            : FindAnyObjectByType<StampUIController>();

        GameObject panel = stamp != null ? stamp.stampMenuPanel : null;
        if (panel == null)
        {
            panel = FindInactiveByName("Image");
        }
        if (panel == null)
        {
            Debug.LogWarning("[DemoLoopBootstrap] Stamp Menu Panel not found, skipping stamp button fix");
            return;
        }

        Button[] buttons = panel.GetComponentsInChildren<Button>(true);
        foreach (Button b in buttons)
        {
            if (b == null) continue;
            TextMeshProUGUI label = b.GetComponentInChildren<TextMeshProUGUI>(true);
            string hint = label != null ? label.text : b.gameObject.name;
            bool approve = ContainsWord(hint, "accept") || ContainsWord(b.gameObject.name, "accept");

            b.onClick = new Button.ButtonClickedEvent();

            if (approve)
            {
                if (label != null) label.text = "APPROVE";
                b.onClick.AddListener(OnApproveClicked);
            }
            else
            {
                if (label != null) label.text = "REJECT";
                b.onClick.AddListener(OnRejectClicked);
            }
        }

        FixStampLayout(panel);
    }

    private void FixStampLayout(GameObject panel)
    {
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            panelRect.sizeDelta = new Vector2(460f, 190f);
        }

        Button[] buttons = panel.GetComponentsInChildren<Button>(true);
        foreach (Button b in buttons)
        {
            if (b == null) continue;
            RectTransform rt = b.GetComponent<RectTransform>();
            if (rt == null) continue;

            TextMeshProUGUI label = b.GetComponentInChildren<TextMeshProUGUI>(true);
            string hint = label != null ? label.text : b.gameObject.name;
            bool approve = ContainsWord(hint, "approve") || ContainsWord(b.gameObject.name, "accept");

            rt.anchoredPosition = approve ? new Vector2(-115f, -46f) : new Vector2(115f, -46f);
            rt.sizeDelta = new Vector2(200f, 60f);

            if (label != null)
            {
                RectTransform lrt = label.rectTransform;
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.offsetMin = Vector2.zero;
                lrt.offsetMax = Vector2.zero;
                label.fontSize = 30f;
                label.alignment = TextAlignmentOptions.Center;
            }
        }

        bool hasTitle = false;
        foreach (TMP_Text t in panel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t != null && ContainsWord(t.text, "inspect")) hasTitle = true;
        }
        if (!hasTitle)
        {
            TMP_Text title = CreateText(panel.transform, "StampTitle", "INSPECT ITEM", 28f,
                TextAlignmentOptions.Center, new Color(0.95f, 0.95f, 0.95f, 1f));
            RectTransform tr = title.rectTransform;
            tr.anchorMin = new Vector2(0.5f, 0.5f);
            tr.anchorMax = new Vector2(0.5f, 0.5f);
            tr.pivot = new Vector2(0.5f, 0.5f);
            tr.anchoredPosition = new Vector2(0f, 62f);
            tr.sizeDelta = new Vector2(420f, 44f);
        }
    }

    private void OnApproveClicked()
    {
        StampUIController c = StampUIController.Instance;
        if (c != null) c.OnClickApprove();
    }

    private void OnRejectClicked()
    {
        StampUIController c = StampUIController.Instance;
        if (c != null) c.OnClickReject();
    }

    private void BuildEndDayButton()
    {
        if (uiCanvas == null) return;

        if (uiCanvas.transform.Find("EndDayButton") != null) return;

        Button btn = CreateButton(uiCanvas.transform, "EndDayButton", "END DAY",
            new Vector2(0f, 0f), new Vector2(230f, 60f), OnEndDayClicked);
        RectTransform rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-40f, -40f);
        rt.sizeDelta = new Vector2(230f, 60f);

        Image bg = btn.GetComponent<Image>();
        if (bg != null) bg.color = new Color(0.75f, 0.16f, 0.16f, 0.95f);
    }

    private void OnEndDayClicked()
    {
        GameDayManager d = GameDayManager.Instance;
        if (d != null) d.EndDay();
    }

    private void FixScoreLayout()
    {
        if (uiCanvas == null) return;

        Transform score = uiCanvas.transform.Find("Score");
        if (score != null)
        {
            RectTransform rt = score.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.zero;
                rt.pivot = Vector2.zero;
                rt.anchoredPosition = new Vector2(24f, 24f);
            }
        }

        Transform reject = uiCanvas.transform.Find("Reject");
        if (reject != null)
        {
            RectTransform rt = reject.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(1f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(1f, 0f);
                rt.anchoredPosition = new Vector2(-24f, 24f);
            }
        }
    }

    private void BuildHud()
    {
        if (uiCanvas == null) return;

        GameObject root = new GameObject("DemoHud", typeof(RectTransform));
        root.transform.SetParent(uiCanvas.transform, false);
        RectTransform rt = (RectTransform)root.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(24f, -24f);
        rt.sizeDelta = new Vector2(380f, 148f);

        Image bg = root.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);
        bg.raycastTarget = false;

        dayLabel = CreateLine(root.transform, "DayLine", 30f, new Color(1f, 0.85f, 0.3f, 1f), -16f);
        openLabel = CreateLine(root.transform, "OpenLine", 23f, new Color(0.75f, 0.9f, 1f, 1f), -60f);
        spawnLabel = CreateLine(root.transform, "SpawnLine", 23f, new Color(0.75f, 1f, 0.8f, 1f), -102f);
    }

    private TMP_Text CreateLine(Transform parent, string name, float size, Color color, float y)
    {
        TMP_Text t = CreateText(parent, name, string.Empty, size, TextAlignmentOptions.Left, color);
        RectTransform rt = t.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(22f, y);
        rt.sizeDelta = new Vector2(340f, 40f);
        t.enableWordWrapping = false;
        return t;
    }

    private void BuildToast()
    {
        if (uiCanvas == null) return;

        toastRoot = new GameObject("DemoToast", typeof(RectTransform));
        toastRoot.transform.SetParent(uiCanvas.transform, false);
        RectTransform rt = (RectTransform)toastRoot.transform;
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 130f);
        rt.sizeDelta = new Vector2(1100f, 92f);

        Image bg = toastRoot.AddComponent<Image>();
        bg.color = new Color(0.62f, 0.07f, 0.07f, 0.94f);
        bg.raycastTarget = false;

        toastLabel = CreateText(toastRoot.transform, "ToastText", string.Empty, 24f,
            TextAlignmentOptions.Center, Color.white);
        RectTransform trt = toastLabel.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(24f, 8f);
        trt.offsetMax = new Vector2(-24f, -8f);
        toastLabel.enableWordWrapping = true;

        toastRoot.SetActive(false);
    }

    private void ShowToast(string message)
    {
        if (toastRoot == null || toastLabel == null) return;
        toastLabel.text = message;
        toastRoot.SetActive(true);
        toastHideAt = Time.unscaledTime + 5f;
    }

    private void BuildSummary()
    {
        if (summaryRoot != null || uiCanvas == null) return;

        summaryRoot = new GameObject("DemoSummary", typeof(RectTransform));
        summaryRoot.transform.SetParent(uiCanvas.transform, false);
        RectTransform rt = (RectTransform)summaryRoot.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image bg = summaryRoot.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.03f, 0.05f, 0.94f);
        bg.raycastTarget = true;

        summaryTitle = CreateText(summaryRoot.transform, "SummaryTitle", "DAY COMPLETE", 58f,
            TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.3f, 1f));
        Place(summaryTitle.rectTransform, new Vector2(0f, 190f), new Vector2(1000f, 90f));

        summaryBody = CreateText(summaryRoot.transform, "SummaryBody", string.Empty, 34f,
            TextAlignmentOptions.Center, Color.white);
        Place(summaryBody.rectTransform, new Vector2(0f, 70f), new Vector2(1000f, 180f));

        CreateButton(summaryRoot.transform, "PlayAgainButton", "PLAY AGAIN",
            new Vector2(-170f, -80f), new Vector2(280f, 70f), OnPlayAgain);
        CreateButton(summaryRoot.transform, "QuitButton", "QUIT",
            new Vector2(170f, -80f), new Vector2(280f, 70f), OnQuit);

        summaryRoot.SetActive(false);
    }

    private void ShowSummary(int completedDay)
    {
        BuildSummary();
        if (summaryRoot == null) return;

        FreezePlayer();

        if (summaryTitle != null) summaryTitle.text = "DAY " + completedDay + " COMPLETE";

        int score = ScoreManager.Instance != null ? ScoreManager.Instance.totalScore : 0;
        int rejected = ScoreManager.Instance != null ? ScoreManager.Instance.rejectedPackagesCount : 0;

        if (summaryBody != null)
        {
            summaryBody.text = "SCORE: " + score + "\nREJECTED: " + rejected;
        }

        summaryRoot.SetActive(true);
        Debug.Log("[DemoLoopBootstrap] Demo day " + completedDay + " finished | SCORE " + score);
    }

    private void FreezePlayer()
    {
        StampUIController stamp = StampUIController.Instance;
        if (stamp != null) stamp.CloseStampMenu();

        GameDayManager d = GameDayManager.Instance;
        if (d != null) d.enabled = false;

        string[] playerTypes =
        {
            "PlayerController", "PlayerPickupSystem", "PlayerInteractor",
            "InspectionTablet", "UVFlashlightController", "CrosshairUI"
        };

        MonoBehaviour[] all = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude);
        foreach (MonoBehaviour mb in all)
        {
            if (mb == null || mb == this) continue;
            string n = mb.GetType().Name;
            foreach (string target in playerTypes)
            {
                if (n == target)
                {
                    mb.enabled = false;
                    break;
                }
            }
        }

        Rigidbody[] bodies = Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude);
        foreach (Rigidbody body in bodies)
        {
            if (body == null) continue;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        if (uiCanvas != null)
        {
            Transform crosshair = uiCanvas.transform.Find("Crosshair");
            if (crosshair != null) crosshair.gameObject.SetActive(false);
            Transform prompt = uiCanvas.transform.Find("PrompText");
            if (prompt != null) prompt.gameObject.SetActive(false);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnPlayAgain()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void HandleDayChanged(int newDay)
    {
        RefreshHud();
    }

    private void HandleQuotaChanged(int remaining)
    {
        RefreshHud();
    }

    private void HandleSpawnQuotaChanged(int remaining, int total)
    {
        RefreshHud();
    }

    private void HandleDayEndFailed(string reason)
    {
        ShowToast(reason);
    }

    private void HandleDayEnded(int completedDay)
    {
        if (demoFinished) return;
        if (completedDay >= demoDayLimit)
        {
            demoFinished = true;
            ShowSummary(completedDay);
        }
    }

    private void RefreshHud()
    {
        if (dayManager == null)
        {
            dayManager = GameDayManager.Instance;
            if (dayManager == null) return;
        }

        if (dayLabel != null) dayLabel.text = "DAY " + dayManager.currentDay;
        if (openLabel != null)
        {
            openLabel.text = "OPEN QUOTA   " + dayManager.RemainingQuotaToday + "/" + dayManager.dailyOpenQuota;
        }
        if (spawnLabel != null)
        {
            spawnLabel.text = "SPAWN QUOTA   " + dayManager.RemainingSpawnQuotaToday + "/" + dayManager.TotalSpawnQuotaToday;
        }
    }

    private Button CreateButton(Transform parent, string name, string label, Vector2 position,
        Vector2 size, UnityEngine.Events.UnityAction action)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Image bg = go.AddComponent<Image>();
        bg.color = new Color(0.16f, 0.18f, 0.22f, 0.98f);

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.onClick = new Button.ButtonClickedEvent();
        if (action != null) btn.onClick.AddListener(action);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        TMP_Text text = CreateText(go.transform, "Label", label, 30f,
            TextAlignmentOptions.Center, Color.white);
        RectTransform trt = text.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        text.enableWordWrapping = false;

        return btn;
    }

    private TMP_Text CreateText(Transform parent, string name, string content, float size,
        TextAlignmentOptions alignment, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = size;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.raycastTarget = false;

        TMP_FontAsset font = null;
        try
        {
            font = TMP_Settings.defaultFontAsset;
        }
        catch (System.Exception)
        {
        }

        if (font == null)
        {
            TextMeshProUGUI any = FindAnyObjectByType<TextMeshProUGUI>();
            if (any != null) font = any.font;
        }

        if (font != null) tmp.font = font;

        return tmp;
    }

    private void Place(RectTransform rt, Vector2 position, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    private GameObject FindInactiveByName(string name)
    {
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        foreach (Transform t in all)
        {
            if (t != null && t.name == name) return t.gameObject;
        }
        return null;
    }

    private static bool ContainsWord(string source, string word)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(word)) return false;
        return source.IndexOf(word, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

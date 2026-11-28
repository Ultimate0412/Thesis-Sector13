using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// ระบบจัดการหน้าต่าง Game Over เมื่อผู้เล่นเสียชีวิต
/// แสดงข้อความเตือนเด่นชัด พร้อมปุ่มให้เลือก "เริ่มวันใหม่ (Restart Day)" หรือ "เกิดใหม่ (Respawn)"
/// </summary>
public class GameOverUI : MonoBehaviour
{
    public static GameOverUI Instance { get; private set; }

    [Header("Player Reference")]
    public PlayerStats playerStats;

    [Header("UI References")]
    [Tooltip("Panel หน้าต่าง Game Over ทั้งหมด")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI subtitleText;
    public TextMeshProUGUI infoText;
    public Button restartDayButton;
    public Button respawnButton;
    public Button mainMenuButton;

    public bool IsGameOverOpen => gameOverPanel != null && gameOverPanel.activeSelf;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }

        EnsureUIElements();
    }

    private void Start()
    {
        if (playerStats == null)
        {
            playerStats = FindFirstObjectByType<PlayerStats>();
        }

        if (playerStats != null)
        {
            playerStats.OnPlayerDeath += HandlePlayerDeath;
            playerStats.OnPlayerRespawned += HandlePlayerRespawned;
        }

        // Panel ที่อยู่ใน scene ถูกผูกไว้แล้ว จึงไม่ได้ผ่าน EnsureUIElements()
        // ต้องหาปุ่มและผูก onClick เองทุกครั้ง ไม่งั้นปุ่มจะกดแล้วไม่เกิดอะไรขึ้น
        ResolvePanelButtons();
        WireButtons();

        // ซ่อนหน้าต่างเป็นค่าเริ่มต้น
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }

    private void ResolvePanelButtons()
    {
        if (gameOverPanel == null) return;

        Button[] buttons = gameOverPanel.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            switch (button.name)
            {
                case "RestartButton":
                case "RestartDayButton":
                    restartDayButton = button;
                    break;
                case "RespawnButton":
                    respawnButton = button;
                    break;
                case "ReturnButton":
                case "MainMenuButton":
                    mainMenuButton = button;
                    break;
            }
        }
    }

    private void WireButtons()
    {
        BindButton(restartDayButton, OnClickRestartDay);
        BindButton(respawnButton, OnClickRespawn);
        BindButton(mainMenuButton, OnClickReturnToMainMenu);
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private void OnDestroy()
    {
        if (playerStats != null)
        {
            playerStats.OnPlayerDeath -= HandlePlayerDeath;
            playerStats.OnPlayerRespawned -= HandlePlayerRespawned;
        }
    }

    private void HandlePlayerDeath()
    {
        ShowGameOver();
    }

    private void HandlePlayerRespawned()
    {
        HideGameOver();
    }

    public void ShowGameOver()
    {
        if (gameOverPanel != null)
        {
            RefreshInfoLine();
            gameOverPanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    /// <summary>
    /// The death screen used to show whatever placeholder text was authored in the
    /// scene ("DAY 1 · 0 PTS"). Fill it with the live run instead.
    /// </summary>
    private void RefreshInfoLine()
    {
        if (infoText == null) return;
        int day = GameDayManager.Instance != null ? GameDayManager.Instance.currentDay : 1;
        int pts = ScoreManager.Instance != null ? ScoreManager.Instance.totalScore : 0;
        infoText.text = $"DAY {day}  ·  {pts} PTS";
    }

    public void HideGameOver()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }

    public void OnClickRestartDay()
    {
        Debug.Log("<color=#FFCC00>[GameOverUI]</color> ผู้เล่นเลือกเริ่มวันใหม่ (Restart Day)");
        HideGameOver();

        // โหลดวันปัจจุบันใหม่ตั้งแต่ต้น (ใช้ autosave ต้นวันถ้ามี)
        // เพื่อให้ของใน Storage/คะแนน/โควต้า กลับไปสถานะเริ่มวัน
        SaveSystem.RestartCurrentDay();
    }

    public void OnClickRespawn()
    {
        Debug.Log("<color=#55FF88>[GameOverUI]</color> ผู้เล่นเลือกเกิดใหม่ (Respawn)");
        HideGameOver();
        Time.timeScale = 1f;

        if (playerStats != null)
        {
            playerStats.Respawn();
        }
    }

    public void OnClickReturnToMainMenu()
    {
        Debug.Log("<color=#FFCC00>[GameOverUI]</color> ผู้เล่นเลือกกลับสู่ Main Menu");
        HideGameOver();

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SceneManager.LoadScene("MainMenu");
    }

    /// <summary>
    /// ตรวจสอบและสร้างหน้าต่าง GameOver UI ให้อัตโนมัติหากยังไม่ได้ผูกใน Inspector
    /// </summary>
    private void EnsureUIElements()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        if (gameOverPanel == null)
        {
            Transform existing = canvas.transform.Find("GameOverPanel");
            if (existing != null)
            {
                gameOverPanel = existing.gameObject;
            }
            else
            {
                // สร้าง Panel หลักครอบทั้งจอ
                gameOverPanel = new GameObject("GameOverPanel");
                gameOverPanel.transform.SetParent(canvas.transform, false);

                RectTransform rect = gameOverPanel.AddComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = Vector2.zero;

                Image bg = gameOverPanel.AddComponent<Image>();
                bg.color = new Color(0.04f, 0.02f, 0.03f, 0.92f);
                bg.raycastTarget = true; // บล็อกคลิกเมาส์ทะลุ

                // หัวข้อ YOU DIED
                GameObject titleObj = new GameObject("TitleText");
                titleObj.transform.SetParent(gameOverPanel.transform, false);
                RectTransform titleRect = titleObj.AddComponent<RectTransform>();
                titleRect.anchorMin = new Vector2(0.5f, 0.5f);
                titleRect.anchorMax = new Vector2(0.5f, 0.5f);
                titleRect.pivot = new Vector2(0.5f, 0.5f);
                titleRect.anchoredPosition = new Vector2(0f, 75f);
                titleRect.sizeDelta = new Vector2(600f, 90f);

                titleText = titleObj.AddComponent<TextMeshProUGUI>();
                titleText.text = "YOU DIED";
                titleText.fontSize = 64f;
                titleText.fontStyle = FontStyles.Bold;
                titleText.alignment = TextAlignmentOptions.Center;
                titleText.color = new Color(1f, 0.15f, 0.15f);

                // ข้อความย่อย
                GameObject subObj = new GameObject("SubtitleText");
                subObj.transform.SetParent(gameOverPanel.transform, false);
                RectTransform subRect = subObj.AddComponent<RectTransform>();
                subRect.anchorMin = new Vector2(0.5f, 0.5f);
                subRect.anchorMax = new Vector2(0.5f, 0.5f);
                subRect.pivot = new Vector2(0.5f, 0.5f);
                subRect.anchoredPosition = new Vector2(0f, 15f);
                subRect.sizeDelta = new Vector2(600f, 40f);

                subtitleText = subObj.AddComponent<TextMeshProUGUI>();
                subtitleText.text = "SECURITY COMPROMISED - SECTOR 13";
                subtitleText.fontSize = 18f;
                subtitleText.alignment = TextAlignmentOptions.Center;
                subtitleText.color = new Color(0.7f, 0.7f, 0.75f);

                // ปุ่ม Restart Day
                restartDayButton = CreateButton(gameOverPanel.transform, "RestartDayButton", "RESTART DAY", new Vector2(-110f, -65f), new Color(0.6f, 0.2f, 0.2f));
                restartDayButton.onClick.AddListener(OnClickRestartDay);

                // ปุ่ม Respawn
                respawnButton = CreateButton(gameOverPanel.transform, "RespawnButton", "RESPAWN", new Vector2(110f, -65f), new Color(0.2f, 0.5f, 0.7f));
                respawnButton.onClick.AddListener(OnClickRespawn);

                // ปุ่มกลับ Main Menu
                mainMenuButton = CreateButton(gameOverPanel.transform, "ReturnButton", "MAIN MENU", new Vector2(0f, -125f), new Color(0.25f, 0.25f, 0.3f));
                mainMenuButton.onClick.AddListener(OnClickReturnToMainMenu);

                gameOverPanel.SetActive(false);
            }
        }
    }

    private Button CreateButton(Transform parent, string name, string label, Vector2 pos, Color btnColor)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(190f, 48f);

        Image img = btnObj.AddComponent<Image>();
        img.color = btnColor;

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.normalColor = btnColor;
        colors.highlightedColor = btnColor * 1.3f;
        colors.pressedColor = btnColor * 0.7f;
        btn.colors = colors;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 18f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        return btn;
    }
}

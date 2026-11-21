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
    public Button restartDayButton;
    public Button respawnButton;

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

        // ซ่อนหน้าต่างเป็นค่าเริ่มต้น
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
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
            gameOverPanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
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

        // 1. ล้างศัตรูทั้งหมดที่อาจค้างอยู่ในฉาก
        if (EnemySpawner.Instance != null)
        {
            EnemySpawner.Instance.ClearAllEnemies();
        }

        // 2. ฟื้นคืนชีพผู้เล่น
        if (playerStats != null)
        {
            playerStats.Respawn();
        }

        // 3. รีเซ็ตโควต้าและสถานะวัน
        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.ResetDailyQuota();
        }
    }

    public void OnClickRespawn()
    {
        Debug.Log("<color=#55FF88>[GameOverUI]</color> ผู้เล่นเลือกเกิดใหม่ (Respawn)");
        HideGameOver();

        if (playerStats != null)
        {
            playerStats.Respawn();
        }
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

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ตัวควบคุม HUD แสดงผลการแจ้งเตือนอีเวนต์ด้านบนหน้าจอ (Top Sci-Fi Event Banner)
/// แสดงชื่ออีเวนต์, คำอธิบาย และเวลานับถอยหลังแบบเรียลไทม์ พร้อมแอนิเมชัน Fade In/Out
/// มีระบบ Auto-Setup สร้าง UI บน Canvas ให้อัตโนมัติหากยังไม่มีในฉาก
/// </summary>
public class EventUIController : MonoBehaviour
{
    public static EventUIController Instance { get; private set; }

    [Header("UI References")]
    public GameObject bannerPanel;
    public Image bannerBackground;
    public Image bannerAccentBar;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI timerText;

    private CanvasGroup canvasGroup;
    private GameEventBase currentEvent;
    private Coroutine displayCoroutine;

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

        EnsureUIHierarchy();
    }

    private void Update()
    {
        // อัปเดตเวลานับถอยหลังแบบเรียลไทม์
        if (currentEvent != null && currentEvent.IsActive && timerText != null)
        {
            if (currentEvent.duration > 0f)
            {
                int seconds = Mathf.CeilToInt(currentEvent.RemainingTime);
                timerText.text = $"⏱️ {seconds:D2}s";
            }
            else
            {
                timerText.text = "⏱️ ACTIVE";
            }
        }
    }

    /// <summary>
    /// แสดงแบนเนอร์แจ้งเตือนอีเวนต์
    /// </summary>
    public void ShowEventBanner(GameEventBase ev)
    {
        if (ev == null) return;

        currentEvent = ev;
        EnsureUIHierarchy();

        if (titleText != null)
        {
            titleText.text = $"⚠️ {ev.eventName.ToUpper()} ⚠️";
            titleText.color = ev.eventColor;
        }

        if (descriptionText != null)
        {
            descriptionText.text = ev.eventDescription;
        }

        if (bannerAccentBar != null)
        {
            bannerAccentBar.color = ev.eventColor;
        }

        if (displayCoroutine != null) StopCoroutine(displayCoroutine);
        displayCoroutine = StartCoroutine(FadeBanner(true, 0.3f));
    }

    /// <summary>
    /// ซ่อนแบนเนอร์พร้อมแสดงข้อความแจ้งเตือนว่าอีเวนต์คลี่คลายแล้ว
    /// </summary>
    public void HideEventBanner(GameEventBase ev)
    {
        if (displayCoroutine != null) StopCoroutine(displayCoroutine);
        displayCoroutine = StartCoroutine(ShowResolvedAndFadeOut(ev));
    }

    private IEnumerator ShowResolvedAndFadeOut(GameEventBase ev)
    {
        if (titleText != null)
        {
            titleText.text = "✅ ANOMALY RESOLVED: GRAVITY RESTORED";
            titleText.color = new Color(0.2f, 1.0f, 0.4f); // Neon Green
        }

        if (timerText != null)
        {
            timerText.text = "NORMALIZED";
        }

        if (bannerAccentBar != null)
        {
            bannerAccentBar.color = new Color(0.2f, 1.0f, 0.4f);
        }

        // ค้างข้อความไว้ให้ผู้เล่นเห็น 2.5 วินาที
        yield return new WaitForSeconds(2.5f);

        // ค่อยๆ จางหายไป
        yield return FadeBanner(false, 0.5f);
        currentEvent = null;
    }

    private IEnumerator FadeBanner(bool fadeIn, float duration)
    {
        if (canvasGroup == null) yield break;

        bannerPanel.SetActive(true);
        float startAlpha = canvasGroup.alpha;
        float targetAlpha = fadeIn ? 1f : 0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        if (!fadeIn)
        {
            bannerPanel.SetActive(false);
        }
    }

    /// <summary>
    /// ตรวจสอบและสร้างโครงสร้าง UI บน Canvas อัตโนมัติหากยังไม่มี
    /// </summary>
    public void EnsureUIHierarchy()
    {
        if (bannerPanel != null)
        {
            if (canvasGroup == null) canvasGroup = bannerPanel.GetComponent<CanvasGroup>();
            return;
        }

        // ค้นหา Canvas ในฉาก
        Canvas targetCanvas = GetComponentInParent<Canvas>();
        if (targetCanvas == null) targetCanvas = FindFirstObjectByType<Canvas>();
        if (targetCanvas == null)
        {
            GameObject canvasObj = new GameObject("EventCanvas");
            targetCanvas = canvasObj.AddComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // 1. สร้าง Banner Panel (กึ่งกลางจอด้านบน)
        GameObject panelObj = new GameObject("EventBannerPanel");
        panelObj.transform.SetParent(targetCanvas.transform, false);

        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.sizeDelta = new Vector2(620f, 85f);
        panelRect.anchoredPosition = new Vector2(0f, -15f);

        bannerBackground = panelObj.AddComponent<Image>();
        bannerBackground.color = new Color(0.11764706f, 0.101960786f, 0.043137256f, 0.92f); // #1e1a0b // Deep Sci-Fi dark blue
        bannerBackground.raycastTarget = false;

        canvasGroup = panelObj.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        bannerPanel = panelObj;

        // 2. แถบสี Accent Bar ด้านล่าง
        GameObject barObj = new GameObject("AccentBar");
        barObj.transform.SetParent(panelObj.transform, false);
        RectTransform barRect = barObj.AddComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.sizeDelta = new Vector2(0f, 4f);
        barRect.anchoredPosition = Vector2.zero;

        bannerAccentBar = barObj.AddComponent<Image>();
        bannerAccentBar.color = new Color(1f, 0.87058824f, 0.34901962f, 1.0f); // #ffde59
        bannerAccentBar.raycastTarget = false;

        // 3. Title Text
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(panelObj.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(-40f, 32f);
        titleRect.anchoredPosition = new Vector2(0f, -8f);

        titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.fontSize = 20f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = new Color(1f, 0.87058824f, 0.34901962f, 1.0f); // #ffde59
        titleText.text = "⚠️ ANOMALY DETECTED ⚠️";
        titleText.raycastTarget = false;

        // 4. Description Text
        GameObject descObj = new GameObject("DescriptionText");
        descObj.transform.SetParent(panelObj.transform, false);
        RectTransform descRect = descObj.AddComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0f, 0f);
        descRect.anchorMax = new Vector2(0.75f, 1f);
        descRect.pivot = new Vector2(0f, 0.5f);
        descRect.sizeDelta = new Vector2(-20f, -40f);
        descRect.anchoredPosition = new Vector2(25f, -10f);

        descriptionText = descObj.AddComponent<TextMeshProUGUI>();
        descriptionText.fontSize = 13f;
        descriptionText.alignment = TextAlignmentOptions.Left;
        descriptionText.color = new Color(0.85f, 0.9f, 0.95f, 0.9f);
        descriptionText.text = "Sector 13 gravity offline. Objects and entities are floating.";
        descriptionText.raycastTarget = false;

        // 5. Timer Text (มุมขวาล่าง)
        GameObject timerObj = new GameObject("TimerText");
        timerObj.transform.SetParent(panelObj.transform, false);
        RectTransform timerRect = timerObj.AddComponent<RectTransform>();
        timerRect.anchorMin = new Vector2(0.75f, 0f);
        timerRect.anchorMax = new Vector2(1f, 1f);
        timerRect.pivot = new Vector2(1f, 0.5f);
        timerRect.sizeDelta = new Vector2(-20f, -40f);
        timerRect.anchoredPosition = new Vector2(-20f, -10f);

        timerText = timerObj.AddComponent<TextMeshProUGUI>();
        timerText.fontSize = 18f;
        timerText.fontStyle = FontStyles.Bold;
        timerText.alignment = TextAlignmentOptions.Right;
        timerText.color = new Color(1.0f, 0.85f, 0.2f); // Golden yellow timer
        timerText.text = "⏱️ 25s";
        timerText.raycastTarget = false;

        panelObj.SetActive(false);
    }
}

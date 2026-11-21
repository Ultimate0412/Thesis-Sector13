using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Retro CRT segmented health bar. Keeps PlayerStats events, damage flash and low HP pulse.
/// Put this on the same GameObject as the original PlayerHealthUI script.
/// </summary>
public class PlayerHealthUI : MonoBehaviour
{
    public static PlayerHealthUI Instance { get; private set; }

    [Header("Player Reference")]
    public PlayerStats playerStats;

    [Header("Existing UI References (optional)")]
    public Image healthBarFill;
    public TextMeshProUGUI healthText;
    public RectTransform healthBarContainer;

    [Header("CRT Segmented Bar")]
    [Min(1)] public int segmentCount = 12;
    public Color crtYellow = new Color(1f, 0.77f, 0.17f, 1f);
    public Color emptySegmentColor = new Color(0.13f, 0.10f, 0.055f, 0.8f);
    public bool changeColorOnLowHP = false;
    public Color mediumHealthColor = new Color(1f, 0.55f, 0.12f, 1f);
    public Color lowHealthColor = new Color(1f, 0.22f, 0.16f, 1f);
    [Min(0)] public float segmentGap = 2f;

    [Header("Damage Flash Settings (Vignette)")]
    public Image damageFlashOverlay;
    public Color flashColor = new Color(1f, 0f, 0f, 0.45f);
    public float flashFadeDuration = 0.35f;

    [Header("Low Health Pulse")]
    public bool enableLowHealthPulse = true;
    public float lowHealthThreshold = 25f;
    public float pulseSpeed = 3.5f;

    [Header("Smooth Bar Fill")]
    public bool smoothFill = true;
    public float fillLerpSpeed = 8f;

    private float targetFillAmount = 1f;
    private float displayedFillAmount = 1f;
    private Coroutine flashCoroutine;
    private Image[] segmentImages;
    private Image[] frameImages;
    private TextMeshProUGUI crtLabel;
    private Sprite whiteSprite;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        EnsureUIElements();
        BuildCRTHealthBar();
    }

    private void Start()
    {
        if (playerStats == null)
            playerStats = FindFirstObjectByType<PlayerStats>();

        if (playerStats != null)
        {
            playerStats.OnHPChanged += HandleHPChanged;
            playerStats.OnDamageTaken += HandleDamageTaken;
            playerStats.OnPlayerRespawned += HandleRespawned;
            HandleHPChanged(playerStats.CurrentHealth, playerStats.maxHealth);
            displayedFillAmount = targetFillAmount;
            UpdateSegments();
        }
        else
        {
            Debug.LogWarning("PlayerHealthUI: ไม่พบ PlayerStats ใน Scene", this);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (playerStats != null)
        {
            playerStats.OnHPChanged -= HandleHPChanged;
            playerStats.OnDamageTaken -= HandleDamageTaken;
            playerStats.OnPlayerRespawned -= HandleRespawned;
        }
    }

    private void Update()
    {
        if (smoothFill)
            displayedFillAmount = Mathf.MoveTowards(displayedFillAmount, targetFillAmount,
                Mathf.Max(0.01f, fillLerpSpeed) * Time.deltaTime * 0.12f);
        else
            displayedFillAmount = targetFillAmount;

        UpdateSegments();

        if (enableLowHealthPulse && playerStats != null && !playerStats.IsDead &&
            playerStats.CurrentHealth <= lowHealthThreshold && damageFlashOverlay != null && flashCoroutine == null)
        {
            Color c = flashColor;
            c.a = Mathf.PingPong(Time.time * pulseSpeed, 0.28f);
            damageFlashOverlay.color = c;
        }
        else if (flashCoroutine == null && damageFlashOverlay != null)
        {
            Color c = damageFlashOverlay.color;
            if (c.a != 0f) { c.a = 0f; damageFlashOverlay.color = c; }
        }
    }

    private void HandleHPChanged(float current, float max)
    {
        targetFillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        if (healthText != null)
            healthText.text = $"HP {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }

    private void UpdateSegments()
    {
        if (segmentImages == null) return;
        Color active = crtYellow;
        if (changeColorOnLowHP)
        {
            if (targetFillAmount <= 0.25f) active = lowHealthColor;
            else if (targetFillAmount <= 0.5f) active = mediumHealthColor;
        }
        for (int i = 0; i < segmentImages.Length; i++)
        {
            float amount = Mathf.Clamp01(displayedFillAmount * segmentImages.Length - i);
            // Use an Image fill for a partially filled segment.
            segmentImages[i].fillAmount = amount;
            segmentImages[i].color = active;
        }
    }

    private void HandleDamageTaken(float amount)
    {
        if (damageFlashOverlay == null) return;
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(DamageFlashRoutine());
    }

    private void HandleRespawned()
    {
        if (flashCoroutine != null) { StopCoroutine(flashCoroutine); flashCoroutine = null; }
        if (damageFlashOverlay != null)
        {
            Color c = damageFlashOverlay.color; c.a = 0f; damageFlashOverlay.color = c;
        }
        if (playerStats != null) HandleHPChanged(playerStats.CurrentHealth, playerStats.maxHealth);
        displayedFillAmount = targetFillAmount;
        UpdateSegments();
    }

    private IEnumerator DamageFlashRoutine()
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, flashFadeDuration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            Color c = flashColor;
            c.a = Mathf.Lerp(flashColor.a, 0f, Mathf.Clamp01(elapsed / duration));
            damageFlashOverlay.color = c;
            yield return null;
        }
        Color finalColor = flashColor; finalColor.a = 0f;
        damageFlashOverlay.color = finalColor;
        flashCoroutine = null;
    }

    private void EnsureUIElements()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) { Debug.LogWarning("PlayerHealthUI: ไม่พบ Canvas", this); return; }

        if (damageFlashOverlay == null)
        {
            Transform existing = canvas.transform.Find("DamageFlashOverlay");
            if (existing != null) damageFlashOverlay = existing.GetComponent<Image>();
            if (damageFlashOverlay == null)
            {
                GameObject obj = new GameObject("DamageFlashOverlay", typeof(RectTransform), typeof(Image));
                obj.transform.SetParent(canvas.transform, false);
                obj.transform.SetAsFirstSibling();
                Stretch(obj.GetComponent<RectTransform>());
                damageFlashOverlay = obj.GetComponent<Image>();
                damageFlashOverlay.sprite = CreateVignetteSprite();
                Color c = flashColor; c.a = 0f; damageFlashOverlay.color = c;
                damageFlashOverlay.raycastTarget = false;
            }
        }

        if (healthBarContainer == null)
        {
            Transform existing = canvas.transform.Find("PlayerHealthHUD");
            if (existing != null) healthBarContainer = existing.GetComponent<RectTransform>();
            if (healthBarContainer == null)
            {
                GameObject obj = new GameObject("PlayerHealthHUD", typeof(RectTransform));
                obj.transform.SetParent(canvas.transform, false);
                healthBarContainer = obj.GetComponent<RectTransform>();
                healthBarContainer.anchorMin = Vector2.zero;
                healthBarContainer.anchorMax = Vector2.zero;
                healthBarContainer.pivot = Vector2.zero;
                healthBarContainer.anchoredPosition = new Vector2(28f, 26f);
                healthBarContainer.sizeDelta = new Vector2(245f, 34f);
            }
        }
    }

    private void BuildCRTHealthBar()
    {
        if (healthBarContainer == null) return;

        // Hide the old bar graphics, but keep their GameObjects and references intact.
        foreach (string oldName in new[] { "HealthBackground", "HealthFill", "HealthText" })
        {
            Transform old = healthBarContainer.Find(oldName);
            if (old != null)
            {
                Graphic graphic = old.GetComponent<Graphic>();
                if (graphic != null) graphic.enabled = false;
            }
        }
        if (healthBarFill != null) healthBarFill.enabled = false;
        if (healthText != null) healthText.enabled = false;

        Transform prior = healthBarContainer.Find("CRTHealthBar");
        if (prior != null) Destroy(prior.gameObject);

        RectTransform root = NewRect("CRTHealthBar", healthBarContainer);
        Stretch(root);
        whiteSprite = CreateWhiteSprite();

        RectTransform labelRect = NewRect("HP_Label", root);
        SetRect(labelRect, 0f, 3f, 28f, 28f);
        crtLabel = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        crtLabel.text = "HP";
        crtLabel.fontSize = 20f;
        crtLabel.fontStyle = FontStyles.Bold;
        crtLabel.alignment = TextAlignmentOptions.MidlineLeft;
        crtLabel.color = crtYellow;
        crtLabel.raycastTarget = false;

        // Bar starts to the right of the HP label.
        float barX = 33f;
        float barY = 8f;
        float barWidth = 192f;
        float barHeight = 18f;
        float innerPadding = 5f;
        float innerX = barX + innerPadding;
        float innerY = barY + 3f;
        float innerWidth = barWidth - innerPadding * 2f;
        float innerHeight = barHeight - 6f;
        int count = Mathf.Max(1, segmentCount);
        float gap = Mathf.Clamp(segmentGap, 0f, innerWidth / (count * 2f));
        float width = (innerWidth - gap * (count - 1)) / count;
        segmentImages = new Image[count];
        for (int i = 0; i < count; i++)
        {
            float x = innerX + (width + gap) * i;
            CreateImage("EmptySegment_" + (i + 1), root, x, innerY, width, innerHeight, emptySegmentColor);
            Image fill = CreateImage("Segment_" + (i + 1), root, x, innerY, width, innerHeight, crtYellow);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            segmentImages[i] = fill;
        }

        // Outer frame: four independently rendered lines (doesn't share alpha with segments).
        frameImages = new Image[6];
        frameImages[0] = CreateImage("FrameTop", root, barX + 3, barY + barHeight, barWidth - 6, 1.5f, crtYellow);
        frameImages[1] = CreateImage("FrameBottom", root, barX + 3, barY - 1.5f, barWidth - 6, 1.5f, crtYellow);
        frameImages[2] = CreateImage("FrameLeft", root, barX, barY + 2, 1.5f, barHeight - 4, crtYellow);
        frameImages[3] = CreateImage("FrameRight", root, barX + barWidth - 1.5f, barY + 2, 1.5f, barHeight - 4, crtYellow);
        frameImages[4] = CreateImage("LeftTab", root, barX - 3, barY + 5, 2f, barHeight - 10, crtYellow);
        frameImages[5] = CreateImage("RightTab", root, barX + barWidth + 1, barY + 5, 2f, barHeight - 10, crtYellow);
    }

    private RectTransform NewRect(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj.GetComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetRect(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private Image CreateImage(string name, Transform parent, float x, float y, float w, float h, Color color)
    {
        RectTransform rect = NewRect(name, parent);
        SetRect(rect, x, y, w, h);
        Image img = rect.gameObject.AddComponent<Image>();
        img.sprite = whiteSprite;
        img.type = Image.Type.Simple;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static Sprite CreateWhiteSprite()
    {
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        texture.filterMode = FilterMode.Point;
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
    }

    private Sprite CreateVignetteSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2((size - 1) / 2f, (size - 1) / 2f);
        float maxDist = Vector2.Distance(Vector2.zero, center);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Pow(Mathf.Clamp01(dist / maxDist), 2.2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f);
    }
}

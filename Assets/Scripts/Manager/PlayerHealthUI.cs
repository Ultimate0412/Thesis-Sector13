using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ระบบจัดการ UI แสดงพลังชีวิตของผู้เล่น (Player Health Bar HUD)
/// แสดงหลอดเลือดที่มุมซ้ายล่าง, ตัวเลข HP, เอฟเฟกต์หน้าจอแดงเมื่อถูกโจมตี (Damage Flash),
/// และเอฟเฟกต์กะพริบเตือนเมื่อเลือดต่ำ (Low HP Pulse)
/// </summary>
public class PlayerHealthUI : MonoBehaviour
{
    public static PlayerHealthUI Instance { get; private set; }

    [Header("Player Reference")]
    [Tooltip("PlayerStats ของผู้เล่น (หากเว้นว่างจะค้นหาให้อัตโนมัติ)")]
    public PlayerStats playerStats;

    [Header("Health Bar UI Elements (Bottom-Left)")]
    [Tooltip("Image หลอดเลือดส่วนที่ลด/เพิ่ม (ต้องตั้ง Image Type เป็น Filled)")]
    public Image healthBarFill;
    [Tooltip("TextMeshProUGUI แสดงตัวเลข HP เช่น '100 / 100'")]
    public TextMeshProUGUI healthText;
    [Tooltip("Container สำหรับจัดตำแหน่งแถบเลือด")]
    public RectTransform healthBarContainer;

    [Header("Health Bar Colors")]
    public Color highHealthColor = new Color(0.2f, 0.9f, 0.5f, 1f);   // เขียวสดใส (>50%)
    public Color mediumHealthColor = new Color(1f, 0.75f, 0.15f, 1f); // ส้ม/เหลือง (25-50%)
    public Color lowHealthColor = new Color(1f, 0.2f, 0.2f, 1f);      // แดง (<25%)

    [Header("Damage Flash Settings (Vignette)")]
    [Tooltip("Image เต็มจอสำหรับแสดงเอฟเฟกต์แฟลชสีแดงเมื่อถูกโจมตี")]
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
    private Coroutine flashCoroutine;

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
            playerStats.OnHPChanged += HandleHPChanged;
            playerStats.OnDamageTaken += HandleDamageTaken;
            playerStats.OnPlayerRespawned += HandleRespawned;

            // อัปเดตค่าเริ่มต้นทันที
            HandleHPChanged(playerStats.CurrentHealth, playerStats.maxHealth);
        }
    }

    private void OnDestroy()
    {
        if (playerStats != null)
        {
            playerStats.OnHPChanged -= HandleHPChanged;
            playerStats.OnDamageTaken -= HandleDamageTaken;
            playerStats.OnPlayerRespawned -= HandleRespawned;
        }
    }

    private void Update()
    {
        // 1. Smooth Lerp สำหรับหลอดเลือด
        if (healthBarFill != null)
        {
            if (smoothFill)
            {
                healthBarFill.fillAmount = Mathf.Lerp(healthBarFill.fillAmount, targetFillAmount, Time.deltaTime * fillLerpSpeed);
            }
            else
            {
                healthBarFill.fillAmount = targetFillAmount;
            }
        }

        // 2. เอฟเฟกต์ Low HP Pulse กะพริบขอบจอเมื่อเลือดต่ำกว่าเกณฑ์
        if (enableLowHealthPulse && playerStats != null && !playerStats.IsDead && playerStats.CurrentHealth <= lowHealthThreshold && damageFlashOverlay != null)
        {
            if (flashCoroutine == null)
            {
                float pulseAlpha = Mathf.PingPong(Time.time * pulseSpeed, 0.28f);
                Color c = flashColor;
                c.a = pulseAlpha;
                damageFlashOverlay.color = c;
            }
        }
    }

    private void HandleHPChanged(float current, float max)
    {
        targetFillAmount = Mathf.Clamp01(current / max);

        // อัปเดตตัวเลข
        if (healthText != null)
        {
            healthText.text = $"HP {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }

        // อัปเดตสีตามระดับเลือด
        if (healthBarFill != null)
        {
            if (targetFillAmount > 0.5f)
            {
                healthBarFill.color = highHealthColor;
            }
            else if (targetFillAmount > 0.25f)
            {
                healthBarFill.color = mediumHealthColor;
            }
            else
            {
                healthBarFill.color = lowHealthColor;
            }
        }
    }

    private void HandleDamageTaken(float amount)
    {
        if (damageFlashOverlay == null) return;

        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }
        flashCoroutine = StartCoroutine(DamageFlashRoutine());
    }

    private void HandleRespawned()
    {
        if (damageFlashOverlay != null)
        {
            Color c = damageFlashOverlay.color;
            c.a = 0f;
            damageFlashOverlay.color = c;
        }
        if (playerStats != null)
        {
            HandleHPChanged(playerStats.CurrentHealth, playerStats.maxHealth);
        }
    }

    private IEnumerator DamageFlashRoutine()
    {
        float elapsed = 0f;
        while (elapsed < flashFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / flashFadeDuration;
            float alpha = Mathf.Lerp(flashColor.a, 0f, t);
            Color c = flashColor;
            c.a = alpha;
            damageFlashOverlay.color = c;
            yield return null;
        }

        Color finalC = flashColor;
        finalC.a = 0f;
        damageFlashOverlay.color = finalC;
        flashCoroutine = null;
    }

    /// <summary>
    /// ตรวจสอบและสร้าง UI Elements ที่มุมซ้ายล่างให้อัตโนมัติหากยังไม่ได้ผูกใน Inspector
    /// </summary>
    private void EnsureUIElements()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // 1. สร้าง Damage Flash Overlay เต็มจอหากยังไม่มี
        if (damageFlashOverlay == null)
        {
            Transform existingFlash = canvas.transform.Find("DamageFlashOverlay");
            if (existingFlash != null)
            {
                damageFlashOverlay = existingFlash.GetComponent<Image>();
            }
            else
            {
                GameObject flashObj = new GameObject("DamageFlashOverlay");
                flashObj.transform.SetParent(canvas.transform, false);
                flashObj.transform.SetAsFirstSibling(); // ให้อยู่ข้างหลัง UI เมนูอื่นๆ

                RectTransform rect = flashObj.AddComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = Vector2.zero;

                damageFlashOverlay = flashObj.AddComponent<Image>();
                damageFlashOverlay.sprite = CreateVignetteSprite();
                Color c = flashColor;
                c.a = 0f;
                damageFlashOverlay.color = c;
                damageFlashOverlay.raycastTarget = false;
            }
        }

        // 2. สร้าง Health Bar Container มุมซ้ายล่างหากยังไม่มี
        if (healthBarContainer == null || healthBarFill == null || healthText == null)
        {
            Transform existingHUD = canvas.transform.Find("PlayerHealthHUD");
            if (existingHUD != null)
            {
                healthBarContainer = existingHUD.GetComponent<RectTransform>();
                healthBarFill = existingHUD.Find("HealthFill")?.GetComponent<Image>();
                healthText = existingHUD.Find("HealthText")?.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                // Container หลัก (Anchor: Bottom-Left)
                GameObject hudObj = new GameObject("PlayerHealthHUD");
                hudObj.transform.SetParent(canvas.transform, false);

                healthBarContainer = hudObj.AddComponent<RectTransform>();
                healthBarContainer.anchorMin = new Vector2(0f, 0f);
                healthBarContainer.anchorMax = new Vector2(0f, 0f);
                healthBarContainer.pivot = new Vector2(0f, 0f);
                healthBarContainer.anchoredPosition = new Vector2(35f, 35f);
                healthBarContainer.sizeDelta = new Vector2(240f, 32f);

                // พื้นหลังหลอดเลือด (Dark Background Frame)
                GameObject bgObj = new GameObject("HealthBackground");
                bgObj.transform.SetParent(healthBarContainer, false);
                RectTransform bgRect = bgObj.AddComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.sizeDelta = Vector2.zero;
                Image bgImage = bgObj.AddComponent<Image>();
                bgImage.color = new Color(0.08f, 0.08f, 0.12f, 0.85f);
                bgImage.raycastTarget = false;

                // แถบหลอดเลือด (Filled Health Bar)
                GameObject fillObj = new GameObject("HealthFill");
                fillObj.transform.SetParent(healthBarContainer, false);
                RectTransform fillRect = fillObj.AddComponent<RectTransform>();
                fillRect.anchorMin = new Vector2(0.02f, 0.12f);
                fillRect.anchorMax = new Vector2(0.98f, 0.88f);
                fillRect.sizeDelta = Vector2.zero;

                healthBarFill = fillObj.AddComponent<Image>();
                healthBarFill.type = Image.Type.Filled;
                healthBarFill.fillMethod = Image.FillMethod.Horizontal;
                healthBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                healthBarFill.fillAmount = 1f;
                healthBarFill.color = highHealthColor;
                healthBarFill.raycastTarget = false;

                // ข้อความตัวเลข HP
                GameObject textObj = new GameObject("HealthText");
                textObj.transform.SetParent(healthBarContainer, false);
                RectTransform textRect = textObj.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.sizeDelta = Vector2.zero;

                healthText = textObj.AddComponent<TextMeshProUGUI>();
                healthText.fontSize = 16f;
                healthText.fontStyle = FontStyles.Bold;
                healthText.alignment = TextAlignmentOptions.Center;
                healthText.color = Color.white;
                healthText.outlineWidth = 0.25f;
                healthText.outlineColor = Color.black;
                healthText.raycastTarget = false;
                healthText.text = "HP 100 / 100";
            }
        }
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
                float normalized = Mathf.Clamp01(dist / maxDist);
                // กึ่งกลางโปร่งใส ขอบนอกทึบ
                float alpha = Mathf.Pow(normalized, 2.2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}

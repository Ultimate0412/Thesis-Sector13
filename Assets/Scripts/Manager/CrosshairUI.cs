using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CrosshairUI : MonoBehaviour
{
    public static CrosshairUI Instance { get; private set; }

    [Header("Crosshair Visuals")]
    [Tooltip("Image component สำหรับแสดงเป้าเล็ง")]
    public Image crosshairImage;

    [Tooltip("ขนาดของเป้าเล็ง (พิกเซล)")]
    public Vector2 dotSize = new Vector2(8f, 8f);

    [Tooltip("สีเป้าเล็งปกติ")]
    public Color normalColor = new Color(1f, 1f, 1f, 0.85f);

    [Tooltip("สีเป้าเล็งเมื่อเล็งโดนวัตถุที่โต้ตอบได้ (Interactable / Item)")]
    public Color hoverColor = new Color(0.2f, 1f, 0.6f, 1f);

    [Tooltip("อัตราส่วนการขยายเมื่อเล็งโดนวัตถุ")]
    public float hoverScale = 1.35f;

    [Header("Hold Progress Settings")]
    [Tooltip("Image component สำหรับแสดงหลอดโหลดการกดค้าง (Radial Fill)")]
    public Image holdProgressImage;
    public Color holdProgressColor = new Color(0.2f, 0.9f, 1f, 0.9f);
    public Vector2 holdProgressSize = new Vector2(38f, 38f);

    [Header("Interaction Prompt Text (Optional)")]
    [Tooltip("TextMeshProUGUI สำหรับแสดงข้อความแนะนำปุ่มกด (เช่น 'กด E เพื่อเปิดกล่อง')")]
    public TextMeshProUGUI promptText;

    [Header("Raycast Settings")]
    [Tooltip("ระยะยิง Raycast ตรวจจับวัตถุ")]
    public float raycastDistance = 3.5f;
    public LayerMask detectionLayers = ~0; // ทุก Layer

    private Camera playerCam;
    private RectTransform rectTransform;
    private bool isHovering = false;

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

        // ค้นหา Image และ RectTransform
        if (crosshairImage == null) crosshairImage = GetComponent<Image>();
        if (crosshairImage == null) crosshairImage = GetComponentInChildren<Image>();

        if (crosshairImage != null)
        {
            rectTransform = crosshairImage.rectTransform;
            SetupCrosshairImage();
        }

        SetupHoldProgressRing();
        SetupPromptText();

        if (promptText != null)
        {
            promptText.text = string.Empty;
        }
    }

    private void SetupPromptText()
    {
        if (promptText != null) return;

        // ค้นหาว่ามีลูกชื่อ PromptText อยู่แล้วไหม
        Transform parentTransform = transform.parent != null ? transform.parent : transform;
        Transform child = parentTransform.Find("PromptText");
        if (child != null)
        {
            promptText = child.GetComponent<TextMeshProUGUI>();
            if (promptText != null) return;
        }

        // หากยังไม่มี ให้สร้าง GameObject ขึ้นมาอัตโนมัติใต้ Canvas
        GameObject promptObj = new GameObject("PromptText");
        promptObj.transform.SetParent(parentTransform, false);

        RectTransform rect = promptObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -40f); // อยู่ใต้เป้าเล็ง 40px
        rect.sizeDelta = new Vector2(800f, 60f);

        promptText = promptObj.AddComponent<TextMeshProUGUI>();
        promptText.fontSize = 20f;
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.color = Color.white;
        promptText.raycastTarget = false;
        promptText.enableWordWrapping = false;
        promptText.outlineWidth = 0.2f;
        promptText.outlineColor = Color.black;

        promptObj.SetActive(false);
    }

    private void Start()
    {
        playerCam = Camera.main;
        if (playerCam == null)
        {
            playerCam = FindFirstObjectByType<Camera>();
        }
    }

    private void Update()
    {
        // ซ่อนเป้าเล็งเมื่อเปิดเมนูที่แสดง Cursor (เช่น เมนูปั๊มตรา)
        if (Cursor.visible)
        {
            if (crosshairImage != null && crosshairImage.enabled) crosshairImage.enabled = false;
            if (promptText != null && promptText.gameObject.activeSelf) promptText.gameObject.SetActive(false);
            return;
        }
        else
        {
            if (crosshairImage != null && !crosshairImage.enabled) crosshairImage.enabled = true;
        }

        CheckHoverInteraction();
    }

    private void SetupCrosshairImage()
    {
        if (rectTransform != null)
        {
            // จัดกึ่งกลางหน้าจอเสมอ
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = dotSize;
        }

        // หากไม่มี Sprite กำหนดไว้ ให้สร้าง Sprite วงกลมแบบ Anti-aliased อัตโนมัติ
        if (crosshairImage.sprite == null)
        {
            crosshairImage.sprite = CreateProceduralCircleSprite();
        }

        crosshairImage.color = normalColor;
        crosshairImage.raycastTarget = false; // ป้องกันการบล็อกคลิกเมาส์
    }

    private void CheckHoverInteraction()
    {
        if (playerCam == null)
        {
            playerCam = Camera.main;
            if (playerCam == null) return;
        }

        Ray ray = new Ray(playerCam.transform.position, playerCam.transform.forward);
        string currentPrompt = string.Empty;
        bool foundInteractable = false;

        if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, detectionLayers))
        {
            // 1. เช็ค IInteractable (กล่อง, จุดสปอน, จุดวาง)
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null)
            {
                currentPrompt = interactable.GetInteractPrompt();
                if (!string.IsNullOrEmpty(currentPrompt))
                {
                    foundInteractable = true;
                }
            }

            // 2. เช็ค ItemObject (สินค้าที่หยิบได้)
            if (!foundInteractable)
            {
                ItemObject item = hit.collider.GetComponentInParent<ItemObject>();
                if (item != null)
                {
                    PackageBox parentBox = item.GetComponentInParent<PackageBox>();
                    if (parentBox == null || parentBox.isOpen)
                    {
                        foundInteractable = true;
                        currentPrompt = $"[Q] Pick Up [{item.itemName}]";
                    }
                }
            }
        }

        // อัปเดตสีและขนาดของ Crosshair แบบ Smooth
        isHovering = foundInteractable;
        Color targetColor = isHovering ? hoverColor : normalColor;
        Vector3 targetScale = isHovering ? Vector3.one * hoverScale : Vector3.one;

        if (crosshairImage != null)
        {
            crosshairImage.color = Color.Lerp(crosshairImage.color, targetColor, Time.deltaTime * 15f);
            if (rectTransform != null)
            {
                rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, Time.deltaTime * 15f);
            }
        }

        // อัปเดตข้อความ Prompt
        if (promptText != null)
        {
            if (!string.IsNullOrEmpty(currentPrompt))
            {
                promptText.text = currentPrompt;
                if (!promptText.gameObject.activeSelf) promptText.gameObject.SetActive(true);
            }
            else
            {
                if (promptText.gameObject.activeSelf) promptText.gameObject.SetActive(false);
            }
        }
    }

    // สร้าง Texture วงกลมเนียนตาที่ Runtime ป้องกันการพึ่งพาไฟล์ภายนอก
    private Sprite CreateProceduralCircleSprite()
    {
        int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2((size - 1) / 2f, (size - 1) / 2f);
        float radius = (size / 2f) - 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist < radius - 1.5f)
                {
                    texture.SetPixel(x, y, Color.white);
                }
                else if (dist <= radius)
                {
                    float alpha = Mathf.Clamp01(1f - (dist - (radius - 1.5f)) / 1.5f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                else
                {
                    texture.SetPixel(x, y, Color.clear);
                }
            }
        }

        texture.filterMode = FilterMode.Bilinear;
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private void SetupHoldProgressRing()
    {
        if (holdProgressImage != null) return;

        Transform parentTransform = transform.parent != null ? transform.parent : transform;
        Transform child = parentTransform.Find("HoldProgressRing");
        if (child != null)
        {
            holdProgressImage = child.GetComponent<Image>();
            if (holdProgressImage != null) return;
        }

        GameObject ringObj = new GameObject("HoldProgressRing");
        ringObj.transform.SetParent(parentTransform, false);

        RectTransform rect = ringObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = holdProgressSize;

        holdProgressImage = ringObj.AddComponent<Image>();
        holdProgressImage.sprite = CreateProceduralRingSprite();
        holdProgressImage.type = Image.Type.Filled;
        holdProgressImage.fillMethod = Image.FillMethod.Radial360;
        holdProgressImage.fillOrigin = (int)Image.Origin360.Top;
        holdProgressImage.fillClockwise = true;
        holdProgressImage.fillAmount = 0f;
        holdProgressImage.color = holdProgressColor;
        holdProgressImage.raycastTarget = false;

        ringObj.SetActive(false);
    }

    private Sprite CreateProceduralRingSprite()
    {
        int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2((size - 1) / 2f, (size - 1) / 2f);
        float outerRadius = (size / 2f) - 2f;
        float innerRadius = outerRadius - 6f; // ความหนาของวงแหวน 6px

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist >= innerRadius && dist <= outerRadius)
                {
                    // ขอบในและขอบนอกทำ Anti-aliasing ให้เนียนตา
                    float alphaInner = Mathf.Clamp01((dist - innerRadius) / 1.0f);
                    float alphaOuter = Mathf.Clamp01((outerRadius - dist) / 1.0f);
                    float alpha = Mathf.Min(alphaInner, alphaOuter);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                else
                {
                    texture.SetPixel(x, y, Color.clear);
                }
            }
        }

        texture.filterMode = FilterMode.Bilinear;
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    public void SetHoldProgress(float progress)
    {
        if (holdProgressImage == null) return;

        if (progress > 0.005f)
        {
            if (!holdProgressImage.gameObject.activeSelf)
            {
                holdProgressImage.gameObject.SetActive(true);
            }
            holdProgressImage.fillAmount = Mathf.Clamp01(progress);
        }
        else
        {
            if (holdProgressImage.gameObject.activeSelf)
            {
                holdProgressImage.fillAmount = 0f;
                holdProgressImage.gameObject.SetActive(false);
            }
        }
    }
}

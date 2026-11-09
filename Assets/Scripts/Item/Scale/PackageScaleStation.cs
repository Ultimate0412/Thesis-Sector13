using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Checking Point / ตาชั่งพัสดุ
/// ผู้เล่นนำกล่องพัสดุ (PackageBox) มาวางบนแท่น (กด Q/E ผ่านระบบ BaseDropPoint)
/// แล้วหน้าจอของตาชั่งจะแสดง "น้ำหนักจริง" ของพัสดุ (actualWeight)
/// </summary>
public class PackageScaleStation : BaseDropPoint
{
    [Header("Screen References (เว้นว่างได้ ระบบจะสร้างหน้าจอให้อัตโนมัติ)")]
    [Tooltip("จุดยึดหน้าจอ (โดยปกติคือกรอบจอของตาชั่ง)")]
    public Transform screenAnchor;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI weightText;
    public TextMeshProUGUI hintText;

    [Header("Display Settings")]
    public Color standbyColor = new Color(1f, 0.67f, 0f);   // amber
    public Color weighingColor = new Color(0f, 0.9f, 1f);   // cyan
    public string standbyStatus = "● STANDBY";
    public string weighingStatus = "● WEIGHING";
    public string standbyHint = "PLACE PACKAGE ON PLATFORM";
    public string weighingHint = "ACTUAL WEIGHT";
    public string standbyWeight = "--.-";
    public string weightUnit = "kg";

    [Header("Screen Build Settings")]
    [Tooltip("ขนาดหน้าจอ (พิกเซล) ก่อนถูกย่อด้วย screenScale")]
    public Vector2 screenResolution = new Vector2(720f, 480f);
    [Tooltip("สเกลของ Canvas (0.001 = 720px กว้างประมาณ 0.72 เมตร)")]
    public float screenScale = 0.001f;
    [Tooltip("ดันหน้าจอออกไปข้างหน้ากรอบเล็กน้อย (เมตร) เพื่อกันหน้าจอจมอยู่ในกรอบ")]
    public float screenForwardOffset = 0.01f;

    [Header("Audio")]
    public AudioClip weighSound;
    [Range(0f, 1f)] public float weighSoundVolume = 0.8f;

    private AudioSource audioSource;
    private AudioClip proceduralBeepClip;
    private bool screenBuilt = false;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 14f;
            audioSource.playOnAwake = false;
        }

        EnsureScreen();
    }

    private void Start()
    {
        ShowStandby();
    }

    // ---------------------------------------------------------------------
    // BaseDropPoint overrides
    // ---------------------------------------------------------------------

    /// <summary>
    /// ตาชั่งรับเฉพาะกล่องพัสดุ (PackageBox) เท่านั้น
    /// </summary>
    public override bool CanPlaceHere()
    {
        if (currentPlacedItem != null) return false;

        GameObject held = (PlayerPickupSystem.Instance != null) ? PlayerPickupSystem.Instance.heldObject : null;
        if (held == null) return true; // ใช้สำหรับสี hologram ตอนยังไม่ถือของ

        return GetPackageBox(held) != null;
    }

    public override string GetInteractPrompt()
    {
        if (currentPlacedItem != null)
        {
            return "[E]/[Q] Pick Up Package";
        }

        GameObject held = (PlayerPickupSystem.Instance != null) ? PlayerPickupSystem.Instance.heldObject : null;
        if (held != null && GetPackageBox(held) != null)
        {
            return "[Q] Place Package on Scale";
        }

        return string.Empty;
    }

    public override void PlaceItem(GameObject itemToPlace, float itemWeight)
    {
        PackageBox box = GetPackageBox(itemToPlace);
        if (box == null)
        {
            Debug.LogWarning("[PackageScaleStation] วางไม่ได้: ตาชั่งรับเฉพาะกล่องพัสดุ (PackageBox)");
            return;
        }

        base.PlaceItem(itemToPlace, itemWeight);

        float actualWeight = GetActualWeight(box);
        ShowWeight(actualWeight);
        PlayWeighSound();

        Debug.Log($"<color=#00E6FF>[PackageScaleStation]</color> ชั่งพัสดุ '{box.name}' = {actualWeight:0.0} kg");
    }

    public override GameObject RemoveItem()
    {
        GameObject item = base.RemoveItem();
        if (item != null)
        {
            ShowStandby();
            Debug.Log("[PackageScaleStation] หยิบพัสดุออกจากตาชั่งแล้ว");
        }
        return item;
    }

    // ---------------------------------------------------------------------
    // Weight / display
    // ---------------------------------------------------------------------

    /// <summary>
    /// น้ำหนักจริงของพัสดุ (ไม่ใช่น้ำหนักที่สำแดงบนแท็บเล็ต)
    /// </summary>
    private float GetActualWeight(PackageBox box)
    {
<<<<<<< Updated upstream
        // Source of truth เดียวกับระบบถือของ: PackageBox.Weight
        return box != null ? box.Weight : 0f;
=======
        if (box == null) return 0f;

        if (box.officialDatabaseData != null)
        {
            return Mathf.Max(0f, box.officialDatabaseData.actualWeight);
        }

        if (box.innerItemPrefab != null)
        {
            ItemObject item = box.innerItemPrefab.GetComponent<ItemObject>();
            if (item != null) return Mathf.Max(0f, item.itemWeight);
        }

        return 0f;
>>>>>>> Stashed changes
    }

    private PackageBox GetPackageBox(GameObject obj)
    {
        if (obj == null) return null;

        PackageBox box = obj.GetComponent<PackageBox>();
        if (box == null) box = obj.GetComponentInParent<PackageBox>();
        if (box == null) box = obj.GetComponentInChildren<PackageBox>();
        return box;
    }

    private void ShowStandby()
    {
        if (statusText != null)
        {
            statusText.text = standbyStatus;
            statusText.color = standbyColor;
        }

        if (weightText != null)
        {
            weightText.text = $"{standbyWeight} {weightUnit}";
            weightText.color = standbyColor;
        }

        if (hintText != null)
        {
            hintText.text = standbyHint;
        }
    }

    private void ShowWeight(float kg)
    {
        if (statusText != null)
        {
            statusText.text = weighingStatus;
            statusText.color = weighingColor;
        }

        if (weightText != null)
        {
            weightText.text = $"{kg:0.0} {weightUnit}";
            weightText.color = weighingColor;
        }

        if (hintText != null)
        {
            hintText.text = weighingHint;
        }
    }

    // ---------------------------------------------------------------------
    // Procedural screen (สร้างอัตโนมัติถ้ายังไม่ได้ผูกใน Inspector)
    // ---------------------------------------------------------------------

    private void EnsureScreen()
    {
        if (screenBuilt) return;
        if (statusText != null && weightText != null && hintText != null)
        {
            screenBuilt = true;
            return;
        }

        Transform anchor = screenAnchor != null ? screenAnchor : transform;

        // สร้างจุดยึดหน้าจอแบบไม่มีสเกลบิดเบี้ยว
        // (screenAnchor มักอยู่ใต้กรอบจอที่ถูกสเกลไว้ ทำให้ตัวอักษรถูกยืด/บี้)
        GameObject mountObj = new GameObject("ScaleScreenMount");
        mountObj.transform.SetParent(transform, false);
        mountObj.transform.localScale = Vector3.one;
        mountObj.transform.SetPositionAndRotation(
            anchor.position - anchor.forward * screenForwardOffset,
            anchor.rotation);

        GameObject canvasObj = new GameObject("ScaleScreenCanvas");
        canvasObj.transform.SetParent(mountObj.transform, false);
        canvasObj.transform.localPosition = Vector3.zero;
        canvasObj.transform.localRotation = Quaternion.identity;
        canvasObj.transform.localScale = Vector3.one * screenScale;

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        if (canvasRect == null) canvasRect = canvasObj.AddComponent<RectTransform>();
        canvasRect.sizeDelta = screenResolution;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;

        // พื้นหลังเข้ม
        GameObject bgObj = CreateUIObject("Background", canvasObj.transform);
        StretchFull(bgObj.GetComponent<RectTransform>());
        Image bg = bgObj.AddComponent<Image>();
        bg.color = new Color(0.015f, 0.05f, 0.075f, 0.96f);
        bg.raycastTarget = false;

        // ขอบเรืองแสง cyan
        GameObject borderObj = CreateUIObject("Border", canvasObj.transform);
        RectTransform borderRt = borderObj.GetComponent<RectTransform>();
        StretchFull(borderRt);
        Image border = borderObj.AddComponent<Image>();
        border.color = new Color(0f, 0.75f, 0.9f, 0.55f);
        border.raycastTarget = false;

        // แผ่นด้านในทับขอบ เพื่อให้เหลือแค่เส้นขอบบาง ๆ
        GameObject innerObj = CreateUIObject("InnerPanel", canvasObj.transform);
        RectTransform innerRt = innerObj.GetComponent<RectTransform>();
        StretchFull(innerRt);
        innerRt.offsetMin = new Vector2(3f, 3f);
        innerRt.offsetMax = new Vector2(-3f, -3f);
        Image inner = innerObj.AddComponent<Image>();
        inner.color = new Color(0.01f, 0.035f, 0.055f, 0.98f);
        inner.raycastTarget = false;

        // Header
        TextMeshProUGUI header = CreateText(canvasObj.transform, "ScaleHeaderText",
            "SECTOR 13 // CARGO SCALE", 30f, new Color(0f, 0.9f, 1f), TextAlignmentOptions.Center);
        SetRect(header.rectTransform, new Vector2(0f, -26f), new Vector2(-24f, 44f), new Vector2(0.5f, 1f));

        // เส้นคั่น
        GameObject dividerObj = CreateUIObject("Divider", canvasObj.transform);
        RectTransform dividerRt = dividerObj.GetComponent<RectTransform>();
        dividerRt.anchorMin = new Vector2(0f, 1f);
        dividerRt.anchorMax = new Vector2(1f, 1f);
        dividerRt.pivot = new Vector2(0.5f, 1f);
        dividerRt.anchoredPosition = new Vector2(0f, -64f);
        dividerRt.sizeDelta = new Vector2(-40f, 2f);
        Image divider = dividerObj.AddComponent<Image>();
        divider.color = new Color(0f, 0.75f, 0.9f, 0.4f);
        divider.raycastTarget = false;

        // Status
        statusText = CreateText(canvasObj.transform, "ScaleStatusText",
            standbyStatus, 24f, standbyColor, TextAlignmentOptions.Left);
        SetRect(statusText.rectTransform, new Vector2(0f, -82f), new Vector2(-48f, 32f), new Vector2(0.5f, 1f));

        // น้ำหนัก (ตัวใหญ่กลางจอ)
        weightText = CreateText(canvasObj.transform, "ScaleWeightText",
            $"{standbyWeight} {weightUnit}", 110f, standbyColor, TextAlignmentOptions.Center);
        SetRect(weightText.rectTransform, new Vector2(0f, -18f), new Vector2(-24f, 160f), new Vector2(0.5f, 0.5f));
        weightText.fontStyle = FontStyles.Bold;

        // Hint
        hintText = CreateText(canvasObj.transform, "ScaleHintText",
            standbyHint, 22f, new Color(0.55f, 0.7f, 0.8f), TextAlignmentOptions.Center);
        SetRect(hintText.rectTransform, new Vector2(0f, 30f), new Vector2(-40f, 40f), new Vector2(0.5f, 0f));

        screenBuilt = true;
    }

    private GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, string text, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = CreateUIObject(name, parent);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    private void SetRect(RectTransform rt, Vector2 anchoredPos, Vector2 sizeDelta, Vector2 pivot)
    {
        rt.anchorMin = pivot;
        rt.anchorMax = pivot;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
    }

    private void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // ---------------------------------------------------------------------
    // Audio
    // ---------------------------------------------------------------------

    private void PlayWeighSound()
    {
        if (audioSource == null) return;

        if (weighSound != null)
        {
            audioSource.PlayOneShot(weighSound, weighSoundVolume);
            return;
        }

        if (proceduralBeepClip == null)
        {
            proceduralBeepClip = CreateBeepClip(980f, 0.08f);
        }

        audioSource.PlayOneShot(proceduralBeepClip, weighSoundVolume * 0.5f);
    }

    private AudioClip CreateBeepClip(float frequency, float duration)
    {
        const int sampleRate = 44100;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Clamp01(1f - (t / duration));
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.35f;
        }

        AudioClip clip = AudioClip.Create("ScaleWeighBeep", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}

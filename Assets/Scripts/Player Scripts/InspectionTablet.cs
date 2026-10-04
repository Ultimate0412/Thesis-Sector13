using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InspectionTablet : MonoBehaviour
{
    public static InspectionTablet Instance { get; private set; }

    [Header("Keybindings")]
    [Tooltip("ปุ่มหยิบ/เก็บแท็บเล็ต")]
    public KeyCode toggleKey = KeyCode.Alpha1;

    [Header("Tablet Held States")]
    [Tooltip("สถานะว่ากำลังถือแท็บเล็ตอยู่หรือไม่")]
    public bool isHeld = false;

    [Tooltip("สถานะว่าอยู่ในโหมด Close-up ส่องใกล้ชิดหรือไม่ (คลิกขวา RMB เพื่อ Toggle)")]
    public bool isCloseUp = false;

    public bool IsTabletHeld => isHeld;
    public bool IsCloseUp => isCloseUp;

    [Header("Positions Relative to Camera")]
    [Tooltip("ตำแหน่งขณะซ่อน (Holstered)")]
    public Vector3 holsteredLocalPosition = new Vector3(0.25f, -0.85f, 0.45f);
    public Vector3 holsteredLocalRotation = new Vector3(55f, -10f, 0f);

    [Tooltip("ตำแหน่งขณะถือปกติ (Lowered) - หงายขึ้นเล็กน้อย ก้มมองได้ขณะเดิน")]
    public Vector3 loweredLocalPosition = new Vector3(0.24f, -0.27f, 0.50f);
    public Vector3 loweredLocalRotation = new Vector3(22f, -12f, 2f);

    [Tooltip("ตำแหน่งโหมด Close-up (ยกสูงและใกล้ขึ้น เยื้องขวาเล็กน้อยเพื่อให้เห็นกล่องข้างหน้าด้วย)")]
    public Vector3 closeUpLocalPosition = new Vector3(0.18f, -0.09f, 0.38f);
    public Vector3 closeUpLocalRotation = new Vector3(8f, -6f, 0f);

    [Tooltip("ความเร็วในการสลับท่าทาง")]
    public float transitionSpeed = 12f;

    [Header("Procedural Visual References")]
    public Transform tabletTransform;
    public GameObject tabletContainer;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI manifestContentText;
    public Image screenGlowImage;

    private Camera playerCam;
    private PlayerPickupSystem pickupSystem;
    private AudioSource audioSource;
    private AudioClip scanBeepClip;

    private PackageManifestData lastScannedData;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }

        pickupSystem = GetComponentInParent<PlayerPickupSystem>();
        if (pickupSystem == null) pickupSystem = GetComponent<PlayerPickupSystem>();

        playerCam = GetComponentInChildren<Camera>();
        if (playerCam == null) playerCam = Camera.main;

        CreateProceduralTabletIfNeeded();
        CreateBeepAudioClip();
    }

    private void Start()
    {
        if (tabletTransform != null)
        {
            tabletTransform.localPosition = holsteredLocalPosition;
            tabletTransform.localRotation = Quaternion.Euler(holsteredLocalRotation);
        }

        if (tabletContainer != null)
        {
            tabletContainer.SetActive(false);
        }

        UpdateScreenDisplay(null);
    }

    private void Update()
    {
        HandleInput();
        UpdateTabletTransform();
    }

    private void HandleInput()
    {
        // กดปุ่ม 1 เพื่อเปิด/เก็บแท็บเล็ต
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleTablet();
        }

        // หากกำลังถือแท็บเล็ตอยู่ ให้กดคลิกขวา (RMB) เพื่อสลับ Close-up
        if (isHeld && Input.GetMouseButtonDown(1))
        {
            isCloseUp = !isCloseUp;
            PlayBeepSound(1400f, 0.04f);
        }

        // หากผู้เล่นหยิบของขึ้นมือขณะถือแท็บเล็ต ให้เก็บแท็บเล็ตอัตโนมัติ
        if (isHeld && pickupSystem != null && pickupSystem.heldObject != null)
        {
            ForceHolster();
        }
    }

    public void ToggleTablet()
    {
        if (!isHeld)
        {
            // ตรวจสอบว่ามือว่างหรือไม่
            if (pickupSystem != null && pickupSystem.heldObject != null)
            {
                Debug.LogWarning("[InspectionTablet] มือไม่ว่าง! วางของก่อนหยิบแท็บเล็ต");
                return;
            }

            DrawTablet();
        }
        else
        {
            HolsterTablet();
        }
    }

    public void DrawTablet()
    {
        isHeld = true;
        isCloseUp = false;

        if (tabletContainer != null)
        {
            tabletContainer.SetActive(true);
        }

        PlayBeepSound(900f, 0.06f);
    }

    public void HolsterTablet()
    {
        isHeld = false;
        isCloseUp = false;
        PlayBeepSound(700f, 0.05f);
    }

    public void ForceHolster()
    {
        if (isHeld)
        {
            HolsterTablet();
        }
    }

    private void UpdateTabletTransform()
    {
        if (tabletTransform == null) return;

        Vector3 targetPos = holsteredLocalPosition;
        Vector3 targetRot = holsteredLocalRotation;

        if (isHeld)
        {
            if (isCloseUp)
            {
                targetPos = closeUpLocalPosition;
                targetRot = closeUpLocalRotation;
            }
            else
            {
                targetPos = loweredLocalPosition;
                targetRot = loweredLocalRotation;
            }
        }

        // นุ่มนวลด้วย Lerp / Slerp
        tabletTransform.localPosition = Vector3.Lerp(tabletTransform.localPosition, targetPos, Time.deltaTime * transitionSpeed);
        tabletTransform.localRotation = Quaternion.Slerp(tabletTransform.localRotation, Quaternion.Euler(targetRot), Time.deltaTime * transitionSpeed);

        // หากเก็บแท็บเล็ตและเคลื่อนที่ใกล้ตำแหน่งที่ซ่อนแล้ว ให้ปิด GameObject เพื่อประหยัด Render
        if (!isHeld && tabletContainer != null && tabletContainer.activeSelf)
        {
            if (Vector3.Distance(tabletTransform.localPosition, holsteredLocalPosition) < 0.05f)
            {
                tabletContainer.SetActive(false);
            }
        }
    }

    // --- ฟังก์ชันสแกนพัสดุ ---
    public void ScanPackage(PackageBox box)
    {
        if (box == null) return;

        if (box.officialDatabaseData == null)
        {
            box.InitializeManifest();
        }

        lastScannedData = box.officialDatabaseData;
        UpdateScreenDisplay(lastScannedData);

        // เอฟเฟกต์เสียงและแสงสแกน
        PlayBeepSound(1250f, 0.09f);
        StartCoroutine(FlashScreenGlow());

        Debug.Log($"[InspectionTablet] สแกนกล่องสำเร็จ! รหัส: {lastScannedData.serialNumber} ผู้รับ: {lastScannedData.recipientName}");
    }

    private void UpdateScreenDisplay(PackageManifestData data)
    {
        if (manifestContentText == null) return;

        if (data == null)
        {
            if (statusText != null)
            {
                statusText.text = "<color=#ffaa00>● STANDBY</color> // READY TO SCAN";
            }

            manifestContentText.text =
                "<align=center><color=#668899>\n\n\n\n" +
                "[ NO CARGO MANIFEST LOADED ]\n\n" +
                "Aim at a package box and press <b>[E]</b>\n" +
                "to query Central Customs Database\n\n" +
                "</color></align>";
        }
        else
        {
            if (statusText != null)
            {
                statusText.text = "<color=#00ff88>● VERIFIED</color> // RECORD RETRIEVED";
            }

            string catColor = "#00e6ff";
            if (data.declaredCategory == ItemCategory.Illegal) catColor = "#ff4444";
            else if (data.declaredCategory == ItemCategory.Alien) catColor = "#d444ff";

            manifestContentText.text =
                $"<b>RECIPIENT :</b> <color=#ffffff>{data.recipientName}</color>\n" +
                $"<b>DESTINATION :</b> <color=#ffffff>{data.destinationAddress}</color>\n" +
                $"<b>SERIAL NO  :</b> <color=#ffea00><b>{data.serialNumber}</b></color>\n" +
                $"<b>CATEGORY   :</b> <color={catColor}>{data.declaredCategory}</color>\n" +
                $"<b>WEIGHT     :</b> {data.declaredWeight:F1} kg\n" +
                $"──────────────────────────────\n" +
                $"<size=75%><color=#88aabb>CHECK FOR MISMATCHES ON PHYSICAL BOX LABEL\n" +
                $"PRESS <b>[R]</b> TO STAMP APPROVE OR REJECT</color></size>";
        }
    }

    private IEnumerator FlashScreenGlow()
    {
        if (screenGlowImage == null) yield break;

        Color startColor = new Color(0f, 0.9f, 1f, 0.45f);
        Color endColor = new Color(0f, 0.9f, 1f, 0f);

        float elapsed = 0f;
        float duration = 0.25f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            screenGlowImage.color = Color.Lerp(startColor, endColor, elapsed / duration);
            yield return null;
        }

        screenGlowImage.color = endColor;
    }

    #region Procedural Tablet Generation

    public void CreateProceduralTabletIfNeeded()
    {
        if (tabletTransform != null) return;

        if (playerCam == null)
        {
            playerCam = Camera.main;
            if (playerCam == null) playerCam = GetComponentInChildren<Camera>();
        }

        if (playerCam == null) return;

        // ค้นหาว่ามีอยู่แล้วหรือยัง
        Transform existing = playerCam.transform.Find("InspectionTabletRoot");
        if (existing != null)
        {
            tabletTransform = existing;
            tabletContainer = existing.gameObject;
            BindExistingReferences(existing);
            return;
        }

        // สร้าง Root ของแท็บเล็ต
        GameObject tabletRoot = new GameObject("InspectionTabletRoot");
        tabletRoot.transform.SetParent(playerCam.transform, false);
        tabletTransform = tabletRoot.transform;
        tabletContainer = tabletRoot;

        // ตัวบอดี้เครื่องแท็บเล็ต (Body Mesh)
        GameObject bodyObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bodyObj.name = "TabletBody";
        bodyObj.transform.SetParent(tabletRoot.transform, false);
        bodyObj.transform.localScale = new Vector3(0.24f, 0.17f, 0.012f);
        Collider bodyCol = bodyObj.GetComponent<Collider>();
        if (bodyCol != null) Destroy(bodyCol); // ไม่ต้องการ Collider

        // Material สีดำเข้มด้านสไตล์ฮาร์ดแวร์อุตสาหกรรมอวกาศ
        Renderer bodyRend = bodyObj.GetComponent<Renderer>();
        if (bodyRend != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.12f, 0.13f, 0.16f, 1f);
            mat.SetFloat("_Smoothness", 0.35f);
            bodyRend.material = mat;
        }

        // หน้าจอ World-Space Canvas
        GameObject canvasObj = new GameObject("TabletScreenCanvas");
        canvasObj.transform.SetParent(tabletRoot.transform, false);
        canvasObj.transform.localPosition = new Vector3(0f, 0f, 0.007f); // ยื่นออกมาข้างหน้าบอดี้เล็กน้อย
        canvasObj.transform.localRotation = Quaternion.identity;
        canvasObj.transform.localScale = Vector3.one * 0.00045f;

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform canvasRt = canvasObj.GetComponent<RectTransform>();
        canvasRt.sizeDelta = new Vector2(510f, 355f);

        // หน้าจอด้านหลัง (Screen Background)
        Image bgImg = canvasObj.AddComponent<Image>();
        bgImg.color = new Color(0.04f, 0.06f, 0.09f, 0.98f);
        bgImg.raycastTarget = false;

        // ขอบแสงเรืองหน้าจอ (Screen Border Glow)
        GameObject borderObj = new GameObject("ScreenBorder");
        borderObj.transform.SetParent(canvasObj.transform, false);
        RectTransform borderRt = borderObj.AddComponent<RectTransform>();
        borderRt.anchorMin = Vector2.zero;
        borderRt.anchorMax = Vector2.one;
        borderRt.offsetMin = new Vector2(4, 4);
        borderRt.offsetMax = new Vector2(-4, -4);
        Image borderImg = borderObj.AddComponent<Image>();
        borderImg.color = new Color(0f, 0.7f, 0.9f, 0.25f);
        borderImg.raycastTarget = false;

        // หัวกระดาษ Header Bar
        GameObject headerObj = new GameObject("HeaderBar");
        headerObj.transform.SetParent(canvasObj.transform, false);
        RectTransform headerRt = headerObj.AddComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.anchoredPosition = new Vector2(0f, -8f);
        headerRt.sizeDelta = new Vector2(-24f, 42f);

        TextMeshProUGUI headerText = headerObj.AddComponent<TextMeshProUGUI>();
        headerText.text = "<b><color=#00e6ff>SECTOR 13</color> // MANIFEST DATABASE</b>";
        headerText.fontSize = 20f;
        headerText.alignment = TextAlignmentOptions.Center;
        headerText.color = Color.white;
        headerText.raycastTarget = false;

        // แถบสถานะ Status Bar
        GameObject statusObj = new GameObject("StatusBar");
        statusObj.transform.SetParent(canvasObj.transform, false);
        RectTransform statusRt = statusObj.AddComponent<RectTransform>();
        statusRt.anchorMin = new Vector2(0f, 1f);
        statusRt.anchorMax = new Vector2(1f, 1f);
        statusRt.pivot = new Vector2(0.5f, 1f);
        statusRt.anchoredPosition = new Vector2(0f, -48f);
        statusRt.sizeDelta = new Vector2(-24f, 26f);

        statusText = statusObj.AddComponent<TextMeshProUGUI>();
        statusText.text = "<color=#ffaa00>● STANDBY</color> // READY TO SCAN";
        statusText.fontSize = 14f;
        statusText.alignment = TextAlignmentOptions.Left;
        statusText.color = Color.white;
        statusText.raycastTarget = false;

        // ข้อความเนื้อหาข้อมูลพัสดุ
        GameObject contentObj = new GameObject("ManifestContent");
        contentObj.transform.SetParent(canvasObj.transform, false);
        RectTransform contentRt = contentObj.AddComponent<RectTransform>();
        contentRt.anchorMin = Vector2.zero;
        contentRt.anchorMax = Vector2.one;
        contentRt.pivot = new Vector2(0.5f, 0.5f);
        contentRt.offsetMin = new Vector2(16, 12);
        contentRt.offsetMax = new Vector2(-16, -75);

        manifestContentText = contentObj.AddComponent<TextMeshProUGUI>();
        manifestContentText.fontSize = 17f;
        manifestContentText.lineSpacing = -8f;
        manifestContentText.color = new Color(0.85f, 0.92f, 0.98f, 1f);
        manifestContentText.enableWordWrapping = true;
        manifestContentText.raycastTarget = false;

        // แผ่นเอฟเฟกต์แฟลชตอนสแกน
        GameObject glowObj = new GameObject("ScanGlow");
        glowObj.transform.SetParent(canvasObj.transform, false);
        RectTransform glowRt = glowObj.AddComponent<RectTransform>();
        glowRt.anchorMin = Vector2.zero;
        glowRt.anchorMax = Vector2.one;
        glowRt.offsetMin = Vector2.zero;
        glowRt.offsetMax = Vector2.zero;
        screenGlowImage = glowObj.AddComponent<Image>();
        screenGlowImage.color = new Color(0f, 0.9f, 1f, 0f);
        screenGlowImage.raycastTarget = false;
    }

    private void BindExistingReferences(Transform root)
    {
        statusText = root.Find("TabletScreenCanvas/StatusBar")?.GetComponent<TextMeshProUGUI>();
        manifestContentText = root.Find("TabletScreenCanvas/ManifestContent")?.GetComponent<TextMeshProUGUI>();
        screenGlowImage = root.Find("TabletScreenCanvas/ScanGlow")?.GetComponent<Image>();
    }

    private void CreateBeepAudioClip()
    {
        audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D Sound ให้ผู้เล่นได้ยินชัดเจน
        }
    }

    private void PlayBeepSound(float frequency, float duration)
    {
        if (audioSource == null) return;

        int sampleRate = 44100;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        AudioClip clip = AudioClip.Create("Beep", sampleCount, 1, sampleRate, false);

        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = 1f - ((float)i / sampleCount); // Fade out ป้องกันเสียงคลิก
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.25f;
        }

        clip.SetData(samples, 0);
        audioSource.PlayOneShot(clip);
    }

    #endregion
}

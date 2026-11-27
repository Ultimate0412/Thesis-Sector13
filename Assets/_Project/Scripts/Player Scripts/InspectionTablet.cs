using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InspectionTablet : MonoBehaviour
{
    public static InspectionTablet Instance { get; private set; }

    [Header("Keybindings")]
    public KeyCode toggleKey = KeyCode.Alpha1;

    [Header("Tablet Held States")]
    public bool isHeld = false;
    public bool isCloseUp = false;

    public bool IsTabletHeld => isHeld;
    public bool IsCloseUp => isCloseUp;

    [Header("Pose Anchors Under Camera")]
    public Transform holsteredAnchor;
    public Transform loweredAnchor;
    public Transform closeUpAnchor;
    public float transitionSpeed = 12f;

    [Header("Procedural Visual References")]
    public Transform tabletTransform;
    public GameObject tabletContainer;
    public Image screenGlowImage;

    [Header("Modular Screen UI Elements")]
    public TextMeshProUGUI headerText;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI recipientText;
    public TextMeshProUGUI destinationText;
    public TextMeshProUGUI subCategoryText;
    public TextMeshProUGUI serialText;
    public TextMeshProUGUI weightText;
    public TextMeshProUGUI hintText;

    [Header("Fallback / Standby Display Text")]
    public TextMeshProUGUI manifestContentText;

    private Camera playerCam;
    private PlayerPickupSystem pickupSystem;
    private AudioSource audioSource;

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
        EnsurePoseAnchors();
        CreateBeepAudioClip();
    }

    private void Start()
    {
        SnapToAnchorImmediate(holsteredAnchor);

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
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleTablet();
        }

        if (isHeld && Input.GetMouseButtonDown(1))
        {
            isCloseUp = !isCloseUp;
            PlayBeepSound(1400f, 0.04f);
        }

        if (isHeld && pickupSystem != null && pickupSystem.heldObject != null)
        {
            ForceHolster();
        }
    }

    public void ToggleTablet()
    {
        if (!isHeld)
        {
            if (PlayerEquipment.Instance != null) PlayerEquipment.Instance.ForceHolster();

            if (pickupSystem != null && pickupSystem.heldObject != null)
            {
                if (!pickupSystem.PutAwayHeldObject())
                {
                    Debug.LogWarning("[InspectionTablet] วางของที่ถืออยู่ไม่สำเร็จ ไม่สามารถหยิบแท็บเล็ตได้");
                    return;
                }
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

    [ContextMenu("Create Pose Anchors Under Camera")]
    public void EnsurePoseAnchors()
    {
        if (playerCam == null)
        {
            playerCam = GetComponentInChildren<Camera>();
            if (playerCam == null) playerCam = Camera.main;
        }

        if (playerCam == null) return;

        Transform camRoot = playerCam.transform;

        if (holsteredAnchor == null)
        {
            holsteredAnchor = FindOrCreateAnchor(
                camRoot,
                "Tablet_HolsteredAnchor",
                new Vector3(0.25f, -0.85f, 0.45f),
                new Vector3(55f, -10f, 0f)
            );
        }

        if (loweredAnchor == null)
        {
            loweredAnchor = FindOrCreateAnchor(
                camRoot,
                "Tablet_LoweredAnchor",
                new Vector3(-0.24f, -0.27f, 0.50f),
                new Vector3(-22f, -12f, 2f)
            );
        }

        if (closeUpAnchor == null)
        {
            closeUpAnchor = FindOrCreateAnchor(
                camRoot,
                "Tablet_CloseUpAnchor",
                new Vector3(-0.18f, -0.09f, 0.38f),
                new Vector3(-8f, -6f, 0f)
            );
        }
    }

    private static Transform FindOrCreateAnchor(Transform parent, string anchorName, Vector3 defaultLocalPos, Vector3 defaultLocalEuler)
    {
        Transform existing = parent.Find(anchorName);
        if (existing != null) return existing;

        GameObject go = new GameObject(anchorName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = defaultLocalPos;
        go.transform.localRotation = Quaternion.Euler(defaultLocalEuler);
        return go.transform;
    }

    private void SnapToAnchorImmediate(Transform anchor)
    {
        if (tabletTransform == null || anchor == null) return;

        GetLocalPoseFromAnchor(anchor, out Vector3 targetLocalPos, out Quaternion targetLocalRot);
        tabletTransform.localPosition = targetLocalPos;
        tabletTransform.localRotation = targetLocalRot;
    }

    private void GetLocalPoseFromAnchor(Transform anchor, out Vector3 localPos, out Quaternion localRot)
    {
        Transform parent = tabletTransform.parent;
        if (parent != null)
        {
            localPos = parent.InverseTransformPoint(anchor.position);
            localRot = Quaternion.Inverse(parent.rotation) * anchor.rotation;
        }
        else
        {
            localPos = anchor.position;
            localRot = anchor.rotation;
        }
    }

    private void UpdateTabletTransform()
    {
        if (tabletTransform == null) return;

        Transform activeAnchor = holsteredAnchor;
        if (isHeld)
        {
            activeAnchor = isCloseUp ? closeUpAnchor : loweredAnchor;
        }

        if (activeAnchor == null) return;

        GetLocalPoseFromAnchor(activeAnchor, out Vector3 targetPos, out Quaternion targetRot);

        tabletTransform.localPosition = Vector3.Lerp(tabletTransform.localPosition, targetPos, Time.deltaTime * transitionSpeed);
        tabletTransform.localRotation = Quaternion.Slerp(tabletTransform.localRotation, targetRot, Time.deltaTime * transitionSpeed);

        if (!isHeld && tabletContainer != null && tabletContainer.activeSelf && holsteredAnchor != null)
        {
            GetLocalPoseFromAnchor(holsteredAnchor, out Vector3 holsteredPos, out _);
            if (Vector3.Distance(tabletTransform.localPosition, holsteredPos) < 0.05f)
            {
                tabletContainer.SetActive(false);
            }
        }
    }

    public void ScanPackage(PackageBox box)
    {
        if (box == null) return;

        if (box.officialDatabaseData == null)
        {
            box.InitializeManifest();
        }

        lastScannedData = box.officialDatabaseData;
        UpdateScreenDisplay(lastScannedData);

        PlayBeepSound(1250f, 0.09f);
        StartCoroutine(FlashScreenGlow());

        Debug.Log($"[InspectionTablet] สแกนกล่องสำเร็จ! รหัส: {lastScannedData.serialNumber} ผู้รับ: {lastScannedData.recipientName}");
    }

    private void UpdateScreenDisplay(PackageManifestData data)
    {
        if (data == null)
        {
            if (statusText != null)
            {
                statusText.text = "<color=#ffaa00>● STANDBY</color> // READY TO SCAN";
            }

            if (recipientText != null) recipientText.gameObject.SetActive(false);
            if (destinationText != null) destinationText.gameObject.SetActive(false);
            if (subCategoryText != null) subCategoryText.gameObject.SetActive(false);
            if (serialText != null) serialText.gameObject.SetActive(false);
            if (weightText != null) weightText.gameObject.SetActive(false);
            if (hintText != null) hintText.gameObject.SetActive(false);

            if (manifestContentText != null)
            {
                manifestContentText.gameObject.SetActive(true);
                manifestContentText.text =
                    "<align=center><color=#668899>\n\n\n\n" +
                    "[ NO CARGO MANIFEST LOADED ]\n\n" +
                    "Aim at a package box and press <b>[E]</b>\n" +
                    "to query Central Customs Database\n\n" +
                    "</color></align>";
            }
        }
        else
        {
            if (statusText != null)
            {
                statusText.text = "<color=#00ff88>● VERIFIED</color> // RECORD RETRIEVED";
            }

            string subName = ItemObject.GetSubCategoryDisplayName(data.declaredSubCategory);

            if (recipientText != null && destinationText != null && serialText != null && weightText != null)
            {
                if (manifestContentText != null) manifestContentText.gameObject.SetActive(false);

                recipientText.gameObject.SetActive(true);
                recipientText.text = $"<b>RECIPIENT :</b> <color=#ffffff>{data.recipientName}</color>";

                destinationText.gameObject.SetActive(true);
                destinationText.text = $"<b>DESTINATION :</b> <color=#ffffff>{data.destinationAddress}</color>";

                if (subCategoryText != null)
                {
                    subCategoryText.gameObject.SetActive(true);
                    subCategoryText.text = $"<b>CARGO TYPE  :</b> <color=#FFD34D>{subName}</color>";
                }

                serialText.gameObject.SetActive(true);
                serialText.text = $"<b>SERIAL NO  :</b> <color=#ffea00><b>{data.serialNumber}</b></color>";

                weightText.gameObject.SetActive(true);
                weightText.text = $"<b>WEIGHT     :</b> <color=#ffffff>{data.declaredWeight:F1} kg</color>";

                if (hintText != null)
                {
                    hintText.gameObject.SetActive(true);
                    hintText.text =
                        $"──────────────────────────────\n" +
                        $"<size=75%><color=#88aabb>CHECK FOR MISMATCHES ON PHYSICAL BOX LABEL\n" +
                        $"PRESS <b>[F]</b> FOR UV LIGHT | PRESS <b>[R]</b> TO STAMP</color></size>";
                }
            }
            else if (manifestContentText != null)
            {
                manifestContentText.gameObject.SetActive(true);
                manifestContentText.text =
                    $"<b>RECIPIENT  :</b> <color=#ffffff>{data.recipientName}</color>\n" +
                    $"<b>DESTINATION:</b> <color=#ffffff>{data.destinationAddress}</color>\n" +
                    $"<b>CARGO TYPE :</b> <color=#FFD34D>{subName}</color>\n" +
                    $"<b>SERIAL NO  :</b> <color=#ffea00><b>{data.serialNumber}</b></color>\n" +
                    $"<b>WEIGHT     :</b> <color=#ffffff>{data.declaredWeight:F1} kg</color>\n" +
                    $"──────────────────────────────\n" +
                    $"<size=75%><color=#88aabb>CHECK FOR MISMATCHES ON PHYSICAL BOX LABEL\n" +
                    $"PRESS <b>[F]</b> FOR UV LIGHT | PRESS <b>[R]</b> TO STAMP</color></size>";
            }
        }
    }

    private IEnumerator FlashScreenGlow()
    {
        if (screenGlowImage == null) yield break;

        Color startColor = new Color(1f, 0.827451f, 0.3019608f, 0.45f);
        Color endColor = new Color(1f, 0.827451f, 0.3019608f, 0f);

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

    [ContextMenu("Rebuild Procedural Tablet Hierarchy")]
    public void RebuildTabletUI()
    {
        if (playerCam == null)
        {
            playerCam = Camera.main;
            if (playerCam == null) playerCam = GetComponentInChildren<Camera>();
        }
        if (playerCam == null) return;

        Transform existing = playerCam.transform.Find("InspectionTabletRoot");
        if (existing != null)
        {
            DestroyImmediate(existing.gameObject);
            tabletTransform = null;
            tabletContainer = null;
        }
        CreateProceduralTabletIfNeeded();
    }

    public void CreateProceduralTabletIfNeeded()
    {
        if (tabletTransform != null) return;

        if (playerCam == null)
        {
            playerCam = Camera.main;
            if (playerCam == null) playerCam = GetComponentInChildren<Camera>();
        }

        if (playerCam == null) return;

        Transform existing = playerCam.transform.Find("InspectionTabletRoot");
        if (existing != null)
        {
            tabletTransform = existing;
            tabletContainer = existing.gameObject;
            BindExistingReferences(existing);
            return;
        }

        GameObject tabletRoot = new GameObject("InspectionTabletRoot");
        tabletRoot.transform.SetParent(playerCam.transform, false);
        tabletTransform = tabletRoot.transform;
        tabletContainer = tabletRoot;

        GameObject bodyObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bodyObj.name = "TabletBody";
        bodyObj.transform.SetParent(tabletRoot.transform, false);
        bodyObj.transform.localScale = new Vector3(0.24f, 0.17f, 0.012f);
        Collider bodyCol = bodyObj.GetComponent<Collider>();
        if (bodyCol != null)
        {
            if (Application.isPlaying) Destroy(bodyCol);
            else DestroyImmediate(bodyCol);
        }

        Renderer bodyRend = bodyObj.GetComponent<Renderer>();
        if (bodyRend != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.12f, 0.13f, 0.16f, 1f);
            mat.SetFloat("_Smoothness", 0.35f);
            bodyRend.material = mat;
        }

        GameObject canvasObj = new GameObject("TabletScreenCanvas");
        canvasObj.transform.SetParent(tabletRoot.transform, false);
        canvasObj.transform.localPosition = new Vector3(0f, 0f, 0.007f);
        canvasObj.transform.localRotation = Quaternion.identity;
        canvasObj.transform.localScale = Vector3.one * 0.00045f;

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform canvasRt = canvasObj.GetComponent<RectTransform>();
        canvasRt.sizeDelta = new Vector2(510f, 355f);

        Image bgImg = canvasObj.AddComponent<Image>();
        bgImg.color = new Color(0.06666667f, 0.06666667f, 0.06666667f, 0.98f);
        bgImg.raycastTarget = false;

        GameObject borderObj = new GameObject("ScreenBorder");
        borderObj.transform.SetParent(canvasObj.transform, false);
        RectTransform borderRt = borderObj.AddComponent<RectTransform>();
        borderRt.anchorMin = Vector2.zero;
        borderRt.anchorMax = Vector2.one;
        borderRt.offsetMin = new Vector2(4, 4);
        borderRt.offsetMax = new Vector2(-4, -4);
        Image borderImg = borderObj.AddComponent<Image>();
        borderImg.color = new Color(1f, 0.827451f, 0.3019608f, 0.25f);
        borderImg.raycastTarget = false;

        GameObject headerObj = new GameObject("HeaderBar");
        headerObj.transform.SetParent(canvasObj.transform, false);
        RectTransform headerRt = headerObj.AddComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.anchoredPosition = new Vector2(0f, -8f);
        headerRt.sizeDelta = new Vector2(-24f, 38f);

        headerText = headerObj.AddComponent<TextMeshProUGUI>();
        headerText.text = "<b><color=#FFD34D>SECTOR 13</color> // MANIFEST DATABASE</b>";
        headerText.fontSize = 20f;
        headerText.alignment = TextAlignmentOptions.Center;
        headerText.color = Color.white;
        headerText.raycastTarget = false;

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

        recipientText = CreateScreenTextField(canvasObj.transform, "RecipientText", new Vector2(0f, -80f), new Vector2(-40f, 26f), 16f,
            "<b>RECIPIENT :</b> <color=#ffffff>Unknown</color>");

        destinationText = CreateScreenTextField(canvasObj.transform, "DestinationText", new Vector2(0f, -108f), new Vector2(-40f, 26f), 16f,
            "<b>DESTINATION :</b> <color=#ffffff>Unknown</color>");

        subCategoryText = CreateScreenTextField(canvasObj.transform, "SubCategoryText", new Vector2(0f, -136f), new Vector2(-40f, 26f), 16f,
            "<b>CARGO TYPE  :</b> <color=#FFD34D>UNKNOWN</color>");

        serialText = CreateScreenTextField(canvasObj.transform, "SerialText", new Vector2(0f, -164f), new Vector2(-40f, 26f), 16f,
            "<b>SERIAL NO  :</b> <color=#ffea00><b>UNKNOWN</b></color>");

        weightText = CreateScreenTextField(canvasObj.transform, "WeightText", new Vector2(0f, -192f), new Vector2(-40f, 26f), 16f,
            "<b>WEIGHT     :</b> <color=#FFD34D>0.0 kg</color>");

        hintText = CreateScreenTextField(canvasObj.transform, "HintText", new Vector2(0f, -236f), new Vector2(-40f, 50f), 12f,
            "──────────────────────────────\n<size=75%><color=#88aabb>CHECK FOR MISMATCHES ON PHYSICAL BOX LABEL\nPRESS <b>[R]</b> TO STAMP APPROVE OR REJECT</color></size>");
        hintText.lineSpacing = -8f;

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

        GameObject glowObj = new GameObject("ScanGlow");
        glowObj.transform.SetParent(canvasObj.transform, false);
        RectTransform glowRt = glowObj.AddComponent<RectTransform>();
        glowRt.anchorMin = Vector2.zero;
        glowRt.anchorMax = Vector2.one;
        glowRt.offsetMin = Vector2.zero;
        glowRt.offsetMax = Vector2.zero;
        screenGlowImage = glowObj.AddComponent<Image>();
        screenGlowImage.color = new Color(1f, 0.827451f, 0.3019608f, 0f);
        screenGlowImage.raycastTarget = false;
    }

    private TextMeshProUGUI CreateScreenTextField(Transform parent, string name, Vector2 anchoredPos, Vector2 sizeDelta, float fontSize, string defaultText)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = defaultText;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;

        return tmp;
    }

    private void BindExistingReferences(Transform root)
    {
        Transform canvas = root.Find("TabletScreenCanvas");
        if (canvas == null) return;

        headerText = canvas.Find("HeaderBar")?.GetComponent<TextMeshProUGUI>();
        statusText = canvas.Find("StatusBar")?.GetComponent<TextMeshProUGUI>();
        recipientText = canvas.Find("RecipientText")?.GetComponent<TextMeshProUGUI>();
        destinationText = canvas.Find("DestinationText")?.GetComponent<TextMeshProUGUI>();
        subCategoryText = canvas.Find("SubCategoryText")?.GetComponent<TextMeshProUGUI>();
        serialText = canvas.Find("SerialText")?.GetComponent<TextMeshProUGUI>();
        weightText = canvas.Find("WeightText")?.GetComponent<TextMeshProUGUI>();
        hintText = canvas.Find("HintText")?.GetComponent<TextMeshProUGUI>();
        manifestContentText = canvas.Find("ManifestContent")?.GetComponent<TextMeshProUGUI>();
        screenGlowImage = canvas.Find("ScanGlow")?.GetComponent<Image>();
    }

    private void CreateBeepAudioClip()
    {
        audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
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
            float envelope = 1f - ((float)i / sampleCount);
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.25f;
        }

        clip.SetData(samples, 0);
        audioSource.PlayOneShot(clip);
    }

    #endregion
}

// --- สรุปการทำงานของโค้ด ---
// 1. เปลี่ยนการกำหนดตำแหน่งจาก Vector3 ทั้ง 6 ช่องใน Inspector มาเป็นช่อง Transform 3 จุด ได้แก่ holsteredAnchor, loweredAnchor และ closeUpAnchor
// 2. เพิ่มคำสั่ง ContextMenu "Create Pose Anchors Under Camera" และฟังก์ชัน EnsurePoseAnchors() เพื่อสร้าง Empty 3 ตัวใต้ Main Camera ให้อัตโนมัติโดยดึงค่าพิกัดล่าสุดที่คุณตั้งไว้มาเป็นค่าเริ่มต้น
// 3. ใน UpdateTabletTransform() จะแปลงพิกัดของ Empty เป้าหมายเทียบกับ Parent ของแท็บเล็ตผ่าน InverseTransformPoint ทำให้ขยับ/หมุน Empty ใต้กล้องในหน้า Scene View เพื่อปรับท่าถือแบบเรียลไทม์ได้ทันที
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PackageLabelView : MonoBehaviour
{
    [Header("Label Placement Settings")]
    [Tooltip("ตำแหน่ง Local ของใบปะหน้าบนกล่อง (ผู้ใช้ปรับเลื่อนได้อิสระ)")]
    public Vector3 labelLocalPosition = new Vector3(0f, 0.05f, -0.505f);

    [Tooltip("มุมหมุน Local ของใบปะหน้า")]
    public Vector3 labelLocalRotation = new Vector3(0f, 180f, 0f);

    [Tooltip("ขนาด Canvas ของใบปะหน้า (Pixels)")]
    public Vector2 canvasSize = new Vector2(420f, 290f);

    [Tooltip("Scale ของใบปะหน้าใน World Space")]
    public float worldScale = 0.0013f;

    [Header("UI Canvas & Background")]
    public Canvas labelCanvas;
    public Image backgroundImage;

    [Header("Modular Text Elements ")]
    public TextMeshProUGUI headerText;
    public TextMeshProUGUI recipientText;
    public TextMeshProUGUI destinationText;
    public TextMeshProUGUI subCategoryText;
    public TextMeshProUGUI serialText;
    public TextMeshProUGUI barcodeText;

    [Header("Fallback Single Text (ถ้าต้องการแสดงแบบก้อนเดียว)")]
    public TextMeshProUGUI labelText;

    private PackageManifestData currentData;

    private void Awake()
    {
        EnsureLabelSetup();
    }

    public void EnsureLabelSetup()
    {
        if (labelCanvas == null)
        {
            Transform existing = transform.Find("ShippingLabelCanvas");
            if (existing != null)
            {
                labelCanvas = existing.GetComponent<Canvas>();
                backgroundImage = existing.GetComponent<Image>();
                BindModularReferences(existing);
            }
        }

        if (labelCanvas == null)
        {
            CreateProceduralLabel();
        }
    }

    public void BindModularReferences(Transform canvasRoot)
    {
        if (canvasRoot == null) return;

        if (headerText == null) headerText = canvasRoot.Find("LabelBorder/InnerPaper/HeaderText")?.GetComponent<TextMeshProUGUI>();
        if (recipientText == null) recipientText = canvasRoot.Find("LabelBorder/InnerPaper/RecipientText")?.GetComponent<TextMeshProUGUI>();
        if (destinationText == null) destinationText = canvasRoot.Find("LabelBorder/InnerPaper/DestinationText")?.GetComponent<TextMeshProUGUI>();
        if (subCategoryText == null) subCategoryText = canvasRoot.Find("LabelBorder/InnerPaper/SubCategoryText")?.GetComponent<TextMeshProUGUI>();
        if (serialText == null) serialText = canvasRoot.Find("LabelBorder/InnerPaper/SerialText")?.GetComponent<TextMeshProUGUI>();
        if (barcodeText == null) barcodeText = canvasRoot.Find("LabelBorder/InnerPaper/BarcodeText")?.GetComponent<TextMeshProUGUI>();
        if (labelText == null) labelText = canvasRoot.Find("LabelBorder/InnerPaper/LabelText")?.GetComponent<TextMeshProUGUI>();
    }

    [ContextMenu("Rebuild Modular Label Hierarchy")]
    public void CreateProceduralLabel()
    {
        Transform existing = transform.Find("ShippingLabelCanvas");
        if (existing != null)
        {
            DestroyImmediate(existing.gameObject);
        }

        GameObject canvasGo = new GameObject("ShippingLabelCanvas");
        canvasGo.transform.SetParent(transform, false);
        canvasGo.transform.localPosition = labelLocalPosition;
        canvasGo.transform.localRotation = Quaternion.Euler(labelLocalRotation);
        canvasGo.transform.localScale = Vector3.one * worldScale;

        labelCanvas = canvasGo.AddComponent<Canvas>();
        labelCanvas.renderMode = RenderMode.WorldSpace;

        RectTransform canvasRt = canvasGo.GetComponent<RectTransform>();
        canvasRt.sizeDelta = canvasSize;

        // พื้นหลังสติกเกอร์กระดาษ (สีขาวนวลอมเหลืองสไตล์ใบปะหน้า #EDEBE4)
        backgroundImage = canvasGo.AddComponent<Image>();
        backgroundImage.color = new Color(0.93f, 0.92f, 0.88f, 1f);
        backgroundImage.raycastTarget = false;

        // เส้นขอบสติกเกอร์
        GameObject borderGo = new GameObject("LabelBorder");
        borderGo.transform.SetParent(canvasGo.transform, false);
        RectTransform borderRt = borderGo.AddComponent<RectTransform>();
        borderRt.anchorMin = Vector2.zero;
        borderRt.anchorMax = Vector2.one;
        borderRt.offsetMin = new Vector2(6, 6);
        borderRt.offsetMax = new Vector2(-6, -6);
        Image borderImg = borderGo.AddComponent<Image>();
        borderImg.color = new Color(0.2f, 0.2f, 0.22f, 0.4f);
        borderImg.raycastTarget = false;

        // พื้นกระดาษด้านใน
        GameObject innerGo = new GameObject("InnerPaper");
        innerGo.transform.SetParent(borderGo.transform, false);
        RectTransform innerRt = innerGo.AddComponent<RectTransform>();
        innerRt.anchorMin = Vector2.zero;
        innerRt.anchorMax = Vector2.one;
        innerRt.offsetMin = new Vector2(2, 2);
        innerRt.offsetMax = new Vector2(-2, -2);
        Image innerImg = innerGo.AddComponent<Image>();
        innerImg.color = new Color(0.94f, 0.93f, 0.89f, 1f);
        innerImg.raycastTarget = false;

        // 1. Header Text
        headerText = CreateTextField(innerGo.transform, "HeaderText", new Vector2(0f, 96f), new Vector2(390f, 42f), 17f,
            "<b><align=center>SECTOR 13 // WAYBILL</align></b>\n<size=65%><align=center>LOGISTICS DISPATCH CERTIFICATE</align></size>",
            new Color(0.12f, 0.12f, 0.14f, 1f));

        // 2. Recipient Text
        recipientText = CreateTextField(innerGo.transform, "RecipientText", new Vector2(0f, 55f), new Vector2(380f, 28f), 17f,
            "<b>TO:</b> Marcus Vance", new Color(0.12f, 0.12f, 0.14f, 1f));

        // 3. Destination Text
        destinationText = CreateTextField(innerGo.transform, "DestinationText", new Vector2(0f, 25f), new Vector2(380f, 28f), 16f,
            "<b>DEST:</b> Medical Facility - Ward 9", new Color(0.12f, 0.12f, 0.14f, 1f));

        // 4. SubCategory Text (หมวดหมู่ย่อย)
        subCategoryText = CreateTextField(innerGo.transform, "SubCategoryText", new Vector2(0f, -5f), new Vector2(380f, 28f), 16f,
            "<b>TYPE:</b> MEDICAL / PHARMACEUTICALS", new Color(0.12f, 0.12f, 0.14f, 1f));

        // 5. Serial Text
        serialText = CreateTextField(innerGo.transform, "SerialText", new Vector2(0f, -35f), new Vector2(380f, 28f), 17f,
            "<b>SERIAL:</b> <b>MF-84920-X</b>", new Color(0.12f, 0.12f, 0.14f, 1f));

        // 6. Barcode Text
        barcodeText = CreateTextField(innerGo.transform, "BarcodeText", new Vector2(0f, -78f), new Vector2(380f, 46f), 18f,
            "<align=center><size=130%>||| | || ||||| | |||| ||| ||</size>\n<size=70%>TRK#849201</size></align>",
            new Color(0.15f, 0.15f, 0.18f, 1f));

        // กำหนด Layer ให้ตรงกับกล่อง
        canvasGo.layer = gameObject.layer;
        borderGo.layer = gameObject.layer;
        innerGo.layer = gameObject.layer;
    }

    private TextMeshProUGUI CreateTextField(Transform parent, string name, Vector2 anchoredPos, Vector2 size, float fontSize, string defaultText, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.layer = gameObject.layer;

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = defaultText;
        tmp.color = color;
        tmp.fontSize = fontSize;
        tmp.lineSpacing = -8f;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;

        return tmp;
    }

    public void SetLabelData(PackageManifestData data)
    {
        currentData = data;
        EnsureLabelSetup();

        if (data == null) return;

        int hash = Mathf.Abs(data.serialNumber.GetHashCode()) % 1000000;
        string barcodeNumber = $"{hash:D6}";
        string subName = ItemObject.GetSubCategoryDisplayName(data.declaredSubCategory);

        // 1. อัปเดตข้อความแยกชิ้น (Modular Text)
        if (recipientText != null)
        {
            recipientText.text = $"<b>TO:</b> {data.recipientName}";
        }
        if (destinationText != null)
        {
            destinationText.text = $"<b>DEST:</b> {data.destinationAddress}";
        }
        if (subCategoryText != null)
        {
            subCategoryText.text = $"<b>TYPE:</b> {subName}";
        }
        if (serialText != null)
        {
            serialText.text = $"<b>SERIAL:</b> <b>{data.serialNumber}</b>";
        }
        if (barcodeText != null)
        {
            barcodeText.text = $"<align=center><size=130%>||| | || ||||| | |||| ||| ||</size>\n<size=70%>TRK#{barcodeNumber}</size></align>";
        }

        // 2. อัปเดต labelText สำรอง (ถ้ามีการใช้งานแบบก้อนเดียว)
        // สังเกต: ไม่แสดง Category และไม่แสดง Weight ตามคำขอของผู้ใช้!
        if (labelText != null)
        {
            labelText.text =
                $"<align=center><b><size=115%>SECTOR 13 // WAYBILL</size></b></align>\n" +
                $"<align=center><size=70%>LOGISTICS DISPATCH CERTIFICATE</size></align>\n" +
                $"──────────────────────────\n" +
                $"<b>TO:</b> {data.recipientName}\n" +
                $"<b>DEST:</b> {data.destinationAddress}\n" +
                $"<b>TYPE:</b> {subName}\n" +
                $"<b>SERIAL:</b> <b>{data.serialNumber}</b>\n" +
                $"──────────────────────────\n" +
                $"<align=center><size=130%>||| | || ||||| | |||| ||| ||</size>\n" +
                $"<size=75%>TRK#{barcodeNumber}</size></align>";
        }
    }
}

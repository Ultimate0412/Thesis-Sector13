using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PackageLabelView : MonoBehaviour
{
    [Header("Label Placement Settings")]
    [Tooltip("ตำแหน่ง Local ของใบปะหน้าบนกล่อง")]
    public Vector3 labelLocalPosition = new Vector3(0f, 0.05f, -0.505f);

    [Tooltip("มุมหมุน Local ของใบปะหน้า (หันออกหาผู้เล่น)")]
    public Vector3 labelLocalRotation = new Vector3(0f, 180f, 0f);

    [Tooltip("ขนาด Canvas ของใบปะหน้า (Pixels)")]
    public Vector2 canvasSize = new Vector2(420f, 290f);

    [Tooltip("Scale ของใบปะหน้าใน World Space")]
    public float worldScale = 0.0013f;

    [Header("UI References (ปล่อยว่างได้ ระบบจะสร้างให้อัตโนมัติ)")]
    public Canvas labelCanvas;
    public Image backgroundImage;
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
                labelText = existing.GetComponentInChildren<TextMeshProUGUI>();
            }
        }

        if (labelCanvas == null)
        {
            CreateProceduralLabel();
        }
    }

    private void CreateProceduralLabel()
    {
        GameObject canvasGo = new GameObject("ShippingLabelCanvas");
        canvasGo.transform.SetParent(transform, false);
        canvasGo.transform.localPosition = labelLocalPosition;
        canvasGo.transform.localRotation = Quaternion.Euler(labelLocalRotation);
        canvasGo.transform.localScale = Vector3.one * worldScale;

        labelCanvas = canvasGo.AddComponent<Canvas>();
        labelCanvas.renderMode = RenderMode.WorldSpace;

        // ไม่ต้องการ GraphicRaycaster เพื่อไม่ให้ขวาง Raycast ของตัวผู้เล่น
        RectTransform canvasRt = canvasGo.GetComponent<RectTransform>();
        canvasRt.sizeDelta = canvasSize;

        // พื้นหลังสติกเกอร์กระดาษ (สีขาวนวลอมเหลืองนิดๆ สไตล์ใบปะหน้าสินค้า)
        backgroundImage = canvasGo.AddComponent<Image>();
        backgroundImage.color = new Color(0.93f, 0.92f, 0.88f, 1f);
        backgroundImage.raycastTarget = false;

        // เส้นขอบสติกเกอร์บางๆ
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

        // พื้นที่ด้านในสำหรับข้อความ
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

        // กล่องข้อความ TextMeshPro
        GameObject textGo = new GameObject("LabelText");
        textGo.transform.SetParent(innerGo.transform, false);
        RectTransform textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(10, 8);
        textRt.offsetMax = new Vector2(-10, -8);

        labelText = textGo.AddComponent<TextMeshProUGUI>();
        labelText.color = new Color(0.12f, 0.12f, 0.14f, 1f);
        labelText.fontSize = 18f;
        labelText.lineSpacing = -10f;
        labelText.enableWordWrapping = true;
        labelText.overflowMode = TextOverflowModes.Ellipsis;
        labelText.raycastTarget = false;

        // กำหนด Layer ให้ตรงกับกล่อง
        canvasGo.layer = gameObject.layer;
        borderGo.layer = gameObject.layer;
        innerGo.layer = gameObject.layer;
        textGo.layer = gameObject.layer;
    }

    public void SetLabelData(PackageManifestData data)
    {
        currentData = data;
        EnsureLabelSetup();

        if (labelText == null || data == null) return;

        int hash = Mathf.Abs(data.serialNumber.GetHashCode()) % 1000000;
        string barcodeNumber = $"{hash:D6}";

        labelText.text =
            $"<align=center><b><size=115%>SECTOR 13 // WAYBILL</size></b></align>\n" +
            $"<align=center><size=70%>LOGISTICS DISPATCH CERTIFICATE</size></align>\n" +
            $"──────────────────────────\n" +
            $"<b>TO:</b> {data.recipientName}\n" +
            $"<b>DEST:</b> {data.destinationAddress}\n" +
            $"<b>SERIAL:</b> <b>{data.serialNumber}</b>\n" +
            $"<b>DECL:</b> {data.declaredCategory} | {data.declaredWeight:F1}kg\n" +
            $"──────────────────────────\n" +
            $"<align=center><size=130%>||| | || ||||| | |||| ||| ||</size>\n" +
            $"<size=75%>TRK#{barcodeNumber}</size></align>";
    }
}

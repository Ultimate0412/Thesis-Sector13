using UnityEngine;

public class ShippingDropPoint : BaseDropPoint
{
    [Header("Shipping Settings")]
    public KeyCode shipKey = KeyCode.F; // ปุ่มกดส่งสินค้าออก

    [Tooltip("เปิด: กด F จะส่งเฉพาะจุดที่ผู้เล่นกำลังเล็งอยู่ (จำเป็นเมื่อมีหลายจุดติดกัน)")]
    public bool shipOnlyWhenAimedAt = true;

    [Tooltip("ระยะเล็งสูงสุดก่อนกด F ได้")]
    public float shipAimDistance = 4f;

    [Header("Pipe Routing")]
    [Tooltip("ท่อที่พัสดุจะถูกดูดเข้าเมื่อผู้เล่นประทับ Approve")]
    public PipeSuck approvePipe;

    [Tooltip("ท่อที่พัสดุจะถูกดูดเข้าเมื่อผู้เล่นประทับ Reject")]
    public PipeSuck rejectPipe;

    public override string GetInteractPrompt()
    {
        if (currentPlacedItem != null)
        {
            PackageBox box = currentPlacedItem.GetComponent<PackageBox>();
            string itemName = box != null ? "Box" : "Item";
            return $"[Q]/[E] Pick Up {itemName} | [{shipKey}] Ship Item";
        }
        return string.Empty;
    }

    public override bool CanPlaceHere()
    {
        return currentPlacedItem == null;
    }

    protected override void Update()
    {
        base.Update();

        if (currentPlacedItem == null) return;
        if (!Input.GetKeyDown(shipKey)) return;

        if (shipOnlyWhenAimedAt && !IsPlayerAimingAtThis()) return;

        ProcessShipping();
    }

    private static Camera cachedCamera;

    private bool IsPlayerAimingAtThis()
    {
        if (cachedCamera == null) cachedCamera = Camera.main;
        if (cachedCamera == null) return true; // ไม่มีกล้อง = ปล่อยผ่าน

        Ray ray = new Ray(cachedCamera.transform.position, cachedCamera.transform.forward);
        int mask = (1 << 6) | (1 << 7); // Item + Drop Point

        if (!Physics.Raycast(ray, out RaycastHit hit, shipAimDistance, mask))
        {
            return false;
        }

        return hit.collider.GetComponentInParent<ShippingDropPoint>() == this;
    }

    /// <summary>
    /// ใช้โดยระบบอื่น (เช่น ไฟฉาย UV) เพื่อเช็คว่าปุ่มนี้กำลังถูกใช้ส่งของอยู่หรือไม่
    /// จะได้ไม่ทำงานซ้อนกันในเฟรมเดียวกัน
    /// </summary>
    public static bool IsKeyConsumedByShipping(KeyCode key)
    {
        if (cachedCamera == null) cachedCamera = Camera.main;

        ShippingDropPoint aimed = null;
        float aimedDistance = float.MaxValue;

        if (cachedCamera != null)
        {
            Ray ray = new Ray(cachedCamera.transform.position, cachedCamera.transform.forward);
            int mask = (1 << 6) | (1 << 7); // Item + Drop Point

            if (Physics.Raycast(ray, out RaycastHit hit, 10f, mask))
            {
                aimed = hit.collider.GetComponentInParent<ShippingDropPoint>();
                aimedDistance = hit.distance;
            }
        }

        // โหมดส่งทีละจุด: ต้องเล็งจุดที่มีกล่องอยู่จริงและอยู่ในระยะ
        if (aimed != null && aimed.isActiveAndEnabled && aimed.currentPlacedItem != null
            && aimed.shipKey == key && aimedDistance <= aimed.shipAimDistance)
        {
            return true;
        }

        // โหมดส่งพร้อมกันทุกจุด (ไม่ต้องเล็ง): ถ้ามีกล่องวางอยู่ที่ใดก็ได้ ปุ่มนี้ถูกใช้ส่งของ
        ShippingDropPoint[] points = Object.FindObjectsByType<ShippingDropPoint>(FindObjectsSortMode.None);
        for (int i = 0; i < points.Length; i++)
        {
            ShippingDropPoint point = points[i];
            if (point == null || !point.isActiveAndEnabled) continue;
            if (point.shipOnlyWhenAimedAt) continue;
            if (point.currentPlacedItem == null || point.shipKey != key) continue;
            return true;
        }

        return false;
    }

    private void ProcessShipping()
    {
        if (currentPlacedItem == null) return;

        // 1. เช็คว่าเป็นกล่องพัสดุ (PackageBox) หรือไม่
        PackageBox packageBox = currentPlacedItem.GetComponent<PackageBox>();
        if (packageBox == null)
        {
            Debug.LogWarning("[Shipping Point] ส่งไม่สำเร็จ: วัตถุที่วางไม่มี Component 'PackageBox'!", currentPlacedItem);
            return;
        }

        // 2. เช็คว่ากล่องว่างเปล่าหรือไม่ (ห้ามส่งกล่องเปล่าที่สินค้าถูกนำออกไปแล้ว)
        if (packageBox.isItemExtracted)
        {
            Debug.LogWarning("[Shipping Point] ส่งไม่สำเร็จ: กล่องนี้เป็นกล่องเปล่า (สินค้าถูกนำออกไปแล้ว)! กรุณาใส่สินค้ากลับเข้ากล่องก่อนส่ง", currentPlacedItem);
            return;
        }

        // 3. เช็คว่าประทับตราหรือยัง
        if (packageBox.currentStamp == InspectionStatus.None)
        {
            Debug.LogWarning("[Shipping Point] ส่งไม่สำเร็จ: กล่องนี้ยังไม่ได้ประทับตรา (Approved/Rejected)!");
            return;
        }

        // 3. ดึงข้อมูลสินค้าด้านในกล่อง
        ItemObject itemData = null;
        if (packageBox.innerItemPrefab != null)
        {
            itemData = packageBox.innerItemPrefab.GetComponent<ItemObject>();
        }

        if (itemData == null)
        {
            Debug.LogError("[Shipping Point] ส่งไม่สำเร็จ: ไม่พบ Component 'ItemObject' ที่ Prefab สินค้าด้านในกล่อง!");
            return;
        }

        int scoreChange = 0;

        // 4. คำนวณคะแนนตามสถานะตราประทับ
        if (packageBox.currentStamp == InspectionStatus.Rejected)
        {
            // ตีกลับสินค้าถูกกฎหมาย โดนหักคะแนน
            if (itemData.category == ItemCategory.Legal)
            {
                scoreChange = -100;
                Debug.Log("[Shipping Point] ตีกลับผิดพลาด: ส่งคืนสินค้าถูกกฎหมาย โดนหักคะแนน!");
            }
            else
            {
                scoreChange = 50; // ตีกลับของเถื่อน/เอเลี่ยนถูกต้อง ได้โบนัส
                Debug.Log("[Shipping Point] ตีกลับถูกต้อง: คัดแยกวัตถุอันตรายสำเร็จ!");
            }
        }
        else if (packageBox.currentStamp == InspectionStatus.Approved)
        {
            scoreChange = CalculateScore(itemData);
        }

        // 5. บันทึกสถิติและคะแนนไปที่ ScoreManager ตัวกลาง
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.RecordShippedPackage(itemData.category, packageBox.currentStamp, scoreChange);
            Debug.Log($"[Shipping Point] สำเร็จ! สินค้า: {itemData.itemName} | ประเภท: {itemData.category} | ตรา: {packageBox.currentStamp} | คะแนนที่เปลี่ยน: {scoreChange}");
        }
        else
        {
            Debug.LogError("[Shipping Point] หา ScoreManager.Instance ไม่พบในฉาก!");
        }

        // ส่งกล่องเข้าท่อตามผลการตรวจ (ผ่าน -> ท่อ approve, ไม่ผ่าน -> ท่อ reject)
        GameObject shipped = currentPlacedItem;
        currentPlacedItem = null;

        PipeSuck pipe = packageBox.currentStamp == InspectionStatus.Approved ? approvePipe : rejectPipe;
        if (pipe != null && shipped != null) pipe.Suck(shipped);
        else if (shipped != null) Destroy(shipped);
    }

    private int CalculateScore(ItemObject item)
    {
        switch (item.category)
        {
            case ItemCategory.Legal:
                return 100;   // ของถูกกฎหมาย ปล่อยผ่าน ได้คะแนน
            case ItemCategory.Illegal:
                return -200;  // ของผิดกฎหมาย ปล่อยผ่าน โดนหักคะแนน
            case ItemCategory.Alien:
                return -400;  // ของเอเลี่ยน ปล่อยผ่าน โดนหักคะแนนหนัก
            default:
                return 0;
        }
    }
}
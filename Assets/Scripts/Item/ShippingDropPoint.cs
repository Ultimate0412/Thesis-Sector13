using UnityEngine;

public class ShippingDropPoint : BaseDropPoint
{
    [Header("Shipping Settings")]
    public KeyCode shipKey = KeyCode.F; // ปุ่มกดส่งสินค้าออก

    protected override void Update()
    {
        base.Update();

        if (currentPlacedItem != null && Input.GetKeyDown(shipKey))
        {
            ProcessShipping();
        }
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

        // 2. เช็คว่าประทับตราหรือยัง
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
            // บันทึกสถิติตีกลับลง ScoreManager
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.AddRejectedCount();
            }

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

        // 5. ส่งคะแนนไปที่ ScoreManager ตัวกลาง
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.AddScore(scoreChange);
            Debug.Log($"[Shipping Point] สำเร็จ! สินค้า: {itemData.itemName} | ประเภท: {itemData.category} | คะแนนที่เปลี่ยน: {scoreChange}");
        }
        else
        {
            Debug.LogError("[Shipping Point] หา ScoreManager.Instance ไม่พบในฉาก!");
        }

        // ลบกล่องออกจากฉาก
        Destroy(currentPlacedItem);
        currentPlacedItem = null;
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
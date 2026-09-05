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
        ItemObject itemData = currentPlacedItem.GetComponent<ItemObject>();
        if (itemData != null)
        {
            int scoreChange = CalculateScore(itemData);

            // ส่งคะแนนไปที่ ScoreManager ตัวกลาง
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.AddScore(scoreChange);
            }

            Debug.Log($"[Shipping Point] Shipped: {itemData.itemName} | Category: {itemData.category} | Score Change: {scoreChange}");
        }

        Destroy(currentPlacedItem);
        currentPlacedItem = null;
    }

    private int CalculateScore(ItemObject item)
    {
        switch (item.category)
        {
            case ItemCategory.Legal:
                return 100;   // ของถูกกฎหมาย ได้คะแนนบวก
            case ItemCategory.Illegal:
                return -150;  // ของผิดกฎหมาย ลบคะแนน
            case ItemCategory.Alien:
                return -300;  // ของเอเลี่ยน ลบคะแนนเยอะเป็นพิเศษ
            default:
                return 0;
        }
    }
}
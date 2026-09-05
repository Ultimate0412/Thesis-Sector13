using UnityEngine;

public class StorageDropPoint : BaseDropPoint
{
    public override void PlaceItem(GameObject itemToPlace, float itemWeight)
    {
        base.PlaceItem(itemToPlace, itemWeight);

        // เช็คประเภทไอเท็มทันทีที่เอามาวาง
        ItemObject itemData = currentPlacedItem.GetComponent<ItemObject>();
        if (itemData != null)
        {
            Debug.Log($"[Storage Point] Placed item: {itemData.itemName} (Category: {itemData.category})");

            // รองรับฟังก์ชันเสริมในอนาคต: ถ้าเป็นของเอเลี่ยนวางที่จุดพัก
            if (itemData.category == ItemCategory.Alien)
            {
                TriggerAlienEventReaction();
            }
        }
    }

    private void TriggerAlienEventReaction()
    {
        Debug.Log("WARNING: Alien item detected in storage! Preparing for enemy spawn system in the future...");
        // TODO: เขียนโค้ดเรียกสปอนเซอร์ศัตรู / ปล่อยเอฟเฟกต์ประหลาดตรงนี้ในอนาคต
    }
}
using UnityEngine;

public class TrashDropPoint : BaseDropPoint
{
    public override void PlaceItem(GameObject itemToPlace, float itemWeight)
    {
        base.PlaceItem(itemToPlace, itemWeight);

        ItemObject itemData = currentPlacedItem.GetComponent<ItemObject>();
        if (itemData != null)
        {
            Debug.Log($"[Trash Point] Item placed for disposal: {itemData.itemName}");
        }
    }

    // ฟังก์ชันที่จะเรียกใช้งานตอนจบวัน (End of Day) เพื่อกวาดล้างขยะทิ้งทั้งหมด
    public void ClearTrashAtEndOfDay()
    {
        if (currentPlacedItem != null)
        {
            Debug.Log($"[Trash Point] Disposing of item at end of day: {currentPlacedItem.name}");
            Destroy(currentPlacedItem);
            currentPlacedItem = null;
        }
    }
}

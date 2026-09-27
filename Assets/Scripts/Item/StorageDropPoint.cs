using UnityEngine;

public class StorageDropPoint : BaseDropPoint
{
    [Header("Storage Specific Settings")]
    public StorageRoomManager roomManager;
    public int targetSlotIndex = 0;

    // เช็คว่า ณ จุดนี้ สามารถวางของที่ผู้เล่นถืออยู่ได้ไหม
    public override bool CanPlaceHere()
    {
        if (roomManager == null) return false;

        // ดึงข้อมูลไอเท็มที่ผู้เล่นกำลังถืออยู่ผ่าน PlayerPickupSystem หรือเช็คจากตัวละคร
        // (เราสามารถอ้างอิงผ่าน PlayerPickupSystem ที่ส่องโฮโลแกรมอยู่ได้)
        GameObject heldItem = GetCurrentlyHeldItem();
        if (heldItem == null) return true;

        ItemObject itemData = heldItem.GetComponent<ItemObject>();
        int itemSize = itemData != null ? itemData.sizeInSlots : 1;

        // คืนค่า True ถ้าวางได้, False ถ้าพื้นที่ไม่พอ
        return roomManager.CanPlaceItemAt(targetSlotIndex, itemSize);
    }

    // ฟังก์ชันช่วยดึงไอเท็มที่ผู้เล่นกำลังถืออยู่มาเช็คขนาด
    private GameObject GetCurrentlyHeldItem()
    {
        // ค้นหา PlayerPickupSystem ในฉากเพื่อดูว่าตอนนี้ถืออะไรอยู่
        PlayerPickupSystem playerPickup = FindObjectOfType<PlayerPickupSystem>();
        if (playerPickup != null)
        {
            return playerPickup.GetHeldObject(); // หมายเหตุ: หากยังไม่มีฟังก์ชันนี้ ให้เพิ่ม method เล็กๆ ใน PlayerPickupSystem คืนค่า heldObject
        }
        return null;
    }

    public override void ShowHologram(GameObject heldPrefab)
    {
        if (roomManager == null || roomManager.slotPositions == null) return;

        ItemObject itemData = heldPrefab.GetComponent<ItemObject>();
        int itemSize = itemData != null ? itemData.sizeInSlots : 1;

        if (targetSlotIndex < roomManager.slotPositions.Length)
        {
            Transform targetSlot = roomManager.slotPositions[targetSlotIndex];

            if (hologramInstance == null && heldPrefab != null)
            {
                hologramInstance = Instantiate(heldPrefab, targetSlot.position, targetSlot.rotation);
                hologramInstance.transform.localScale = targetSlot.localScale;

                Destroy(hologramInstance.GetComponent<Rigidbody>());
                Destroy(hologramInstance.GetComponent<Collider>());
                Destroy(hologramInstance.GetComponent<ItemObject>());
                Destroy(hologramInstance.GetComponent<PackageBox>());

                // เลือกใช้สีเขียวหรือแดงตั้งแต่ตอนสร้างตามพื้นที่
                bool canPlace = roomManager.CanPlaceItemAt(targetSlotIndex, itemSize);
                Material activeMat = canPlace ? hologramMaterial : redHologramMaterial;

                if (activeMat != null)
                {
                    Renderer[] renderers = hologramInstance.GetComponentsInChildren<Renderer>();
                    foreach (Renderer rend in renderers)
                    {
                        Material[] mats = new Material[rend.sharedMaterials.Length];
                        for (int i = 0; i < mats.Length; i++)
                        {
                            mats[i] = activeMat;
                        }
                        rend.materials = mats;
                    }
                }
            }
        }
    }

    public override void PlaceItem(GameObject itemToPlace, float itemWeight)
    {
        if (roomManager == null) return;

        ItemObject itemData = itemToPlace.GetComponent<ItemObject>();
        int itemSize = itemData != null ? itemData.sizeInSlots : 1;

        if (roomManager.CanPlaceItemAt(targetSlotIndex, itemSize))
        {
            HideHologram();
            currentPlacedItem = itemToPlace;

            roomManager.PlaceItemToSlots(currentPlacedItem, targetSlotIndex, itemSize);

            Rigidbody rb = currentPlacedItem.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = true; }

            Collider col = currentPlacedItem.GetComponent<Collider>();
            if (col != null) { col.enabled = false; }

            if (itemData != null && itemData.category == ItemCategory.Alien)
            {
                TriggerAlienEventReaction();
            }
        }
        else
        {
            Debug.LogWarning("[Storage Point] พื้นที่ Slot ไม่เพียงพอ ไม่สามารถวางได้!");
        }
    }

    public override GameObject RemoveItem()
    {
        if (currentPlacedItem == null) return null;

        if (roomManager != null)
        {
            roomManager.RemoveItemFromSlots(currentPlacedItem);
        }

        return base.RemoveItem();
    }

    private void TriggerAlienEventReaction()
    {
        Debug.Log("WARNING: Alien item detected in storage!");
    }
}
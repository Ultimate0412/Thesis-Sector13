using UnityEngine;

public abstract class BaseDropPoint : MonoBehaviour, IInteractable
{
    [Header("Base Point Settings")]
    public Transform placementSlot;

    [Header("Hologram Settings")]
    public Material hologramMaterial;    // สีเขียว (วางได้)
    public Material redHologramMaterial; // สีแดง (วางไม่ได้) - ลาก Material สีแดงโปร่งใสมาใส่ที่นี่

    [HideInInspector] public GameObject currentPlacedItem = null;
    [HideInInspector] public GameObject hologramInstance = null;

    protected virtual void Update()
    {
        // หากกำลังแสดง Hologram อยู่ ให้เช็คตลอดว่าสามารถวางได้ไหม เพื่ออัปเดตสี
        if (hologramInstance != null)
        {
            UpdateHologramColor(CanPlaceHere());
        }
    }

    // ฟังก์ชันให้คลาสลูกเขียนเงื่อนไขเช็คว่าตอนนี้วางได้หรือเปล่า (True = วางได้, False = วางไม่ได้)
    public virtual bool CanPlaceHere()
    {
        return true; // ค่าเริ่มต้นของ Base คือวางได้
    }

    // ฟังก์ชันอัปเดตเปลี่ยน Material ของโฮโลแกรม
    public void UpdateHologramColor(bool isAvailable)
    {
        if (hologramInstance == null) return;

        Material targetMat = isAvailable ? hologramMaterial : redHologramMaterial;
        if (targetMat == null) return;

        Renderer[] renderers = hologramInstance.GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers)
        {
            Material[] mats = new Material[rend.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i] = targetMat;
            }
            rend.materials = mats;
        }
    }

    // --- Implement จาก IInteractable ---
    public string GetInteractPrompt()
    {
        if (currentPlacedItem != null)
        {
            PackageBox box = currentPlacedItem.GetComponent<PackageBox>();
            string itemName = box != null ? "กล่องพัสดุ" : "วัตถุ";
            return $"กด E เพื่อหยิบ {itemName} กลับขึ้นมา";
        }
        return string.Empty;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (currentPlacedItem != null)
        {
            PlayerPickupSystem pickupSystem = interactor.GetComponent<PlayerPickupSystem>();
            if (pickupSystem != null)
            {
                pickupSystem.PickUpItemFromDropPoint(this);
            }
        }
    }
    // ------------------------------------

    public virtual void ShowHologram(GameObject heldPrefab)
    {
        Transform targetSlot = placementSlot != null ? placementSlot : transform;

        if (hologramInstance == null && heldPrefab != null)
        {
            hologramInstance = Instantiate(heldPrefab, targetSlot.position, targetSlot.rotation);
            hologramInstance.transform.localScale = targetSlot.localScale;

            Destroy(hologramInstance.GetComponent<Rigidbody>());
            Destroy(hologramInstance.GetComponent<Collider>());
            Destroy(hologramInstance.GetComponent<ItemObject>());
            Destroy(hologramInstance.GetComponent<PackageBox>());

            // เช็คสถานะตอนสร้างครั้งแรกแล้วใส่สีให้ถูกต้องทันที
            bool canPlace = CanPlaceHere();
            Material initialMat = canPlace ? hologramMaterial : redHologramMaterial;

            if (initialMat != null)
            {
                Renderer[] renderers = hologramInstance.GetComponentsInChildren<Renderer>();
                foreach (Renderer rend in renderers)
                {
                    Material[] mats = new Material[rend.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++)
                    {
                        mats[i] = initialMat;
                    }
                    rend.materials = mats;
                }
            }
        }
    }

    public virtual void HideHologram()
    {
        if (hologramInstance != null)
        {
            Destroy(hologramInstance);
        }
    }

    public virtual void PlaceItem(GameObject itemToPlace, float itemWeight)
    {
        HideHologram();
        currentPlacedItem = itemToPlace;

        if (placementSlot != null)
        {
            currentPlacedItem.transform.SetParent(placementSlot);
            currentPlacedItem.transform.localPosition = Vector3.zero;
            currentPlacedItem.transform.localRotation = Quaternion.identity;
        }
        else
        {
            currentPlacedItem.transform.position = transform.position;
            currentPlacedItem.transform.rotation = transform.rotation;
        }

        Rigidbody rb = currentPlacedItem.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; }

        Collider col = currentPlacedItem.GetComponent<Collider>();
        if (col != null) { col.enabled = false; }
    }

    public virtual GameObject RemoveItem()
    {
        if (currentPlacedItem == null) return null;

        GameObject itemToTake = currentPlacedItem;
        currentPlacedItem = null;

        Collider col = itemToTake.GetComponent<Collider>();
        if (col != null) { col.enabled = true; }

        return itemToTake;
    }
}
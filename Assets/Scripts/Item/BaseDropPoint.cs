using UnityEngine;

public abstract class BaseDropPoint : MonoBehaviour
{
    [Header("Base Point Settings")]
    public Transform placementSlot; // จุดตำแหน่งที่จะให้ไอเท็มไปวางแปะอยู่

    [Header("Hologram Settings")]
    public Material hologramMaterial; // ลาก Material สีเขียวโปร่งใสมาใส่ที่นี่ใน Inspector

    [HideInInspector] public GameObject currentPlacedItem = null;
    [HideInInspector] public GameObject hologramInstance = null;

    protected virtual void Update()
    {
        // เปิดให้คลาสลูกเขียนเงื่อนไขเพิ่มเติม
    }

    // ฟังก์ชันสร้าง Hologram
    public virtual void ShowHologram(GameObject heldPrefab)
    {
        if (hologramInstance == null && placementSlot != null && heldPrefab != null)
        {
            // 1. สร้างร่างเงาขึ้นมา
            hologramInstance = Instantiate(heldPrefab, placementSlot.position, placementSlot.rotation);
            hologramInstance.transform.localScale = placementSlot.localScale;

            // 2. ปิด Component ที่ไม่ต้องการฟิสิกส์ออก
            Destroy(hologramInstance.GetComponent<Rigidbody>());
            Destroy(hologramInstance.GetComponent<Collider>());
            Destroy(hologramInstance.GetComponent<ItemObject>());

            // 3. เปลี่ยน Material ของทุกชิ้นส่วนในโฮโลแกรมให้เป็น Material สีเขียวโปร่งใสที่เราเตรียมไว้
            if (hologramMaterial != null)
            {
                Renderer[] renderers = hologramInstance.GetComponentsInChildren<Renderer>();
                foreach (Renderer rend in renderers)
                {
                    Material[] mats = new Material[rend.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++)
                    {
                        mats[i] = hologramMaterial; // แทนที่ด้วย Material สีเขียวโปร่งใส
                    }
                    rend.materials = mats;
                }
            }
        }
    }

    // ลบ Hologram ทิ้งเมื่อผู้เล่นมองออกไป
    public virtual void HideHologram()
    {
        if (hologramInstance != null)
        {
            Destroy(hologramInstance);
        }
    }

    // ฟังก์ชันสำหรับวางไอเท็มลงจุด
    public virtual void PlaceItem(GameObject itemToPlace, float itemWeight)
    {
        HideHologram();
        currentPlacedItem = itemToPlace;

        currentPlacedItem.transform.SetParent(null);
        currentPlacedItem.transform.position = placementSlot.position;
        currentPlacedItem.transform.rotation = placementSlot.rotation;

        Rigidbody rb = currentPlacedItem.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; }

        Collider col = currentPlacedItem.GetComponent<Collider>();
        if (col != null) { col.enabled = false; }
    }
}
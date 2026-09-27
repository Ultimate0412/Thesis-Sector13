using UnityEngine;

public class PlayerPickupSystem : MonoBehaviour
{
    [Header("Pickup Settings")]
    public float pickupDistance = 3f;
    public LayerMask itemLayer;         // เลเยอร์สำหรับสินค้าที่หยิบได้
    public LayerMask dropPointLayer;    // เลเยอร์สำหรับจุดวางของ (Drop Point)
    public Transform holdPosition;      // จุดยึดสินค้าหน้ากล้อง

    [HideInInspector] public GameObject heldObject;
    private PlayerStats playerStats;
    private Camera playerCam;
    private BaseDropPoint currentHoveredDropPoint;

    private void Start()
    {
        playerStats = GetComponent<PlayerStats>();
        playerCam = GetComponentInChildren<Camera>();
    }

    private void Update()
    {
        HandleDropPointHover();

        // ใช้ปุ่มคลิกซ้ายหรือปุ่มอื่น (เช่น ปุ่ม G หรือคลิกขวา) สำหรับการหยิบ/วางของ
        // หรือถ้ายังใช้ปุ่ม E ร่วมกัน สามารถปรับเปลี่ยนเงื่อนไขตรงนี้ได้ตามความเหมาะสม
        if (Input.GetKeyDown(KeyCode.Q)) // ตัวอย่างใช้ปุ่ม Q หรือ F ในการหยิบ/วาง หรือจะใช้ E ก็ได้
        {
            if (heldObject == null)
            {
                TryPickUp();
            }
            else
            {
                TryDropOrPlace();
            }
        }
    }

    private void TryPickUp()
    {
        Ray ray = new Ray(playerCam.transform.position, playerCam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, pickupDistance, itemLayer))
        {
            GameObject targetItem = hit.collider.gameObject;
            ItemObject itemComponent = targetItem.GetComponent<ItemObject>();

            if (itemComponent != null)
            {
                bool canAdd = playerStats.AddWeight(itemComponent.itemWeight);
                if (canAdd)
                {
                    heldObject = targetItem;

                    Rigidbody rb = heldObject.GetComponent<Rigidbody>();
                    if (rb != null) { rb.isKinematic = true; }

                    Collider col = heldObject.GetComponent<Collider>();
                    if (col != null) { col.enabled = true; }

                    heldObject.transform.SetParent(holdPosition);
                    heldObject.transform.localPosition = Vector3.zero;
                    heldObject.transform.localRotation = Quaternion.identity;
                }
            }
        }
    }

    private void TryDropOrPlace()
    {
        // 1. ถ้ามองจุดวางของอยู่ (DropPoint)
        if (currentHoveredDropPoint != null && currentHoveredDropPoint.currentPlacedItem == null)
        {
            ItemObject item = heldObject.GetComponent<ItemObject>();
            float weight = item != null ? item.itemWeight : 0f;

            playerStats.RemoveWeight(weight);
            currentHoveredDropPoint.PlaceItem(heldObject, weight);

            heldObject = null;
            ClearHoveredDropPoint();
            return;
        }

        // 2. ถ้าไม่ได้มองจุดวาง ให้ทิ้งลงพื้น
        DropObjectToFloor();
    }

    private void DropObjectToFloor()
    {
        if (heldObject != null)
        {
            ItemObject item = heldObject.GetComponent<ItemObject>();
            if (item != null)
            {
                playerStats.RemoveWeight(item.itemWeight);
            }

            Rigidbody rb = heldObject.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = false; }

            Collider col = heldObject.GetComponent<Collider>();
            if (col != null) { col.enabled = true; }

            heldObject.transform.SetParent(null);
            heldObject = null;
        }
    }

    private void HandleDropPointHover()
    {
        Ray ray = new Ray(playerCam.transform.position, playerCam.transform.forward);

        // ยิงเช็คเลเยอร์ DropPoint
        if (Physics.Raycast(ray, out RaycastHit hit, pickupDistance, dropPointLayer))
        {
            BaseDropPoint detectedDropPoint = hit.collider.GetComponent<BaseDropPoint>();

            // [เพิ่ม Debug เช็ค]
            Debug.Log($"[Debug Raycast] ยิงโดน DropPoint: {hit.collider.name} | มีของถืออยู่ไหม: {heldObject != null}");

            if (heldObject != null && detectedDropPoint != null && detectedDropPoint.currentPlacedItem == null)
            {
                if (currentHoveredDropPoint != detectedDropPoint)
                {
                    ClearHoveredDropPoint();
                    currentHoveredDropPoint = detectedDropPoint;
                    currentHoveredDropPoint.ShowHologram(heldObject);
                    Debug.Log("[Debug Hologram] สั่งแสดง Hologram สำเร็จ!");
                }
                return;
            }
        }
        else
        {
            // ถ้าผู้เล่นถือของอยู่แต่กวาดสายตาแล้วไม่โดน Layer DropPoint จะขึ้นเตือนตรงนี้
            if (heldObject != null && Input.GetMouseButton(1)) // หรือเช็คตอนถือของ
            {
                // Debug.Log("Raycast ยังไม่โดน Layer DropPoint เช็ค Layer ดูอีกทีนะ");
            }
        }

        ClearHoveredDropPoint();
    }

    private void ClearHoveredDropPoint()
    {
        if (currentHoveredDropPoint != null)
        {
            currentHoveredDropPoint.HideHologram();
            currentHoveredDropPoint = null;
        }
    }
    // เพิ่มฟังก์ชันนี้เข้าไปใน PlayerPickupSystem.cs
    public void PickUpItemFromDropPoint(BaseDropPoint dropPoint)
    {
        if (heldObject != null) return; // ถ้ามือถือของอยู่แล้ว ให้หยิบเพิ่มไม่ได้

        GameObject itemToTake = dropPoint.RemoveItem();
        if (itemToTake != null)
        {
            // คำนวณน้ำหนัก (ถ้าเป็นกล่องพัสดุหรือไอเท็ม ให้ดึงน้ำหนักตามจริง)
            // หมายเหตุ: ถ้ากล่องพัสดุไม่มีน้ำหนักเฉพาะ ให้กำหนดค่าคงที่หรือดึงจาก ItemObject ด้านใน
            float weight = 5f;
            ItemObject itemComp = itemToTake.GetComponent<ItemObject>();
            if (itemComp != null) weight = itemComp.itemWeight;

            // เช็คโควต้าน้ำหนักผู้เล่น
            bool canAdd = playerStats.AddWeight(weight);
            if (canAdd)
            {
                heldObject = itemToTake;

                Rigidbody rb = heldObject.GetComponent<Rigidbody>();
                if (rb != null) { rb.isKinematic = true; }

                Collider col = heldObject.GetComponent<Collider>();
                if (col != null) { col.enabled = true; } // เปิด Collider กลับมา

                heldObject.transform.SetParent(holdPosition);
                heldObject.transform.localPosition = Vector3.zero;
                heldObject.transform.localRotation = Quaternion.identity;

                Debug.Log("หยิบวัตถุออกจาก Drop Point กลับขึ้นมือสำเร็จ");
            }
            else
            {
                // ถ้าแบกน้ำหนักไม่ไหว ให้วางกลับที่เดิม
                dropPoint.PlaceItem(itemToTake, weight);
                Debug.Log("น้ำหนักเกิน ไม่สามารถหยิบขึ้นมาได้!");
            }
        }
    }
    public GameObject GetHeldObject()
    {
        return heldObject;
    }
}
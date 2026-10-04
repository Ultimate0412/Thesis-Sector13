using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    [Header("Interaction Settings")]
    public float interactDistance = 3f;
    public LayerMask interactionLayer; // เลเยอร์สำหรับวัตถุที่กด Interact ได้ (กล่อง, ปุ่ม)
    private Camera playerCam;

    private void Start()
    {
        playerCam = GetComponentInChildren<Camera>();

        // ตรวจสอบให้แน่ใจว่า interactionLayer ครอบคลุมทั้ง Item (Layer 6) และ DropPoint (Layer 7)
        int requiredMask = (1 << 6) | (1 << 7);
        if (interactionLayer.value == 0 || (interactionLayer.value & requiredMask) != requiredMask)
        {
            interactionLayer |= requiredMask;
        }
    }

    private void Update()
    {
        // เมื่อกดปุ่ม E สำหรับการ Interact ทั่วไป (เช่น เปิด/ปิดกล่อง หรือหยิบของจากจุดวาง)
        if (Input.GetKeyDown(KeyCode.E))
        {
            TryInteract();
        }
    }

    private void TryInteract()
    {
        Ray ray = new Ray(playerCam.transform.position, playerCam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactionLayer))
        {
            // 1. เช็ค IInteractable (ทั้งบน collider หรือ parent เช่น กล่อง, จุดวาง, ปุ่ม)
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null)
            {
                interactable.Interact(this);
                return;
            }

            // 2. ถ้าวัตถุที่เล็งไม่มี IInteractable แต่เป็นวัตถุที่วางอยู่บน DropPoint ให้หยิบกลับขึ้นมือ
            BaseDropPoint dropPoint = BaseDropPoint.GetDropPointHolding(hit.collider.gameObject);
            if (dropPoint != null)
            {
                PlayerPickupSystem pickupSystem = GetComponent<PlayerPickupSystem>();
                if (pickupSystem != null)
                {
                    pickupSystem.PickUpItemFromDropPoint(dropPoint);
                }
            }
        }
    }
}
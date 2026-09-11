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
    }

    private void Update()
    {
        // เมื่อกดปุ่ม E สำหรับการ Interact ทั่วไป (เช่น เปิด/ปิดกล่อง)
        if (Input.GetKeyDown(KeyCode.E))
        {
            TryInteract();
            Debug.Log("E");
        }
    }
    private void TryInteract()
    {
        Ray ray = new Ray(playerCam.transform.position, playerCam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactionLayer))
        {
            // เช็คผ่าน Interface กลาง
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();
            if (interactable != null)
            {
                interactable.Interact(this);
            }
        }
    }
}
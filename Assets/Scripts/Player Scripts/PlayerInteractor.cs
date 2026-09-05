using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    [Header("Interaction Settings")]
    public float interactDistance = 3f;
    public LayerMask interactableLayer;      // Layer สำหรับสินค้า
    public LayerMask dropPointLayer;         // Layer สำหรับจุดวางของ
    public Transform holdPosition;

    private GameObject heldObject;
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

        if (Input.GetKeyDown(KeyCode.E))
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

    private void HandleDropPointHover()
    {
        Ray ray = new Ray(playerCam.transform.position, playerCam.transform.forward);

        // ยิงเช็คจุดวาง (DropPoint)
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, dropPointLayer))
        {
            BaseDropPoint detectedDropPoint = hit.collider.GetComponent<BaseDropPoint>();

            if (heldObject != null && detectedDropPoint != null && detectedDropPoint.currentPlacedItem == null)
            {
                if (currentHoveredDropPoint != detectedDropPoint)
                {
                    ClearHoveredDropPoint();
                    currentHoveredDropPoint = detectedDropPoint;
                    currentHoveredDropPoint.ShowHologram(heldObject);
                }
                return;
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

    private void TryPickUp()
    {
        Ray ray = new Ray(playerCam.transform.position, playerCam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactableLayer))
        {
            ItemObject item = hit.collider.GetComponent<ItemObject>();
            if (item != null)
            {
                bool canAdd = playerStats.AddWeight(item.itemWeight);
                if (canAdd)
                {
                    heldObject = hit.collider.gameObject;

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
}
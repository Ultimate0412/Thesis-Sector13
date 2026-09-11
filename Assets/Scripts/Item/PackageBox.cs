using UnityEngine;

public enum InspectionStatus { None, Approved, Rejected }

public class PackageBox : MonoBehaviour, IInteractable
{
    [Header("Inner Item Settings")]
    public GameObject innerItemPrefab;
    public Transform openSlot;
    public GameObject Hath;
    private GameObject spawnedInnerItem;

    [Header("Inspection Settings")]
    public int maxOpenPerDay = 3;
    public bool isOpen = false;

    public InspectionStatus currentStamp = InspectionStatus.None;
    public KeyCode stampMenuKey = KeyCode.R; // ปุ่มกดเปิดเมนูประทับตรา (เปลี่ยนปุ่มได้ตามต้องการ)

    // --- Implement จาก IInteractable (กดปุ่ม E เพื่อเปิด/ปิดกล่อง) ---
    public string GetInteractPrompt()
    {
        string boxAction = isOpen ? "กด E เพื่อปิดกล่อง" : "กด E เพื่อเปิดตรวจกล่อง";
        return $"{boxAction} | [กดปุ่ม R] เพื่อเปิดเมนูประทับตรา (สถานะ: {currentStamp})";
    }

    public void Interact(PlayerInteractor interactor)
    {
        // กด E เพื่อเปิดหรือปิดกล่องดูสินค้าภายใน
        ToggleOpenBox();
    }
    // ------------------------------------

    private void Update()
    {
        // หากผู้เล่นมองกล่องนี้อยู่ (ในระยะที่ Raycast ถึง หรือปรับใช้ระบบเช็คส, ผู้เล่นสามารถกดปุ่ม R เพื่อเปิดเมนูปั๊มตราได้)
        // หมายเหตุ: เพื่อให้ใช้งานง่าย สามารถใช้ร่วมกับการเช็ค Raycast หรือกดปุ่มเมื่ออยู่ใกล้
        if (Input.GetKeyDown(stampMenuKey))
        {
            // เช็คระยะห่างผู้เล่นกับกล่องคร่าวๆ ป้องกันกดได้จากทั่วแมพ (เช่น ภายในระยะ 3 เมตร)
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && Vector3.Distance(transform.position, player.transform.position) <= 3.5f)
            {
                OpenStampMenu();
            }
        }
    }

    public void OpenStampMenu()
    {
        if (StampUIController.Instance != null)
        {
            StampUIController.Instance.OpenStampMenu(this);
        }
        else
        {
            Debug.LogWarning("ยังไม่ได้สร้าง StampUIController ในฉาก!");
        }
    }

    public void SetStamp(InspectionStatus status)
    {
        currentStamp = status;
        Debug.Log($"ประทับตรากล่องเรียบร้อย: {currentStamp}");
    }

    public void ToggleOpenBox()
    {
        if (!isOpen)
        {
            if (GameDayManager.Instance != null && !GameDayManager.Instance.CanOpenBoxToday())
            {
                Debug.Log("โควต้าการเปิดกล่องตรวจภายในของวันนี้หมดแล้ว!");
                return;
            }
            OpenBox();
        }
        else
        {
            CloseBox();
        }
    }

    private void OpenBox()
    {
        isOpen = true;
        Hath.SetActive(false);
        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.ConsumeOpenQuota();
        }

        if (innerItemPrefab != null && spawnedInnerItem == null)
        {
            spawnedInnerItem = Instantiate(innerItemPrefab, openSlot.position, openSlot.rotation, openSlot);
        }
        else if (spawnedInnerItem != null)
        {
            spawnedInnerItem.SetActive(true);
        }
    }

    private void CloseBox()
    {
        isOpen = false;
        if (spawnedInnerItem != null)
        {
            spawnedInnerItem.SetActive(false);
        }
        Hath.SetActive(true);
    }
}
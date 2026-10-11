using UnityEngine;

public class StampUIController : MonoBehaviour
{
    public static StampUIController Instance;

    [Header("UI Canvas Group / Panel")]
    public GameObject stampMenuPanel; // ลาก Panel UI มาใส่

    private PackageBox targetPackage;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (stampMenuPanel != null) stampMenuPanel.SetActive(false);
    }

    private int menuOpenedFrame = -1;

    public void OpenStampMenu(PackageBox package)
    {
        targetPackage = package;
        menuOpenedFrame = Time.frameCount;
        if (stampMenuPanel != null)
        {
            stampMenuPanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public bool IsMenuOpen => stampMenuPanel != null && stampMenuPanel.activeSelf;
    public PackageBox CurrentTargetPackage => targetPackage;

    private void Update()
    {
        // ต้องผ่านไปอย่างน้อย 1 เฟรมหลังจากเปิดเมนู จึงจะตรวจจับคีย์ยกเลิก/ปิดเมนู
        // เพื่อป้องกัน Same-Frame Input Race Condition ที่เปิดแล้วปิดทับในเฟรมเดียวกัน
        if (IsMenuOpen && Time.frameCount > menuOpenedFrame)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.R))
            {
                CloseStampMenu();
            }
        }
    }

    public void CloseStampMenu()
    {
        targetPackage = null;
        if (stampMenuPanel != null)
        {
            stampMenuPanel.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // ปุ่ม UI "ผ่าน" จะเรียกฟังก์ชันนี้
    public void OnClickApprove()
    {
        if (targetPackage != null)
        {
            targetPackage.SetStamp(InspectionStatus.Approved);
        }
        CloseStampMenu();
    }

    // ปุ่ม UI "ตีกลับ" จะเรียกฟังก์ชันนี้
    public void OnClickReject()
    {
        if (targetPackage != null)
        {
            targetPackage.SetStamp(InspectionStatus.Rejected);
        }
        CloseStampMenu();
    }
}
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

    public void OpenStampMenu(PackageBox package)
    {
        targetPackage = package;
        if (stampMenuPanel != null)
        {
            stampMenuPanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
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
using UnityEngine;

/// <summary>
/// เก็บสถานะของ session ที่ต้องใช้ข้ามฉาก (MainMenu -> GamePlay)
/// ช่องเซฟใช้หมายเลข 1..SlotCount เพื่อให้อ่านง่ายใน UI
/// </summary>
public static class GameSession
{
    public const int SlotCount = 3;

    private const string LastSlotKey = "Sector13.LastSaveSlot";

    /// <summary>ช่องเซฟที่กำลังเล่นอยู่ (จำค่าไว้ใน PlayerPrefs)</summary>
    public static int CurrentSlot
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(LastSlotKey, 1), 1, SlotCount);
        set
        {
            PlayerPrefs.SetInt(LastSlotKey, Mathf.Clamp(value, 1, SlotCount));
            PlayerPrefs.Save();
        }
    }

    /// <summary>เปิด/ปิดการเซฟอัตโนมัติ (ปิดได้ตอนทดสอบ)</summary>
    public static bool AutoSaveEnabled = true;

    /// <summary>
    /// true เมื่อเริ่มเล่นจาก Main Menu (New Game / Load Game)
    /// ใช้กันไม่ให้ Auto Save เขียนทับเซฟตอนกด Play ฉาก GamePlay ตรง ๆ ใน Editor
    /// </summary>
    public static bool SessionActive = false;
}

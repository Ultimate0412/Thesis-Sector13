using System.Collections;
using UnityEngine;

/// <summary>
/// ทำงานหลังโหลดฉาก GamePlay: รอให้ Start ของทุกวัตถุทำงานครบ 1 เฟรม
/// แล้วจึงคืนสถานะจากเซฟ (หรือเริ่มเกมใหม่) เพื่อไม่ให้ ItemSpawner/UI แย่งกันตั้งค่า
/// </summary>
public class GameplaySaveLoader : MonoBehaviour
{
    private SaveGameData pendingLoad;
    private bool pendingNewGame;
    private int pendingNewGameSlot;

    private void Awake()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        pendingNewGame = SaveSystem.ConsumePendingNewGame(out pendingNewGameSlot);
        if (!pendingNewGame)
        {
            pendingLoad = SaveSystem.ConsumePendingLoad();
        }

        // ตั้งวันก่อน Start ของวัตถุอื่น จะได้โหลด DayConfig ของวันที่ถูกต้อง
        int targetDay = pendingNewGame ? 1 : (pendingLoad != null ? pendingLoad.day : -1);
        if (targetDay > 0 && GameDayManager.Instance != null)
        {
            GameDayManager.Instance.SetDayBeforeStart(targetDay);
        }
    }

    private IEnumerator Start()
    {
        // รอ 1 เฟรมให้ Start/Awake ของทุกอย่างในฉากทำงานเสร็จก่อน
        yield return null;

        if (pendingNewGame)
        {
            StartFreshGame();
        }
        else if (pendingLoad != null)
        {
            SaveSystem.ApplyLoadedGame(pendingLoad);
            Debug.Log($"[GameplaySaveLoader] โหลดเซฟช่อง {pendingLoad.slotIndex} สำเร็จ (Day {pendingLoad.day})");
        }

        pendingLoad = null;
        pendingNewGame = false;
    }

    private void StartFreshGame()
    {
        SaveGameData fresh = new SaveGameData
        {
            version = 1,
            slotIndex = pendingNewGameSlot,
            savedAtUtc = System.DateTime.UtcNow.ToString("o"),
            day = 1,
            totalScore = 0,
            rejectedPackagesCount = 0,
            remainingOpenQuota = -1,   // -1 = ใช้โควต้าเต็มของวันตาม DayConfig
            remainingSpawnQuota = -1,
            player = null,             // เกมใหม่ไม่ต้องย้ายตำแหน่งผู้เล่น
            items = new SaveItemEntry[0]
        };

        SaveSystem.ApplyLoadedGame(fresh);

        // เขียนเซฟเริ่มต้นทันที เพื่อให้ช่องแสดง Day 1 ตั้งแต่ยังไม่จบวัน
        SaveSystem.SaveToSlot(pendingNewGameSlot);

        Debug.Log($"[GameplaySaveLoader] เริ่มเกมใหม่ในช่อง {pendingNewGameSlot} (Day 1)");
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// เตรียมวัตถุที่จำเป็นแบบ runtime (ไม่สร้าง UI)
/// - GamePlay -> GameplaySaveLoader
/// UI ทั้งหมด (Main Menu / Pause Menu) อยู่ใน scene และผูก reference ผ่าน Inspector
/// </summary>
public static class GameBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
#if UNITY_EDITOR
        // ใน Editor ให้ Play Mode เดินต่อแม้สลับหน้าต่าง (ค่าใน Player Settings ยังเป็น false สำหรับ build จริง)
        Application.runInBackground = true;
#endif

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureSceneObjects(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureSceneObjects(scene);
    }

    private static void EnsureSceneObjects(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        if (scene.name != "GamePlay") return;

        if (Object.FindAnyObjectByType<GameplaySaveLoader>() == null)
        {
            new GameObject("SaveLoader").AddComponent<GameplaySaveLoader>();
        }
    }
}

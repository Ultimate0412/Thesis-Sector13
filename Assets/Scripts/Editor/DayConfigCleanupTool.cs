using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// เครื่องมือลบ GameEventManager / ThiefManager ออกจาก Scene ที่ไม่มี DayConfig
/// ใช้เมื่อต้องการทำความสะอาดฉากที่ไม่ได้ใช้ระบบรายวัน
/// </summary>
public static class DayConfigCleanupTool
{
    private static readonly string[] ManagerGuids =
    {
        "668780903b2cd6344a30faaddabb55ab", // GameEventManager
        "beafd8c768ea49cb8c5fe3130a7ce170"  // ThiefManager
    };

    [MenuItem("Tools/Sector13/Remove Day Managers Without Config")]
    public static void RemoveDayManagersWithoutConfig()
    {
        if (SceneManager.GetActiveScene().isDirty)
        {
            Debug.LogWarning("[DayConfigCleanupTool] กรุณา save Scene ปัจจุบันก่อนรันเครื่องมือนี้");
            return;
        }

        List<string> candidatePaths = FindCandidateScenePaths();
        if (candidatePaths.Count == 0)
        {
            Debug.Log("[DayConfigCleanupTool] ไม่พบ Scene ที่มี GameEventManager/ThiefManager");
            return;
        }

        string originalScenePath = SceneManager.GetActiveScene().path;
        int changedScenes = 0;
        int removedManagers = 0;

        foreach (string scenePath in candidatePaths)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            GameDayManager dayManager = Object.FindAnyObjectByType<GameDayManager>();

            bool hasConfig = dayManager != null
                && dayManager.dayConfigs != null
                && dayManager.dayConfigs.Length > 0;

            if (hasConfig)
            {
                Debug.Log($"[DayConfigCleanupTool] {scenePath}: มี DayConfig {dayManager.dayConfigs.Length} วัน — ไม่ลบ Manager");
                continue;
            }

            int removedInScene = 0;

            GameEventManager[] eventManagers = Object.FindObjectsByType<GameEventManager>(FindObjectsSortMode.None);
            foreach (GameEventManager manager in eventManagers)
            {
                Object.DestroyImmediate(manager.gameObject);
                removedInScene++;
            }

            ThiefManager[] thiefManagers = Object.FindObjectsByType<ThiefManager>(FindObjectsSortMode.None);
            foreach (ThiefManager manager in thiefManagers)
            {
                Object.DestroyImmediate(manager.gameObject);
                removedInScene++;
            }

            if (removedInScene > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                changedScenes++;
                removedManagers += removedInScene;
                Debug.Log($"[DayConfigCleanupTool] {scenePath}: ลบ Manager {removedInScene} ตัว");
            }
            else
            {
                Debug.Log($"[DayConfigCleanupTool] {scenePath}: ไม่มี Manager ให้ลบ");
            }
        }

        if (!string.IsNullOrEmpty(originalScenePath))
        {
            EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[DayConfigCleanupTool] เสร็จสิ้น: เปลี่ยน {changedScenes} ฉาก, ลบ Manager {removedManagers} ตัว");
    }

    private static List<string> FindCandidateScenePaths()
    {
        List<string> paths = new List<string>();
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");

        foreach (string guid in sceneGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/")) continue;

            string contents = System.IO.File.ReadAllText(path);
            foreach (string managerGuid in ManagerGuids)
            {
                if (contents.Contains(managerGuid))
                {
                    paths.Add(path);
                    break;
                }
            }
        }

        return paths;
    }
}

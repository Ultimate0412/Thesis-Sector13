using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// สร้าง/อัปเดต asset ที่ระบบเซฟต้องใช้ใน Resources
/// - SavePrefabDatabase: รายการ prefab สินค้า/กล่องสำหรับแปลงชื่อกลับเป็น prefab ตอนโหลด
/// </summary>
public static class SavePrefabDatabaseBuilder
{
    private const string ResourcesFolder = "Assets/Resources";
    private const string DatabasePath = "Assets/Resources/SavePrefabDatabase.asset";

    private static readonly string[] PrefabSearchFolders =
    {
        "Assets/Prefab",
        "Assets/PrefabPrtotype",
        "Assets/_Project/Prefab"
    };

    [MenuItem("Tools/Sector13/Save/Rebuild Save Resources")]
    public static void RebuildAll()
    {
        EnsureFolder();
        RebuildPrefabDatabase();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SavePrefabDatabaseBuilder] อัปเดต SavePrefabDatabase เรียบร้อย");
    }

    [MenuItem("Tools/Sector13/Save/Open Save Folder")]
    public static void OpenSaveFolder()
    {
        string folder = System.IO.Path.Combine(Application.persistentDataPath, "Saves");
        System.IO.Directory.CreateDirectory(folder);
        EditorUtility.RevealInFinder(folder);
    }

    /// <summary>สร้าง asset อัตโนมัติถ้ายังไม่มี (ไม่สแกนซ้ำทุกครั้งที่ domain reload)</summary>
    [InitializeOnLoadMethod]
    private static void EnsureResourcesExist()
    {
        if (AssetDatabase.LoadAssetAtPath<SavePrefabDatabase>(DatabasePath) != null)
        {
            return;
        }

        EditorApplication.delayCall += () =>
        {
            if (AssetDatabase.LoadAssetAtPath<SavePrefabDatabase>(DatabasePath) == null)
            {
                RebuildAll();
            }
        };
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(ResourcesFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
    }

    private static void RebuildPrefabDatabase()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", PrefabSearchFolders);

        List<GameObject> candidates = new List<GameObject>();
        HashSet<string> seenNames = new HashSet<string>();
        HashSet<string> duplicateNames = new HashSet<string>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            bool isPackage = prefab.GetComponentInChildren<PackageBox>(true) != null;
            bool isItem = prefab.GetComponentInChildren<ItemObject>(true) != null;
            if (!isPackage && !isItem) continue;

            if (!seenNames.Add(prefab.name))
            {
                duplicateNames.Add(prefab.name);
                continue;
            }

            candidates.Add(prefab);
        }

        candidates = candidates.OrderBy(p => p.name).ToList();

        foreach (string duplicate in duplicateNames)
        {
            Debug.LogWarning($"[SavePrefabDatabaseBuilder] ชื่อ prefab ซ้ำ: '{duplicate}' — ใช้ตัวแรกที่เจอเท่านั้น");
        }

        SavePrefabDatabase database = AssetDatabase.LoadAssetAtPath<SavePrefabDatabase>(DatabasePath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<SavePrefabDatabase>();
            AssetDatabase.CreateAsset(database, DatabasePath);
        }

        database.prefabs = candidates.ToArray();
        EditorUtility.SetDirty(database);

        Debug.Log($"[SavePrefabDatabaseBuilder] เก็บ prefab {candidates.Count} รายการลง SavePrefabDatabase");
    }
}

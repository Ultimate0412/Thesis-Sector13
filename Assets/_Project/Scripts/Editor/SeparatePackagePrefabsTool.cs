using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SeparatePackagePrefabsTool
{
    private const string IllegalPackagePath = "Assets/_Project/Prefabs/Gameplay/Illegal_Package.prefab";
    private const string MimicPackagePath = "Assets/_Project/Prefabs/Gameplay/Mimic_Package.prefab";

    static SeparatePackagePrefabsTool()
    {
        EditorApplication.delayCall += CheckAndUnpack;
    }

    [MenuItem("Tools/Sector13/Separate Package Prefabs (Unpack Variants)")]
    public static void ManualUnpack()
    {
        ExecuteUnpack(true);
    }

    private static void CheckAndUnpack()
    {
        ExecuteUnpack(false);
    }

    private static void ExecuteUnpack(bool force)
    {
        bool changed = false;

        if (UnpackPrefabIfVariant(IllegalPackagePath, force)) changed = true;
        if (UnpackPrefabIfVariant(MimicPackagePath, force)) changed = true;

        if (changed)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=#55FF88>[SeparatePackagePrefabsTool] Successfully separated Illegal_Package and Mimic_Package into independent prefabs!</color>");
        }
    }

    private static bool UnpackPrefabIfVariant(string assetPath, bool force)
    {
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefabAsset == null)
        {
            Debug.LogError($"[SeparatePackagePrefabsTool] Cannot find asset at {assetPath}");
            return false;
        }

        PrefabAssetType assetType = PrefabUtility.GetPrefabAssetType(prefabAsset);
        bool isVariant = (assetType == PrefabAssetType.Variant) || PrefabUtility.IsPartOfVariantPrefab(prefabAsset);
        if (!isVariant && !force)
        {
            return false;
        }

        Debug.Log($"[SeparatePackagePrefabsTool] Unpacking {assetPath} from its base prefab...");

        // Instantiate into current scene
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
        if (instance == null)
        {
            Debug.LogError($"[SeparatePackagePrefabsTool] Failed to instantiate {assetPath}");
            return false;
        }

        // Unpack all prefab links completely so it is 100% independent
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        // Save back over the existing asset (preserves GUID in .meta)
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, assetPath);
        bool success = (saved != null);

        // Destroy temporary scene instance
        Object.DestroyImmediate(instance);

        if (success)
        {
            Debug.Log($"[SeparatePackagePrefabsTool] Successfully saved independent prefab: {assetPath}");
            return true;
        }
        else
        {
            Debug.LogError($"[SeparatePackagePrefabsTool] Failed to save prefab asset to {assetPath}");
            return false;
        }
    }
}

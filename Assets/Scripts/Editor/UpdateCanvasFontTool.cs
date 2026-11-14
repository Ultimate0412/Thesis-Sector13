using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace Sector13.EditorTools
{
    public static class UpdateCanvasFontTool
    {
        private const string SunspireFontGuid = "851ea7628f91ef0418f31c506e1851eb";

        [MenuItem("Tools/Sector13/Apply Sunspire Font to All UI Canvases")]
        public static void ApplySunspireFont()
        {
            string fontPath = AssetDatabase.GUIDToAssetPath(SunspireFontGuid);
            TMP_FontAsset sunspireFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);

            if (sunspireFont == null)
            {
                Debug.LogError($"[UpdateCanvasFontTool] Could not load Sunspire Font Asset from GUID {SunspireFontGuid}");
                return;
            }

            int totalUpdated = 0;

            // 1. Process Canvas prefabs (excluding ShippingLabelCanvas)
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            foreach (string guid in prefabGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                // Skip shipping label prefabs and box prefabs containing shipping label
                if (assetPath.Contains("ShippingLabel", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);
                if (prefabRoot == null) continue;

                bool modified = false;
                TMP_Text[] texts = prefabRoot.GetComponentsInChildren<TMP_Text>(true);
                foreach (TMP_Text text in texts)
                {
                    // Ignore anything under a ShippingLabelCanvas hierarchy
                    Canvas parentCanvas = text.GetComponentInParent<Canvas>(true);
                    if (parentCanvas != null && parentCanvas.gameObject.name.Contains("ShippingLabel", System.StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (text.font != sunspireFont)
                    {
                        Undo.RecordObject(text, "Change Font to Sunspire");
                        text.font = sunspireFont;
                        modified = true;
                        totalUpdated++;
                    }
                }

                if (modified)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
                    Debug.Log($"[UpdateCanvasFontTool] Updated font in prefab: {assetPath}");
                }

                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            // 2. Process active open scene
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.isLoaded)
            {
                bool sceneModified = false;
                TMP_Text[] sceneTexts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (TMP_Text text in sceneTexts)
                {
                    Canvas parentCanvas = text.GetComponentInParent<Canvas>(true);
                    if (parentCanvas != null && parentCanvas.gameObject.name.Contains("ShippingLabel", System.StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (text.font != sunspireFont)
                    {
                        Undo.RecordObject(text, "Change Font to Sunspire");
                        text.font = sunspireFont;
                        sceneModified = true;
                        totalUpdated++;
                    }
                }

                if (sceneModified)
                {
                    EditorSceneManager.MarkSceneDirty(activeScene);
                    Debug.Log($"[UpdateCanvasFontTool] Updated font in scene: {activeScene.name}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[UpdateCanvasFontTool] Complete! Updated {totalUpdated} TextMeshPro components to Sunspire SDF.");
        }
    }
}

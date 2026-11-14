using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Sector13.EditorTools
{
    public static class UpdateCanvasThemeTool
    {
        // Theme Colors
        // Cyan -> #ffde59
        public static readonly Color GoldColor = new Color(1f, 0.87058824f, 0.34901962f, 1f);
        // Dark Blue -> #1e1a0b
        public static readonly Color DarkOliveColor = new Color(0.11764706f, 0.101960786f, 0.043137256f, 1f);

        [MenuItem("Tools/Sector13/Apply Canvas Theme (#ffde59 and #1e1a0b)")]
        public static void ApplyTheme()
        {
            int updatedCount = 0;

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            foreach (string guid in prefabGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (assetPath.Contains("ShippingLabel", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                GameObject root = PrefabUtility.LoadPrefabContents(assetPath);
                if (root == null) continue;

                bool modified = ProcessHierarchy(root.transform);
                if (modified)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                    Debug.Log($"[UpdateCanvasThemeTool] Updated colors in prefab: {assetPath}");
                    updatedCount++;
                }

                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[UpdateCanvasThemeTool] Completed theme update across prefabs.");
        }

        private static bool ProcessHierarchy(Transform root)
        {
            bool modified = false;

            Image[] images = root.GetComponentsInChildren<Image>(true);
            foreach (Image img in images)
            {
                if (IsCyanColor(img.color))
                {
                    img.color = new Color(GoldColor.r, GoldColor.g, GoldColor.b, img.color.a);
                    modified = true;
                }
                else if (IsDarkBlueColor(img.color))
                {
                    img.color = new Color(DarkOliveColor.r, DarkOliveColor.g, DarkOliveColor.b, img.color.a);
                    modified = true;
                }
            }

            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text txt in texts)
            {
                if (IsCyanColor(txt.color))
                {
                    txt.color = new Color(GoldColor.r, GoldColor.g, GoldColor.b, txt.color.a);
                    modified = true;
                }
            }

            return modified;
        }

        private static bool IsCyanColor(Color c)
        {
            // Detect cyan / sky blue tones (high blue & green, lower red)
            return c.b > 0.65f && c.g > 0.65f && c.r < 0.65f;
        }

        private static bool IsDarkBlueColor(Color c)
        {
            // Detect deep dark blue / navy tones
            return c.b > c.r && c.b > c.g && c.r < 0.25f && c.g < 0.35f && c.b < 0.65f;
        }
    }
}

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
        // Primary Accent -> #FFD34D
        public static readonly Color GoldColor = new Color(1f, 0.827451f, 0.3019608f, 1f);
        // Background Dark -> #111111
        public static readonly Color DarkColor = new Color(0.06666667f, 0.06666667f, 0.06666667f, 1f);

        [MenuItem("Tools/Sector13/Apply Canvas Theme (#FFD34D and #111111)")]
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
            Debug.Log($"[UpdateCanvasThemeTool] Completed theme update across prefabs. Updated {updatedCount} prefabs.");
        }

        private static bool ProcessHierarchy(Transform root)
        {
            bool modified = false;

            Image[] images = root.GetComponentsInChildren<Image>(true);
            foreach (Image img in images)
            {
                if (IsAccentColor(img.color))
                {
                    img.color = new Color(GoldColor.r, GoldColor.g, GoldColor.b, img.color.a);
                    modified = true;
                }
                else if (IsDarkColor(img.color))
                {
                    img.color = new Color(DarkColor.r, DarkColor.g, DarkColor.b, img.color.a);
                    modified = true;
                }
            }

            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text txt in texts)
            {
                if (IsAccentColor(txt.color))
                {
                    txt.color = new Color(GoldColor.r, GoldColor.g, GoldColor.b, txt.color.a);
                    modified = true;
                }
            }

            return modified;
        }

        private static bool IsAccentColor(Color c)
        {
            // Detect cyan or old #ffde59 gold/yellow tones
            bool isCyan = c.b > 0.65f && c.g > 0.65f && c.r < 0.65f;
            bool isOldGold = c.r > 0.95f && c.g > 0.84f && c.b > 0.30f && c.b < 0.40f;
            return isCyan || isOldGold;
        }

        private static bool IsDarkColor(Color c)
        {
            // Detect deep dark blue / navy or old #1e1a0b tones
            bool isDarkBlue = c.b > c.r && c.b > c.g && c.r < 0.25f && c.g < 0.35f && c.b < 0.65f;
            bool isOldDark = c.r > 0.10f && c.r < 0.13f && c.g > 0.08f && c.g < 0.12f && c.b > 0.03f && c.b < 0.06f;
            return isDarkBlue || isOldDark;
        }
    }
}

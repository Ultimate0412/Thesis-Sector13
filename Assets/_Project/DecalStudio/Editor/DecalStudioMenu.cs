using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Sector13.Decals.EditorTools
{
    /// <summary>
    /// เมนูลัดของ Decal Studio: build atlas, สแกนโฟลเดอร์ texture, และช่วยติดตั้ง DecalSlot
    /// </summary>
    public static class DecalStudioMenu
    {
        const string DefaultSettingsPath = "Assets/_Project/DecalStudio/Settings/AtlasBuildSettings.asset";

        [MenuItem("Tools/Sector13/Decal Studio/Build Atlas (default settings)", false, 11)]
        public static void BuildDefaultAtlas()
        {
            var settings = LoadOrCreateSettings();

            if (settings.sources == null || settings.sources.Count == 0)
            {
                settings.sources = CollectDefaultSources();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }

            var atlas = DecalAtlasBuilder.Build(settings);
            Selection.activeObject = atlas;
            EditorGUIUtility.PingObject(atlas);
        }

        [MenuItem("Tools/Sector13/Decal Studio/Scan external decal folders", false, 12)]
        public static void ScanExternalFolders()
        {
            var settings = LoadOrCreateSettings();
            var found = CollectExternalSources(settings);

            if (found.Count == 0)
            {
                Debug.LogWarning("[DecalStudio] ไม่พบไฟล์ *_Albedo.png ใต้: " + settings.externalSourceRoot +
                                 " — ตรวจว่า export จาก Substance ลงที่ <ชื่อ>/Textures/ แล้ว");
                return;
            }

            settings.sources = Merge(settings.sources, found);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DecalStudio] สแกนพบ {found.Count} ชุด (รวมแล้ว {settings.sources.Count}): " +
                      string.Join(", ", found.ConvertAll(f => f.id)));
        }

        [MenuItem("Tools/Sector13/Decal Studio/Add DecalSlot to selected projectors", false, 30)]
        public static void AddSlotToSelection()
        {
            int added = 0;
            foreach (var go in Selection.gameObjects)
            {
                if (go.GetComponent<DecalProjector>() == null) continue;
                if (go.GetComponent<DecalSlot>() != null) continue;
                Undo.AddComponent<DecalSlot>(go);
                added++;
            }
            Debug.Log($"[DecalStudio] เพิ่ม DecalSlot {added} ตัว");
        }

        static DecalAtlasBuildSettings LoadOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<DecalAtlasBuildSettings>(DefaultSettingsPath);
            if (settings != null) return settings;

            EnsureFolder(DefaultSettingsPath);
            settings = ScriptableObject.CreateInstance<DecalAtlasBuildSettings>();
            AssetDatabase.CreateAsset(settings, DefaultSettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        /// <summary>สแกน &lt;root&gt;/&lt;ชื่อ&gt;/Textures/*_Albedo.png แล้วจับคู่ TraceType จากชื่อโฟลเดอร์</summary>
        static List<DecalSourceEntry> CollectExternalSources(DecalAtlasBuildSettings settings)
        {
            var list = new List<DecalSourceEntry>();
            string root = settings.externalSourceRoot;
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return list;

            foreach (string dir in Directory.GetDirectories(root))
            {
                string folderName = Path.GetFileName(dir);
                if (folderName.StartsWith("_")) continue;

                string textures = Path.Combine(dir, "Textures");
                if (!Directory.Exists(textures)) textures = dir;

                string albedoFile = Path.Combine(textures, string.Format(settings.albedoPattern, folderName));
                if (!File.Exists(albedoFile)) continue;

                if (!TryResolveTraceType(folderName, out TraceType type))
                {
                    Debug.LogWarning($"[DecalStudio] ข้าม '{folderName}' — ชื่อไม่ตรงกับ TraceType");
                    continue;
                }

                list.Add(new DecalSourceEntry
                {
                    id = folderName,
                    traceType = type,
                    requiresUV = DecalTintPresets.IsUV(type),
                    externalFolder = textures,
                    tint = DecalTintPresets.For(type),
                    heightStrength = DecalTintPresets.HeightFor(type)
                });
            }

            list.Sort((a, b) => a.traceType.CompareTo(b.traceType));
            return list;
        }

        /// <summary>รวม source ใหม่เข้ากับของเดิม โดยยึด id เป็นตัวจับคู่ (ของใหม่ทับของเก่า)</summary>
        static List<DecalSourceEntry> Merge(List<DecalSourceEntry> existing, List<DecalSourceEntry> incoming)
        {
            var merged = existing != null ? new List<DecalSourceEntry>(existing) : new List<DecalSourceEntry>();
            foreach (var entry in incoming)
            {
                int index = merged.FindIndex(e => e != null && e.id == entry.id);
                if (index >= 0) merged[index] = entry;
                else merged.Add(entry);
            }
            merged.Sort((a, b) => a.traceType.CompareTo(b.traceType));
            return merged;
        }

        /// <summary>แปลงชื่อโฟลเดอร์/ชื่อ texture set → TraceType (รองรับชื่อย่อของ SP)</summary>
        static bool TryResolveTraceType(string name, out TraceType type)
        {
            if (System.Enum.TryParse(name, true, out type)) return true;

            switch (name.ToLowerInvariant())
            {
                case "blood": type = TraceType.BloodStain; return true;
                case "acid": type = TraceType.AcidBurn; return true;
                case "tape": type = TraceType.TapeResidue; return true;
                case "scuff": type = TraceType.CardboardScuff; return true;
                case "slime": type = TraceType.AlienSlime_UV; return true;
                case "chemical": type = TraceType.ChemicalResidue_UV; return true;
            }

            foreach (string enumName in System.Enum.GetNames(typeof(TraceType)))
            {
                if (enumName.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf(enumName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    type = (TraceType)System.Enum.Parse(typeof(TraceType), enumName);
                    return true;
                }
            }

            type = default;
            return false;
        }

        static List<DecalSourceEntry> CollectDefaultSources()
        {
            var list = new List<DecalSourceEntry>();
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Project/Art/Textures/Traces" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path);
                if (!fileName.StartsWith("T_")) continue;
                if (!System.Enum.TryParse(fileName.Substring(2), out TraceType type)) continue;

                list.Add(new DecalSourceEntry
                {
                    id = type.ToString(),
                    traceType = type,
                    requiresUV = DecalTintPresets.IsUV(type),
                    albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(path),
                    tint = DecalTintPresets.For(type),
                    heightStrength = DecalTintPresets.HeightFor(type)
                });
            }
            list.Sort((a, b) => a.traceType.CompareTo(b.traceType));
            return list;
        }

        static void EnsureFolder(string assetPath)
        {
            string dir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(dir) || AssetDatabase.IsValidFolder(dir)) return;
            string parent = Path.GetDirectoryName(dir)?.Replace('\\', '/');
            string leaf = Path.GetFileName(dir);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent + "/dummy.tmp");
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}

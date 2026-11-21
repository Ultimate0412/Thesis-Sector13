using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Sector13.Decals.EditorTools
{
    /// <summary>
    /// เมนูลัดของ Decal Studio: build atlas, สแกนโฟลเดอร์, ติดตั้ง/ตรวจ slot, และ bake layout
    /// </summary>
    public static class DecalStudioMenu
    {
        const string DefaultSettingsPath = "Assets/_Project/DecalStudio/Settings/AtlasBuildSettings.asset";
        const string LibraryPath = "Assets/_Project/DecalStudio/Art/DecalLibrary.asset";

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
                Debug.LogWarning("[DecalStudio] ไม่พบไฟล์ *_Albedo.png ใต้: " + settings.externalSourceRoot);
                return;
            }

            settings.sources = Merge(settings.sources, found);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DecalStudio] สแกนพบ {found.Count} ชุด (รวมแล้ว {settings.sources.Count})");
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

        [MenuItem("Tools/Sector13/Decal Studio/Bake Procedural Layout to Slots", false, 32)]
        public static void BakeSelectionToSlots()
        {
            int baked = 0;
            foreach (var go in Selection.gameObjects)
            {
                var path = AssetDatabase.GetAssetPath(go);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab")) continue;
                if (BakePrefab(path)) baked++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[DecalStudio] bake slots สำเร็จ {baked} prefab");
        }

        [MenuItem("Tools/Sector13/Decal Studio/Validate Decal Slots (overlap)", false, 33)]
        public static void ValidateSlots()
        {
            int problems = 0;
            foreach (var go in Selection.gameObjects)
            {
                var path = AssetDatabase.GetAssetPath(go);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab")) continue;

                var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
                var slots = root.GetComponentsInChildren<DecalSlot>(true);
                for (int i = 0; i < slots.Length; i++)
                {
                    var a = slots[i];
                    var pa = a.GetComponent<DecalProjector>();
                    if (pa == null) continue;

                    for (int j = i + 1; j < slots.Length; j++)
                    {
                        var b = slots[j];
                        var pb = b.GetComponent<DecalProjector>();
                        if (pb == null) continue;
                        if (a.allowedVerdicts != null && b.allowedVerdicts != null &&
                            a.allowedVerdicts.Length > 0 && b.allowedVerdicts.Length > 0 &&
                            a.allowedVerdicts[0] != b.allowedVerdicts[0]) continue;

                        float minDist = (pa.size.x + pb.size.x) * 0.5f * 0.9f;
                        float dist = Vector3.Distance(a.transform.localPosition, b.transform.localPosition);
                        if (dist < minDist)
                        {
                            problems++;
                            Debug.LogWarning($"[DecalStudio] {Path.GetFileName(path)}: '{a.name}' ทับ '{b.name}' (dist {dist:F2} < {minDist:F2})");
                        }
                    }
                }
                UnityEditor.PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log($"[DecalStudio] ตรวจแล้วพบรอยทับกัน {problems} คู่");
        }

        static bool BakePrefab(string path)
        {
            var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
            var applier = root.GetComponentInChildren<PackageTraceApplier>(true);
            if (applier == null)
            {
                UnityEditor.PrefabUtility.UnloadPrefabContents(root);
                return false;
            }

            // ลบ container เดิมทั้งหมด (อาจมีหลายอันค้างจาก bake รอบก่อน)
            var staleContainers = new List<Transform>();
            foreach (var t in applier.GetComponentsInChildren<Transform>(true))
            {
                if (t != applier.transform && t.name == "DecalSlots") staleContainers.Add(t);
            }
            foreach (var t in staleContainers)
            {
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }

            var container = new GameObject("DecalSlots");
            container.transform.SetParent(applier.transform, false);

            var library = AssetDatabase.LoadAssetAtPath<DecalLibrary>(LibraryPath);

            var plans = new (VerdictType verdict, ItemCategory category)[]
            {
                (VerdictType.Approve, ItemCategory.Legal),
                (VerdictType.Illegal, ItemCategory.Illegal),
                (VerdictType.Alien, ItemCategory.Alien)
            };

            foreach (var plan in plans)
            {
                var verdictRule = applier.spawnTable != null ? applier.spawnTable.Get(plan.verdict) : null;
                if (verdictRule == null || !verdictRule.IsUsable) continue;

                applier.spawnMode = TraceSpawnMode.Procedural;
                applier.ClearTraces();

                int seedBase = 1000 + (int)plan.verdict * 100;
                for (int r = 0; r < verdictRule.rules.Count; r++)
                {
                    var rule = verdictRule.rules[r];
                    if (rule == null || !rule.enabled) continue;

                    GameObject created = null;
                    for (int attempt = 0; attempt < 6 && created == null; attempt++)
                        created = applier.SpawnRuleForBake(rule, seedBase + r * 10 + attempt, r + attempt);
                    if (created == null) continue;

                    var slot = created.GetComponent<DecalSlot>();
                    if (slot == null) slot = created.AddComponent<DecalSlot>();

                    slot.traceType = rule.traceType;
                    slot.visibility = rule.visibility;
                    slot.allowedVerdicts = new[] { plan.verdict };
                    slot.weight = Mathf.Max(1f, rule.weight);

                    if (!string.IsNullOrEmpty(rule.decalId)) slot.decalId = rule.decalId;
                    else
                    {
                        var projector = created.GetComponent<DecalProjector>();
                        if (library != null && projector != null && projector.material != null)
                        {
                            foreach (var entry in library.AllEntries())
                            {
                                if (entry != null && entry.material == projector.material)
                                {
                                    slot.decalId = entry.id;
                                    break;
                                }
                            }
                        }
                    }

                    created.name = $"Slot_{plan.verdict}_{r:00}_{rule.traceType}";
                    created.transform.SetParent(container.transform, true);
                }

                applier.ClearTraces();
            }

            applier.spawnMode = TraceSpawnMode.AuthoredSlots;
            UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEditor.PrefabUtility.UnloadPrefabContents(root);
            return true;
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

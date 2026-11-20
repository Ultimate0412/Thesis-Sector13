using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sector13.Decals.EditorTools
{
    /// <summary>
    /// สร้าง placement profile + spawn table เริ่มต้น (ยึดตามตารางดีไซน์)
    /// หลังจากนี้แก้ค่าได้ใน Inspector ทั้งหมด ไม่ต้องแตะโค้ด
    /// </summary>
    public static class DecalSpawnDefaults
    {
        const string Root = "Assets/_Project/DecalStudio/Art/Spawn";
        const string ProfileDir = Root + "/Placement";
        const string TablePath = "Assets/_Project/DecalStudio/Resources/TraceSpawnTable.asset";

        [MenuItem("Tools/Sector13/Decal Studio/Create Default Spawn Table", false, 20)]
        public static void CreateDefaults()
        {
            EnsureFolder(ProfileDir);
            EnsureFolder(Root);

            var water = Profile("WaterStain", DecalFaceMask.Bottom | DecalFaceMask.Sides, margin: 0.75f,
                edge: 0.15f, gravity: 0.35f, spin: new Vector2(0, 360), size: new Vector2(0.35f, 0.6f));
            var oil = Profile("OilStain", DecalFaceMask.Bottom | DecalFaceMask.Sides, margin: 0.7f,
                edge: 0.1f, gravity: 0.25f, spin: new Vector2(0, 360), size: new Vector2(0.3f, 0.55f));
            var scuff = Profile("CardboardScuff", DecalFaceMask.Sides | DecalFaceMask.Top, margin: 0.85f,
                edge: 0.35f, gravity: 0f, spin: new Vector2(0, 360), size: new Vector2(0.3f, 0.6f));
            var claw = Profile("ClawMarks", DecalFaceMask.Sides | DecalFaceMask.Top, margin: 0.8f,
                edge: 0.05f, gravity: 0.1f, spin: new Vector2(0, 20), size: new Vector2(0.35f, 0.55f));
            var blood = Profile("BloodStain", DecalFaceMask.Sides | DecalFaceMask.Bottom, margin: 0.75f,
                edge: 0.1f, gravity: 0.7f, spin: new Vector2(0, 15), size: new Vector2(0.3f, 0.5f));
            var acid = Profile("AcidBurn", DecalFaceMask.Top | DecalFaceMask.Sides, margin: 0.8f,
                edge: 0.05f, gravity: 0f, spin: new Vector2(0, 360), size: new Vector2(0.2f, 0.4f));
            var frost = Profile("Frost", DecalFaceMask.Top | DecalFaceMask.Sides, margin: 0.85f,
                edge: 0.4f, gravity: 0f, spin: new Vector2(0, 360), size: new Vector2(0.4f, 0.7f));
            var tape = Profile("TapeResidue", DecalFaceMask.Sides, margin: 0.9f,
                edge: 0.95f, gravity: 0f, spin: new Vector2(0, 5), size: new Vector2(0.35f, 0.6f));
            var slime = Profile("AlienSlime_UV", DecalFaceMask.Sides | DecalFaceMask.Top, margin: 0.75f,
                edge: 0.1f, gravity: 0.3f, spin: new Vector2(0, 360), size: new Vector2(0.35f, 0.6f));
            var chemical = Profile("ChemicalResidue_UV", DecalFaceMask.Sides, margin: 0.8f,
                edge: 0.1f, gravity: 0.2f, spin: new Vector2(0, 360), size: new Vector2(0.3f, 0.55f));

            var table = AssetDatabase.LoadAssetAtPath<TraceSpawnTable>(TablePath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<TraceSpawnTable>();
                AssetDatabase.CreateAsset(table, TablePath);
            }

            table.verdicts = new List<VerdictSpawnRule>
            {
                Verdict(VerdictType.Approve, 45f, 1, 2,
                    Rule(TraceType.WaterStain, 10f, water, 1, 2),
                    Rule(TraceType.CardboardScuff, 8f, scuff, 1, 1),
                    Rule(TraceType.TapeResidue, 3f, tape, 1, 1),
                    Rule(TraceType.BloodStain, 2f, blood, 1, 1)),

                Verdict(VerdictType.Return, 85f, 1, 3,
                    Rule(TraceType.WaterStain, 10f, water, 1, 2),
                    Rule(TraceType.CardboardScuff, 10f, scuff, 1, 2),
                    Rule(TraceType.TapeResidue, 8f, tape, 1, 2),
                    Rule(TraceType.Frost, 4f, frost, 1, 1),
                    Rule(TraceType.BloodStain, 3f, blood, 1, 1),
                    Rule(TraceType.AcidBurn, 2f, acid, 1, 1)),

                Verdict(VerdictType.Illegal, 95f, 1, 3,
                    Rule(TraceType.OilStain, 10f, oil, 1, 2),
                    Rule(TraceType.BloodStain, 9f, blood, 1, 2),
                    Rule(TraceType.AcidBurn, 7f, acid, 1, 1),
                    Rule(TraceType.WaterStain, 7f, water, 1, 2),
                    Rule(TraceType.CardboardScuff, 6f, scuff, 1, 1),
                    Rule(TraceType.TapeResidue, 5f, tape, 1, 1),
                    Rule(TraceType.Frost, 2f, frost, 1, 1)),

                Verdict(VerdictType.Alien, 100f, 2, 4,
                    Rule(TraceType.ClawMarks, 20f, claw, 1, 2),
                    Rule(TraceType.AlienSlime_UV, 15f, slime, 1, 2, TraceVisibility.RequiresUV),
                    Rule(TraceType.ChemicalResidue_UV, 10f, chemical, 1, 2, TraceVisibility.RequiresUV),
                    Rule(TraceType.BloodStain, 8f, blood, 1, 2),
                    Rule(TraceType.Frost, 5f, frost, 1, 1))
            };

            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = table;
            EditorGUIUtility.PingObject(table);
            Debug.Log($"[DecalStudio] สร้าง spawn table + {10} placement profiles ที่ {Root}");
        }

        static VerdictSpawnRule Verdict(VerdictType type, float chance, int minTypes, int maxTypes,
            params TraceSpawnRule[] rules)
        {
            return new VerdictSpawnRule
            {
                verdict = type,
                enabled = true,
                spawnChancePercent = chance,
                minTypes = minTypes,
                maxTypes = maxTypes,
                rules = new List<TraceSpawnRule>(rules)
            };
        }

        static TraceSpawnRule Rule(TraceType type, float weight, TracePlacementProfile profile,
            int min, int max, TraceVisibility visibility = TraceVisibility.NakedEye)
        {
            return new TraceSpawnRule
            {
                traceType = type,
                enabled = true,
                weight = weight,
                minCount = min,
                maxCount = max,
                visibility = visibility,
                placement = profile
            };
        }

        static TracePlacementProfile Profile(string name, DecalFaceMask faces, float margin,
            float edge, float gravity, Vector2 spin, Vector2 size)
        {
            string path = $"{ProfileDir}/TP_{name}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<TracePlacementProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<TracePlacementProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            profile.allowedFaces = faces;
            profile.avoidLabelFace = true;
            profile.margin = margin;
            profile.edgeChance = edge;
            profile.gravityFlow = gravity;
            profile.spinRange = spin;
            profile.sizeRange = size;
            profile.minSpacing = 0.12f;

            EditorUtility.SetDirty(profile);
            return profile;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}

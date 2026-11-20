using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sector13.Decals.EditorTools
{
    /// <summary>
    /// สีเริ่มต้นของแต่ละชนิดรอย (atlas เก็บ luminance, สีมาจาก tint ของ material)
    /// </summary>
    public static class DecalTintPresets
    {
        public static Color For(TraceType type)
        {
            switch (type)
            {
                case TraceType.WaterStain: return new Color(0.46f, 0.38f, 0.27f);
                case TraceType.OilStain: return new Color(0.14f, 0.11f, 0.08f);
                case TraceType.CardboardScuff: return new Color(0.55f, 0.48f, 0.38f);
                case TraceType.ClawMarks: return new Color(0.33f, 0.26f, 0.19f);
                case TraceType.AlienSlime_UV: return new Color(0.35f, 1.00f, 0.50f);
                case TraceType.ChemicalResidue_UV: return new Color(0.30f, 0.85f, 1.00f);
                case TraceType.BloodStain: return new Color(0.36f, 0.06f, 0.05f);
                case TraceType.AcidBurn: return new Color(0.55f, 0.62f, 0.24f);
                case TraceType.Frost: return new Color(0.82f, 0.92f, 1.00f);
                case TraceType.TapeResidue: return new Color(0.50f, 0.45f, 0.34f);
                default: return Color.white;
            }
        }

        public static bool IsUV(TraceType type)
        {
            return type == TraceType.AlienSlime_UV || type == TraceType.ChemicalResidue_UV;
        }

        public static float HeightFor(TraceType type)
        {
            switch (type)
            {
                case TraceType.ClawMarks: return 0.6f;
                case TraceType.AcidBurn: return 0.6f;
                case TraceType.CardboardScuff: return 0.4f;
                case TraceType.TapeResidue: return 0.35f;
                case TraceType.BloodStain: return 0.25f;
                default: return 0f;
            }
        }
    }

    public class DecalAtlasWindow : EditorWindow
    {
        const string DefaultSettingsPath = "Assets/_Project/DecalStudio/Settings/AtlasBuildSettings.asset";

        DecalAtlasBuildSettings settings;
        SerializedObject serialized;
        Vector2 scroll;

        [MenuItem("Tools/Sector13/Decal Studio/Atlas Builder", false, 10)]
        public static void Open()
        {
            var window = GetWindow<DecalAtlasWindow>("Decal Atlas Builder");
            window.minSize = new Vector2(420, 480);
            window.Show();
        }

        void OnEnable()
        {
            LoadSettings();
        }

        void LoadSettings()
        {
            settings = AssetDatabase.LoadAssetAtPath<DecalAtlasBuildSettings>(DefaultSettingsPath);
            serialized = settings != null ? new SerializedObject(settings) : null;
        }

        void OnGUI()
        {
            EditorGUILayout.Space(4);
            if (settings == null)
            {
                EditorGUILayout.HelpBox("ยังไม่มีไฟล์ settings — กดปุ่มด้านล่างเพื่อสร้างชุดเริ่มต้น (6 รอยที่มีในโปรเจกต์)", MessageType.Info);
                if (GUILayout.Button("Create default settings", GUILayout.Height(28)))
                {
                    CreateDefaultSettings();
                }
                return;
            }

            serialized.Update();
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.LabelField("Atlas", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serialized.FindProperty("atlasId"));
            EditorGUILayout.PropertyField(serialized.FindProperty("atlasSize"));
            EditorGUILayout.PropertyField(serialized.FindProperty("cellSize"));
            EditorGUILayout.PropertyField(serialized.FindProperty("padding"));
            EditorGUILayout.PropertyField(serialized.FindProperty("grayscaleAlbedo"));
            EditorGUILayout.PropertyField(serialized.FindProperty("flipV"));
            EditorGUILayout.PropertyField(serialized.FindProperty("outputFolder"));

            int size = serialized.FindProperty("atlasSize").intValue;
            int cell = serialized.FindProperty("cellSize").intValue;
            int grid = Mathf.Max(1, size / Mathf.Max(1, cell));
            EditorGUILayout.HelpBox($"{size}x{size} / cell {cell} → grid {grid}x{grid} = {grid * grid} ช่อง", MessageType.None);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Sources", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serialized.FindProperty("sources"), true);

            serialized.ApplyModifiedProperties();

            EditorGUILayout.Space(10);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Fill from Assets/Textures/Traces", GUILayout.Height(26)))
                {
                    FillFromTracesFolder();
                }
                if (GUILayout.Button("Scan external folders", GUILayout.Height(26)))
                {
                    DecalStudioMenu.ScanExternalFolders();
                    LoadSettings();
                }
                if (GUILayout.Button("Select settings", GUILayout.Width(110), GUILayout.Height(26)))
                {
                    Selection.activeObject = settings;
                }
            }

            EditorGUILayout.Space(6);
            GUI.backgroundColor = new Color(0.55f, 0.9f, 0.6f);
            if (GUILayout.Button("Build Atlas", GUILayout.Height(34)))
            {
                Build();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndScrollView();
        }

        void Build()
        {
            try
            {
                var atlas = DecalAtlasBuilder.Build(settings);
                Selection.activeObject = atlas;
                EditorGUIUtility.PingObject(atlas);
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Build failed", e.Message, "OK");
                Debug.LogException(e);
            }
        }

        void CreateDefaultSettings()
        {
            EnsureFolder(DefaultSettingsPath);
            var asset = ScriptableObject.CreateInstance<DecalAtlasBuildSettings>();
            AssetDatabase.CreateAsset(asset, DefaultSettingsPath);
            AssetDatabase.SaveAssets();
            LoadSettings();
            FillFromTracesFolder();
        }

        void FillFromTracesFolder()
        {
            const string folder = "Assets/Textures/Traces";
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            var found = new List<DecalSourceEntry>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!fileName.StartsWith("T_")) continue;

                string typeName = fileName.Substring(2);
                if (!System.Enum.TryParse(typeName, out TraceType type)) continue;

                found.Add(new DecalSourceEntry
                {
                    id = typeName,
                    traceType = type,
                    requiresUV = DecalTintPresets.IsUV(type),
                    albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(path),
                    tint = DecalTintPresets.For(type),
                    heightStrength = DecalTintPresets.HeightFor(type)
                });
            }

            found.Sort((a, b) => a.traceType.CompareTo(b.traceType));
            settings.sources = found;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            serialized = new SerializedObject(settings);
            Debug.Log($"[DecalStudio] เติม source จาก {folder}: {found.Count} รายการ");
        }

        static void EnsureFolder(string assetPath)
        {
            string dir = System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(dir) || AssetDatabase.IsValidFolder(dir)) return;
            string parent = System.IO.Path.GetDirectoryName(dir)?.Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(dir);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent + "/dummy.tmp");
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}

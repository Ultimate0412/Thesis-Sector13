using System.Collections.Generic;
using Sector13.Decals;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Sector13.Decals.EditorTools
{
    /// <summary>
    /// Shared floor-decal placement used by the stamper window and the scatter areas.
    /// </summary>
    public static class DecalPlacement
    {
        public const float DefaultDepth = 0.6f;
        private const float SurfaceInset = 0.03f;
        private const float UvInset = 0.004f;

        // Every atlas entry has an Env copy in this folder: M_Env_<entry.id>.mat
        // They point at the same atlas cell, but carry their own opacity/height/tint,
        // so a stain reads as "on the floor" instead of a sticker.
        private const string EnvMaterialFolder = "Assets/_Project/Art/Materials/EnvDecals";
        private static readonly Dictionary<string, Material> EnvCache = new Dictionary<string, Material>();

        private static Material ResolveMaterial(DecalEntry entry)
        {
            if (entry == null) return null;

            string matName = "M_Env_" + entry.id;
            if (EnvCache.TryGetValue(matName, out var cached) && cached != null) return cached;

            var mat = AssetDatabase.LoadAssetAtPath<Material>(EnvMaterialFolder + "/" + matName + ".mat");
            EnvCache[matName] = mat;
            return mat != null ? mat : entry.material;
        }

        public static DecalLibrary FindLibrary()
        {
            var guids = AssetDatabase.FindAssets("t:DecalLibrary");
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<DecalLibrary>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        public static Transform EnsureRoot()
        {
            var existing = GameObject.Find("[World]/Decals");
            if (existing != null) return existing.transform;

            var world = GameObject.Find("[World]");
            var go = new GameObject("Decals");
            if (world != null) go.transform.SetParent(world.transform, false);
            Undo.RegisterCreatedObjectUndo(go, "Create decal root");
            return go.transform;
        }

        /// <summary>Places one projector sitting just under the surface, looking into it.</summary>
        public static GameObject Place(DecalEntry entry, Vector3 point, Vector3 normal,
                                       float size, float roll, Transform parent)
        {
            if (entry == null || entry.material == null) return null;

            var go = new GameObject("Decal_" + entry.id);
            if (parent != null) go.transform.SetParent(parent, true);

            var rot = Quaternion.LookRotation(-normal, Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up)
                      * Quaternion.Euler(0f, 0f, roll);
            go.transform.rotation = rot;

            var dp = go.AddComponent<DecalProjector>();
            dp.size = new Vector3(size, size, DefaultDepth);
            dp.material = ResolveMaterial(entry) ?? entry.material;
            dp.uvScale = new Vector2(entry.uvRect.width - UvInset * 2f,
                                     entry.uvRect.height - UvInset * 2f);
            dp.uvBias = new Vector2(entry.uvRect.x + UvInset, entry.uvRect.y + UvInset);
            dp.fadeFactor = 1f;

            // the projection volume starts at pivot.z * size.z along +Z, so sit the
            // transform slightly above the surface to keep the whole volume inside it
            go.transform.position = point + normal * (DefaultDepth * 0.5f - SurfaceInset);

            Undo.RegisterCreatedObjectUndo(go, "Place decal");
            return go;
        }

        public static List<DecalEntry> Resolve(DecalLibrary lib, IList<string> ids)
        {
            var list = new List<DecalEntry>();
            if (lib == null || ids == null) return list;
            foreach (var id in ids)
            {
                if (string.IsNullOrEmpty(id)) continue;
                if (lib.TryGetEntryById(id, out var e) && e != null && e.material != null) list.Add(e);
            }
            return list;
        }

        public static int Scatter(DecalScatterArea area, DecalLibrary lib)
        {
            var box = area.GetComponent<BoxCollider>();
            if (box == null) return 0;

            var entries = Resolve(lib, area.decalIds);
            if (entries.Count == 0) return 0;

            var parent = area.outputParent != null
                ? area.outputParent
                : EnsureRoot();

            Bounds b = box.bounds;
            var rng = new System.Random(area.seed);
            var placedPoints = new List<Vector3>();
            int made = 0;
            int attempts = Mathf.Max(20, area.count * 20);

            for (int i = 0; i < attempts && made < area.count; i++)
            {
                float x = Mathf.Lerp(b.min.x + area.edgeMargin, b.max.x - area.edgeMargin, (float)rng.NextDouble());
                float z = Mathf.Lerp(b.min.z + area.edgeMargin, b.max.z - area.edgeMargin, (float)rng.NextDouble());
                var origin = new Vector3(x, b.max.y + 2f, z);

                if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, b.size.y + 8f, ~0, QueryTriggerInteraction.Ignore))
                    continue;
                if (hit.normal.y < 0.85f) continue;

                bool tooClose = false;
                foreach (var p in placedPoints)
                {
                    if ((p - hit.point).sqrMagnitude < area.minSpacing * area.minSpacing) { tooClose = true; break; }
                }
                if (tooClose) continue;

                float size = Mathf.Lerp(area.sizeRange.x, area.sizeRange.y, (float)rng.NextDouble());
                float roll = (float)rng.NextDouble() * 360f;
                var entry = entries[rng.Next(entries.Count)];

                if (Place(entry, hit.point, hit.normal, size, roll, parent) != null)
                {
                    placedPoints.Add(hit.point);
                    made++;
                }
            }

            // The area collider is only a placement guide. Leaving it enabled makes it
            // swallow every interaction raycast that crosses the zone.
            box.enabled = false;
            return made;
        }

        public static int ClearBaked(DecalScatterArea area)
        {
            var parent = area.outputParent != null
                ? area.outputParent
                : (GameObject.Find("[World]/Decals") != null ? GameObject.Find("[World]/Decals").transform : null);
            if (parent == null) return 0;

            int removed = 0;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child.name.StartsWith("Decal_"))
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                    removed++;
                }
            }
            return removed;
        }
    }

    // ------------------------------------------------------------------ stamper
    public class DecalStamperWindow : EditorWindow
    {
        private DecalLibrary library;
        private string[] ids = new string[0];
        private int selected;
        private Vector2 sizeRange = new Vector2(1.5f, 3.5f);
        private bool randomRoll = true;
        private float roll;
        private bool active;

        [MenuItem("Tools/Sector13/Decal Stamper")]
        public static void Open()
        {
            var w = GetWindow<DecalStamperWindow>("Decal Stamper");
            w.minSize = new Vector2(280, 220);
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            RefreshLibrary();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void RefreshLibrary()
        {
            library = DecalPlacement.FindLibrary();
            if (library == null) { ids = new string[0]; return; }

            var list = new List<string>();
            foreach (var e in library.AllEntries())
                if (e != null && e.material != null) list.Add(e.id);
            ids = list.ToArray();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Floor decals", EditorStyles.boldLabel);
            if (library == null)
            {
                EditorGUILayout.HelpBox("DecalLibrary not found.", MessageType.Error);
                if (GUILayout.Button("Retry")) RefreshLibrary();
                return;
            }

            if (ids.Length == 0)
            {
                EditorGUILayout.HelpBox("No entries in the library.", MessageType.Warning);
                return;
            }

            int cols = 3;
            for (int i = 0; i < ids.Length; i += cols)
            {
                EditorGUILayout.BeginHorizontal();
                for (int c = 0; c < cols && i + c < ids.Length; c++)
                {
                    int idx = i + c;
                    var style = new GUIStyle(GUI.skin.button);
                    if (idx == selected) style.fontStyle = FontStyle.Bold;
                    if (GUILayout.Button(ids[idx], style)) selected = idx;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            sizeRange = EditorGUILayout.Vector2Field("Size range (m)", sizeRange);
            randomRoll = EditorGUILayout.Toggle("Random roll", randomRoll);
            if (!randomRoll) roll = EditorGUILayout.Slider("Roll", roll, 0f, 360f);

            EditorGUILayout.Space();
            active = EditorGUILayout.ToggleLeft("Click in Scene View to stamp", active);
            EditorGUILayout.HelpBox("Left click = place, Ctrl+Z = undo, right click = random roll", MessageType.Info);
        }

        private void OnSceneGUI(SceneView view)
        {
            if (!active || library == null || ids.Length == 0) return;

            var e = Event.current;
            if (e.type != EventType.MouseDown) return;

            var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore))
                return;

            if (e.button == 1)
            {
                roll = Random.Range(0f, 360f);
                e.Use();
                Repaint();
                return;
            }
            if (e.button != 0 || e.alt) return;

            if (!library.TryGetEntryById(ids[selected], out var entry) || entry == null) return;

            float size = Random.Range(sizeRange.x, sizeRange.y);
            float r = randomRoll ? Random.Range(0f, 360f) : roll;

            var go = DecalPlacement.Place(entry, hit.point, hit.normal, size, r, DecalPlacement.EnsureRoot());
            if (go != null)
            {
                e.Use();
                view.Repaint();
            }
        }
    }

    // ------------------------------------------------------------------ scatter inspector
    [CustomEditor(typeof(DecalScatterArea))]
    public class DecalScatterAreaEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var area = (DecalScatterArea)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Bake", EditorStyles.boldLabel);

            if (GUILayout.Button("Scatter " + area.count + " decals"))
            {
                var lib = DecalPlacement.FindLibrary();
                if (lib == null)
                {
                    EditorUtility.DisplayDialog("Decal", "DecalLibrary not found.", "OK");
                    return;
                }
                int made = DecalPlacement.Scatter(area, lib);
                Debug.Log($"[DecalScatterArea] placed {made} decals in {area.name}");
            }

            if (GUILayout.Button("Clear baked decals in scene"))
            {
                int removed = DecalPlacement.ClearBaked(area);
                Debug.Log($"[DecalScatterArea] removed {removed} decals");
            }
        }
    }
}

using UnityEngine;
using UnityEditor;

namespace ThesisSector13.Editor
{
    public class DoorFrameColliderTool : EditorWindow
    {
        private float openingWidth = 1.0f;
        private float openingHeight = 2.1f;
        private float extraPost = 0f;
        private float extraDepth = 0f;

        private const string HolderName = "_DoorFrameColliders";

        [MenuItem("Thesis-Sector13/Door Frame/Setup Colliders")]
        public static void ShowWindow()
        {
            GetWindow<DoorFrameColliderTool>("Door Frame Collider");
        }

        private void OnGUI()
        {
            GUILayout.Label("Door Frame Collider Setup", EditorStyles.boldLabel);
            GUILayout.Label("Replaces the MeshCollider with 3 BoxColliders\n(left / right posts + top). The doorway stays open.", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(8);

            openingWidth = EditorGUILayout.FloatField(new GUIContent("Opening Width (X)"), openingWidth);
            openingHeight = EditorGUILayout.FloatField(new GUIContent("Opening Height (Y)"), openingHeight);
            extraPost = EditorGUILayout.FloatField(new GUIContent("Post Thickness +"), extraPost);
            extraDepth = EditorGUILayout.FloatField(new GUIContent("Collider Depth +"), extraDepth);

            GUILayout.Space(10);
            GUILayout.Label("Select the door frame GameObject (scene or prefab edit mode), then click.", EditorStyles.miniLabel);

            if (GUILayout.Button("Setup on Selection", GUILayout.Height(38)))
                Setup();

            if (GUILayout.Button("Remove Generated Colliders", GUILayout.Height(24)))
                Remove(Selection.activeGameObject);
        }

        private void Setup()
        {
            GameObject go = Selection.activeGameObject;
            if (go == null)
            {
                Debug.LogWarning("[DoorFrame] Select the door frame GameObject first.");
                return;
            }

            Renderer rend = go.GetComponentInChildren<Renderer>();
            if (rend == null)
            {
                Debug.LogWarning("[DoorFrame] No Renderer found in selection.");
                return;
            }

            Remove(go);

            MeshCollider[] meshColliders = go.GetComponentsInChildren<MeshCollider>(true);
            foreach (MeshCollider mc in meshColliders)
                Undo.DestroyObjectImmediate(mc);

            GameObject holder = new GameObject(HolderName);
            Undo.RegisterCreatedObjectUndo(holder, "Door Frame Colliders");
            holder.transform.SetParent(go.transform, false);

            Bounds wb = rend.bounds;
            Vector3 parentScale = go.transform.lossyScale;
            Vector3 center = wb.center;

            float openW = Mathf.Clamp(openingWidth, 0.01f, wb.size.x);
            float openH = Mathf.Clamp(openingHeight, 0.01f, wb.size.y);

            float postW = (wb.size.x - openW) * 0.5f + extraPost;
            float lintelH = (wb.size.y - openH) + extraPost;
            float depth = wb.size.z + extraDepth;

            CreateBox(holder.transform, "Post_Left",
                new Vector3(wb.min.x + postW * 0.5f, center.y, center.z),
                new Vector3(postW, wb.size.y, depth), parentScale);

            CreateBox(holder.transform, "Post_Right",
                new Vector3(wb.max.x - postW * 0.5f, center.y, center.z),
                new Vector3(postW, wb.size.y, depth), parentScale);

            CreateBox(holder.transform, "Lintel_Top",
                new Vector3(center.x, wb.max.y - lintelH * 0.5f, center.z),
                new Vector3(openW, lintelH, depth), parentScale);

            EditorUtility.SetDirty(go);
            Debug.Log($"[DoorFrame] Colliders created. Frame world size = {wb.size.x:F2} x {wb.size.y:F2} x {wb.size.z:F2}. Opening = {openW:F2} x {openH:F2}. Tune fields and run again if needed.");
        }

        private void CreateBox(Transform parent, string name, Vector3 worldCenter, Vector3 worldSize, Vector3 parentScale)
        {
            GameObject g = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(g, "Door Frame Collider");
            g.transform.SetParent(parent, false);
            g.transform.position = worldCenter;

            BoxCollider box = g.AddComponent<BoxCollider>();
            box.size = new Vector3(
                worldSize.x / Mathf.Max(Mathf.Abs(parentScale.x), 0.0001f),
                worldSize.y / Mathf.Max(Mathf.Abs(parentScale.y), 0.0001f),
                worldSize.z / Mathf.Max(Mathf.Abs(parentScale.z), 0.0001f));
        }

        private void Remove(GameObject go)
        {
            if (go == null)
                return;

            Transform old = go.transform.Find(HolderName);
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
        }
    }
}

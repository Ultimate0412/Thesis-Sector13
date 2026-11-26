using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Sector13.Decals.EditorTools
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(DecalProjectorBinding))]
    public class DecalProjectorBindingEditor : Editor
    {
        const string EnvLibraryPath = "Assets/_Project/DecalStudio/Art/EnvDecalLibrary.asset";
        const string PackageLibraryPath = "Assets/_Project/DecalStudio/Art/DecalLibrary.asset";

        SerializedProperty libraryProperty;
        SerializedProperty entryIdProperty;
        SerializedProperty applyMaterialProperty;
        SerializedProperty applyUvProperty;
        SerializedProperty autoApplyProperty;
        SerializedProperty uvInsetProperty;

        void OnEnable()
        {
            libraryProperty = serializedObject.FindProperty("library");
            entryIdProperty = serializedObject.FindProperty("entryId");
            applyMaterialProperty = serializedObject.FindProperty("applyMaterial");
            applyUvProperty = serializedObject.FindProperty("applyUv");
            autoApplyProperty = serializedObject.FindProperty("autoApply");
            uvInsetProperty = serializedObject.FindProperty("uvInset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var binding = (DecalProjectorBinding)target;
            var library = libraryProperty.objectReferenceValue as DecalLibrary;

            EditorGUILayout.PropertyField(libraryProperty, new GUIContent("Decal Library"));

            if (library == null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "ยังไม่ได้เลือก Decal Library\n" +
                    "Env ใช้ EnvDecalLibrary / Package ใช้ DecalLibrary",
                    MessageType.Info);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Use EnvDecalLibrary"))
                    {
                        libraryProperty.objectReferenceValue = AssetDatabase.LoadAssetAtPath<DecalLibrary>(EnvLibraryPath);
                        serializedObject.ApplyModifiedProperties();
                    }
                    if (GUILayout.Button("Use DecalLibrary (Package)"))
                    {
                        libraryProperty.objectReferenceValue = AssetDatabase.LoadAssetAtPath<DecalLibrary>(PackageLibraryPath);
                        serializedObject.ApplyModifiedProperties();
                    }
                }

                serializedObject.ApplyModifiedProperties();
                return;
            }

            var entries = library.AllEntries();
            if (entries.Count == 0)
            {
                EditorGUILayout.HelpBox("Decal Library นี้ยังไม่มี entry", MessageType.Warning);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            int current = -1;
            var labels = new string[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                labels[i] = e == null ? "(null)" : $"{e.id}   ({e.traceType})";
                if (e != null && e.id == entryIdProperty.stringValue) current = i;
            }

            int popup = current >= 0 ? current : 0;
            int newPopup = EditorGUILayout.Popup("Decal Entry", popup, labels);
            if (newPopup != popup || current < 0)
            {
                entryIdProperty.stringValue = entries[newPopup].id;
                serializedObject.ApplyModifiedProperties();
                binding.Apply();
            }

            if (current >= 0 && current < entries.Count)
            {
                var entry = entries[current];
                if (entry != null)
                {
                    EditorGUILayout.Space(4);
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.ObjectField("Material", entry.material, typeof(Material), false);
                        EditorGUILayout.RectField("UV Rect", entry.uvRect);
                        EditorGUILayout.EnumPopup("Trace Type", entry.traceType);
                        EditorGUILayout.Toggle("Requires UV", entry.requiresUV);
                    }

                    if (entry.material == null)
                        EditorGUILayout.HelpBox("Entry นี้ยังไม่มี material", MessageType.Warning);
                }
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Apply", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(applyMaterialProperty, new GUIContent("Apply Material"));
            EditorGUILayout.PropertyField(applyUvProperty, new GUIContent("Apply UV"));
            EditorGUILayout.PropertyField(autoApplyProperty, new GUIContent("Auto Apply"));
            if (applyUvProperty.boolValue)
                EditorGUILayout.PropertyField(uvInsetProperty, new GUIContent("UV Inset"));

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply Now", GUILayout.Height(26)))
                {
                    serializedObject.ApplyModifiedProperties();
                    binding.Apply();
                }

                if (GUILayout.Button("ใช้ material ปัจจุบัน", GUILayout.Height(26)))
                {
                    var projector = binding.Projector;
                    if (projector != null && projector.material != null)
                    {
                        for (int i = 0; i < entries.Count; i++)
                        {
                            if (entries[i] != null && entries[i].material == projector.material)
                            {
                                entryIdProperty.stringValue = entries[i].id;
                                serializedObject.ApplyModifiedProperties();
                                binding.Apply();
                                break;
                            }
                        }
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Sync UV from Material"))
                {
                    if (DecalProjectorSync.Sync(binding.Projector))
                        SceneView.RepaintAll();
                }

                if (GUILayout.Button("เปิด Decal Preview"))
                {
                    if (entryIdProperty.stringValue != null)
                    {
                        var entry = library.AllEntries();
                        for (int i = 0; i < entry.Count; i++)
                        {
                            if (entry[i] != null && entry[i].id == entryIdProperty.stringValue)
                            {
                                Selection.activeObject = entry[i].material;
                                break;
                            }
                        }
                    }
                    DecalPreviewWindow.Open();
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}

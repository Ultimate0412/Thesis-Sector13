using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Sector13.Decals.EditorTools
{
    /// <summary>
    /// หน้าต่างพรีวิว decal แบบ "ของจริง":
    /// สร้าง rig ชั่วคราวในฉาก (ซ่อนไว้ไกล ๆ) แล้วให้ URP Decal Projector ฉายลงพื้นจริง
    /// → เห็น material/atlas cell/แสง/emission ตรงกับ Scene มากที่สุด
    /// ใช้ได้ทั้ง material ฝั่ง Env และ Package
    /// </summary>
    public class DecalPreviewWindow : EditorWindow
    {
        const string EnvLibraryPath = "Assets/_Project/DecalStudio/Art/EnvDecalLibrary.asset";
        const string PackageLibraryPath = "Assets/_Project/DecalStudio/Art/DecalLibrary.asset";
        const float UvInset = 0.004f;
        static readonly Vector3 RigOrigin = new Vector3(100000f, 0f, 100000f);

        Material material;
        DecalLibrary library;
        Vector2 scroll;

        GameObject rigRoot;
        Camera previewCamera;
        DecalProjector previewDecal;
        Light previewLight;
        RenderTexture previewRT;
        MeshRenderer previewPlaneRenderer;
        Material neutralPlaneMaterial;
        GameObject surfaceMeshGo;
        MeshFilter surfaceMeshFilter;
        MeshRenderer surfaceMeshRenderer;
        GameObject localLightsRoot;
        DecalProjector sourceProjector;
        bool useSceneLights = true;
        bool useSceneSurface;
        bool lightsDirty = true;
        Vector3 lastProjectorPosition;

        [MenuItem("Tools/Sector13/Decal Studio/Decal Preview", false, 14)]
        public static void Open()
        {
            var window = GetWindow<DecalPreviewWindow>("Decal Preview");
            window.minSize = new Vector2(380, 560);
            window.Show();
        }

        void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            if (library == null)
                library = AssetDatabase.LoadAssetAtPath<DecalLibrary>(EnvLibraryPath);
            PickFromSelection();
        }

        void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            DestroyRig();
        }

        void OnSelectionChanged()
        {
            PickFromSelection();
            Repaint();
        }

        void OnInspectorUpdate()
        {
            // อัปเดตพรีวิวตามค่าที่แก้ใน Material Inspector
            Repaint();
        }

        void PickFromSelection()
        {
            sourceProjector = null;
            lightsDirty = true;

            if (Selection.activeObject is Material selectedMaterial && IsDecalMaterial(selectedMaterial))
            {
                material = selectedMaterial;
                if (library == null) library = LibraryForMaterial(material);
                return;
            }

            var go = Selection.activeGameObject;
            if (go == null) return;

            var projector = go.GetComponent<DecalProjector>();
            if (projector != null && projector.material != null && IsDecalMaterial(projector.material))
            {
                material = projector.material;
                sourceProjector = projector;
                if (library == null) library = LibraryForMaterial(material);
            }
        }

        static DecalLibrary LibraryForMaterial(Material mat)
        {
            if (mat == null) return null;
            string path = AssetDatabase.GetAssetPath(mat);
            string libPath = path.Contains("/EnvDecals/") ? EnvLibraryPath : PackageLibraryPath;
            return AssetDatabase.LoadAssetAtPath<DecalLibrary>(libPath);
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                material = (Material)EditorGUILayout.ObjectField(
                    material, typeof(Material), false, GUILayout.MinWidth(160));

                if (GUILayout.Button("ใช้ที่เลือก", EditorStyles.toolbarButton, GUILayout.Width(82)))
                {
                    PickFromSelection();
                    Repaint();
                }

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(material == null))
                {
                    if (GUILayout.Button("Ping", EditorStyles.toolbarButton, GUILayout.Width(50)))
                        EditorGUIUtility.PingObject(material);
                }
            }

            EditorGUILayout.Space(4);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Env Library", EditorStyles.miniButton))
                {
                    library = AssetDatabase.LoadAssetAtPath<DecalLibrary>(EnvLibraryPath);
                }
                if (GUILayout.Button("Package Library", EditorStyles.miniButton))
                {
                    library = AssetDatabase.LoadAssetAtPath<DecalLibrary>(PackageLibraryPath);
                }
                library = (DecalLibrary)EditorGUILayout.ObjectField(
                    library, typeof(DecalLibrary), false);
            }

            if (library != null)
            {
                var entries = library.AllEntries();
                if (entries.Count > 0)
                {
                    int current = 0;
                    var labels = new string[entries.Count];
                    for (int i = 0; i < entries.Count; i++)
                    {
                        var e = entries[i];
                        labels[i] = e == null ? "(null)" : $"{e.id}   ({e.traceType})";
                        if (e != null && material != null && e.material == material) current = i;
                    }

                    int picked = EditorGUILayout.Popup("Entry", current, labels);
                    if (picked >= 0 && picked < entries.Count && entries[picked] != null)
                    {
                        if (entries[picked].material != material)
                            material = entries[picked].material;
                    }
                }
            }

            using (new EditorGUI.DisabledScope(sourceProjector == null))
            {
                bool newUseSceneLights = EditorGUILayout.ToggleLeft(
                    "ใช้ไฟ Scene รอบ ๆ projector ที่เลือก", useSceneLights);
                if (newUseSceneLights != useSceneLights)
                {
                    useSceneLights = newUseSceneLights;
                    lightsDirty = true;
                }

                useSceneSurface = EditorGUILayout.ToggleLeft(
                    "ใช้ mesh/material ของพื้นจริง (ทดลอง)", useSceneSurface);
            }

            if (material == null)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox(
                    "เลือก Material (Env หรือ Package) หรือเลือก Decal Projector ใน Scene\n" +
                    "แล้วหน้าต่างนี้จะพรีวิวด้วย URP Decal Projector ตัวจริง",
                    MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            if (!IsDecalMaterial(material))
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox(
                    "Material นี้ไม่ได้ใช้ shader Sector13/Decals/DecalAtlas",
                    MessageType.Warning);
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.Space(6);

            float size = Mathf.Clamp(position.width - 32f, 160f, 520f);
            Rect rect = GUILayoutUtility.GetRect(size, size);
            rect.x = (position.width - size) * 0.5f;

            if (Event.current.type == EventType.Repaint)
                RenderPreview(rect);
            else
                DecalPreview.DrawBorder(rect);

            EditorGUILayout.Space(8);
            Rect cell = DecalPreview.GetCell(material);

            EditorGUILayout.LabelField("Material", material.name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Atlas cell",
                $"x {cell.x:0.###}   y {cell.y:0.###}   {cell.width:0.###} × {cell.height:0.###}");
            EditorGUILayout.LabelField("Opacity", GetFloat(material, "_Opacity", 1f).ToString("0.00"));
            EditorGUILayout.LabelField("Emission", GetFloat(material, "_EmissionIntensity", 0f).ToString("0.00"));
            EditorGUILayout.LabelField("Path", AssetDatabase.GetAssetPath(material), EditorStyles.miniLabel);

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select Material", GUILayout.Height(24)))
                {
                    Selection.activeObject = material;
                    EditorGUIUtility.PingObject(material);
                }

                var projector = Selection.activeGameObject != null
                    ? Selection.activeGameObject.GetComponent<DecalProjector>()
                    : null;

                using (new EditorGUI.DisabledScope(projector == null))
                {
                    if (GUILayout.Button("Sync Projector UV", GUILayout.Height(24)))
                    {
                        if (DecalProjectorSync.Sync(projector))
                            SceneView.RepaintAll();
                    }
                }
            }

            var binding = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<DecalProjectorBinding>()
                : null;
            if (binding != null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Apply Binding"))
                    {
                        binding.Apply();
                        SceneView.RepaintAll();
                    }
                }
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "พรีวิวนี้ใช้ URP Decal Projector ตัวจริง + แสงหลัก/ambient ของฉาก\n" +
                "จึงเห็น material, normal, height, metallic/smoothness และ emission ตรงกับ Scene\n" +
                "(rig ถูกซ่อนไว้ห่าง ๆ และจะถูกลบเมื่อปิดหน้าต่าง)",
                MessageType.None);

            EditorGUILayout.EndScrollView();
        }

        void RenderPreview(Rect rect)
        {
            EnsureRig();
            UpdateRig();
            EnsureRenderTexture(Mathf.Clamp(Mathf.RoundToInt(rect.width * 1.5f), 256, 768));

            previewCamera.Render();
            GUI.DrawTexture(rect, previewRT, ScaleMode.StretchToFill, false);
            DecalPreview.DrawBorder(rect);
        }

        void EnsureRig()
        {
            if (rigRoot != null) return;

            rigRoot = new GameObject("DecalPreviewRig");
            rigRoot.hideFlags = HideFlags.HideAndDontSave;
            rigRoot.transform.position = RigOrigin;

            // ---- plane ----
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Object.DestroyImmediate(plane.GetComponent<Collider>());
            plane.hideFlags = HideFlags.HideAndDontSave;
            plane.transform.SetParent(rigRoot.transform, false);
            plane.transform.localPosition = Vector3.zero;
            plane.transform.localScale = Vector3.one * 0.35f;

            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");
            neutralPlaneMaterial = new Material(litShader) { hideFlags = HideFlags.HideAndDontSave };
            if (neutralPlaneMaterial.HasProperty("_BaseColor"))
                neutralPlaneMaterial.SetColor("_BaseColor", new Color(0.30f, 0.30f, 0.30f, 1f));
            if (neutralPlaneMaterial.HasProperty("_Smoothness"))
                neutralPlaneMaterial.SetFloat("_Smoothness", 0.2f);
            if (neutralPlaneMaterial.HasProperty("_Metallic"))
                neutralPlaneMaterial.SetFloat("_Metallic", 0f);
            previewPlaneRenderer = plane.GetComponent<MeshRenderer>();
            previewPlaneRenderer.sharedMaterial = neutralPlaneMaterial;

            // ---- decal ----
            var decalGo = new GameObject("DecalPreviewProjector");
            decalGo.hideFlags = HideFlags.HideAndDontSave;
            decalGo.transform.SetParent(rigRoot.transform, false);
            decalGo.transform.localPosition = new Vector3(0f, 0.32f, 0f);
            decalGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            previewDecal = decalGo.AddComponent<DecalProjector>();
            previewDecal.size = new Vector3(2.8f, 2.8f, 0.6f);
            previewDecal.fadeFactor = 1f;

            // ---- light (copy scene sun when possible) ----
            var lightGo = new GameObject("DecalPreviewLight");
            lightGo.hideFlags = HideFlags.HideAndDontSave;
            lightGo.transform.SetParent(rigRoot.transform, false);
            previewLight = lightGo.AddComponent<Light>();
            previewLight.type = LightType.Directional;
            previewLight.shadows = LightShadows.None;

            var sun = RenderSettings.sun;
            if (sun != null)
            {
                previewLight.color = sun.color;
                previewLight.intensity = sun.intensity;
                previewLight.transform.rotation = sun.transform.rotation;
            }
            else
            {
                previewLight.color = Color.white;
                previewLight.intensity = 1.5f;
                previewLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            localLightsRoot = new GameObject("LocalSceneLights");
            localLightsRoot.hideFlags = HideFlags.HideAndDontSave;
            localLightsRoot.transform.SetParent(rigRoot.transform, false);

            // ---- camera ----
            var camGo = new GameObject("DecalPreviewCamera");
            camGo.hideFlags = HideFlags.HideAndDontSave;
            camGo.transform.SetParent(rigRoot.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 2.7f, -4.0f);
            camGo.transform.LookAt(RigOrigin + new Vector3(0f, 0.05f, 0f));

            previewCamera = camGo.AddComponent<Camera>();
            previewCamera.enabled = false;
            previewCamera.cullingMask = ~0;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0.055f, 0.06f, 0.07f, 1f);
            previewCamera.nearClipPlane = 0.05f;
            previewCamera.farClipPlane = 20f;
            previewCamera.fieldOfView = 32f;
            previewCamera.allowHDR = false;
            previewCamera.allowMSAA = false;
            previewCamera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        void UpdateRig()
        {
            if (previewDecal == null) return;

            previewDecal.material = material;
            Rect cell = DecalPreview.GetCell(material);
            if (cell.width > 0.0001f && cell.height > 0.0001f)
            {
                previewDecal.uvScale = new Vector2(
                    Mathf.Max(0.0001f, cell.width - UvInset * 2f),
                    Mathf.Max(0.0001f, cell.height - UvInset * 2f));
                previewDecal.uvBias = new Vector2(cell.x + UvInset, cell.y + UvInset);
            }

            UpdateSurface();

            // ตาม scene sun แบบสด ๆ (เผื่อผู้ใช้เปลี่ยนแสง)
            var sun = RenderSettings.sun;
            if (sun != null && previewLight != null)
            {
                previewLight.color = sun.color;
                previewLight.intensity = sun.intensity;
                previewLight.transform.rotation = sun.transform.rotation;
            }

            if (sourceProjector != null && sourceProjector.transform.position != lastProjectorPosition)
            {
                lastProjectorPosition = sourceProjector.transform.position;
                lightsDirty = true;
            }

            if (lightsDirty) RebuildLocalLights();
        }

        void UpdateSurface()
        {
            if (previewPlaneRenderer == null) return;

            if (!useSceneSurface)
            {
                if (surfaceMeshGo != null) surfaceMeshGo.SetActive(false);
                previewPlaneRenderer.enabled = true;
                if (previewPlaneRenderer.sharedMaterial != neutralPlaneMaterial)
                    previewPlaneRenderer.sharedMaterial = neutralPlaneMaterial;
                return;
            }

            // ถ้าเลือก projector อยู่ ให้ใช้ mesh + material จริงของพื้น/ผนังที่ decal ฉายลง
            if (sourceProjector != null)
            {
                Vector3 origin = sourceProjector.transform.position;
                Vector3 direction = sourceProjector.transform.forward;
                if (Physics.Raycast(origin, direction, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore))
                {
                    var hitFilter = hit.collider.GetComponent<MeshFilter>();
                    var hitRenderer = hit.collider.GetComponent<MeshRenderer>();
                    if (hitRenderer == null) hitRenderer = hit.collider.GetComponentInParent<MeshRenderer>();
                    if (hitFilter == null) hitFilter = hit.collider.GetComponentInParent<MeshFilter>();

                    if (hitFilter != null && hitRenderer != null && hitFilter.sharedMesh != null)
                    {
                        if (surfaceMeshGo == null)
                        {
                            surfaceMeshGo = new GameObject("SurfaceMesh");
                            surfaceMeshGo.hideFlags = HideFlags.HideAndDontSave;
                            surfaceMeshGo.transform.SetParent(rigRoot.transform, false);
                            surfaceMeshFilter = surfaceMeshGo.AddComponent<MeshFilter>();
                            surfaceMeshRenderer = surfaceMeshGo.AddComponent<MeshRenderer>();
                        }

                        surfaceMeshFilter.sharedMesh = hitFilter.sharedMesh;
                        surfaceMeshRenderer.sharedMaterials = hitRenderer.sharedMaterials;

                        Quaternion align = Quaternion.FromToRotation(hit.normal, Vector3.up);
                        surfaceMeshGo.transform.position = RigOrigin + align * (hitRenderer.transform.position - hit.point);
                        surfaceMeshGo.transform.rotation = align * hitRenderer.transform.rotation;
                        surfaceMeshGo.transform.localScale = hitRenderer.transform.lossyScale;

                        surfaceMeshGo.SetActive(true);
                        previewPlaneRenderer.enabled = false;
                        return;
                    }
                }
            }

            if (surfaceMeshGo != null) surfaceMeshGo.SetActive(false);
            previewPlaneRenderer.enabled = true;
            if (previewPlaneRenderer.sharedMaterial != neutralPlaneMaterial)
                previewPlaneRenderer.sharedMaterial = neutralPlaneMaterial;
        }

        void RebuildLocalLights()
        {
            lightsDirty = false;

            if (localLightsRoot == null) return;
            for (int i = localLightsRoot.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(localLightsRoot.transform.GetChild(i).gameObject);

            if (!useSceneLights || sourceProjector == null) return;

            Vector3 origin = sourceProjector.transform.position;
            var allLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var candidates = new List<Light>();

            for (int i = 0; i < allLights.Length; i++)
            {
                var light = allLights[i];
                if (light == null || light.type == LightType.Directional) continue;
                if (rigRoot != null && light.transform.IsChildOf(rigRoot.transform)) continue;
                float distance = Vector3.Distance(origin, light.transform.position);
                if (distance > Mathf.Max(12f, light.range + 2f)) continue;
                candidates.Add(light);
            }

            candidates.Sort((a, b) =>
                Vector3.SqrMagnitude(a.transform.position - origin)
                    .CompareTo(Vector3.SqrMagnitude(b.transform.position - origin)));

            int count = Mathf.Min(8, candidates.Count);
            for (int i = 0; i < count; i++)
            {
                var light = candidates[i];
                var copy = new GameObject("LocalLight_" + light.name);
                copy.hideFlags = HideFlags.HideAndDontSave;
                copy.transform.SetParent(localLightsRoot.transform, false);
                copy.transform.localPosition = light.transform.position - origin;
                copy.transform.rotation = light.transform.rotation;

                var cloned = copy.AddComponent<Light>();
                cloned.type = light.type;
                cloned.color = light.color;
                cloned.intensity = light.intensity;
                cloned.range = light.range;
                cloned.spotAngle = light.spotAngle;
                cloned.shadows = LightShadows.None;
                cloned.renderMode = LightRenderMode.Auto;
            }
        }

        void EnsureRenderTexture(int size)
        {
            size = Mathf.Clamp(size, 128, 1024);
            if (previewRT != null && previewRT.width == size && previewRT.height == size) return;

            if (previewCamera != null) previewCamera.targetTexture = null;
            if (previewRT != null)
            {
                previewRT.Release();
                Object.DestroyImmediate(previewRT);
            }

            previewRT = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "DecalPreviewRT"
            };
            previewRT.Create();
            previewCamera.targetTexture = previewRT;
        }

        void DestroyRig()
        {
            if (previewCamera != null) previewCamera.targetTexture = null;
            if (previewRT != null)
            {
                previewRT.Release();
                Object.DestroyImmediate(previewRT);
                previewRT = null;
            }
            if (rigRoot != null)
            {
                Object.DestroyImmediate(rigRoot);
                rigRoot = null;
            }
            if (neutralPlaneMaterial != null)
            {
                Object.DestroyImmediate(neutralPlaneMaterial);
                neutralPlaneMaterial = null;
            }
            previewPlaneRenderer = null;
            surfaceMeshGo = null;
            surfaceMeshFilter = null;
            surfaceMeshRenderer = null;
            previewCamera = null;
            previewDecal = null;
            previewLight = null;
            localLightsRoot = null;
            sourceProjector = null;
        }

        static bool IsDecalMaterial(Material mat)
        {
            return mat != null && mat.shader != null && mat.shader.name == "Sector13/Decals/DecalAtlas";
        }

        static float GetFloat(Material mat, string property, float fallback)
        {
            return mat != null && mat.HasProperty(property) ? mat.GetFloat(property) : fallback;
        }
    }
}

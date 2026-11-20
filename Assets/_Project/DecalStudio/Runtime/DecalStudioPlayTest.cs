using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Sector13.Decals
{
    /// <summary>
    /// Self-test สำหรับ Play mode: ถ้าเจอไฟล์ Temp/DecalTest/run.flag จะวางแผง 4 อันไว้หน้ากล้อง
    /// (material เดิม / atlas WaterStain / atlas ClawMarks / atlas OilStain) แล้วถ่ายภาพเก็บที่ Temp/DecalTest/play.png
    /// ใช้ตรวจว่าวาดถูกช่อง + สีต่างกัน + ไม่กลับหัว โดยไม่ต้องพึ่ง MCP ระหว่าง play mode
    /// </summary>
    public static class DecalStudioPlayTest
    {
        const string FlagPath = "Logs/DecalTest/run.flag";
        const string ShotPath = "Logs/DecalTest/play.png";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            try
            {
                if (!File.Exists(FlagPath)) return;
                File.Delete(FlagPath);
            }
            catch
            {
                return;
            }

            var go = new GameObject("DecalStudioPlayTest");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Runner>();
        }

        class Runner : MonoBehaviour
        {
            IEnumerator Start()
            {
                yield return new WaitForSecondsRealtime(1.5f);

                var cam = Camera.main;
                if (cam == null)
                {
                    Debug.LogWarning("[DecalStudioPlayTest] ไม่พบ Main Camera");
                    yield break;
                }

                var root = new GameObject("DecalStudioPlayTest_Root");
                root.transform.SetParent(cam.transform, false);
                root.transform.localPosition = new Vector3(0f, 0f, 1.7f);
                root.transform.localRotation = Quaternion.identity;

                Material stockMat = null;
                DecalAtlas atlas = null;
#if UNITY_EDITOR
                stockMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Traces/M_Decal_WaterStain.mat");
                atlas = UnityEditor.AssetDatabase.LoadAssetAtPath<DecalAtlas>("Assets/_Project/DecalStudio/Art/Atlases/Evidence.asset");
#endif

                var cubeMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                cubeMat.SetColor("_BaseColor", new Color(0.72f, 0.66f, 0.52f));

                const float s = 0.5f;      // ระยะจากกลาง
                const float panel = 0.42f;

                // แถวบน: material เดิม (ซ้าย) / atlas WaterStain (ขวา)
                AddPanel(root.transform, new Vector3(-s, s, 0), panel, cubeMat,
                    stockMat, Vector2.one, Vector2.zero);

                AddPanel(root.transform, new Vector3(s, s, 0), panel, cubeMat,
                    EntryMaterial(atlas, TraceType.WaterStain), EntryScale(atlas, TraceType.WaterStain), EntryBias(atlas, TraceType.WaterStain));

                // แถวล่าง: atlas ClawMarks (ซ้าย) / atlas OilStain (ขวา)
                AddPanel(root.transform, new Vector3(-s, -s, 0), panel, cubeMat,
                    EntryMaterial(atlas, TraceType.ClawMarks), EntryScale(atlas, TraceType.ClawMarks), EntryBias(atlas, TraceType.ClawMarks));

                AddPanel(root.transform, new Vector3(s, -s, 0), panel, cubeMat,
                    EntryMaterial(atlas, TraceType.OilStain), EntryScale(atlas, TraceType.OilStain), EntryBias(atlas, TraceType.OilStain));

                for (int i = 0; i < 25; i++) yield return null;

                Directory.CreateDirectory("Logs/DecalTest");
                ScreenCapture.CaptureScreenshot(ShotPath);
                Debug.Log("[DecalStudioPlayTest] captured " + ShotPath +
                          " | stock=" + (stockMat != null) + " atlas=" + (atlas != null));

                yield return new WaitForSeconds(0.6f);
                Destroy(root);
                Destroy(gameObject);
            }

            static Material EntryMaterial(DecalAtlas atlas, TraceType type)
            {
                var e = atlas != null ? atlas.Find(type) : null;
                return e != null ? e.material : null;
            }

            static Vector2 EntryScale(DecalAtlas atlas, TraceType type)
            {
                var e = atlas != null ? atlas.Find(type) : null;
                return e != null ? new Vector2(e.uvRect.width, e.uvRect.height) : Vector2.one;
            }

            static Vector2 EntryBias(DecalAtlas atlas, TraceType type)
            {
                var e = atlas != null ? atlas.Find(type) : null;
                return e != null ? new Vector2(e.uvRect.x, e.uvRect.y) : Vector2.zero;
            }

            static void AddPanel(Transform parent, Vector3 localPos, float size, Material cubeMat,
                Material decalMat, Vector2 scale, Vector2 bias)
            {
                var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                panel.name = "PlayTest_Panel";
                panel.transform.SetParent(parent, false);
                panel.transform.localPosition = localPos;
                panel.transform.localScale = new Vector3(size, size, 0.04f);
                panel.GetComponent<Renderer>().sharedMaterial = cubeMat;
                Object.Destroy(panel.GetComponent<Collider>());

                if (decalMat == null) return;

                var dgo = new GameObject("PlayTest_Decal");
                dgo.transform.SetParent(panel.transform, false);
                dgo.transform.localPosition = new Vector3(0f, 0f, -0.5f);
                dgo.transform.localRotation = Quaternion.identity;
                var dp = dgo.AddComponent<DecalProjector>();
                dp.material = decalMat;
                dp.uvScale = scale;
                dp.uvBias = bias;
                dp.fadeFactor = 1f;
                dp.size = new Vector3(1f, 1f, 1.2f);
                dp.scaleMode = DecalScaleMode.InheritFromHierarchy;
            }
        }
    }
}

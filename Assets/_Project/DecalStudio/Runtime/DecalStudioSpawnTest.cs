using System.Collections;
using System.IO;
using UnityEngine;

namespace Sector13.Decals
{
    /// <summary>
    /// Self-test การ spawn: ถ้าเจอไฟล์ Logs/DecalTest/spawn.flag จะสร้างกล่อง 4 ใบ
    /// (ส่งได้ / ส่งกลับ / ผิดกฎ / เอเลี่ยน) แล้วถ่ายภาพรวมไว้ที่ Logs/DecalTest/spawn.png
    /// </summary>
    public static class DecalStudioSpawnTest
    {
        const string FlagPath = "Logs/DecalTest/spawn.flag";
        const string ShotPath = "Logs/DecalTest/spawn.png";

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

            var go = new GameObject("DecalStudioSpawnTest");
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
                    Debug.LogWarning("[DecalSpawnTest] ไม่พบ Main Camera");
                    yield break;
                }

                DecalLibrary library = null;
                TraceSpawnTable table = null;
#if UNITY_EDITOR
                library = UnityEditor.AssetDatabase.LoadAssetAtPath<DecalLibrary>(
                    "Assets/_Project/DecalStudio/Art/DecalLibrary.asset");
                table = UnityEditor.AssetDatabase.LoadAssetAtPath<TraceSpawnTable>(
                    "Assets/_Project/DecalStudio/Resources/TraceSpawnTable.asset");
#endif

                var root = new GameObject("DecalSpawnTest_Root");
                root.transform.SetParent(cam.transform, false);
                root.transform.localPosition = new Vector3(0f, 0f, 2.0f);
                root.transform.localRotation = Quaternion.identity;

                const float spread = 0.42f;
                SpawnBox(root.transform, library, table, VerdictType.Approve, ItemCategory.Legal, new Vector3(-spread, spread, 0));
                SpawnBox(root.transform, library, table, VerdictType.Return, ItemCategory.Legal, new Vector3(spread, spread, 0));
                SpawnBox(root.transform, library, table, VerdictType.Illegal, ItemCategory.Illegal, new Vector3(-spread, -spread, 0));
                SpawnBox(root.transform, library, table, VerdictType.Alien, ItemCategory.Alien, new Vector3(spread, -spread, 0));

                for (int i = 0; i < 25; i++) yield return null;

                Directory.CreateDirectory("Logs/DecalTest");
                ScreenCapture.CaptureScreenshot(ShotPath);
                Debug.Log("[DecalSpawnTest] captured " + ShotPath + " | library=" + (library != null) + " table=" + (table != null));

                yield return new WaitForSeconds(0.6f);
                Destroy(root);
                Destroy(gameObject);
            }

            static void SpawnBox(Transform parent, DecalLibrary library, TraceSpawnTable table,
                VerdictType verdict, ItemCategory category, Vector3 localPos)
            {
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = $"SpawnTest_{verdict}";
                box.transform.SetParent(parent, false);
                box.transform.localPosition = localPos;
                box.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

                var lit = Shader.Find("Universal Render Pipeline/Lit");
                var mat = new Material(lit);
                mat.SetColor("_BaseColor", new Color(0.88f, 0.87f, 0.84f));
                box.GetComponent<Renderer>().sharedMaterial = mat;

                var col = box.GetComponent<BoxCollider>();
                if (col == null) col = box.AddComponent<BoxCollider>();

                var applier = box.AddComponent<PackageTraceApplier>();
                applier.decalLibrary = library;
                applier.spawnTable = table;
                applier.traceSeed = 12345;     // ค่าเดียวกันทุกกล่องเพื่อเทียบผล
                applier.ApplyTraces(verdict, category);

                var traces = box.transform.Find("SurfaceTracesRoot");
                Debug.Log($"[DecalSpawnTest] {verdict}: traces={(traces != null ? traces.childCount : 0)}");
            }
        }
    }
}

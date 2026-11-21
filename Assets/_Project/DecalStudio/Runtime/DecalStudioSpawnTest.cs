using System.Collections;
using System.IO;
using UnityEngine;

namespace Sector13.Decals
{
    /// <summary>
    /// Self-test ด้วย prefab กล่องจริง — รองรับทั้ง Procedural และ AuthoredSlots
    /// ทำงานเมื่อมีไฟล์ Logs/DecalTest/spawn.flag แล้วเก็บภาพที่ Logs/DecalTest/spawn.png
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

                GameObject legal = null, alien = null;
#if UNITY_EDITOR
                legal = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Legal_Package.prefab");
                alien = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Alien_Package.prefab");
#endif

                var root = new GameObject("DecalSpawnTest_Root");
                root.transform.SetParent(cam.transform, false);
                root.transform.localPosition = new Vector3(0f, 0f, 1.6f);
                root.transform.localRotation = Quaternion.identity;

                var legalBox = Spawn(root.transform, legal, new Vector3(-0.35f, 0.28f, 0f));
                var alienBox = Spawn(root.transform, alien, new Vector3(0.35f, 0.28f, 0f));

                for (int i = 0; i < 30; i++) yield return null;

                LogBox(legalBox, "Legal");
                LogBox(alienBox, "Alien");

                Directory.CreateDirectory("Logs/DecalTest");
                ScreenCapture.CaptureScreenshot(ShotPath);
                Debug.Log("[DecalSpawnTest] captured " + ShotPath);

                yield return new WaitForSeconds(0.6f);
                Destroy(root);
                Destroy(gameObject);
            }

            static GameObject Spawn(Transform parent, GameObject prefab, Vector3 localPos)
            {
                if (prefab == null) return null;

                var box = Instantiate(prefab, parent, false);
                box.name = "SpawnTest_" + prefab.name;
                box.transform.localPosition = localPos;
                box.transform.localRotation = Quaternion.identity;
                box.transform.localScale = Vector3.one * 0.9f;

                foreach (var rb in box.GetComponentsInChildren<Rigidbody>(true))
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                }

                var applier = box.GetComponentInChildren<PackageTraceApplier>();
                if (applier != null) applier.traceSeed = 12345;
                return box;
            }

            static void LogBox(GameObject box, string label)
            {
                if (box == null) { Debug.Log($"[DecalSpawnTest] {label}: null"); return; }
                var applier = box.GetComponentInChildren<PackageTraceApplier>();
                if (applier == null) { Debug.Log($"[DecalSpawnTest] {label}: no applier"); return; }

                int procedural = 0;
                var tr = applier.transform.Find("SurfaceTracesRoot");
                if (tr != null) procedural = tr.childCount;

                int activeSlots = 0;
                string slotNames = "";
                var slots = applier.GetComponentsInChildren<DecalSlot>(true);
                foreach (var s in slots)
                {
                    if (s.gameObject.activeSelf)
                    {
                        activeSlots++;
                        slotNames += s.name.Replace("Slot_", "") + " ";
                    }
                }

                Debug.Log($"[DecalSpawnTest] {label}: mode={applier.spawnMode} procedural={procedural} activeSlots={activeSlots} [{slotNames.Trim()}] library={(applier.decalLibrary != null)}");
            }
        }
    }
}

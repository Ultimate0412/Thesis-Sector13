using UnityEngine;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Sector13.Decals
{
    /// <summary>
    /// ผูก Decal Projector กับ 1 entry ใน DecalLibrary
    /// → material และ UV Tiling/Offset ถูกเซ็ตจาก entry เดียวกัน (แหล่งความจริงเดียว)
    ///
    /// ใช้กับงาน Environment เป็นหลัก ส่วน Package ยังใช้ PackageTraceApplier
    /// ที่เซ็ต material/UV ตอน runtime อยู่แล้ว
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(DecalProjector))]
    [AddComponentMenu("Sector13/Decals/Decal Projector Binding")]
    public class DecalProjectorBinding : MonoBehaviour
    {
        [Tooltip("คลัง decal ที่จะเลือก entry (Env ใช้ EnvDecalLibrary)")]
        public DecalLibrary library;

        [Tooltip("id ของ entry ใน library เช่น Blood, WaterStain, BloodStain_Down_A")]
        public string entryId;

        [Header("Apply")]
        [Tooltip("เซ็ต material ของ projector จาก entry")]
        public bool applyMaterial = true;

        [Tooltip("เซ็ต UV Tiling/Offset ของ projector จาก entry")]
        public bool applyUv = true;

        [Tooltip("อัปเดตอัตโนมัติเมื่อ entry/library เปลี่ยน และเมื่อ projector ถูกแก้ให้หลุด")]
        public bool autoApply = true;

        [Tooltip("ระยะ inset กันขอบ atlas bleed")]
        [Range(0f, 0.02f)]
        public float uvInset = 0.004f;

        DecalProjector projector;

        public DecalProjector Projector
        {
            get
            {
                if (projector == null) projector = GetComponent<DecalProjector>();
                return projector;
            }
        }

        void OnEnable()
        {
            Apply(false);
        }

        void OnValidate()
        {
            Apply(false);
        }

        void Update()
        {
            // ใน edit mode คอยดึงกลับมาถ้ามีคนแก้ material/UV ของ projector ตรง ๆ
            if (!Application.isPlaying && autoApply)
                Apply(false);
        }

        /// <summary>เรียกจาก Inspector (พร้อม Undo)</summary>
        public void Apply()
        {
            Apply(true);
        }

        public void Apply(bool recordUndo)
        {
            if (!autoApply && !recordUndo) return;

            var target = Projector;
            if (target == null || library == null || string.IsNullOrEmpty(entryId)) return;
            if (!library.TryGetEntryById(entryId, out var entry) || entry == null) return;

            Vector2 targetScale = target.uvScale;
            Vector2 targetBias = target.uvBias;
            bool hasUv = applyUv && entry.uvRect.width > 0.0001f && entry.uvRect.height > 0.0001f;

            if (hasUv)
            {
                float inset = Mathf.Max(0f, uvInset);
                targetScale = new Vector2(
                    Mathf.Max(0.0001f, entry.uvRect.width - inset * 2f),
                    Mathf.Max(0.0001f, entry.uvRect.height - inset * 2f));
                targetBias = new Vector2(entry.uvRect.x + inset, entry.uvRect.y + inset);
            }

            bool changeMaterial = applyMaterial && entry.material != null && target.material != entry.material;
            bool changeUv = hasUv && (target.uvScale != targetScale || target.uvBias != targetBias);
            if (!changeMaterial && !changeUv) return;

#if UNITY_EDITOR
            if (recordUndo && !Application.isPlaying)
                Undo.RecordObject(target, "Apply Decal Binding");
#endif

            if (changeMaterial) target.material = entry.material;
            if (changeUv)
            {
                target.uvScale = targetScale;
                target.uvBias = targetBias;
            }

#if UNITY_EDITOR
            EditorUtility.SetDirty(target);
            if (PrefabUtility.IsPartOfPrefabInstance(target))
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
#endif
        }

#if UNITY_EDITOR
        void Reset()
        {
            if (library == null)
                library = AssetDatabase.LoadAssetAtPath<DecalLibrary>(
                    "Assets/_Project/DecalStudio/Art/EnvDecalLibrary.asset");
            Apply(false);
        }
#endif
    }
}

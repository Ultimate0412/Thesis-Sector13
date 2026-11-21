using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Sector13.Decals
{
    /// <summary>
    /// "ช่องรอย" ที่ผู้ใช้ลากวางเองใน prefab กล่อง
    /// - กำหนดได้ว่าช่องนี้คือรอยอะไร (traceType หรือ decalId เจาะจง)
    /// - ตอนรัน PackageTraceApplier จะเลือกเปิดเฉพาะช่องที่ตรงกับ verdict
    /// - ถ้าไม่ใส่ allowedVerdicts = ใช้ได้ทุก verdict
    /// </summary>
    [RequireComponent(typeof(DecalProjector))]
    public class DecalSlot : MonoBehaviour
    {
        [Header("Identity")]
        public TraceType traceType = TraceType.WaterStain;

        [Tooltip("ระบุ decal ใน atlas ตรง ๆ (เช่น BloodStain_Down_A) — ว่าง = ใช้ traceType")]
        public string decalId;

        public TraceVisibility visibility = TraceVisibility.NakedEye;

        [Header("Rules")]
        [Tooltip("แสดงเสมอเมื่อ verdict ตรง (ไม่ต้องผ่านการสุ่ม)")]
        public bool alwaysVisible;

        [Tooltip("ถ้าเว้นว่าง = ใช้ได้ทุก verdict")]
        public VerdictType[] allowedVerdicts;

        [Header("Weight")]
        [Tooltip("น้ำหนักเมื่อมีหลาย slot ชนิดเดียวกัน (เลือกอันเดียวต่อกล่อง)")]
        [Range(0f, 100f)] public float weight = 10f;

        [Header("UV Tweak (ปรับลายในช่อง atlas)")]
        [Tooltip("เลื่อนลายภายในช่อง (หน่วย 0-1 ของช่อง)")]
        public Vector2 uvOffset = Vector2.zero;

        [Tooltip("ซูมลาย (1 = เต็มช่อง, 0.8 = ย่อลง 20%)")]
        public Vector2 uvScaleMul = Vector2.one;

        public DecalProjector Projector => projector != null ? projector : (projector = GetComponent<DecalProjector>());

        private DecalProjector projector;

        private void Awake()
        {
            // กันรอยโผล่ก่อนระบบเลือก
            if (Application.isPlaying) SetVisible(false);
        }

        public bool Allows(VerdictType verdict)
        {
            if (allowedVerdicts == null || allowedVerdicts.Length == 0) return true;
            for (int i = 0; i < allowedVerdicts.Length; i++)
                if (allowedVerdicts[i] == verdict) return true;
            return false;
        }

        public void SetVisible(bool visible)
        {
            var p = Projector;
            if (p != null)
            {
                p.enabled = visible;
                p.fadeFactor = visible ? 1f : 0f;
            }
            gameObject.SetActive(visible);
        }

        /// <summary>ผูก material + uvRect จาก library (ถ้ามี)</summary>
        public void Bind(DecalLibrary library)
        {
            if (library == null) return;
            var p = Projector;
            if (p == null) return;

            if (!string.IsNullOrEmpty(decalId) && library.TryGetEntryById(decalId, out var entryById))
            {
                Apply(p, entryById);
                return;
            }

            if (library.TryGetEntry(traceType, out var entry))
            {
                Apply(p, entry);
            }
        }

        private void Apply(DecalProjector p, DecalEntry entry)
        {
            if (entry == null) return;
            if (entry.material != null) p.material = entry.material;

            var rect = entry.uvRect;
            p.uvScale = new Vector2(rect.width * uvScaleMul.x, rect.height * uvScaleMul.y);
            p.uvBias = new Vector2(
                rect.x + uvOffset.x * rect.width,
                rect.y + uvOffset.y * rect.height);
        }
    }
}

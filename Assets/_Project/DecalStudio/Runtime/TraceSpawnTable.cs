using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sector13.Decals
{
    /// <summary>ผลลัพธ์ปลายทางของพัสดุ (ตามตารางดีไซน์ 5 คอลัมน์)</summary>
    public enum VerdictType
    {
        Approve,    // ส่งได้
        Return,     // ส่งกลับ
        Illegal,    // ผิดกฎ
        Alien,      // เอเลี่ยน
        OpenBox     // ต้องเปิดกล่อง (ไม่ใช้ decal — เก็บไว้เป็น hook ของระบบอื่น)
    }

    /// <summary>กฎของรอย 1 ชนิด ภายใน verdict หนึ่ง ๆ</summary>
    [Serializable]
    public class TraceSpawnRule
    {
        public TraceType traceType = TraceType.WaterStain;
        public bool enabled = true;

        [Tooltip("น้ำหนักการสุ่มเทียบกับรอยอื่นใน verdict เดียวกัน")]
        [Range(0f, 100f)] public float weight = 10f;

        [Tooltip("จำนวนรอยของชนิดนี้ต่อกล่อง (สุ่มในช่วง)")]
        public int minCount = 1;
        public int maxCount = 1;

        public TraceVisibility visibility = TraceVisibility.NakedEye;

        [Tooltip("ทับขนาดที่ตั้งไว้ใน placement profile (0 = ใช้ของ profile)")]
        public Vector2 sizeOverride = Vector2.zero;

        [Tooltip("ระบุ decal id เจาะจงใน atlas (เช่น BloodStain_Down_A) — ว่าง = ใช้ traceType")]
        public string decalId;

        [Tooltip("โหมด Hybrid: ถ้าเปิด จะใช้ DecalSlot ที่วางไว้ก่อน ถ้าไม่มีค่อยสุ่ม")]
        public bool useAuthoredSlot = true;

        public TracePlacementProfile placement;
    }

    /// <summary>กฎของ verdict 1 แบบ</summary>
    [Serializable]
    public class VerdictSpawnRule
    {
        public VerdictType verdict = VerdictType.Approve;
        public bool enabled = true;

        [Tooltip("โอกาสที่กล่องนี้จะมีรอยอะไรก็ได้")]
        [Range(0f, 100f)] public float spawnChancePercent = 70f;

        [Tooltip("จำนวนชนิดรอยสูงสุดต่อกล่อง")]
        public int minTypes = 1;
        public int maxTypes = 2;

        public List<TraceSpawnRule> rules = new List<TraceSpawnRule>();

        public bool IsUsable => enabled && rules != null && rules.Count > 0;
    }

    /// <summary>
    /// ตาราง spawn ทั้งหมด — แก้ใน Inspector ได้โดยไม่ต้องแตะโค้ด
    /// ยึดตามตารางดีไซน์: ส่งได้ / ส่งกลับ / ผิดกฎ / เอเลี่ยน / ต้องเปิดกล่อง
    /// </summary>
    [CreateAssetMenu(menuName = "Sector13/Decals/Trace Spawn Table", fileName = "TraceSpawnTable")]
    public class TraceSpawnTable : ScriptableObject
    {
        public List<VerdictSpawnRule> verdicts = new List<VerdictSpawnRule>();

        public VerdictSpawnRule Get(VerdictType verdict)
        {
            for (int i = 0; i < verdicts.Count; i++)
            {
                if (verdicts[i] != null && verdicts[i].verdict == verdict) return verdicts[i];
            }
            return null;
        }

        public static VerdictType FromCategory(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Illegal: return VerdictType.Illegal;
                case ItemCategory.Alien: return VerdictType.Alien;
                default: return VerdictType.Approve;
            }
        }
    }
}

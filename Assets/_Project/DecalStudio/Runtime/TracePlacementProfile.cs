using UnityEngine;

namespace Sector13.Decals
{
    /// <summary>หน้าที่อนุญาตให้วาง decal (ใช้เป็น flag รวมกันได้)</summary>
    [System.Flags]
    public enum DecalFaceMask
    {
        None = 0,
        Top = 1 << 0,
        Bottom = 1 << 1,
        Front = 1 << 2,   // +Z
        Back = 1 << 3,    // -Z (ด้านที่มีใบปะหน้า)
        Left = 1 << 4,    // -X
        Right = 1 << 5,   // +X
        Sides = Front | Back | Left | Right,
        All = Top | Bottom | Sides
    }

    /// <summary>
    /// กฎการวางรอยของ decal 1 ชนิด — กำหนดว่าออกหน้าไหน, ชิดขอบแค่ไหน, ไหลลงล่างไหม
    /// </summary>
    [CreateAssetMenu(menuName = "Sector13/Decals/Placement Profile", fileName = "TracePlacement")]
    public class TracePlacementProfile : ScriptableObject
    {
        [Header("Faces")]
        public DecalFaceMask allowedFaces = DecalFaceMask.Sides;

        [Tooltip("หลบด้านที่มีใบปะหน้า (Back / -Z)")]
        public bool avoidLabelFace = true;

        [Header("Position")]
        [Tooltip("ระยะเว้นขอบหน้า (0.8 = ใช้พื้นที่ 80% ตรงกลาง)")]
        [Range(0.4f, 1f)] public float margin = 0.8f;

        [Tooltip("โอกาสไปออกที่สันขอบแทนกลางหน้า")]
        [Range(0f, 1f)] public float edgeChance = 0.1f;

        [Tooltip("0 = กระจายทั่วหน้า, 1 = กองลงด้านล่าง (คราบไหล/เลือด)")]
        [Range(0f, 1f)] public float gravityFlow = 0f;

        [Tooltip("ระยะห่างขั้นต่ำจากรอยอื่น (เมตร)")]
        [Range(0f, 0.6f)] public float minSpacing = 0.1f;

        [Tooltip("กันรอยทับกัน: ถ้าตำแหน่งชนรอยเดิมจะสุ่มใหม่ (สูงสุด 8 ครั้ง)")]
        public bool preventOverlap = true;

        [Header("Orientation")]
        [Tooltip("ช่วงมุมหมุนรอบแกนฉาย (องศา) — 0..0 = ตรงเสมอ")]
        public Vector2 spinRange = new Vector2(0f, 360f);

        [Header("Size (เมตร)")]
        public Vector2 sizeRange = new Vector2(0.35f, 0.60f);

        public bool Allows(DecalFaceMask face) => (allowedFaces & face) != 0;
    }
}

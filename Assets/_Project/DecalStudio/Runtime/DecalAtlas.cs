using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sector13.Decals
{
    /// <summary>
    /// หนึ่งช่อง (cell) ใน atlas = หนึ่ง decal หนึ่งชนิด
    /// เก็บ rect แบบ normalize ไว้ใช้กับ DecalProjector.uvScale / uvBias โดยตรง
    /// </summary>
    [Serializable]
    public class DecalEntry
    {
        [Tooltip("ชื่ออ้างอิง ใช้ตั้งชื่อ material และไฟล์")]
        public string id = "decal";

        [Tooltip("ชนิดร่องรอยในเกม (ใช้ผูกกับ PackageTraceApplier)")]
        public TraceType traceType = TraceType.WaterStain;

        [Tooltip("รอยที่ต้องส่องด้วยไฟ UV ถึงจะเห็น (ใช้ material ที่มี emission)")]
        public bool requiresUV;

        [Tooltip("ตำแหน่งช่องใน atlas (คอลัมน์, แถว)")]
        public Vector2Int cell;

        [Tooltip("พื้นที่ของรอยใน atlas (0-1) — ใช้กับ uvScale/uvBias")]
        public Rect uvRect = new Rect(0, 0, 1, 1);

        [Tooltip("สีของรอย (คูณกับค่า luminance ที่ bake ไว้ใน atlas)")]
        public Color tint = Color.white;

        [Range(0f, 1f)] public float heightStrength;
        [Range(0f, 2f)] public float smoothnessScale = 1f;
        [Range(0f, 2f)] public float metallicScale = 1f;

        [Tooltip("material ที่ builder สร้างให้ (1 ตัวต่อ 1 ช่อง)")]
        public Material material;
    }

    /// <summary>
    /// Atlas เดียวเก็บได้หลาย decal (grid 4x4 = 16 ช่องที่ 2048/512)
    /// </summary>
    [CreateAssetMenu(menuName = "Sector13/Decals/Decal Atlas", fileName = "DecalAtlas")]
    public class DecalAtlas : ScriptableObject
    {
        public string atlasId = "atlas";
        public int size = 2048;
        public int cellSize = 512;
        public int padding = 16;

        [Header("Pages")]
        [Tooltip("RGB = luminance ของรอย, A = coverage")]
        public Texture2D albedo;

        [Tooltip("RG = normal XY, B = height")]
        public Texture2D normal;

        [Tooltip("R = metallic, G = occlusion, B = smoothness, A = blend")]
        public Texture2D mask;

        [Header("Entries")]
        public List<DecalEntry> entries = new List<DecalEntry>();

        public int GridSize => Mathf.Max(1, size / Mathf.Max(1, cellSize));

        public DecalEntry Find(TraceType type)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].traceType == type) return entries[i];
            }
            return null;
        }

        public DecalEntry FindById(string id)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].id == id) return entries[i];
            }
            return null;
        }
    }
}

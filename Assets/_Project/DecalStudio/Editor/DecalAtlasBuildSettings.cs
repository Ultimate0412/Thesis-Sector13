using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sector13.Decals.EditorTools
{
    [Serializable]
    public class DecalSourceEntry
    {
        [Tooltip("ชื่อสั้น ๆ ใช้ตั้งชื่อ material เช่น WaterStain")]
        public string id = "decal";

        public TraceType traceType = TraceType.WaterStain;

        [Tooltip("ต้องส่องด้วยไฟ UV ถึงจะเห็น")]
        public bool requiresUV;

        [Header("Source textures (อย่างน้อยต้องมี albedo)")]
        public Texture2D albedo;

        [Tooltip("Normal map (RGB) — ไม่ใส่ได้")]
        public Texture2D normal;

        [Tooltip("Height map (grayscale, พื้นเรียบ = กลาง ๆ) — ไม่ใส่ได้")]
        public Texture2D height;

        [Tooltip("Mask: R=metallic G=AO B=smoothness — ไม่ใส่ได้")]
        public Texture2D mask;

        [Header("External folder (ใช้แทน Texture2D ด้านบนได้)")]
        [Tooltip("โฟลเดอร์ที่มี <id>_Albedo.png / _Normal.png / _Height.png / _Mask.png — ใช้เมื่อช่อง Texture2D ว่าง")]
        public string externalFolder;

        [Tooltip("mask ที่ export มาเป็น Roughness (ไม่ใช่ Glossiness) — เปิดเพื่อกลับค่า B เป็น smoothness")]
        public bool maskIsRoughness;

        [Header("Look")]
        public Color tint = Color.white;
        [Range(0f, 1f)] public float heightStrength;
        [Range(0f, 2f)] public float metallicScale = 1f;
        [Range(0f, 2f)] public float smoothnessScale = 1f;
    }

    /// <summary>
    /// ค่าการ build atlas (แก้ในหน้าต่าง Decal Studio > Atlas Builder)
    /// </summary>
    [CreateAssetMenu(menuName = "Sector13/Decals/Atlas Build Settings", fileName = "DecalAtlasBuildSettings")]
    public class DecalAtlasBuildSettings : ScriptableObject
    {
        public string atlasId = "Evidence";

        [Header("Layout")]
        public int atlasSize = 2048;
        public int cellSize = 512;
        [Tooltip("ระยะกันขอบต่อช่อง (รวม gutter ระหว่างช่อง = 2x ค่านี้)")]
        public int padding = 16;

        [Header("Look")]
        [Tooltip("แปลง albedo เป็น grayscale (แนะนำ — สีมาจาก Tint ของ material)")]
        public bool grayscaleAlbedo = true;

        [Tooltip("พลิก V ของ uvRect (เปิดถ้ารอยกลับหัวในเกม)")]
        public bool flipV;

        [Header("Output")]
        public string outputFolder = "Assets/_Project/DecalStudio/Art/Atlases";

        [Header("External sources (SP export)")]
        [Tooltip("โฟลเดอร์รากที่มี <ชื่อ>/Textures/<ชื่อ>_Albedo.png — ใช้กับปุ่ม Scan external folders")]
        public string externalSourceRoot = "D:/01_Work_Projects/3D_Models/3D/Maya/Thesis/Asset/2DAlpha/Decal";

        [Tooltip("ชื่อไฟล์ท้ายของแต่ละ map (ตัวคั่นระหว่าง <id> กับชื่อ map)")]
        public string albedoPattern = "{0}_Albedo.png";
        public string normalPattern = "{0}_Normal.png";
        public string heightPattern = "{0}_Height.png";
        public string maskPattern = "{0}_Mask.png";

        [Header("Sources")]
        public List<DecalSourceEntry> sources = new List<DecalSourceEntry>();
    }
}

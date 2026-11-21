using UnityEditor;
using UnityEngine;

namespace Sector13.Decals.EditorTools
{
    /// <summary>
    /// Inspector ของ material decal: Unity วาดพรีวิว (ทรงกลม) ไม่ได้เพราะ shader เป็น DBuffer decal
    /// → วาดรูปช่องใน atlas ที่ material นี้ใช้ แทนพรีวิวปกติ
    /// </summary>
    public class DecalAtlasShaderGUI : ShaderGUI
    {
        const string LibraryPath = "Assets/_Project/DecalStudio/Art/DecalLibrary.asset";

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            if (materialEditor.target is Material material) DrawPreview(material);
            EditorGUILayout.Space(4);
            base.OnGUI(materialEditor, properties);
        }

        static void DrawPreview(Material material)
        {
            Texture texture = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;

            Rect uv;
            if (!TryGetCellFromLibrary(material, out uv)) uv = GetCellFromMaterial(material);
            if (uv.width <= 0.0001f || uv.height <= 0.0001f) uv = new Rect(0f, 0f, 1f, 1f);

            Color tint = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
            if (tint.a <= 0.001f) tint.a = 1f;

            float size = Mathf.Clamp(EditorGUIUtility.currentViewWidth - 70f, 96f, 220f);
            Rect rect = GUILayoutUtility.GetRect(size, size);
            rect.x += 2f;

            DecalPreview.Draw(rect, texture, uv, tint);

            var label = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            GUILayout.Label(material.name, label);
        }

        /// <summary>หาช่องใน atlas จากคลัง decal (material → entry)</summary>
        static bool TryGetCellFromLibrary(Material material, out Rect uv)
        {
            uv = default;
            var library = AssetDatabase.LoadAssetAtPath<DecalLibrary>(LibraryPath);
            if (library == null) return false;

            var entries = library.AllEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.material != material) continue;
                uv = entry.uvRect;
                return true;
            }
            return false;
        }

        /// <summary>fallback: ค่าที่ builder เขียนไว้บน material (_DecalRect)</summary>
        static Rect GetCellFromMaterial(Material material)
        {
            if (!material.HasProperty("_DecalRect")) return new Rect(0f, 0f, 1f, 1f);
            Vector4 cell = material.GetVector("_DecalRect");
            if (cell.z <= 0.0001f || cell.w <= 0.0001f) return new Rect(0f, 0f, 1f, 1f);
            return new Rect(cell.x, cell.y, cell.z, cell.w);
        }
    }
}

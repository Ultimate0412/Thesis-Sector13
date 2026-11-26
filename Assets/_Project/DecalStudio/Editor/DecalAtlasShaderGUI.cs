using UnityEditor;
using UnityEngine;

namespace Sector13.Decals.EditorTools
{
    /// <summary>
    /// Inspector ของ material decal: Unity วาดพรีวิว (ทรงกลม) ไม่ได้เพราะ shader เป็น DBuffer decal
    /// → วาดพรีวิวแบบมีแสงแทน (albedo + normal + height + metallic/smoothness + emission)
    /// </summary>
    public class DecalAtlasShaderGUI : ShaderGUI
    {
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            if (materialEditor.target is Material material) DrawPreview(material);
            EditorGUILayout.Space(4);
            base.OnGUI(materialEditor, properties);
        }

        static void DrawPreview(Material material)
        {
            float size = Mathf.Clamp(EditorGUIUtility.currentViewWidth - 70f, 96f, 240f);
            Rect rect = GUILayoutUtility.GetRect(size, size);
            rect.x += 2f;

            // อ่านค่าจริงจาก material ทุกครั้งที่ Inspector วาดใหม่ → แก้ค่าแล้วเห็นผลทันที
            DecalPreview.DrawLit(rect, material);

            var label = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            GUILayout.Label(material.name, label);
        }
    }
}

using UnityEditor;
using UnityEngine;

namespace Sector13.Decals.EditorTools
{
    /// <summary>
    /// วาดพรีวิว decal 1 ช่องจาก atlas (crop uvRect แล้วคูณ tint)
    /// ใช้ร่วมกันระหว่าง Material Inspector และหน้า Decal Browser
    /// </summary>
    public static class DecalPreview
    {
        static readonly Color Background = new Color(0.10f, 0.10f, 0.10f, 1f);
        static readonly Color Border = new Color(1f, 1f, 1f, 0.15f);

        public static void Draw(Rect rect, Texture texture, Rect uvRect, Color tint, bool drawBackground = true, bool drawBorder = true)
        {
            if (drawBackground) EditorGUI.DrawRect(rect, Background);
            if (texture != null)
            {
                Color previous = GUI.color;
                GUI.color = Normalize(tint);
                GUI.DrawTextureWithTexCoords(rect, texture, uvRect, true);
                GUI.color = previous;
            }
            if (drawBorder) DrawBorder(rect);
        }

        public static void DrawBorder(Rect rect)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), Border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), Border);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), Border);
        }

        /// <summary>ปรับ tint ให้ช่องสว่างสุด = 1 เพื่อให้เห็นสีชัด (GUI.color ไม่รับค่าเกิน 1)</summary>
        public static Color Normalize(Color tint)
        {
            float max = Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b));
            if (max <= 0.001f) return Color.white;
            return new Color(tint.r / max, tint.g / max, tint.b / max, 1f);
        }
    }
}

using UnityEditor;
using UnityEngine;

namespace Sector13.Decals.EditorTools
{
    /// <summary>
    /// พรีวิว decal 1 ช่องจาก atlas
    /// - Draw(): พรีวิว 2D แบบเดิม (ใช้ใน grid ของ Decal Browser)
    /// - DrawLit(): พรีวิวแบบมีแสง/normal/height/specular/emission (ใช้ใน Inspector และหน้าต่าง Decal Preview)
    /// </summary>
    public static class DecalPreview
    {
        const string LibraryPath = "Assets/_Project/DecalStudio/Art/DecalLibrary.asset";
        const string EnvLibraryPath = "Assets/_Project/DecalStudio/Art/EnvDecalLibrary.asset";
        const string LitShaderName = "Hidden/Sector13/DecalPreview";

        static readonly Color Background = new Color(0.10f, 0.10f, 0.10f, 1f);
        static readonly Color Border = new Color(1f, 1f, 1f, 0.15f);

        static Material litMaterial;

        // ------------------------------------------------------------------ cell

        /// <summary>หาช่องใน atlas: ดูจาก DecalLibrary ก่อน แล้ว fallback ไป _DecalRect</summary>
        public static Rect GetCell(Material material)
        {
            if (material == null) return new Rect(0f, 0f, 1f, 1f);

            // ค้นทั้ง Package และ Env library — material ฝั่ง Env ไม่ได้อยู่ใน DecalLibrary
            var libraries = new[]
            {
                AssetDatabase.LoadAssetAtPath<DecalLibrary>(LibraryPath),
                AssetDatabase.LoadAssetAtPath<DecalLibrary>(EnvLibraryPath)
            };

            for (int l = 0; l < libraries.Length; l++)
            {
                var library = libraries[l];
                if (library == null) continue;

                var entries = library.AllEntries();
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (entry == null || entry.material != material) continue;
                    return entry.uvRect;
                }
            }

            if (material.HasProperty("_DecalRect"))
            {
                Vector4 cell = material.GetVector("_DecalRect");
                if (cell.z > 0.0001f && cell.w > 0.0001f)
                    return new Rect(cell.x, cell.y, cell.z, cell.w);
            }

            return new Rect(0f, 0f, 1f, 1f);
        }

        // ------------------------------------------------------------------ 2D

        public static void Draw(Rect rect, Texture texture, Rect uvRect, Color tint, bool drawBackground = true, bool drawBorder = true)
        {
            Draw(rect, texture, uvRect, tint, 1f, 1f, 1f, Color.black, 0f, drawBackground, drawBorder);
        }

        /// <summary>พรีวิว 2D แบบอ่านค่าจาก material จริง</summary>
        public static void Draw(Rect rect, Texture texture, Rect uvRect, Color tint,
            float opacity, float brightness, float saturation,
            Color emissionColor, float emissionIntensity,
            bool drawBackground = true, bool drawBorder = true)
        {
            if (drawBackground) EditorGUI.DrawRect(rect, Background);

            if (texture != null)
            {
                Color previous = GUI.color;

                Color color = TintForPreview(tint, brightness, saturation);
                color.a = Mathf.Clamp01(opacity);
                GUI.color = color;
                GUI.DrawTextureWithTexCoords(rect, texture, uvRect, true);

                if (emissionIntensity > 0.001f)
                {
                    Color emission = Normalize(emissionColor);
                    emission.a = Mathf.Clamp01(emissionIntensity * 0.35f);
                    GUI.color = emission;
                    GUI.DrawTextureWithTexCoords(rect, texture, uvRect, true);
                }

                GUI.color = previous;
            }

            if (drawBorder) DrawBorder(rect);
        }

        // ------------------------------------------------------------------ lit

        /// <summary>
        /// พรีวิวแบบมีแสง: ใช้ normal/height/metallic/smoothness/emission จาก material
        /// ถ้า shader preview หาไม่เจอ จะ fallback ไปพรีวิว 2D
        /// </summary>
        public static void DrawLit(Rect rect, Material material, bool drawBorder = true)
        {
            Rect cell = GetCell(material);

            if (material == null || Shader.Find(LitShaderName) == null)
            {
                Draw(rect, material != null && material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null,
                    cell,
                    GetColor(material, "_BaseColor", Color.white),
                    GetFloat(material, "_Opacity", 1f),
                    GetFloat(material, "_Brightness", 1f),
                    GetFloat(material, "_Saturation", 1f),
                    GetColor(material, "_EmissionColor", Color.black),
                    GetFloat(material, "_EmissionIntensity", 0f));
                return;
            }

            if (Event.current != null && Event.current.type != EventType.Repaint) return;

            var mat = GetLitMaterial();
            Apply(material, cell, mat);

            int width = Mathf.Max(8, Mathf.RoundToInt(rect.width));
            int height = Mathf.Max(8, Mathf.RoundToInt(rect.height));
            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            Graphics.Blit(Texture2D.whiteTexture, rt, mat);
            RenderTexture.active = previous;

            GUI.DrawTexture(rect, rt, ScaleMode.StretchToFill, false);
            RenderTexture.ReleaseTemporary(rt);

            if (drawBorder) DrawBorder(rect);
        }

        /// <summary>เรนเดอร์พรีวิวเป็น Texture2D สำหรับ Asset Preview / thumbnail</summary>
        public static Texture2D RenderStatic(Material material, int width, int height)
        {
            if (material == null || Shader.Find(LitShaderName) == null) return null;

            var mat = GetLitMaterial();
            Apply(material, GetCell(material), mat);

            width = Mathf.Max(8, width);
            height = Mathf.Max(8, height);
            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            Graphics.Blit(Texture2D.whiteTexture, rt, mat);
            RenderTexture.active = rt;

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            texture.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return texture;
        }

        static Material GetLitMaterial()
        {
            if (litMaterial == null)
            {
                var shader = Shader.Find(LitShaderName);
                if (shader == null) return null;
                litMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            return litMaterial;
        }

        static void Apply(Material source, Rect cell, Material target)
        {
            if (target == null) return;

            target.SetTexture("_BaseMap", GetTexture(source, "_BaseMap"));
            target.SetTexture("_NormalMap", GetTexture(source, "_NormalMap"));
            target.SetTexture("_MaskMap", GetTexture(source, "_MaskMap"));
            target.SetVector("_DecalRect", new Vector4(cell.x, cell.y, cell.width, cell.height));

            target.SetColor("_BaseColor", GetColor(source, "_BaseColor", Color.white));
            target.SetFloat("_Opacity", GetFloat(source, "_Opacity", 1f));
            target.SetFloat("_Brightness", GetFloat(source, "_Brightness", 1f));
            target.SetFloat("_Saturation", GetFloat(source, "_Saturation", 1f));
            target.SetFloat("_Variation", GetFloat(source, "_Variation", 0f));
            target.SetFloat("_HeightStrength", GetFloat(source, "_HeightStrength", 0f));
            target.SetFloat("_NormalStrength", GetFloat(source, "_NormalStrength", 1f));
            target.SetFloat("_MetallicScale", GetFloat(source, "_MetallicScale", 1f));
            target.SetFloat("_SmoothnessScale", GetFloat(source, "_SmoothnessScale", 1f));
            target.SetColor("_EmissionColor", GetColor(source, "_EmissionColor", Color.black));
            target.SetFloat("_EmissionIntensity", GetFloat(source, "_EmissionIntensity", 0f));

            // แสงจำลองที่สว่างกว่าฉากจริงเล็กน้อย เพื่อให้เห็นลาย/สีชัดใน thumbnail
            target.SetVector("_LightDir", new Vector4(-0.35f, 0.55f, 0.75f, 0f));
            target.SetColor("_LightColor", new Color(2.2f, 2.2f, 2.2f, 1f));
            target.SetColor("_Ambient", new Color(0.55f, 0.55f, 0.55f, 1f));
            target.SetColor("_SurfaceColor", new Color(0.20f, 0.20f, 0.20f, 1f));
        }

        static Texture GetTexture(Material material, string property)
        {
            return material != null && material.HasProperty(property) ? material.GetTexture(property) : null;
        }

        static Color GetColor(Material material, string property, Color fallback)
        {
            return material != null && material.HasProperty(property) ? material.GetColor(property) : fallback;
        }

        static float GetFloat(Material material, string property, float fallback)
        {
            return material != null && material.HasProperty(property) ? material.GetFloat(property) : fallback;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>แปลง tint + brightness + saturation เป็นสีที่ใช้แสดงผลใน GUI (RGB ไม่เกิน 1)</summary>
        public static Color TintForPreview(Color tint, float brightness, float saturation)
        {
            float luminance = tint.r * 0.2126f + tint.g * 0.7152f + tint.b * 0.0722f;
            Color gray = new Color(luminance, luminance, luminance, 1f);

            saturation = Mathf.Clamp(saturation, 0f, 2.5f);
            Color result = new Color(
                Mathf.Max(0f, gray.r + (tint.r - gray.r) * saturation),
                Mathf.Max(0f, gray.g + (tint.g - gray.g) * saturation),
                Mathf.Max(0f, gray.b + (tint.b - gray.b) * saturation),
                1f);

            result = Normalize(result);

            // ให้ brightness มีผลกับพรีวิว แต่ไม่ดำสนิทจนดูไม่ออก
            float exposure = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(brightness));
            result.r *= exposure;
            result.g *= exposure;
            result.b *= exposure;
            result.a = 1f;
            return result;
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

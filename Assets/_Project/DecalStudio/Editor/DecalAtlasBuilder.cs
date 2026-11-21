using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Sector13.Decals.EditorTools
{
    /// <summary>
    /// รวม source texture หลายไฟล์เป็น atlas 1 แผ่น (albedo / normal+height / mask)
    /// แล้วสร้าง DecalAtlas + material ต่อช่องให้อัตโนมัติ
    /// </summary>
    public static class DecalAtlasBuilder
    {
        const string ShaderName = "Sector13/Decals/DecalAtlas";

        public static DecalAtlas Build(DecalAtlasBuildSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            int size = Mathf.Max(64, settings.atlasSize);
            int cell = Mathf.Clamp(settings.cellSize, 32, size);
            int pad = Mathf.Clamp(settings.padding, 0, cell / 4);
            int grid = Mathf.Max(1, size / cell);
            int capacity = grid * grid;

            var valid = new List<DecalSourceEntry>();
            foreach (var s in settings.sources)
            {
                if (s == null) continue;
                if (HasAlbedo(s, settings)) valid.Add(s);
            }

            if (valid.Count == 0)
                throw new InvalidOperationException("ไม่มี source ที่มี albedo เลย — เพิ่มอย่างน้อย 1 ช่องก่อน build");

            if (valid.Count > capacity)
                throw new InvalidOperationException($"ช่องไม่พอ: มี {valid.Count} source แต่ atlas {size}/{cell} เก็บได้ {capacity} ช่อง");

            // ---- pages ------------------------------------------------------
            var albedoPx = NewArray(size, new Color32(0, 0, 0, 0));
            var normalPx = NewArray(size, new Color32(128, 128, 128, 255));
            var maskPx = NewArray(size, new Color32(0, 255, 128, 255));

            int content = Mathf.Max(1, cell - pad * 2);

            for (int i = 0; i < valid.Count; i++)
            {
                var src = valid[i];
                int cx = i % grid;
                int cy = i / grid;
                int originX = cx * cell;
                int originY = cy * cell;

                string folder = ResolveFolder(src, settings);

                // albedo
                if (TryLoad(src.albedo, folder, string.Format(settings.albedoPattern, src.id), out var aPx, out int aw, out int ah))
                {
                    var resized = Resize(aPx, aw, ah, content, content, settings.grayscaleAlbedo);
                    DilateRGB(resized, content, content, pad);
                    Blit(albedoPx, size, originX + pad, originY + pad, resized, content, content, blend: false);
                    EdgeExtend(albedoPx, size, originX, originY, cell, pad, keepAlpha: true);
                }

                // normal (RG) + height (B)
                if (TryLoad(src.normal, folder, string.Format(settings.normalPattern, src.id), out var nPx, out int nw, out int nh))
                {
                    var resized = Resize(nPx, nw, nh, content, content, grayscale: false);
                    if (TryLoad(src.height, folder, string.Format(settings.heightPattern, src.id), out var hPx, out int hw, out int hh))
                    {
                        var hResized = Resize(hPx, hw, hh, content, content, grayscale: true);
                        for (int p = 0; p < resized.Length; p++)
                            resized[p].b = hResized[p].r;
                    }
                    Blit(normalPx, size, originX + pad, originY + pad, resized, content, content, blend: false);
                    EdgeExtend(normalPx, size, originX, originY, cell, pad, keepAlpha: true);
                }
                else if (TryLoad(src.height, folder, string.Format(settings.heightPattern, src.id), out var h2Px, out int h2w, out int h2h))
                {
                    var hResized = Resize(h2Px, h2w, h2h, content, content, grayscale: true);
                    for (int p = 0; p < hResized.Length; p++)
                        hResized[p].b = hResized[p].r;
                    Blit(normalPx, size, originX + pad, originY + pad, hResized, content, content, blend: false);
                    EdgeExtend(normalPx, size, originX, originY, cell, pad, keepAlpha: true);
                }

                // mask (R metallic / G AO / B smoothness / A blend)
                if (TryLoad(src.mask, folder, string.Format(settings.maskPattern, src.id), out var mPx, out int mw, out int mh))
                {
                    var resized = Resize(mPx, mw, mh, content, content, grayscale: false);
                    for (int p = 0; p < resized.Length; p++)
                    {
                        resized[p].a = 255;
                        // SP export เป็น Roughness → กลับค่าเป็น smoothness
                        if (src.maskIsRoughness) resized[p].b = (byte)(255 - resized[p].b);
                    }
                    Blit(maskPx, size, originX + pad, originY + pad, resized, content, content, blend: false);
                    EdgeExtend(maskPx, size, originX, originY, cell, pad, keepAlpha: true);
                }
            }

            // ---- write textures --------------------------------------------
            EnsureFolder(settings.outputFolder);
            string baseName = string.IsNullOrEmpty(settings.atlasId) ? "DecalAtlas" : settings.atlasId;
            string albedoPath = $"{settings.outputFolder}/{baseName}_Albedo.png";
            string normalPath = $"{settings.outputFolder}/{baseName}_Normal.png";
            string maskPath = $"{settings.outputFolder}/{baseName}_Mask.png";

            WritePng(albedoPath, albedoPx, size, srgb: true, alpha: true);
            WritePng(normalPath, normalPx, size, srgb: false, alpha: false);
            WritePng(maskPath, maskPx, size, srgb: false, alpha: true);

            // ---- atlas asset ------------------------------------------------
            string atlasPath = $"{settings.outputFolder}/{baseName}.asset";
            var atlas = AssetDatabase.LoadAssetAtPath<DecalAtlas>(atlasPath);
            if (atlas == null)
            {
                atlas = ScriptableObject.CreateInstance<DecalAtlas>();
                AssetDatabase.CreateAsset(atlas, atlasPath);
            }

            atlas.atlasId = baseName;
            atlas.size = size;
            atlas.cellSize = cell;
            atlas.padding = pad;
            atlas.albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            atlas.normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            atlas.mask = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
            atlas.entries = new List<DecalEntry>();

            // ---- materials --------------------------------------------------
            EnsureFolder($"{settings.outputFolder}/Materials");
            var shader = Shader.Find(ShaderName);
            if (shader == null) throw new InvalidOperationException($"หา shader '{ShaderName}' ไม่เจอ");

            for (int i = 0; i < valid.Count; i++)
            {
                var src = valid[i];
                int cx = i % grid;
                int cy = i / grid;

                float u0 = cx * cell / (float)size;
                float v0 = cy * cell / (float)size;
                float u1 = (cx + 1) * cell / (float)size;
                float v1 = (cy + 1) * cell / (float)size;
                if (settings.flipV)
                {
                    float t = v0; v0 = 1f - v1; v1 = 1f - t;
                }

                string matName = Sanitize(string.IsNullOrEmpty(src.id) ? src.traceType.ToString() : src.id);
                string matPath = $"{settings.outputFolder}/Materials/M_Decal_{matName}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                else
                {
                    mat.shader = shader;
                }

                mat.SetTexture("_BaseMap", atlas.albedo);
                mat.SetTexture("_NormalMap", atlas.normal);
                mat.SetTexture("_MaskMap", atlas.mask);
                mat.SetColor("_BaseColor", src.tint);
                mat.SetFloat("_HeightStrength", src.heightStrength);
                mat.SetFloat("_MetallicScale", src.metallicScale);
                mat.SetFloat("_SmoothnessScale", src.smoothnessScale);
                mat.SetFloat("_Opacity", 1f);
                mat.SetVector("_DecalRect", new Vector4(u0, v0, u1 - u0, v1 - v0));
                mat.enableInstancing = true;

                if (src.requiresUV)
                {
                    mat.SetColor("_EmissionColor", src.tint);
                    mat.SetFloat("_EmissionIntensity", 1.5f);
                }
                else
                {
                    mat.SetColor("_EmissionColor", Color.black);
                    mat.SetFloat("_EmissionIntensity", 0f);
                }

                EditorUtility.SetDirty(mat);

                atlas.entries.Add(new DecalEntry
                {
                    id = string.IsNullOrEmpty(src.id) ? src.traceType.ToString() : src.id,
                    traceType = src.traceType,
                    requiresUV = src.requiresUV,
                    cell = new Vector2Int(cx, cy),
                    uvRect = new Rect(u0, v0, u1 - u0, v1 - v0),
                    tint = src.tint,
                    heightStrength = src.heightStrength,
                    metallicScale = src.metallicScale,
                    smoothnessScale = src.smoothnessScale,
                    material = mat
                });
            }

            EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[DecalStudio] สร้าง atlas '{baseName}' เสร็จ: {valid.Count} ช่อง, {size}x{size}, cell {cell}, padding {pad} → {settings.outputFolder}");
            return atlas;
        }

        // ------------------------------------------------------------------
        // texture helpers
        // ------------------------------------------------------------------
        static Color32[] NewArray(int size, Color32 fill)
        {
            var arr = new Color32[size * size];
            for (int i = 0; i < arr.Length; i++) arr[i] = fill;
            return arr;
        }

        static bool HasAlbedo(DecalSourceEntry src, DecalAtlasBuildSettings settings)
        {
            if (src.albedo != null) return true;
            string folder = ResolveFolder(src, settings);
            if (string.IsNullOrEmpty(folder)) return false;
            return File.Exists(Path.Combine(folder, string.Format(settings.albedoPattern, src.id)));
        }

        /// <summary>หาโฟลเดอร์ของ source: externalFolder → โฟลเดอร์ของ albedo → root/&lt;id&gt;/Textures</summary>
        static string ResolveFolder(DecalSourceEntry src, DecalAtlasBuildSettings settings)
        {
            if (!string.IsNullOrEmpty(src.externalFolder) && Directory.Exists(src.externalFolder))
                return src.externalFolder;

            if (src.albedo != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(src.albedo);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    string dir = Path.GetDirectoryName(Path.GetFullPath(assetPath));
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) return dir;
                }
            }

            if (!string.IsNullOrEmpty(settings.externalSourceRoot) && !string.IsNullOrEmpty(src.id))
            {
                string withTextures = Path.Combine(settings.externalSourceRoot, src.id, "Textures");
                if (Directory.Exists(withTextures)) return withTextures;

                string plain = Path.Combine(settings.externalSourceRoot, src.id);
                if (Directory.Exists(plain)) return plain;
            }
            return null;
        }

        /// <summary>โหลด map จาก Texture2D ก่อน ถ้าว่างค่อยอ่านไฟล์ &lt;folder&gt;/&lt;pattern(id)&gt;</summary>
        static bool TryLoad(Texture2D texture, string folder, string fileName, out Color32[] pixels, out int width, out int height)
        {
            if (texture != null) return TryDecode(texture, out pixels, out width, out height);

            if (!string.IsNullOrEmpty(folder) && !string.IsNullOrEmpty(fileName))
            {
                string path = Path.Combine(folder, fileName);
                if (File.Exists(path)) return TryDecodeFile(path, out pixels, out width, out height);
            }

            pixels = null;
            width = height = 0;
            return false;
        }

        static bool TryDecodeFile(string path, out Color32[] pixels, out int width, out int height)
        {
            pixels = null;
            width = height = 0;
            var tmp = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!tmp.LoadImage(File.ReadAllBytes(path))) return false;
                pixels = tmp.GetPixels32();
                width = tmp.width;
                height = tmp.height;
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tmp);
            }
        }

        static bool TryDecode(Texture2D tex, out Color32[] pixels, out int width, out int height)
        {
            pixels = null;
            width = height = 0;
            if (tex == null) return false;

            if (tex.isReadable)
            {
                try
                {
                    pixels = tex.GetPixels32();
                    width = tex.width;
                    height = tex.height;
                    if (pixels != null && pixels.Length == width * height) return true;
                }
                catch
                {
                    // fall through to file load
                }
            }

            string path = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;

            var tmp = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!tmp.LoadImage(File.ReadAllBytes(path))) return false;
                pixels = tmp.GetPixels32();
                width = tmp.width;
                height = tmp.height;
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tmp);
            }
        }

        /// <summary>Box-filter resize (ใช้ได้ทั้งย่อและขยาย) + แปลงเป็น grayscale ได้</summary>
        static Color32[] Resize(Color32[] src, int sw, int sh, int dw, int dh, bool grayscale)
        {
            var dst = new Color32[dw * dh];
            float sx = sw / (float)dw;
            float sy = sh / (float)dh;

            for (int y = 0; y < dh; y++)
            {
                int y0 = Mathf.FloorToInt(y * sy);
                int y1 = Mathf.Max(y0 + 1, Mathf.FloorToInt((y + 1) * sy));
                y1 = Mathf.Min(sh, y1);

                for (int x = 0; x < dw; x++)
                {
                    int x0 = Mathf.FloorToInt(x * sx);
                    int x1 = Mathf.Max(x0 + 1, Mathf.FloorToInt((x + 1) * sx));
                    x1 = Mathf.Min(sw, x1);

                    float r = 0, g = 0, b = 0, a = 0;
                    int count = 0;
                    for (int yy = y0; yy < y1; yy++)
                    {
                        int row = yy * sw;
                        for (int xx = x0; xx < x1; xx++)
                        {
                            var c = src[row + xx];
                            r += c.r; g += c.g; b += c.b; a += c.a;
                            count++;
                        }
                    }

                    if (count == 0) count = 1;
                    float rr = r / count, gg = g / count, bb = b / count, aa = a / count;

                    if (grayscale)
                    {
                        float l = rr * 0.299f + gg * 0.587f + bb * 0.114f;
                        rr = gg = bb = l;
                    }

                    dst[y * dw + x] = new Color32((byte)rr, (byte)gg, (byte)bb, (byte)aa);
                }
            }
            return dst;
        }

        /// <summary>เติมสีให้พิกเซลโปร่งใสโดยดึงจากเพื่อนบ้าน (กันขอบดำตอน bilinear)</summary>
        static void DilateRGB(Color32[] px, int w, int h, int iterations)
        {
            var copy = new Color32[px.Length];
            for (int it = 0; it < iterations; it++)
            {
                Array.Copy(px, copy, px.Length);
                bool changed = false;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        if (copy[i].a > 0) continue;

                        int r = 0, g = 0, b = 0, n = 0;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int ny = y + dy;
                            if (ny < 0 || ny >= h) continue;
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx;
                                if (nx < 0 || nx >= w) continue;
                                var c = copy[ny * w + nx];
                                if (c.a == 0) continue;
                                r += c.r; g += c.g; b += c.b; n++;
                            }
                        }
                        if (n == 0) continue;
                        px[i] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 0);
                        changed = true;
                    }
                }
                if (!changed) break;
            }
        }

        /// <summary>คัดลอกลง atlas (ต้องวางในกรอบ content เท่านั้น)</summary>
        static void Blit(Color32[] dst, int dstSize, int ox, int oy, Color32[] src, int w, int h, bool blend)
        {
            for (int y = 0; y < h; y++)
            {
                int dy = oy + y;
                if (dy < 0 || dy >= dstSize) continue;
                for (int x = 0; x < w; x++)
                {
                    int dx = ox + x;
                    if (dx < 0 || dx >= dstSize) continue;
                    dst[dy * dstSize + dx] = src[y * w + x];
                }
            }
        }

        /// <summary>ขยายพิกเซลขอบออกไปใน padding ของช่อง (edge extend)</summary>
        static void EdgeExtend(Color32[] px, int size, int ox, int oy, int cell, int pad, bool keepAlpha)
        {
            if (pad <= 0) return;
            int contentMinX = ox + pad;
            int contentMinY = oy + pad;
            int contentMaxX = ox + cell - pad - 1;
            int contentMaxY = oy + cell - pad - 1;

            for (int y = oy; y < oy + cell; y++)
            {
                for (int x = ox; x < ox + cell; x++)
                {
                    int cx = Mathf.Clamp(x, contentMinX, contentMaxX);
                    int cy = Mathf.Clamp(y, contentMinY, contentMaxY);
                    if (cx == x && cy == y) continue;

                    var src = px[cy * size + cx];
                    var dst = px[y * size + x];
                    dst.r = src.r; dst.g = src.g; dst.b = src.b;
                    if (!keepAlpha) dst.a = src.a;
                    px[y * size + x] = dst;
                }
            }
        }

        // ------------------------------------------------------------------
        // asset helpers
        // ------------------------------------------------------------------
        static void WritePng(string assetPath, Color32[] pixels, int size, bool srgb, bool alpha)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            try
            {
                tex.SetPixels32(pixels);
                tex.Apply(false, false);
                byte[] png = tex.EncodeToPNG();
                string full = Path.GetFullPath(assetPath);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllBytes(full, png);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = srgb;
                importer.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                importer.alphaIsTransparency = false;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 8192;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }
        }

        static string Sanitize(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Replace(' ', '_');
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}

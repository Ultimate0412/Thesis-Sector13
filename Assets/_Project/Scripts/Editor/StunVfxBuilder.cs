using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// สร้างเอฟเฟกต์ "ดาวสตั๊น" ของ Hammer Head (ดาว 5 แฉกวนรอบหัว + หางตามดาว)
/// และแปลงเอฟเฟกต์ตอนชนให้ใช้ระบบ Object Pool (เปิด/ปิด แทนการสร้าง/ทำลาย)
///
/// ไฟล์ที่สร้าง/แก้:
///  - Texture : T_StunStar.png (copy จาก ShaderVfx/Star.png), T_StarGlow.png (สร้างใหม่)
///  - Material: M_StunStar.mat, M_StunStarTrail.mat (URP Particles/Unlit + Additive)
///  - Prefab  : VFX_StunStars.prefab (Stars + Twinkle)
///  - Patch   : VFX_HammerImpact.prefab (stopAction = Callback + PooledParticleVfx)
///  - Patch   : Enemy_Hammer.prefab (เพิ่มลูก VFX_StunStars + ผูก stunVfxObject)
///
/// ใช้งาน: เมนู Tools ▸ Sector13 ▸ Build Stun Stars VFX
/// หรือปล่อยให้รันอัตโนมัติครั้งเดียวหลัง compile (เก็บสถานะใน EditorPrefs)
///
/// หมายเหตุ: builder นี้ไม่แตะ VFX_HammerTrail / M_Smoke_Dust ที่ผู้ใช้จูนไว้
/// </summary>
public static class StunVfxBuilder
{
    private const int BuilderVersion = 2;

    private const string ProjectRoot = "Assets/_Project";
    private const string TextureFolder = ProjectRoot + "/Art/Textures/VFX";
    private const string MaterialFolder = ProjectRoot + "/Art/Materials/VFX";
    private const string VfxFolder = ProjectRoot + "/VFX/Enemy";

    private const string SourceStarPath = "Assets/ShaderVfx/Star.png";
    private const string StarTexturePath = TextureFolder + "/T_StunStar.png";
    private const string GlowTexturePath = TextureFolder + "/T_StarGlow.png";
    private const string StarMaterialPath = MaterialFolder + "/M_StunStar.mat";
    private const string TrailMaterialPath = MaterialFolder + "/M_StunStarTrail.mat";
    private const string StunPrefabPath = VfxFolder + "/VFX_StunStars.prefab";
    private const string ImpactPrefabPath = VfxFolder + "/VFX_HammerImpact.prefab";
    private const string EnemyPrefabPath = ProjectRoot + "/Prefabs/Characters/Enemy_Hammer.prefab";

    // ความสูงเหนือหัว (capsule สูง 2 ม. + เงยขึ้นมานิด)
    private const float StunVfxHeight = 2.05f;

    private static readonly Color StarTint = new Color(1f, 0.92f, 0.55f, 1f);
    private static readonly Color TwinkleTint = new Color(1f, 0.97f, 0.82f, 1f);

    private static string LogPath =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/StunVfxBuilder.log"));

    private static string PrefKey
    {
        get
        {
            string safe = Application.dataPath
                .Replace("\\", "_")
                .Replace("/", "_")
                .Replace(":", "_")
                .Replace(" ", "_");
            return "Sector13.StunVfx." + safe;
        }
    }

    // ------------------------------------------------------------ Auto build once

    [InitializeOnLoadMethod]
    private static void AutoBuildOnce()
    {
        if (EditorPrefs.GetInt(PrefKey, 0) >= BuilderVersion) return;
        EditorApplication.update += TryAutoBuild;
    }

    private static void TryAutoBuild()
    {
        if (EditorApplication.isCompiling ||
            EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        EditorApplication.update -= TryAutoBuild;

        try
        {
            BuildAll(true);
        }
        catch (Exception e)
        {
            Log("AUTO BUILD FAILED: " + e);
            Debug.LogError("[StunVfxBuilder] สร้างเอฟเฟกต์ดาวสตั๊นอัตโนมัติไม่สำเร็จ: " + e.Message);
        }
    }

    // ------------------------------------------------------------ Entry points

    [MenuItem("Tools/Sector13/Build Stun Stars VFX")]
    public static void BuildAllMenu()
    {
        BuildAll(false);
    }

    private static void BuildAll(bool auto)
    {
        try
        {
            if (File.Exists(LogPath)) File.Delete(LogPath);
        }
        catch
        {
            // ไม่เป็นไรถ้าลบ log เก่าไม่ได้
        }

        Log("=== Build Stun Stars VFX start (" + (auto ? "auto" : "menu") + ") ===");

        EnsureFolder(TextureFolder);
        EnsureFolder(MaterialFolder);
        EnsureFolder(VfxFolder);

        ImportStarTexture();
        EnsureGlowTexture();

        Texture2D starTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(StarTexturePath);
        Texture2D glowTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(GlowTexturePath);

        Material starMaterial = CreateAdditiveMaterial(StarMaterialPath, starTexture, "M_StunStar");
        Material trailMaterial = CreateAdditiveMaterial(TrailMaterialPath, glowTexture, "M_StunStarTrail");

        GameObject stunPrefab = CreateStunPrefab(starMaterial, trailMaterial);

        PatchImpactPrefab();
        PatchEnemyPrefab(stunPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorPrefs.SetInt(PrefKey, BuilderVersion);

        Log("=== Build Stun Stars VFX done ===");
        Debug.Log("[StunVfxBuilder] สร้างเอฟเฟกต์ดาวสตั๊น + แปลง impact เป็น pooled เรียบร้อยแล้ว");
    }

    // ------------------------------------------------------------ Assets

    private static void EnsureFolder(string assetFolder)
    {
        if (AssetDatabase.IsValidFolder(assetFolder)) return;

        string parent = Path.GetDirectoryName(assetFolder);
        if (string.IsNullOrEmpty(parent)) return;

        parent = parent.Replace('\\', '/');
        string name = Path.GetFileName(assetFolder);

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static void ImportStarTexture()
    {
        string source = Path.GetFullPath(SourceStarPath);
        string target = Path.GetFullPath(StarTexturePath);

        if (!File.Exists(source))
        {
            Log("WARN: ไม่พบ texture ต้นทาง: " + SourceStarPath);
            return;
        }

        if (!File.Exists(target))
        {
            File.Copy(source, target);
            Log("Copy texture -> " + StarTexturePath);
        }

        AssetDatabase.ImportAsset(StarTexturePath, ImportAssetOptions.ForceSynchronousImport);

        TextureImporter importer = AssetImporter.GetAtPath(StarTexturePath) as TextureImporter;
        if (importer == null)
        {
            Log("WARN: อ่าน TextureImporter ไม่ได้: " + StarTexturePath);
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 256;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        Log("ตั้งค่า texture ดาวเรียบร้อย (Default / no mip / max 256 / Uncompressed)");
    }

    private static void EnsureGlowTexture()
    {
        string target = Path.GetFullPath(GlowTexturePath);

        // สร้างใหม่ทุกครั้ง (asset ที่ generate จากโค้ด — ปรับหน้าตาได้ที่ material แทน)
        {
            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float ny = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);

                    float core = Mathf.Clamp01(1f - r);
                    float glow = Mathf.Pow(core, 2.4f);

                    // แฉกนุ่ม 4 แนว (แนวนอน/แนวตั้ง) ให้ดูเป็นประกาย ไม่ใช่แค่จุดเบลอ
                    float horizontal = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(ny) * 10f), 2f) *
                                       Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(nx)), 1.6f);
                    float vertical = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(nx) * 10f), 2f) *
                                     Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(ny)), 1.6f);

                    float alpha = Mathf.Clamp01(glow + (horizontal + vertical) * 0.45f);
                    // ขอบนอกค่อยๆ จาง (InverseLerp 0.82..1 -> smoothstep 0..1)
                    float edge = Mathf.InverseLerp(0.82f, 1f, r);
                    alpha *= 1f - (edge * edge * (3f - 2f * edge));

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);

            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllBytes(target, png);
            AssetDatabase.ImportAsset(GlowTexturePath, ImportAssetOptions.ForceSynchronousImport);
            Log("สร้าง texture glow -> " + GlowTexturePath);
        }

        TextureImporter importer = AssetImporter.GetAtPath(GlowTexturePath) as TextureImporter;
        if (importer == null)
        {
            Log("WARN: อ่าน TextureImporter ไม่ได้: " + GlowTexturePath);
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 256;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static Material CreateAdditiveMaterial(string path, Texture texture, string label)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            throw new Exception("ไม่พบ shader Universal Render Pipeline/Particles/Unlit");
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
            Log("สร้าง material: " + label + " -> " + path);
        }
        else
        {
            mat.shader = shader;
            Log("อัปเดต material: " + label + " -> " + path);
        }

        mat.SetTexture("_BaseMap", texture);
        mat.SetColor("_BaseColor", Color.white);

        // โปร่งใสแบบ Additive (สีเข้มจะหายไป เหลือแต่ส่วนสว่างเรืองแสง)
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_BlendOp", 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.One);
        mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        mat.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_Cull", (float)CullMode.Off);
        mat.SetFloat("_ColorMode", 0f); // Multiply (สีขาว x texture)
        mat.SetFloat("_AlphaClip", 0f);
        mat.SetFloat("_SoftParticlesEnabled", 0f);
        mat.SetFloat("_CameraFadingEnabled", 0f);

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.DisableKeyword("_ALPHAMODULATE_ON");
        mat.DisableKeyword("_SOFTPARTICLES_ON");
        mat.DisableKeyword("_FADING_ON");

        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)RenderQueue.Transparent;

        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ------------------------------------------------------------ Stun prefab

    private static GameObject CreateStunPrefab(Material starMaterial, Material trailMaterial)
    {
        GameObject root = new GameObject("VFX_StunStars");
        try
        {
            CreateStars(root.transform, starMaterial, trailMaterial);
            CreateTwinkle(root.transform, trailMaterial);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, StunPrefabPath);
            Log("สร้าง prefab ดาวสตั๊น: " + StunPrefabPath);
            return prefab;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void CreateStars(Transform parent, Material starMaterial, Material trailMaterial)
    {
        GameObject go = new GameObject("Stars");
        go.transform.SetParent(parent, false);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = 5f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.22f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(StarTint);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 24;
        main.playOnAwake = true;
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 1.2f; // ~6 ดวงในวง (lifetime 5 วิ)

        // วงแหวนแนวนอนรอบหัว
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.42f;
        shape.radiusThickness = 1f;
        shape.rotation = new Vector3(90f, 0f, 0f);

        // วนรอบแกน Y (~2.2 rad/s = ~126 องศา/วิ -> ~2.8 วิ/รอบ)
        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.orbitalY = 126f;
        velocity.orbitalX = 0f;
        velocity.orbitalZ = 0f;
        velocity.x = 0f;
        velocity.y = 0f;
        velocity.z = 0f;

        // ดาวหมุนติ้วๆ (~1.5 rad/s = ~86 องศา/วิ)
        ParticleSystem.RotationOverLifetimeModule rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = 90f;

        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.03f;
        noise.frequency = 0.5f;
        noise.scrollSpeed = 0.2f;
        noise.damping = true;
        noise.octaveCount = 2;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(MakeStarFadeGradient());

        // ---- หางตามดาว (PerParticle trail) ----
        ParticleSystem.TrailModule trails = ps.trails;
        trails.enabled = true;
        trails.mode = ParticleSystemTrailMode.PerParticle;
        trails.ratio = 1f;
        trails.lifetime = 0.28f;
        trails.minVertexDistance = 0.05f;
        trails.textureMode = ParticleSystemTrailTextureMode.Stretch;
        trails.dieWithParticles = true;
        trails.sizeAffectsLifetime = false;
        trails.sizeAffectsWidth = true;
        trails.inheritParticleColor = true;
        trails.worldSpace = false;
        trails.colorOverLifetime = new ParticleSystem.MinMaxGradient(MakeTrailGradient());
        // ความกว้างหาง = ขนาดดาว x 0.55 -> ไล่จาก ~0.11 เหลือ 0 ที่ปลายหาง
        trails.widthOverTrail = new ParticleSystem.MinMaxCurve(0.55f, new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 0f)));

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sharedMaterial = starMaterial;
        renderer.trailMaterial = trailMaterial;
        renderer.sortingFudge = 0.02f;
        renderer.maxParticleSize = 1f;
        renderer.minParticleSize = 0f;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        Log("  + Stars (orbit 126 deg/s, rate 1.2, size 0.16-0.22, trail 0.28s)");
    }

    private static void CreateTwinkle(Transform parent, Material glowMaterial)
    {
        GameObject go = new GameObject("Twinkle");
        go.transform.SetParent(parent, false);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
        main.startColor = new ParticleSystem.MinMaxGradient(TwinkleTint);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 30;
        main.playOnAwake = true;
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 4f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.45f;
        shape.radiusThickness = 1f;

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(MakeTwinkleGradient());

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.2f),
            new Keyframe(0.35f, 1f),
            new Keyframe(1f, 0f)));

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sharedMaterial = glowMaterial;
        renderer.sortingFudge = 0.03f;
        renderer.maxParticleSize = 0.5f;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        Log("  + Twinkle (rate 4, size 0.04-0.09)");
    }

    // ------------------------------------------------------------ Impact prefab (pooling)

    private static void PatchImpactPrefab()
    {
        if (!File.Exists(Path.GetFullPath(ImpactPrefabPath)))
        {
            Log("WARN: ไม่พบ prefab เอฟเฟกต์ตอนชน: " + ImpactPrefabPath);
            return;
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(ImpactPrefabPath);
        try
        {
            ParticleSystem rootPs = contents.GetComponent<ParticleSystem>();
            if (rootPs == null)
            {
                Log("WARN: prefab impact ไม่มี ParticleSystem ที่ root — ข้ามการ patch");
                return;
            }

            ParticleSystem.MainModule main = rootPs.main;
            main.stopAction = ParticleSystemStopAction.Callback;

            if (contents.GetComponent<PooledParticleVfx>() == null)
            {
                contents.AddComponent<PooledParticleVfx>();
            }

            PrefabUtility.SaveAsPrefabAsset(contents, ImpactPrefabPath);
            Log("patch impact prefab: stopAction = Callback + PooledParticleVfx");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    // ------------------------------------------------------------ Enemy prefab wiring

    private static void PatchEnemyPrefab(GameObject stunPrefab)
    {
        if (stunPrefab == null)
        {
            Log("WARN: ไม่มี prefab ดาวสตั๊น — ข้ามการผูกเข้า Enemy_Hammer");
            return;
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
        try
        {
            Transform existing = contents.transform.Find("VFX_StunStars");
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(stunPrefab, contents.transform);
            instance.name = "VFX_StunStars";
            instance.transform.localPosition = new Vector3(0f, StunVfxHeight, 0f);
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            // เริ่มแบบปิดไว้ -> EnterStunState() จะ SetActive(true) เอง (ไม่มี alloc)
            instance.SetActive(false);

            EnemyBase enemy = contents.GetComponent<EnemyBase>();
            if (enemy == null) enemy = contents.GetComponentInChildren<EnemyBase>(true);
            if (enemy == null)
            {
                throw new Exception("ไม่พบ component EnemyBase ใน " + EnemyPrefabPath);
            }

            enemy.stunVfxObject = instance;
            EditorUtility.SetDirty(enemy);

            PrefabUtility.SaveAsPrefabAsset(contents, EnemyPrefabPath);

            Log("ผูก VFX_StunStars เข้า " + EnemyPrefabPath +
                " (y=" + StunVfxHeight + ", inactive, stunVfxObject=" + (enemy.stunVfxObject != null) + ")");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    // ------------------------------------------------------------ Gradients

    private static Gradient MakeStarFadeGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.08f),
                new GradientAlphaKey(1f, 0.92f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    private static Gradient MakeTrailGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.6f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    private static Gradient MakeTwinkleGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(1f, 0.9f, 0.6f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.3f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    // ------------------------------------------------------------ Logging

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogPath,
                "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine);
        }
        catch
        {
            // การเขียน log ล้มเหลวไม่ควรทำให้ build ล้ม
        }

        Debug.Log("[StunVfxBuilder] " + message);
    }
}

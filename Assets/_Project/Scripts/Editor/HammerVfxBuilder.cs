using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// สร้าง asset เอฟเฟกต์ฝุ่น/ควันของ Hammer Head แบบอัตโนมัติ
/// - Material ควัน (URP Particles/Unlit + Soft Particles)
/// - Prefab หางฝุ่น (Dust) + ควันจากหัว (HeadSmoke)
/// - Prefab เอฟเฟกต์ตอนชน (ฝุ่น + ประกายไฟ)
/// - ผูก reference เข้า Enemy_Hammer.prefab ให้เลย
///
/// ใช้งาน: เมนู Tools ▸ Sector13 ▸ Build Hammer VFX
/// หรือปล่อยให้รันอัตโนมัติครั้งเดียวหลัง compile (เก็บสถานะใน EditorPrefs)
/// </summary>
public static class HammerVfxBuilder
{
    private const int BuilderVersion = 3;

    private const string ProjectRoot = "Assets/_Project";
    private const string TextureFolder = ProjectRoot + "/Art/Textures/VFX";
    private const string MaterialFolder = ProjectRoot + "/Art/Materials/VFX";
    private const string VfxFolder = ProjectRoot + "/VFX/Enemy";

    private const string SmokeTexturePath = TextureFolder + "/T_SmokePuff.png";
    private const string SmokeMaterialPath = MaterialFolder + "/M_Smoke_Dust.mat";
    private const string TrailPrefabPath = VfxFolder + "/VFX_HammerTrail.prefab";
    private const string ImpactPrefabPath = VfxFolder + "/VFX_HammerImpact.prefab";
    private const string EnemyPrefabPath = ProjectRoot + "/Prefabs/Characters/Enemy_Hammer.prefab";

    private const string SourceTexturePath =
        "Assets/GabrielAguiarProductions/FreeQuickEffectsVol1/Textures/DistortedFlare01.png";
    private const string SparkMaterialPath = "Assets/ShaderVfx/Spark.mat";

    private static readonly Color DustTint = new Color(0.80f, 0.77f, 0.71f, 1f);
    private static readonly Color HeadSmokeTint = new Color(0.62f, 0.60f, 0.56f, 1f);
    private static readonly Color SparkTint = new Color(1f, 0.90f, 0.60f, 1f);

    private static string LogPath =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/HammerVfxBuilder.log"));

    private static string PrefKey
    {
        get
        {
            string safe = Application.dataPath
                .Replace("\\", "_")
                .Replace("/", "_")
                .Replace(":", "_")
                .Replace(" ", "_");
            return "Sector13.HammerVfx." + safe;
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
            Debug.LogError("[HammerVfxBuilder] สร้างเอฟเฟกต์อัตโนมัติไม่สำเร็จ: " + e.Message);
        }
    }

    // ------------------------------------------------------------ Entry points

    [MenuItem("Tools/Sector13/Build Hammer VFX")]
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

        Log("=== Build Hammer VFX start (" + (auto ? "auto" : "menu") + ") ===");

        EnsureFolder(TextureFolder);
        EnsureFolder(MaterialFolder);
        EnsureFolder(VfxFolder);

        CopySmokeTexture();
        Material smokeMaterial = CreateSmokeMaterial();
        GameObject trailPrefab = CreateTrailPrefab(smokeMaterial);
        GameObject impactPrefab = CreateImpactPrefab(smokeMaterial);

        PatchEnemyPrefab(trailPrefab, impactPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorPrefs.SetInt(PrefKey, BuilderVersion);

        Log("=== Build Hammer VFX done ===");
        Debug.Log("[HammerVfxBuilder] สร้างเอฟเฟกต์ฝุ่น/ควันของ Hammer Head เรียบร้อยแล้ว");
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

    private static void CopySmokeTexture()
    {
        string source = Path.GetFullPath(SourceTexturePath);
        string target = Path.GetFullPath(SmokeTexturePath);

        if (!File.Exists(source))
        {
            Log("WARN: ไม่พบ texture ต้นทาง: " + SourceTexturePath);
            return;
        }

        if (!File.Exists(target))
        {
            File.Copy(source, target);
            AssetDatabase.ImportAsset(SmokeTexturePath, ImportAssetOptions.ForceSynchronousImport);
            Log("Copy texture -> " + SmokeTexturePath);
        }

        TextureImporter importer = AssetImporter.GetAtPath(SmokeTexturePath) as TextureImporter;
        if (importer == null)
        {
            Log("WARN: อ่าน TextureImporter ไม่ได้: " + SmokeTexturePath);
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 256;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();

        Log("ตั้งค่า texture import เรียบร้อย (max 256)");
    }

    private static Material CreateSmokeMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            throw new Exception("ไม่พบ shader Universal Render Pipeline/Particles/Unlit");
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(SmokeMaterialPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, SmokeMaterialPath);
            Log("สร้าง material: " + SmokeMaterialPath);
        }
        else
        {
            mat.shader = shader;
            Log("อัปเดต material: " + SmokeMaterialPath);
        }

        Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(SmokeTexturePath);

        mat.SetTexture("_BaseMap", texture);
        mat.SetColor("_BaseColor", Color.white);

        // โปร่งใสแบบ Alpha Blend
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_BlendOp", 0f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_Cull", (float)CullMode.Off);
        mat.SetFloat("_ColorMode", 0f);
        mat.SetFloat("_AlphaClip", 0f);

        // Soft Particles (ต้องมี Depth Texture ใน URP Asset)
        mat.SetFloat("_SoftParticlesEnabled", 1f);
        mat.SetFloat("_SoftParticlesNearFadeDistance", 0f);
        mat.SetFloat("_SoftParticlesFarFadeDistance", 1f);
        mat.SetVector("_SoftParticleFadeParams", new Vector4(0f, 1f, 0f, 0f));
        mat.SetFloat("_CameraFadingEnabled", 0f);
        mat.SetVector("_CameraFadeParams", new Vector4(0f, float.PositiveInfinity, 0f, 0f));

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_SOFTPARTICLES_ON");
        mat.EnableKeyword("_FADING_ON");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.DisableKeyword("_ALPHAMODULATE_ON");

        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)RenderQueue.Transparent;

        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ------------------------------------------------------------ Trail prefab

    private static GameObject CreateTrailPrefab(Material smokeMaterial)
    {
        // ไม่ลบ asset เดิม เพื่อให้ GUID คงที่ (reference จาก prefab อื่นไม่ขาด)
        GameObject root = new GameObject("VFX_HammerTrail");
        try
        {
            CreateDust(root.transform, smokeMaterial);
            CreateHeadSmoke(root.transform, smokeMaterial);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, TrailPrefabPath);
            Log("สร้าง prefab หางฝุ่น: " + TrailPrefabPath);
            return prefab;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void CreateDust(Transform parent, Material material)
    {
        GameObject go = new GameObject("Dust");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.15f, -0.4f);
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.prewarm = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(DustTint);
        main.gravityModifier = -0.01f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;
        main.playOnAwake = true;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 2.5f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 20f;
        shape.radius = 0.35f;
        shape.radiusThickness = 1f;
        shape.rotation = Vector3.zero;

        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.25f),
            new Keyframe(0.2f, 0.9f),
            new Keyframe(1f, 1.8f)));

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(MakeFadeGradient(0.75f));

        ParticleSystem.RotationOverLifetimeModule rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);

        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.25f;
        noise.frequency = 0.4f;
        noise.scrollSpeed = 0.2f;
        noise.damping = true;
        noise.octaveCount = 2;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 1.8f;
        renderer.velocityScale = 0.08f;
        renderer.cameraVelocityScale = 0f;
        renderer.maxParticleSize = 2f;
        renderer.sortingFudge = 0f;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        Log("  + Dust (rate/m = 2.5, life 0.8-1.1, size 0.35-0.6)");
    }

    private static void CreateHeadSmoke(Transform parent, Material material)
    {
        GameObject go = new GameObject("HeadSmoke");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 1.75f, -0.5f);
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.prewarm = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 2.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(HeadSmokeTint);
        main.gravityModifier = -0.02f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;
        main.playOnAwake = true;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 0.9f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;
        shape.radiusThickness = 1f;

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.4f),
            new Keyframe(0.3f, 0.9f),
            new Keyframe(1f, 2.2f)));

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(MakeFadeGradient(0.5f));

        ParticleSystem.RotationOverLifetimeModule rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);

        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.15f;
        noise.frequency = 0.3f;
        noise.scrollSpeed = 0.15f;
        noise.damping = true;
        noise.octaveCount = 2;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.maxParticleSize = 2f;
        renderer.sortingFudge = 0.01f;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        Log("  + HeadSmoke (rate/m = 0.9, life 1.8-2.4, size 0.5-0.8)");
    }

    // ------------------------------------------------------------ Impact prefab

    private static GameObject CreateImpactPrefab(Material smokeMaterial)
    {
        // ไม่ลบ asset เดิม เพื่อให้ GUID คงที่
        GameObject root = new GameObject("VFX_HammerImpact");
        try
        {
            // วาง ParticleSystem ไว้ที่ root โดยตรง เพื่อให้ stopAction = Destroy
            // ลบ GameObject ทั้งก้อน (รวม Sparks ลูก) เมื่อเอฟเฟกต์จบ
            CreateImpactDust(root, smokeMaterial);
            CreateImpactSparks(root.transform);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ImpactPrefabPath);
            Log("สร้าง prefab เอฟเฟกต์ตอนชน: " + ImpactPrefabPath);
            return prefab;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void CreateImpactDust(GameObject host, Material material)
    {
        ParticleSystem ps = host.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.prewarm = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 4.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(DustTint);
        main.gravityModifier = 0.15f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;
        main.playOnAwake = true;
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 12, 18)
        });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;
        shape.radiusThickness = 1f;

        ParticleSystem.LimitVelocityOverLifetimeModule limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.space = ParticleSystemSimulationSpace.World;
        limit.limit = 1.5f;
        limit.dampen = 0.4f;

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.5f),
            new Keyframe(0.25f, 1f),
            new Keyframe(1f, 2.2f)));

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(MakeFadeGradient(0.9f));

        ParticleSystem.RotationOverLifetimeModule rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-1f, 1f);

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 1.2f;
        renderer.velocityScale = 0.05f;
        renderer.maxParticleSize = 2f;
        renderer.sortingFudge = 0f;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        Log("  + ImpactDust (burst 12-18, auto destroy)");
    }

    private static void CreateImpactSparks(Transform parent)
    {
        GameObject go = new GameObject("Sparks");
        go.transform.SetParent(parent, false);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.prewarm = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 6.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
        main.startColor = new ParticleSystem.MinMaxGradient(SparkTint);
        main.gravityModifier = 1.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 20;
        main.playOnAwake = true;
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 8, 12)
        });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 45f;
        shape.radius = 0.1f;
        shape.radiusThickness = 1f;

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(1f, 0.55f, 0.15f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = new ParticleSystem.MinMaxGradient(gradient);

        Material sparkMaterial = AssetDatabase.LoadAssetAtPath<Material>(SparkMaterialPath);

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 2f;
        renderer.velocityScale = 0.02f;
        renderer.maxParticleSize = 1f;
        renderer.sortingFudge = -0.01f;
        if (sparkMaterial != null) renderer.sharedMaterial = sparkMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        Log("  + Sparks (burst 8-12)");
    }

    // ------------------------------------------------------------ Enemy prefab wiring

    private static void PatchEnemyPrefab(GameObject trailPrefab, GameObject impactPrefab)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
        try
        {
            Transform existing = contents.transform.Find("VFX_HammerTrail");
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(trailPrefab, contents.transform);
            instance.name = "VFX_HammerTrail";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            EnemyHammer hammer = contents.GetComponent<EnemyHammer>();
            if (hammer == null) hammer = contents.GetComponentInChildren<EnemyHammer>(true);
            if (hammer == null)
            {
                throw new Exception("ไม่พบ component EnemyHammer ใน " + EnemyPrefabPath);
            }

            Transform dust = instance.transform.Find("Dust");
            Transform head = instance.transform.Find("HeadSmoke");

            hammer.dustTrail = dust != null ? dust.GetComponent<ParticleSystem>() : null;
            hammer.headSmoke = head != null ? head.GetComponent<ParticleSystem>() : null;
            hammer.impactVfxPrefab = impactPrefab;
            EditorUtility.SetDirty(hammer);

            PrefabUtility.SaveAsPrefabAsset(contents, EnemyPrefabPath);

            Log("ผูก reference เข้า " + EnemyPrefabPath +
                " (dust=" + (hammer.dustTrail != null) +
                ", head=" + (hammer.headSmoke != null) +
                ", impact=" + (hammer.impactVfxPrefab != null) + ")");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    // ------------------------------------------------------------ Helpers

    private static Gradient MakeFadeGradient(float maxAlpha)
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
                new GradientAlphaKey(maxAlpha, 0.12f),
                new GradientAlphaKey(maxAlpha * 0.8f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

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

        Debug.Log("[HammerVfxBuilder] " + message);
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class PackageTraceApplier : MonoBehaviour
{
    [Header("Inspector-Configurable Spawn Settings")]
    [Tooltip("การตั้งค่าสุ่มร่องรอยสำหรับพัสดุถูกกฎหมาย (Legal)")]
    public CategoryTraceSettings legalSettings = new CategoryTraceSettings(85f, 1, 2, 0f);

    [Tooltip("การตั้งค่าสุ่มร่องรอยสำหรับพัสดุของเถื่อน (Illegal)")]
    public CategoryTraceSettings illegalSettings = new CategoryTraceSettings(90f, 1, 3, 35f);

    [Tooltip("การตั้งค่าสุ่มร่องรอยสำหรับพัสดุเอเลี่ยน (Alien)")]
    public CategoryTraceSettings alienSettings = new CategoryTraceSettings(100f, 2, 4, 70f);

    [Header("Decal Materials (Auto-assigned or Custom)")]
    public Material clawMarksMat;
    public Material oilStainMat;
    public Material waterStainMat;
    public Material cardboardScuffMat;
    public Material alienSlimeUVMat;
    public Material chemicalResidueUVMat;

    [Header("Projection Dimensions")]
    [Tooltip("ช่วงขนาดสุ่มของร่องรอย (Width x Height)")]
    public Vector2 minTraceSize = new Vector2(0.38f, 0.38f);
    public Vector2 maxTraceSize = new Vector2(0.60f, 0.60f);
    [Tooltip("ความลึกในการฉาย Decal เข้าไปในเนื้อกล่อง")]
    public float projectionDepth = 0.90f;

    [Header("Runtime State")]
    public ItemCategory currentCategory = ItemCategory.Legal;
    public bool isUVIlluminated = false;

    private readonly List<DecalProjector> nakedEyeProjectors = new List<DecalProjector>();
    private readonly List<DecalProjector> uvProjectors = new List<DecalProjector>();
    private Transform tracesRoot;
    private BoxCollider boxCollider;

    private void Awake()
    {
        if (boxCollider == null) boxCollider = GetComponent<BoxCollider>();
        EnsureMaterialsLoaded();
    }

    private void Start()
    {
        // หากยังไม่มีร่องรอยสร้างไว้บนกล่อง ให้สร้างอัตโนมัติเมื่อเริ่มเกม
        if (tracesRoot == null || tracesRoot.childCount == 0)
        {
            PackageBox box = GetComponent<PackageBox>();
            if (box != null)
            {
                ItemObject itemObj = box.innerItemPrefab != null ? box.innerItemPrefab.GetComponent<ItemObject>() : null;
                ItemCategory cat = itemObj != null ? itemObj.category : currentCategory;
                ApplyTraces(cat, itemObj);
            }
        }
    }

    private void Update()
    {
        // อัปเดตความโปร่งใสของรอย UV อย่างนุ่มนวลตามสถานะการส่องไฟ UV
        if (uvProjectors.Count > 0)
        {
            float targetFade = isUVIlluminated ? 1.0f : 0.0f;
            for (int i = 0; i < uvProjectors.Count; i++)
            {
                if (uvProjectors[i] != null)
                {
                    uvProjectors[i].fadeFactor = Mathf.MoveTowards(
                        uvProjectors[i].fadeFactor,
                        targetFade,
                        Time.deltaTime * 7f
                    );
                }
            }
        }
    }

    /// ตรวจสอบว่าร่องรอยใช้ไฟ UV ไหม
    public static bool IsUVTrace(TraceType type)
    {
        return type == TraceType.AlienSlime_UV || type == TraceType.ChemicalResidue_UV;
    }

    /// <summary>
    /// สุ่มสร้างร่องรอยบนกล่องตามหมวดหมู่สินค้า หรือกำหนดเฉพาะเจาะจงตาม allowedTraces ของ itemObject
    /// </summary>
    public void ApplyTraces(ItemCategory category, ItemObject itemObject = null)
    {
        currentCategory = category;
        ClearTraces();
        EnsureMaterialsLoaded();

        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider>();
            if (boxCollider == null) boxCollider = GetComponentInChildren<BoxCollider>();
        }

        CategoryTraceSettings settings = GetSettingsForCategory(category);
        if (settings == null) return;

        // ตรวจสอบโอกาสเกิดร่องรอย (Spawn Chance)
        float roll = Random.Range(0f, 100f);
        if (roll > settings.spawnChancePercent)
        {
            return; // กล่องใบนี้สะอาด ไม่มีร่องรอย
        }

        // สุ่มจำนวนร่องรอยตามช่วง Min - Max ใน Inspector
        int min = Mathf.Min(settings.minTraces, settings.maxTraces);
        int max = Mathf.Max(settings.minTraces, settings.maxTraces);
        int traceCount = Random.Range(min, max + 1);

        if (traceCount <= 0) return;

        if (tracesRoot == null)
        {
            Transform existing = transform.Find("SurfaceTracesRoot");
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }

            GameObject rootGo = new GameObject("SurfaceTracesRoot");
            rootGo.transform.SetParent(transform, false);
            tracesRoot = rootGo.transform;
        }

        Vector3 boxCenter = boxCollider != null ? boxCollider.center : Vector3.zero;
        Vector3 boxHalfSize = (boxCollider != null ? boxCollider.size : Vector3.one) * 0.5f;

        bool hasItemCustomTraces = itemObject != null && itemObject.allowedTraces != null && itemObject.allowedTraces.Length > 0;

        for (int i = 0; i < traceCount; i++)
        {
            bool preferUV = Random.Range(0f, 100f) < settings.uvTraceRatioPercent;
            TraceType traceType;

            if (hasItemCustomTraces)
            {
                traceType = PickTraceTypeFromItem(itemObject, preferUV);
            }
            else
            {
                traceType = PickTraceType(category, preferUV);
            }

            bool isUV = IsUVTrace(traceType);
            Material mat = GetMaterialForTraceType(traceType);

            if (mat == null) continue;

            // สุ่มตำแหน่งและทิศทางการฉาย Decal
            Vector3 localPos;
            Quaternion localRot;
            GetRandomTraceTransform(boxCenter, boxHalfSize, out localPos, out localRot);

            // สร้าง GameObject สำหรับ DecalProjector
            string traceName = isUV ? $"UV_Trace_{traceType}_{i + 1}" : $"Trace_{traceType}_{i + 1}";
            GameObject decalGo = new GameObject(traceName);
            decalGo.transform.SetParent(tracesRoot, false);
            decalGo.transform.localPosition = localPos;
            decalGo.transform.localRotation = localRot;

            DecalProjector projector = decalGo.AddComponent<DecalProjector>();
            projector.material = mat;

            float width = Random.Range(minTraceSize.x, maxTraceSize.x);
            float height = Random.Range(minTraceSize.y, maxTraceSize.y);
            projector.size = new Vector3(width, height, projectionDepth);

            if (isUV)
            {
                // รอย UV เริ่มต้นที่มองไม่เห็น (fadeFactor = 0)
                projector.fadeFactor = isUVIlluminated ? 1.0f : 0.0f;
                uvProjectors.Add(projector);
            }
            else
            {
                // รอยตาเปล่า มองเห็นทันที
                projector.fadeFactor = 1.0f;
                nakedEyeProjectors.Add(projector);
            }
        }
    }

    private TraceType PickTraceTypeFromItem(ItemObject itemObject, bool preferUV)
    {
        List<TraceType> preferred = new List<TraceType>();
        for (int i = 0; i < itemObject.allowedTraces.Length; i++)
        {
            TraceType t = itemObject.allowedTraces[i];
            if (IsUVTrace(t) == preferUV)
            {
                preferred.Add(t);
            }
        }

        if (preferred.Count > 0)
        {
            return preferred[Random.Range(0, preferred.Count)];
        }

        return itemObject.allowedTraces[Random.Range(0, itemObject.allowedTraces.Length)];
    }

    /// <summary>
    /// ล้างร่องรอยทั้งหมดบนกล่อง
    /// </summary>
    public void ClearTraces()
    {
        nakedEyeProjectors.Clear();
        uvProjectors.Clear();

        if (tracesRoot != null)
        {
            if (Application.isPlaying)
            {
                Destroy(tracesRoot.gameObject);
            }
            else
            {
                DestroyImmediate(tracesRoot.gameObject);
            }
            tracesRoot = null;
        }
    }

    /// <summary>
    /// แจ้งเตือนเมื่อไฟฉาย UV ส่องโดนกล่องใบนี้
    /// </summary>
    public void SetUVIlluminated(bool illuminated)
    {
        isUVIlluminated = illuminated;
    }

    private CategoryTraceSettings GetSettingsForCategory(ItemCategory category)
    {
        switch (category)
        {
            case ItemCategory.Legal: return legalSettings;
            case ItemCategory.Illegal: return illegalSettings;
            case ItemCategory.Alien: return alienSettings;
            default: return legalSettings;
        }
    }

    private TraceType PickTraceType(ItemCategory category, bool isUV)
    {
        if (isUV)
        {
            if (category == ItemCategory.Alien)
            {
                // Alien ชอบทิ้งคราบเมือกเรืองแสง (80%) หรือสารเคมี (20%)
                return Random.value < 0.8f ? TraceType.AlienSlime_UV : TraceType.ChemicalResidue_UV;
            }
            else
            {
                // Illegal มีคราบสารเคมีเรืองแสง
                return TraceType.ChemicalResidue_UV;
            }
        }
        else
        {
            if (category == ItemCategory.Alien)
            {
                // Alien รอยตาเปล่าเด่นสุดคือรอยกรงเล็บ (75%) หรือรอยถลอก/น้ำมัน (25%)
                float r = Random.value;
                if (r < 0.75f) return TraceType.ClawMarks;
                return r < 0.9f ? TraceType.CardboardScuff : TraceType.OilStain;
            }
            else if (category == ItemCategory.Illegal)
            {
                // Illegal รอยตาเปล่าคือคราบน้ำมัน คราบสารเคมี รอยถลอก
                float r = Random.value;
                if (r < 0.55f) return TraceType.OilStain;
                return r < 0.85f ? TraceType.CardboardScuff : TraceType.WaterStain;
            }
            else
            {
                // Legal รอยตาเปล่าคือคราบน้ำจางๆ หรือรอยถลอกขนส่งทั่วไป
                return Random.value < 0.6f ? TraceType.WaterStain : TraceType.CardboardScuff;
            }
        }
    }

    private Material GetMaterialForTraceType(TraceType type)
    {
        switch (type)
        {
            case TraceType.ClawMarks: return clawMarksMat;
            case TraceType.OilStain: return oilStainMat;
            case TraceType.WaterStain: return waterStainMat;
            case TraceType.CardboardScuff: return cardboardScuffMat;
            case TraceType.AlienSlime_UV: return alienSlimeUVMat;
            case TraceType.ChemicalResidue_UV: return chemicalResidueUVMat;
            default: return waterStainMat;
        }
    }

    private void GetRandomTraceTransform(Vector3 center, Vector3 half, out Vector3 localPos, out Quaternion localRot)
    {
        // 70% ฉายบนหน้ากล่องทั้ง 5 ด้าน, 30% ฉายตรงสันขอบกล่อง (Edge wrap-around)
        bool pickEdge = Random.value < 0.35f;

        float margin = 0.80f;
        float rx = Random.Range(-half.x * margin, half.x * margin);
        float ry = Random.Range(-half.y * margin, half.y * margin);
        float rz = Random.Range(-half.z * margin, half.z * margin);
        float spin = Random.Range(0f, 360f);

        Vector3 forwardDir;

        if (!pickEdge)
        {
            // สุ่ม 1 ใน 5 ด้านหลัก (Top, Front, Back, Left, Right)
            int face = Random.Range(0, 5);

            switch (face)
            {
                case 0: // Top (+Y)
                    localPos = center + new Vector3(rx, half.y, rz);
                    forwardDir = Vector3.down;
                    break;
                case 1: // Front (+Z)
                    localPos = center + new Vector3(rx, ry, half.z);
                    forwardDir = Vector3.back;
                    break;
                case 2: // Back (-Z)
                    localPos = center + new Vector3(rx, ry, -half.z);
                    forwardDir = Vector3.forward;
                    break;
                case 3: // Left (-X)
                    localPos = center + new Vector3(-half.x, ry, rz);
                    forwardDir = Vector3.right;
                    break;
                default: // Right (+X)
                    localPos = center + new Vector3(half.x, ry, rz);
                    forwardDir = Vector3.left;
                    break;
            }
        }
        else
        {
            // ฉายบริเวณสันขอบ มุม 45 องศา
            int edgeIndex = Random.Range(0, 4);

            switch (edgeIndex)
            {
                case 0: // Top-Front edge
                    localPos = center + new Vector3(rx, half.y, half.z);
                    forwardDir = (Vector3.down + Vector3.back).normalized;
                    break;
                case 1: // Top-Right edge
                    localPos = center + new Vector3(half.x, half.y, rz);
                    forwardDir = (Vector3.down + Vector3.left).normalized;
                    break;
                case 2: // Top-Left edge
                    localPos = center + new Vector3(-half.x, half.y, rz);
                    forwardDir = (Vector3.down + Vector3.right).normalized;
                    break;
                default: // Front-Right edge
                    localPos = center + new Vector3(half.x, ry, half.z);
                    forwardDir = (Vector3.left + Vector3.back).normalized;
                    break;
            }
        }

        // คำนวณ Up vector ที่ไม่ขนานกับ forwardDir อย่างเด็ดขาด (ป้องกัน LookRotation collinear bug)
        Vector3 upVector = (Mathf.Abs(Vector3.Dot(forwardDir, Vector3.up)) > 0.85f) ? Vector3.forward : Vector3.up;
        localRot = Quaternion.AngleAxis(spin, forwardDir) * Quaternion.LookRotation(forwardDir, upVector);
    }

    public void EnsureMaterialsLoaded()
    {
#if UNITY_EDITOR
        if (clawMarksMat == null)
            clawMarksMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Traces/M_Decal_ClawMarks.mat");
        if (oilStainMat == null)
            oilStainMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Traces/M_Decal_OilStain.mat");
        if (waterStainMat == null)
            waterStainMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Traces/M_Decal_WaterStain.mat");
        if (cardboardScuffMat == null)
            cardboardScuffMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Traces/M_Decal_CardboardScuff.mat");
        if (alienSlimeUVMat == null)
            alienSlimeUVMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Traces/M_Decal_AlienSlime_UV.mat");
        if (chemicalResidueUVMat == null)
            chemicalResidueUVMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Traces/M_Decal_ChemicalResidue_UV.mat");
#endif
    }

    [ContextMenu("Regenerate Traces (Preview in Editor)")]
    public void RegenerateTracesPreview()
    {
        PackageBox box = GetComponent<PackageBox>();
        ItemObject itemObj = null;
        ItemCategory cat = currentCategory;
        if (box != null && box.innerItemPrefab != null)
        {
            itemObj = box.innerItemPrefab.GetComponent<ItemObject>();
            if (itemObj != null) cat = itemObj.category;
        }
        ApplyTraces(cat, itemObj);
    }

    [ContextMenu("Toggle UV Light Preview")]
    public void ToggleUVLightPreview()
    {
        SetUVIlluminated(!isUVIlluminated);
    }

    [ContextMenu("Clear Traces")]
    private void ContextClearTraces() => ClearTraces();

    [ContextMenu("Test Apply Random Traces (Alien)")]
    private void TestApplyAlien() => ApplyTraces(ItemCategory.Alien);

    [ContextMenu("Test Apply Random Traces (Illegal)")]
    private void TestApplyIllegal() => ApplyTraces(ItemCategory.Illegal);

    [ContextMenu("Test Apply Random Traces (Legal)")]
    private void TestApplyLegal() => ApplyTraces(ItemCategory.Legal);
}

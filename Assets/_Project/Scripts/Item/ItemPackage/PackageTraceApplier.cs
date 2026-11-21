using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Sector13.Decals;

public enum TraceSpawnMode
{
    Procedural,     // สุ่มตำแหน่ง/หน้าเองจาก TracePlacementProfile
    AuthoredSlots,  // ใช้ช่อง DecalSlot ที่ลากวางไว้ใน prefab (ตำแหน่งตายตัว)
    Hybrid          // ใช้ slot ถ้ามี (ตาม rule) ถ้าไม่มีค่อยสุ่ม
}

/// <summary>
/// สร้าง "ร่องรอยหลักฐาน" บนผิวกล่องด้วย DecalProjector
///
/// โหมดใหม่ (แนะนำ): ใช้ <see cref="TraceSpawnTable"/> + <see cref="TracePlacementProfile"/>
///   - ตารางกำหนดว่า verdict ไหนมีรอยอะไรได้บ้าง (ส่งได้ / ส่งกลับ / ผิดกฎ / เอเลี่ยน)
///   - profile กำหนดว่าวางได้หน้าไหน ชิดขอบแค่ไหน ไหลลงล่างไหม หมุนได้กี่องศา
///   - seed สุ่มครั้งแรกต่อกล่อง → กล่องเดิมได้รอยเดิม (ไม่ reroll ตอน reload)
///
/// โหมดเดิม: ถ้าไม่ใส่ spawnTable จะใช้ settings/material 6 ชุดด้านล่างเหมือนก่อน
/// </summary>
public class PackageTraceApplier : MonoBehaviour
{
    [Header("Spawn Table (โหมดใหม่ — ถ้าไม่ใส่จะใช้ค่า Legacy ด้านล่าง)")]
    public TraceSpawnTable spawnTable;

    [Header("Spawn Mode")]
    [Tooltip("Procedural = สุ่มเองทั้งหมด, AuthoredSlots = ใช้เฉพาะช่อง DecalSlot, Hybrid = ใช้ช่องที่ตรงก่อน ถ้าไม่มีค่อยสุ่ม (กันทับกันทุกโหมด)")]
    public TraceSpawnMode spawnMode = TraceSpawnMode.Procedural;

    [Tooltip("0 = สุ่ม seed ใหม่ตอนเริ่ม (แต่คงที่ต่อกล่อง), >0 = ใช้ค่านี้")]
    public int traceSeed = 0;

    [Header("Inspector-Configurable Spawn Settings (Legacy)")]
    [Tooltip("การตั้งค่าสุ่มร่องรอยสำหรับพัสดุถูกกฎหมาย (Legal)")]
    public CategoryTraceSettings legalSettings = new CategoryTraceSettings(85f, 1, 2, 0f);

    [Tooltip("การตั้งค่าสุ่มร่องรอยสำหรับพัสดุของเถื่อน (Illegal)")]
    public CategoryTraceSettings illegalSettings = new CategoryTraceSettings(90f, 1, 3, 35f);

    [Tooltip("การตั้งค่าสุ่มร่องรอยสำหรับพัสดุเอเลี่ยน (Alien)")]
    public CategoryTraceSettings alienSettings = new CategoryTraceSettings(100f, 2, 4, 70f);

    [Header("Decal Studio (optional — ใช้ atlas แทน material 6 ตัวด้านล่าง)")]
    [Tooltip("ถ้าใส่ DecalLibrary ไว้ จะใช้ atlas + uvRect จาก Decal Studio แทน material ด้านล่าง (material เดิมยังใช้เป็น fallback)")]
    public Sector13.Decals.DecalLibrary decalLibrary;

    [Header("Decal Materials (Auto-assigned or Custom)")]
    public Material clawMarksMat;
    public Material oilStainMat;
    public Material waterStainMat;
    public Material cardboardScuffMat;
    public Material alienSlimeUVMat;
    public Material chemicalResidueUVMat;

    [Header("Projection Dimensions")]
    [Tooltip("ช่วงขนาดสุ่มของร่องรอย (Width x Height) — ใช้เมื่อไม่มี placement profile")]
    public Vector2 minTraceSize = new Vector2(0.38f, 0.38f);
    public Vector2 maxTraceSize = new Vector2(0.60f, 0.60f);
    [Tooltip("ความลึกในการฉาย Decal เข้าไปในเนื้อกล่อง (ปรับให้บางลงเพื่อไม่ให้ทะลุพื้น)")]
    public float projectionDepth = 0.35f;

    [Header("Floor Protection Settings (ป้องกันรอยล้นฉายลงพื้น)")]
    [Tooltip("ระยะห่างปลอดภัยขั้นต่ำจากก้นกล่องขึ้นมา")]
    public float floorMargin = 0.04f;
    [Tooltip("มุมองศาที่เริ่มจางรอยหากกระทบระนาบเอียง/ตั้งฉาก (Angle Fade Start)")]
    [Range(0f, 90f)] public float startAngleFade = 45f;
    [Tooltip("มุมองศาที่ตัดการฉายรอยทิ้งโดยเด็ดขาด เช่น พื้นห้องที่ทำมุม 90 องศา (Angle Fade End)")]
    [Range(0f, 90f)] public float endAngleFade = 65f;

    [Header("Runtime State")]
    public ItemCategory currentCategory = ItemCategory.Legal;
    public VerdictType currentVerdict = VerdictType.Approve;
    public bool isUVIlluminated = false;

    private readonly List<DecalProjector> nakedEyeProjectors = new List<DecalProjector>();
    private readonly List<DecalProjector> uvProjectors = new List<DecalProjector>();
    private readonly List<Vector4> placedDecals = new List<Vector4>();
    private readonly List<int> placedFaces = new List<int>();
    private Transform tracesRoot;
    private BoxCollider boxCollider;
    private int resolvedSeed;
    private bool seedInitialized;

    private void Awake()
    {
        if (boxCollider == null) boxCollider = GetComponent<BoxCollider>();

        // ถ้าไม่ได้ผูกตารางไว้ใน prefab ให้ใช้ค่า default จาก Resources
        if (spawnTable == null) spawnTable = Resources.Load<TraceSpawnTable>("TraceSpawnTable");

        EnsureMaterialsLoaded();
    }

    private void Start()
    {
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

    public static bool IsUVTrace(TraceType type)
    {
        return type == TraceType.AlienSlime_UV || type == TraceType.ChemicalResidue_UV;
    }

    /// <summary>
    /// สร้างร่องรอยบนกล่องตาม verdict (มาจาก category ถ้าไม่ระบุ)
    /// </summary>
    public void ApplyTraces(ItemCategory category, ItemObject itemObject = null)
    {
        ApplyTraces(TraceSpawnTable.FromCategory(category), category, itemObject);
    }

    public void ApplyTraces(VerdictType verdict, ItemCategory category, ItemObject itemObject = null)
    {
        currentCategory = category;
        currentVerdict = verdict;

        ClearTraces();
        placedDecals.Clear();
        placedFaces.Clear();
        EnsureMaterialsLoaded();

        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider>();
            if (boxCollider == null) boxCollider = GetComponentInChildren<BoxCollider>();
        }

        // seed ถูกเลือกครั้งเดียวต่อกล่อง → สุ่มเหมือนเดิมทุกครั้งที่เรียกซ้ำ
        if (!seedInitialized)
        {
            resolvedSeed = traceSeed != 0 ? traceSeed : Random.Range(1, int.MaxValue);
            seedInitialized = true;
        }
        var rng = new System.Random(resolvedSeed);

        if (spawnTable != null && spawnTable.Get(verdict) != null)
        {
            if (spawnMode == TraceSpawnMode.AuthoredSlots) ApplyAuthoredSlots(rng);
            else if (spawnMode == TraceSpawnMode.Hybrid) ApplyHybrid(rng);
            else SpawnFromTable(rng);
        }
        else
        {
            if (spawnMode == TraceSpawnMode.AuthoredSlots || spawnMode == TraceSpawnMode.Hybrid) ApplyAuthoredSlots(rng);
            else SpawnLegacy(rng, itemObject);
        }
    }

    // ------------------------------------------------------------------
    // Hybrid: ใช้ slot ถ้ามี ไม่งั้นสุ่ม (และกันทับกันทั้งสองแบบ)
    // ------------------------------------------------------------------
    private void ApplyHybrid(System.Random rng)
    {
        var slots = GetComponentsInChildren<DecalSlot>(true);
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].Bind(decalLibrary);
            slots[i].SetVisible(false);
        }

        var boxCenter = boxCollider != null ? boxCollider.center : Vector3.zero;
        var boxHalf = (boxCollider != null ? boxCollider.size : Vector3.one) * 0.5f;

        // 1) ช่องที่แสดงเสมอ → เปิด + จองพื้นที่
        for (int i = 0; i < slots.Length; i++)
        {
            if (!slots[i].alwaysVisible || !slots[i].Allows(currentVerdict)) continue;
            ShowSlot(slots[i]);
            ReserveSlotSpace(slots[i], boxCenter, boxHalf);
        }

        var rule = spawnTable != null ? spawnTable.Get(currentVerdict) : null;
        if (rule == null || !rule.IsUsable) return;
        if (NextFloat(rng) * 100f > rule.spawnChancePercent) return;

        int minTypes = Mathf.Max(0, rule.minTypes);
        int maxTypes = Mathf.Max(minTypes, rule.maxTypes);
        int typeCount = rng.Next(minTypes, maxTypes + 1);
        if (typeCount <= 0) return;

        var pool = new List<TraceSpawnRule>();
        for (int i = 0; i < rule.rules.Count; i++)
        {
            var r = rule.rules[i];
            if (r == null || !r.enabled) continue;
            pool.Add(r);
        }

        var procedural = new List<TraceSpawnRule>();

        // 2) เลือกชนิดตามน้ำหนัก → ถ้า rule อนุญาตให้ใช้ slot และมี slot ว่าง ก็ใช้ slot
        for (int i = 0; i < typeCount && pool.Count > 0; i++)
        {
            TraceSpawnRule pick = PickWeighted(pool, rng);
            if (pick == null) break;
            pool.Remove(pick);

            if (pick.useAuthoredSlot && TryUseAuthoredSlot(pick, rng, boxCenter, boxHalf)) continue;
            procedural.Add(pick);
        }

        // 3) ที่เหลือสุ่ม (จะหลบพื้นที่ที่ถูกจองไว้)
        for (int i = 0; i < procedural.Count; i++)
            SpawnOneInternal(procedural[i], rng, i);
    }

    private bool TryUseAuthoredSlot(TraceSpawnRule rule, System.Random rng, Vector3 boxCenter, Vector3 boxHalf)
    {
        var slots = GetComponentsInChildren<DecalSlot>(true);
        var candidates = new List<DecalSlot>();
        for (int i = 0; i < slots.Length; i++)
        {
            var s = slots[i];
            if (s == null || s.gameObject.activeSelf || s.alwaysVisible) continue;
            if (!s.Allows(currentVerdict)) continue;

            if (!string.IsNullOrEmpty(rule.decalId))
            {
                if (s.decalId != rule.decalId) continue;
            }
            else if (s.traceType != rule.traceType) continue;

            candidates.Add(s);
        }

        if (candidates.Count == 0) return false;

        var chosen = candidates[rng.Next(0, candidates.Count)];
        ShowSlot(chosen);
        ReserveSlotSpace(chosen, boxCenter, boxHalf);
        return true;
    }

    private void ReserveSlotSpace(DecalSlot slot, Vector3 boxCenter, Vector3 boxHalf)
    {
        var dp = slot.Projector;
        float radius = dp != null ? dp.size.x * 0.5f : 0.2f;
        Vector3 p = slot.transform.localPosition;
        placedDecals.Add(new Vector4(p.x, p.y, p.z, radius));
        placedFaces.Add(FaceOf(p, boxCenter, boxHalf));
    }

    // ------------------------------------------------------------------
    // โหมด AuthoredSlots: ใช้ช่องที่ลากวางไว้ใน prefab
    // ------------------------------------------------------------------
    private void ApplyAuthoredSlots(System.Random rng)
    {
        var slots = GetComponentsInChildren<DecalSlot>(true);
        if (slots == null || slots.Length == 0)
        {
            Debug.LogWarning("[PackageTraceApplier] spawnMode = AuthoredSlots แต่ไม่พบ DecalSlot ใน prefab", this);
            return;
        }

        // ปิดทุกช่อง + ผูก material จาก library ก่อน
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].Bind(decalLibrary);
            slots[i].SetVisible(false);
        }

        // 1) ช่องที่ "แสดงเสมอ" เมื่อ verdict ตรง
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].alwaysVisible && slots[i].Allows(currentVerdict))
                ShowSlot(slots[i]);
        }

        var rule = spawnTable != null ? spawnTable.Get(currentVerdict) : null;
        if (rule == null || !rule.IsUsable) return;

        if (NextFloat(rng) * 100f > rule.spawnChancePercent) return;

        int minTypes = Mathf.Max(0, rule.minTypes);
        int maxTypes = Mathf.Max(minTypes, rule.maxTypes);
        int typeCount = rng.Next(minTypes, maxTypes + 1);
        if (typeCount <= 0) return;

        // 2) รวม slot ตามชนิด (เฉพาะที่ยังว่างและ verdict อนุญาต)
        var pool = new List<TraceSpawnRule>();
        for (int i = 0; i < rule.rules.Count; i++)
        {
            var r = rule.rules[i];
            if (r == null || !r.enabled) continue;
            if (!HasAvailableSlot(slots, r.traceType)) continue;
            pool.Add(r);
        }

        // 3) เลือกชนิดตามน้ำหนัก แล้วเลือก slot ของชนิดนั้น
        for (int i = 0; i < typeCount && pool.Count > 0; i++)
        {
            TraceSpawnRule pick = PickWeighted(pool, rng);
            if (pick == null) break;
            pool.Remove(pick);

            var candidates = new List<DecalSlot>();
            for (int s = 0; s < slots.Length; s++)
            {
                if (!slots[s].alwaysVisible && slots[s].Allows(currentVerdict) && slots[s].traceType == pick.traceType
                    && !slots[s].gameObject.activeSelf)
                {
                    candidates.Add(slots[s]);
                }
            }
            if (candidates.Count == 0) continue;

            var chosen = candidates[rng.Next(0, candidates.Count)];
            ShowSlot(chosen);
        }
    }

    private static bool HasAvailableSlot(DecalSlot[] slots, TraceType type)
    {
        for (int i = 0; i < slots.Length; i++)
            if (!slots[i].alwaysVisible && slots[i].traceType == type) return true;
        return false;
    }

    private void ShowSlot(DecalSlot slot)
    {
        slot.SetVisible(true);
        var projector = slot.Projector;
        if (projector == null) return;

        if (slot.visibility == TraceVisibility.RequiresUV)
        {
            projector.fadeFactor = isUVIlluminated ? 1.0f : 0.0f;
            uvProjectors.Add(projector);
        }
        else
        {
            projector.fadeFactor = 1.0f;
            nakedEyeProjectors.Add(projector);
        }
    }

    // ------------------------------------------------------------------
    // โหมดใหม่: อ่านจาก TraceSpawnTable
    // ------------------------------------------------------------------
    private void SpawnFromTable(System.Random rng)
    {
        VerdictSpawnRule rule = spawnTable.Get(currentVerdict);
        if (rule == null || !rule.IsUsable) return;

        if (NextFloat(rng) * 100f > rule.spawnChancePercent) return;

        int minTypes = Mathf.Max(0, rule.minTypes);
        int maxTypes = Mathf.Max(minTypes, rule.maxTypes);
        int typeCount = rng.Next(minTypes, maxTypes + 1);
        if (typeCount <= 0) return;

        var pool = new List<TraceSpawnRule>();
        for (int i = 0; i < rule.rules.Count; i++)
        {
            var r = rule.rules[i];
            if (r != null && r.enabled) pool.Add(r);
        }
        if (pool.Count == 0) return;

        for (int i = 0; i < typeCount && pool.Count > 0; i++)
        {
            TraceSpawnRule pick = PickWeighted(pool, rng);
            if (pick == null) break;
            pool.Remove(pick);

            int minCount = Mathf.Max(1, pick.minCount);
            int maxCount = Mathf.Max(minCount, pick.maxCount);
            int count = rng.Next(minCount, maxCount + 1);

            for (int c = 0; c < count; c++)
            {
                SpawnOne(pick, rng, c);
            }
        }
    }

    private void SpawnOne(TraceSpawnRule rule, System.Random rng, int index)
    {
        SpawnOneInternal(rule, rng, index);
    }

    /// <summary>ใช้โดยเครื่องมือ Bake: สร้างรอย 1 ตัวจาก rule ที่กำหนด แล้วคืน GameObject (หรือ null)</summary>
    public GameObject SpawnRuleForBake(TraceSpawnRule rule, int seed, int forcedFace = -1)
    {
        if (rule == null) return null;
        if (boxCollider == null) boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null) boxCollider = GetComponentInChildren<BoxCollider>();
        EnsureTracesRoot();
        var rng = new System.Random(seed);
        return SpawnOneInternal(rule, rng, 0, forcedFace);
    }

    private GameObject SpawnOneInternal(TraceSpawnRule rule, System.Random rng, int index, int forcedFace = -1)
    {
        // 1) เลือก decal: ระบุ id เจาะจง (variant) ก่อน ถ้าไม่มีค่อยใช้ traceType
        DecalEntry specific = null;
        bool useSpecific = !string.IsNullOrEmpty(rule.decalId)
                           && decalLibrary != null
                           && decalLibrary.TryGetEntryById(rule.decalId, out specific);

        bool useLibrary = !useSpecific
                          && decalLibrary != null
                          && decalLibrary.TryGetEntry(rule.traceType, out _);

        Material mat = (useSpecific || useLibrary) ? null : GetMaterialForTraceType(rule.traceType);
        if (!useSpecific && !useLibrary && mat == null) return null;

        TracePlacementProfile profile = rule.placement;
        Vector2 sizeRange = (rule.sizeOverride.x > 0f && rule.sizeOverride.y > 0f)
            ? rule.sizeOverride
            : (profile != null ? profile.sizeRange : BetterSizeRange());

        float size = Mathf.Lerp(sizeRange.x, sizeRange.y, NextFloat(rng));
        float depth = Mathf.Max(0.05f, projectionDepth);

        EnsureTracesRoot();

        Vector3 boxCenter = boxCollider != null ? boxCollider.center : Vector3.zero;
        Vector3 boxHalf = (boxCollider != null ? boxCollider.size : Vector3.one) * 0.5f;

        // กันรอยยืดทะลุขอบ: ขนาดต้องไม่เกินหน้าที่เล็กที่สุด
        float maxFaceSize = 2f * Mathf.Min(boxHalf.x, Mathf.Min(boxHalf.y, boxHalf.z)) * 0.92f;
        size = Mathf.Min(size, maxFaceSize);

        // 2) หาตำแหน่ง — ถ้าเปิด preventOverlap จะลองสุ่มใหม่จนไม่ทับรอยเดิม
        Vector3 localPos = Vector3.zero;
        Quaternion localRot = Quaternion.identity;
        bool placed = false;
        int attempts = profile != null && profile.preventOverlap ? 8 : 1;

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            if (profile != null)
                GetProfileTransform(profile, rng, size, boxCenter, boxHalf, out localPos, out localRot, forcedFace);
            else
                GetRandomTraceTransform(boxCenter, boxHalf, size, rng, out localPos, out localRot);

            if (profile == null || !profile.preventOverlap ||
                !OverlapsExisting(localPos, size * 0.5f, profile.minSpacing, FaceOf(localPos, boxCenter, boxHalf)))
            {
                placed = true;
                break;
            }
        }

        if (!placed)
        {
            // หาที่ว่างไม่ได้ → ข้ามรอยนี้ ไม่ให้ทับกัน
            return null;
        }

        placedDecals.Add(new Vector4(localPos.x, localPos.y, localPos.z, size * 0.5f));
        placedFaces.Add(FaceOf(localPos, boxCenter, boxHalf));

        bool isUV = useSpecific
            ? specific.requiresUV
            : rule.visibility == TraceVisibility.RequiresUV;

        string traceName = isUV
            ? $"UV_Trace_{rule.traceType}_{index + 1}"
            : $"Trace_{rule.traceType}_{index + 1}";

        GameObject decalGo = new GameObject(traceName);
        decalGo.transform.SetParent(tracesRoot, false);
        decalGo.transform.localPosition = localPos;
        decalGo.transform.localRotation = localRot;

        DecalProjector projector = decalGo.AddComponent<DecalProjector>();
        projector.size = new Vector3(size, size, depth);
        projector.fadeFactor = 1f;

        if (useSpecific)
        {
            projector.material = specific.material;
            projector.uvScale = new Vector2(specific.uvRect.width, specific.uvRect.height);
            projector.uvBias = new Vector2(specific.uvRect.x, specific.uvRect.y);
        }
        else if (useLibrary)
        {
            decalLibrary.Apply(rule.traceType, projector, size, size);
        }
        else
        {
            projector.material = mat;
        }

        projector.startAngleFade = startAngleFade;
        projector.endAngleFade = endAngleFade;

        if (isUV)
        {
            projector.fadeFactor = isUVIlluminated ? 1.0f : 0.0f;
            uvProjectors.Add(projector);
        }
        else
        {
            projector.fadeFactor = 1.0f;
            nakedEyeProjectors.Add(projector);
        }

        return decalGo;
    }

    private bool OverlapsExisting(Vector3 pos, float radius, float minSpacing, int face)
    {
        for (int i = 0; i < placedDecals.Count; i++)
        {
            // คนละหน้า = ไม่ทับกัน (คนละระนาบ)
            if (placedFaces[i] != face) continue;

            Vector4 other = placedDecals[i];
            Vector3 otherPos = new Vector3(other.x, other.y, other.z);
            float minDistance = radius + other.w + Mathf.Max(0f, minSpacing);
            if (Vector3.Distance(pos, otherPos) < minDistance) return true;
        }
        return false;
    }

    /// <summary>บอกว่าตำแหน่งอยู่หน้าไหนของกล่อง (0=+X,1=-X,2=+Y,3=-Y,4=+Z,5=-Z)</summary>
    private static int FaceOf(Vector3 localPos, Vector3 center, Vector3 half)
    {
        float ax = Mathf.Abs(localPos.x - center.x) / Mathf.Max(0.001f, half.x);
        float ay = Mathf.Abs(localPos.y - center.y) / Mathf.Max(0.001f, half.y);
        float az = Mathf.Abs(localPos.z - center.z) / Mathf.Max(0.001f, half.z);

        if (ax >= ay && ax >= az) return localPos.x >= center.x ? 0 : 1;
        if (ay >= az) return localPos.y >= center.y ? 2 : 3;
        return localPos.z >= center.z ? 4 : 5;
    }

    private Vector2 BetterSizeRange()
    {
        return new Vector2(
            Mathf.Min(minTraceSize.x, maxTraceSize.x),
            Mathf.Max(minTraceSize.y, maxTraceSize.y));
    }

    private static TraceSpawnRule PickWeighted(List<TraceSpawnRule> pool, System.Random rng)
    {
        float total = 0f;
        for (int i = 0; i < pool.Count; i++) total += Mathf.Max(0f, pool[i].weight);

        if (total <= 0f) return pool[rng.Next(0, pool.Count)];

        float roll = NextFloat(rng) * total;
        for (int i = 0; i < pool.Count; i++)
        {
            roll -= Mathf.Max(0f, pool[i].weight);
            if (roll <= 0f) return pool[i];
        }
        return pool[pool.Count - 1];
    }

    /// <summary>วางรอยตาม profile (หน้า, ขอบ, แรงโน้มถ่วง, มุมหมุน)</summary>
    private void GetProfileTransform(TracePlacementProfile profile, System.Random rng, float size,
        Vector3 center, Vector3 half, out Vector3 localPos, out Quaternion localRot, int forcedFace = -1)
    {
        var faces = new List<DecalFaceMask>(6);
        if (profile.Allows(DecalFaceMask.Top)) faces.Add(DecalFaceMask.Top);
        if (profile.Allows(DecalFaceMask.Bottom)) faces.Add(DecalFaceMask.Bottom);
        if (profile.Allows(DecalFaceMask.Front)) faces.Add(DecalFaceMask.Front);
        if (profile.Allows(DecalFaceMask.Back) && !profile.avoidLabelFace) faces.Add(DecalFaceMask.Back);
        if (profile.Allows(DecalFaceMask.Left)) faces.Add(DecalFaceMask.Left);
        if (profile.Allows(DecalFaceMask.Right)) faces.Add(DecalFaceMask.Right);

        if (faces.Count == 0) faces.Add(DecalFaceMask.Top);

        DecalFaceMask face = forcedFace >= 0
            ? faces[Mathf.Abs(forcedFace) % faces.Count]
            : faces[rng.Next(0, faces.Count)];
        float m = Mathf.Clamp(profile.margin, 0.4f, 1f);

        float rx = Mathf.Lerp(-half.x * m, half.x * m, NextFloat(rng));
        float ry = Mathf.Lerp(-half.y * m, half.y * m, NextFloat(rng));
        float rz = Mathf.Lerp(-half.z * m, half.z * m, NextFloat(rng));

        bool edge = NextFloat(rng) < profile.edgeChance;
        Vector3 forward;

        switch (face)
        {
            case DecalFaceMask.Bottom:
                localPos = center + new Vector3(rx, -half.y, rz);
                forward = Vector3.up;
                if (edge) localPos.z = (localPos.z >= center.z ? 1f : -1f) * half.z * 0.95f;
                break;
            case DecalFaceMask.Front:
                localPos = center + new Vector3(rx, ry, half.z);
                forward = Vector3.back;
                if (edge) localPos.y = center.y + (localPos.y >= center.y ? 1f : -1f) * half.y * 0.85f;
                break;
            case DecalFaceMask.Back:
                localPos = center + new Vector3(rx, ry, -half.z);
                forward = Vector3.forward;
                break;
            case DecalFaceMask.Left:
                localPos = center + new Vector3(-half.x, ry, rz);
                forward = Vector3.right;
                if (edge) localPos.y = center.y + (localPos.y >= center.y ? 1f : -1f) * half.y * 0.85f;
                break;
            case DecalFaceMask.Right:
                localPos = center + new Vector3(half.x, ry, rz);
                forward = Vector3.left;
                if (edge) localPos.y = center.y + (localPos.y >= center.y ? 1f : -1f) * half.y * 0.85f;
                break;
            default: // Top
                localPos = center + new Vector3(rx, half.y, rz);
                forward = Vector3.down;
                if (edge) localPos.x = (localPos.x >= center.x ? 1f : -1f) * half.x * 0.85f;
                break;
        }

        // แรงโน้มถ่วง: ดึงลงด้านล่างของหน้า (คราบไหล/เลือด)
        if (profile.gravityFlow > 0f && face != DecalFaceMask.Top && face != DecalFaceMask.Bottom)
        {
            float lowest = center.y - half.y + size * 0.5f + floorMargin;
            float t = profile.gravityFlow * NextFloat(rng);
            localPos.y = Mathf.Lerp(localPos.y, lowest, t);
        }

        // กันรอยล้นขอบหน้า: ดันตำแหน่งให้อยู่ในการ์ดของหน้า
        float padX = Mathf.Min(size * 0.5f + 0.01f, half.x * 0.98f);
        float padY = Mathf.Min(size * 0.5f + 0.01f, half.y * 0.98f);
        float padZ = Mathf.Min(size * 0.5f + 0.01f, half.z * 0.98f);

        switch (face)
        {
            case DecalFaceMask.Top:
            case DecalFaceMask.Bottom:
                localPos.x = Mathf.Clamp(localPos.x, center.x - half.x + padX, center.x + half.x - padX);
                localPos.z = Mathf.Clamp(localPos.z, center.z - half.z + padZ, center.z + half.z - padZ);
                break;
            case DecalFaceMask.Front:
            case DecalFaceMask.Back:
                localPos.x = Mathf.Clamp(localPos.x, center.x - half.x + padX, center.x + half.x - padX);
                localPos.y = Mathf.Clamp(localPos.y, center.y - half.y + padY, center.y + half.y - padY);
                break;
            default: // Left / Right
                localPos.z = Mathf.Clamp(localPos.z, center.z - half.z + padZ, center.z + half.z - padZ);
                localPos.y = Mathf.Clamp(localPos.y, center.y - half.y + padY, center.y + half.y - padY);
                break;
        }

        float spin = Mathf.Lerp(profile.spinRange.x, profile.spinRange.y, NextFloat(rng));
        Vector3 upVector = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.85f ? Vector3.forward : Vector3.up;
        localRot = Quaternion.AngleAxis(spin, forward) * Quaternion.LookRotation(forward, upVector);
    }

    // ------------------------------------------------------------------
    // โหมดเดิม (Legacy)
    // ------------------------------------------------------------------
    private void SpawnLegacy(System.Random rng, ItemObject itemObject)
    {
        CategoryTraceSettings settings = GetSettingsForCategory(currentCategory);
        if (settings == null) return;

        if (NextFloat(rng) * 100f > settings.spawnChancePercent) return;

        int min = Mathf.Min(settings.minTraces, settings.maxTraces);
        int max = Mathf.Max(settings.minTraces, settings.maxTraces);
        int traceCount = rng.Next(min, max + 1);
        if (traceCount <= 0) return;

        EnsureTracesRoot();

        Vector3 boxCenter = boxCollider != null ? boxCollider.center : Vector3.zero;
        Vector3 boxHalf = (boxCollider != null ? boxCollider.size : Vector3.one) * 0.5f;

        bool hasItemCustomTraces = itemObject != null && itemObject.allowedTraces != null && itemObject.allowedTraces.Length > 0;

        for (int i = 0; i < traceCount; i++)
        {
            bool preferUV = NextFloat(rng) * 100f < settings.uvTraceRatioPercent;
            TraceType traceType = hasItemCustomTraces
                ? PickTraceTypeFromItem(itemObject, preferUV, rng)
                : PickTraceType(currentCategory, preferUV, rng);

            bool isUV = IsUVTrace(traceType);
            bool useLibrary = decalLibrary != null && decalLibrary.TryGetEntry(traceType, out _);
            Material mat = useLibrary ? null : GetMaterialForTraceType(traceType);
            if (!useLibrary && mat == null) continue;

            float width = Mathf.Lerp(minTraceSize.x, maxTraceSize.x, NextFloat(rng));
            float height = Mathf.Lerp(minTraceSize.y, maxTraceSize.y, NextFloat(rng));
            float maxDim = Mathf.Max(width, height);

            Vector3 localPos;
            Quaternion localRot;
            GetRandomTraceTransform(boxCenter, boxHalf, maxDim, rng, out localPos, out localRot);

            string traceName = isUV ? $"UV_Trace_{traceType}_{i + 1}" : $"Trace_{traceType}_{i + 1}";
            GameObject decalGo = new GameObject(traceName);
            decalGo.transform.SetParent(tracesRoot, false);
            decalGo.transform.localPosition = localPos;
            decalGo.transform.localRotation = localRot;

            DecalProjector projector = decalGo.AddComponent<DecalProjector>();
            projector.size = new Vector3(width, height, projectionDepth);
            projector.startAngleFade = startAngleFade;
            projector.endAngleFade = endAngleFade;

            if (useLibrary) decalLibrary.Apply(traceType, projector, width, height);
            else projector.material = mat;

            if (isUV)
            {
                projector.fadeFactor = isUVIlluminated ? 1.0f : 0.0f;
                uvProjectors.Add(projector);
            }
            else
            {
                projector.fadeFactor = 1.0f;
                nakedEyeProjectors.Add(projector);
            }
        }
    }

    private TraceType PickTraceTypeFromItem(ItemObject itemObject, bool preferUV, System.Random rng)
    {
        List<TraceType> preferred = new List<TraceType>();
        for (int i = 0; i < itemObject.allowedTraces.Length; i++)
        {
            TraceType t = itemObject.allowedTraces[i];
            if (IsUVTrace(t) == preferUV) preferred.Add(t);
        }

        if (preferred.Count > 0) return preferred[rng.Next(0, preferred.Count)];
        return itemObject.allowedTraces[rng.Next(0, itemObject.allowedTraces.Length)];
    }

    public void ClearTraces()
    {
        nakedEyeProjectors.Clear();
        uvProjectors.Clear();
        placedDecals.Clear();
        placedFaces.Clear();

        if (tracesRoot != null)
        {
            if (Application.isPlaying) Destroy(tracesRoot.gameObject);
            else DestroyImmediate(tracesRoot.gameObject);
            tracesRoot = null;
        }
    }

    public void SetUVIlluminated(bool illuminated)
    {
        isUVIlluminated = illuminated;
    }

    /// <summary>ล้าง seed ที่จำไว้ (ใช้โดยเครื่องมือ bake / เทสต์)</summary>
    public void ResetSeed()
    {
        seedInitialized = false;
    }

    private void EnsureTracesRoot()
    {
        if (tracesRoot != null) return;

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

    private TraceType PickTraceType(ItemCategory category, bool isUV, System.Random rng)
    {
        if (isUV)
        {
            if (category == ItemCategory.Alien)
                return NextFloat(rng) < 0.8f ? TraceType.AlienSlime_UV : TraceType.ChemicalResidue_UV;
            return TraceType.ChemicalResidue_UV;
        }

        if (category == ItemCategory.Alien)
        {
            float r = NextFloat(rng);
            if (r < 0.75f) return TraceType.ClawMarks;
            return r < 0.9f ? TraceType.CardboardScuff : TraceType.OilStain;
        }

        if (category == ItemCategory.Illegal)
        {
            float r = NextFloat(rng);
            if (r < 0.55f) return TraceType.OilStain;
            return r < 0.85f ? TraceType.CardboardScuff : TraceType.WaterStain;
        }

        return NextFloat(rng) < 0.6f ? TraceType.WaterStain : TraceType.CardboardScuff;
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

    /// <summary>ตำแหน่งแบบเดิม (5 หน้า + สันขอบ) — ใช้เมื่อไม่มี placement profile</summary>
    private void GetRandomTraceTransform(Vector3 center, Vector3 half, float traceExtent,
        System.Random rng, out Vector3 localPos, out Quaternion localRot)
    {
        bool pickEdge = NextFloat(rng) < 0.35f;

        float margin = 0.80f;
        float rx = Mathf.Lerp(-half.x * margin, half.x * margin, NextFloat(rng));
        float rz = Mathf.Lerp(-half.z * margin, half.z * margin, NextFloat(rng));
        float spin = Mathf.Lerp(0f, 360f, NextFloat(rng));

        float halfTrace = traceExtent * 0.55f;
        float minY = (-half.y + halfTrace) + floorMargin;
        float maxY = half.y * margin;
        if (minY > maxY) minY = maxY - 0.05f;
        float ry = Mathf.Lerp(minY, maxY, NextFloat(rng));

        Vector3 forwardDir;

        if (!pickEdge)
        {
            int face = rng.Next(0, 5);
            switch (face)
            {
                case 0: // Top
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
            int edgeIndex = rng.Next(0, 4);
            switch (edgeIndex)
            {
                case 0:
                    localPos = center + new Vector3(rx, half.y, half.z);
                    forwardDir = (Vector3.down + Vector3.back).normalized;
                    break;
                case 1:
                    localPos = center + new Vector3(half.x, half.y, rz);
                    forwardDir = (Vector3.down + Vector3.left).normalized;
                    break;
                case 2:
                    localPos = center + new Vector3(-half.x, half.y, rz);
                    forwardDir = (Vector3.down + Vector3.right).normalized;
                    break;
                default:
                    localPos = center + new Vector3(half.x, ry, half.z);
                    forwardDir = (Vector3.left + Vector3.back).normalized;
                    break;
            }
        }

        Vector3 upVector = Mathf.Abs(Vector3.Dot(forwardDir, Vector3.up)) > 0.85f ? Vector3.forward : Vector3.up;
        localRot = Quaternion.AngleAxis(spin, forwardDir) * Quaternion.LookRotation(forwardDir, upVector);
    }

    private static float NextFloat(System.Random rng) => (float)rng.NextDouble();

    public void EnsureMaterialsLoaded()
    {
#if UNITY_EDITOR
        if (clawMarksMat == null)
            clawMarksMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_Decal_ClawMarks.mat");
        if (oilStainMat == null)
            oilStainMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_Decal_OilStain.mat");
        if (waterStainMat == null)
            waterStainMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_Decal_WaterStain.mat");
        if (cardboardScuffMat == null)
            cardboardScuffMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_Decal_CardboardScuff.mat");
        if (alienSlimeUVMat == null)
            alienSlimeUVMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_Decal_AlienSlime_UV.mat");
        if (chemicalResidueUVMat == null)
            chemicalResidueUVMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_Decal_ChemicalResidue_UV.mat");
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
        seedInitialized = false;
        ApplyTraces(cat, itemObj);
    }

    [ContextMenu("Toggle UV Light Preview")]
    public void ToggleUVLightPreview()
    {
        SetUVIlluminated(!isUVIlluminated);
    }

    [ContextMenu("Clear Traces")]
    private void ContextClearTraces() => ClearTraces();

    [ContextMenu("Test Spawn: ส่งได้ (Approve)")]
    private void TestApprove() { seedInitialized = false; ApplyTraces(VerdictType.Approve, ItemCategory.Legal); }

    [ContextMenu("Test Spawn: ส่งกลับ (Return)")]
    private void TestReturn() { seedInitialized = false; ApplyTraces(VerdictType.Return, ItemCategory.Legal); }

    [ContextMenu("Test Spawn: ผิดกฎ (Illegal)")]
    private void TestIllegal() { seedInitialized = false; ApplyTraces(VerdictType.Illegal, ItemCategory.Illegal); }

    [ContextMenu("Test Spawn: เอเลี่ยน (Alien)")]
    private void TestAlien() { seedInitialized = false; ApplyTraces(VerdictType.Alien, ItemCategory.Alien); }
}

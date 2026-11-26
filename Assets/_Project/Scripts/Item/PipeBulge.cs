using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cartoon "something is travelling inside the pipe" bulge.
///
/// หลักการ: ดัน vertex ออกเป็น "รัศมี" จากเส้นกลางท่อ (spine) ไม่ใช่ตาม normal ของผิว
///   - ท่อที่มี seam / หน้าแปลน จะมี vertex ซ้ำและ normal ชี้คนละทิศ
///     ถ้าดันตาม normal หน้าแปลนจะถูกดันออกตามแนวแกน → วงแหวนแยกจากตัวท่อ
///   - ดันออกจากเส้นกลางแบบรัศมี → ทุก vertex ในวงเดียวกันขยับเท่ากัน วงไม่แตก
///   - โปรไฟล์ทรงกลม sqrt(1 - x²) ทำให้ลูกป่องดูกลมเหมือนลูกบอล
///
/// Mesh ต้องเปิด Read/Write Enabled (Model import settings)
/// </summary>
public class PipeBulge : MonoBehaviour
{
    private const string BulgeSuffix = "_bulged";

    [Header("Pipe")]
    [Tooltip("Meshes that make up the pipe. Leave empty to use every MeshFilter in the children.")]
    public MeshFilter[] pipeParts;

    [Header("Travelling lump")]
    [Tooltip("Optional: a transform that moves inside the pipe. Its world position drives the bulge.")]
    public Transform lump;

    [Tooltip("ครึ่งความยาวของลูกป่องตามแนวท่อ (เมตร)")]
    public float radius = 0.32f;

    [Tooltip("ระยะที่ผิวป่องออกจากแนวเดิม (เมตร) — ค่าลบ = บีบเข้า")]
    public float amount = 0.08f;

    [Tooltip("1 = กลมเหมือนลูกบอล, มากกว่า 1 = ลูกเล็กลง/คมขึ้น")]
    [Range(0.5f, 4f)] public float falloffPower = 1f;

    [Tooltip("ตำแหน่งที่ใช้เมื่อไม่ได้ผูก lump")]
    public Vector3 manualPoint;

    public bool HasPoint { get; private set; }

    private class Part
    {
        public MeshFilter filter;
        public Mesh mesh;
        public Vector3[] baseVerts;  // local space
        public Vector3[] baseWorld;  // world space (ตอน init)
        public Vector3[] radial;     // world space, ตั้งฉากกับเส้นกลางท่อ
        public Vector3[] work;       // local space buffer
    }

    private readonly List<Part> parts = new List<Part>();
    private Vector3[] spine;         // world-space polyline (เส้นกลางท่อ)
    private bool radialsDirty = true;
    private bool initialized;
    private bool applied;

    private void Start() => Initialize();

    private void OnEnable()
    {
        if (initialized) radialsDirty = true;
    }

    private void OnDisable() => Restore();

    // ---------------------------------------------------------------- setup

    public void Initialize()
    {
        if (initialized) return;
        initialized = true;

        if (pipeParts == null || pipeParts.Length == 0)
        {
            var found = new List<MeshFilter>();
            foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) found.Add(mf);
            pipeParts = found.ToArray();
        }

        parts.Clear();

        foreach (var mf in pipeParts)
        {
            if (mf == null || mf.sharedMesh == null) continue;

            Mesh source = mf.sharedMesh;
            if (!source.isReadable)
            {
                Debug.LogWarning("[PipeBulge] mesh '" + source.name +
                                 "' is not readable — enable Read/Write on the model.", this);
                continue;
            }

            // ใช้ mesh ที่โคลนไว้แล้วถ้าเคยสร้าง (ชื่อลงท้าย _bulged) กัน clone ซ้ำทุกรอบ
            Mesh instance = source;
            if (!source.name.EndsWith(BulgeSuffix))
            {
                instance = Instantiate(source);
                instance.name = source.name + BulgeSuffix;
                mf.sharedMesh = instance;
            }

            parts.Add(new Part
            {
                filter = mf,
                mesh = instance,
                baseVerts = instance.vertices,
                baseWorld = new Vector3[instance.vertexCount],
                radial = new Vector3[instance.vertexCount],
                work = new Vector3[instance.vertexCount]
            });
        }

        radialsDirty = true;
    }

    /// <summary>เส้นกลางท่อ (world space) — PipeSuck ส่งมาให้หลังหาปาก/ทางออกได้</summary>
    public void SetSpine(Vector3[] worldPoints)
    {
        if (worldPoints == null || worldPoints.Length < 2) return;

        if (spine != null && spine.Length == worldPoints.Length)
        {
            bool same = true;
            for (int i = 0; i < spine.Length; i++)
            {
                if ((spine[i] - worldPoints[i]).sqrMagnitude > 1e-6f) { same = false; break; }
            }
            if (same) return;
        }

        spine = worldPoints;
        radialsDirty = true;
    }

    // ------------------------------------------------------------- runtime

    /// <summary>World position of the travelling lump.</summary>
    public void SetPoint(Vector3 worldPosition)
    {
        manualPoint = worldPosition;
        HasPoint = true;
    }

    /// <summary>หยุดป่องและคืน mesh กลับเป็นรูปเดิม</summary>
    public void ClearPoint()
    {
        HasPoint = false;
        Restore();
    }

    private void LateUpdate()
    {
        Initialize();

        if (lump != null)
        {
            manualPoint = lump.position;
            HasPoint = true;
        }

        if (!HasPoint) return;
        Apply(manualPoint);
    }

    /// <summary>ดันผิวท่อตามระยะจาก worldPoint</summary>
    public void Apply(Vector3 worldPoint)
    {
        Initialize();
        if (radialsDirty) BuildRadials();
        if (parts.Count == 0 || spine == null) return;

        float r = Mathf.Max(0.01f, radius);
        float power = Mathf.Max(0.1f, falloffPower);

        foreach (var p in parts)
        {
            Transform t = p.filter.transform;
            int n = p.baseWorld.Length;

            for (int i = 0; i < n; i++)
            {
                float d = Vector3.Distance(p.baseWorld[i], worldPoint);

                if (d >= r)
                {
                    p.work[i] = p.baseVerts[i];
                    continue;
                }

                float x = d / r;
                float f = Mathf.Sqrt(1f - x * x);   // โปรไฟล์กลม
                f = Mathf.Pow(f, power);

                Vector3 w = p.baseWorld[i] + p.radial[i] * (f * amount);
                p.work[i] = t.InverseTransformPoint(w);
            }

            p.mesh.vertices = p.work;
            p.mesh.RecalculateNormals();
            p.mesh.RecalculateBounds();
        }

        applied = true;
    }

    /// <summary>คืน mesh กลับเป็นรูปเดิม (ไม่ป่อง)</summary>
    public void Restore()
    {
        if (!applied) return;
        applied = false;

        foreach (var p in parts)
        {
            if (p.mesh == null) continue;
            p.mesh.vertices = p.baseVerts;
            p.mesh.RecalculateNormals();
            p.mesh.RecalculateBounds();
        }
    }

    // -------------------------------------------------------------- helpers

    private void BuildRadials()
    {
        radialsDirty = false;
        if (parts.Count == 0) return;

        if (spine == null || spine.Length < 2) BuildFallbackSpine();
        if (spine == null || spine.Length < 2) return;

        foreach (var p in parts)
        {
            Transform t = p.filter.transform;
            int n = p.baseVerts.Length;

            for (int i = 0; i < n; i++)
            {
                Vector3 w = t.TransformPoint(p.baseVerts[i]);
                p.baseWorld[i] = w;

                Vector3 tangent;
                Vector3 c = ClosestOnPolyline(spine, w, out tangent);

                Vector3 radial = w - c;
                radial -= tangent * Vector3.Dot(radial, tangent);

                if (radial.sqrMagnitude < 1e-8f)
                {
                    radial = Vector3.Cross(tangent, Vector3.up);
                    if (radial.sqrMagnitude < 1e-6f) radial = Vector3.Cross(tangent, Vector3.forward);
                    if (radial.sqrMagnitude < 1e-6f) radial = Vector3.up;
                }

                p.radial[i] = radial.normalized;
            }
        }
    }

    private void BuildFallbackSpine()
    {
        if (parts.Count == 0) return;

        Bounds b = parts[0].mesh.bounds;
        Vector3 size = b.size;
        int axis = size.x >= size.y && size.x >= size.z ? 0 : (size.y >= size.z ? 1 : 2);
        Vector3 dir = axis == 0 ? Vector3.right : (axis == 1 ? Vector3.up : Vector3.forward);

        Transform t = parts[0].filter.transform;
        spine = new[]
        {
            t.TransformPoint(b.center - dir * (size[axis] * 1.1f)),
            t.TransformPoint(b.center + dir * (size[axis] * 1.1f))
        };
    }

    private static Vector3 ClosestOnPolyline(Vector3[] pts, Vector3 p, out Vector3 tangent)
    {
        Vector3 best = pts[0];
        float bestSqr = float.MaxValue;
        tangent = Vector3.forward;

        for (int i = 0; i < pts.Length - 1; i++)
        {
            Vector3 a = pts[i];
            Vector3 ab = pts[i + 1] - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-8f) continue;

            float u = Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
            Vector3 c = a + ab * u;
            float d = (p - c).sqrMagnitude;

            if (d < bestSqr)
            {
                bestSqr = d;
                best = c;
                tangent = ab / Mathf.Sqrt(len2);
            }
        }

        return best;
    }

    private void OnDrawGizmosSelected()
    {
        if (spine != null && spine.Length > 1)
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.8f);
            for (int i = 0; i < spine.Length - 1; i++) Gizmos.DrawLine(spine[i], spine[i + 1]);
        }

        Vector3 pt = lump != null ? lump.position : manualPoint;
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.6f);
        Gizmos.DrawWireSphere(pt, radius);
    }
}

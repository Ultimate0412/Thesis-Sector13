using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cartoon "something is travelling inside the pipe" bulge.
/// Displaces the pipe mesh outward along its normals using the distance from a
/// single travelling point, so it works on straight, bent or curved sections
/// without any rigging.
///
/// The mesh must be readable (Model import ▸ Read/Write Enabled).
/// </summary>
public class PipeBulge : MonoBehaviour
{
    [Header("Pipe")]
    [Tooltip("Meshes that make up the pipe. Leave empty to use every MeshFilter in the children.")]
    public MeshFilter[] pipeParts;

    [Header("Travelling lump")]
    [Tooltip("Optional: a transform that moves inside the pipe. Its world position drives the bulge.")]
    public Transform lump;

    [Tooltip("How far from the lump the pipe starts to swell (metres).")]
    public float radius = 0.22f;

    [Tooltip("Extra radius at the lump, in metres.")]
    public float amount = 0.06f;

    [Tooltip("Falloff shape. 1 = smooth, 2 = tighter lump.")]
    [Range(0.5f, 4f)] public float falloffPower = 1.4f;

    [Tooltip("Use the position set through SetPoint() when no lump transform is assigned.")]
    public Vector3 manualPoint;

    public bool HasPoint { get; private set; }

    private class Part
    {
        public MeshFilter filter;
        public Mesh mesh;
        public Vector3[] baseVerts;
        public Vector3[] baseNormals;
        public Vector3[] work;
    }

    private readonly List<Part> parts = new List<Part>();
    private Vector3 currentPoint;
    private Vector3 lastApplied;
    private bool initialized;

    private void Start() => Initialize();

    private void OnEnable() => initialized = false;

    public void Initialize()
    {
        if (initialized) return;
        initialized = true;
        parts.Clear();

        if (pipeParts == null || pipeParts.Length == 0)
        {
            var found = new List<MeshFilter>();
            foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) found.Add(mf);
            pipeParts = found.ToArray();
        }

        foreach (var mf in pipeParts)
        {
            if (mf == null || mf.sharedMesh == null) continue;
            if (!mf.sharedMesh.isReadable)
            {
                Debug.LogWarning("[PipeBulge] mesh '" + mf.sharedMesh.name + "' is not readable — enable Read/Write on the model.");
                continue;
            }

            var source = mf.sharedMesh;
            var inst = Instantiate(source);
            inst.name = source.name + "_bulged";
            mf.sharedMesh = inst;

            parts.Add(new Part
            {
                filter = mf,
                mesh = inst,
                baseVerts = source.vertices,
                baseNormals = source.normals,
                work = new Vector3[source.vertexCount]
            });
        }
    }

    /// <summary>World position of the travelling object.</summary>
    public void SetPoint(Vector3 worldPosition)
    {
        manualPoint = worldPosition;
        HasPoint = true;
    }

    public void ClearPoint() => HasPoint = false;

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

    /// <summary>Applies the bulge for a world-space point. Safe to call directly.</summary>
    public void Apply(Vector3 worldPoint)
    {
        Initialize();
        if (parts.Count == 0) return;

        currentPoint = worldPoint;
        float r = Mathf.Max(0.01f, radius);

        foreach (var p in parts)
        {
            Transform t = p.filter.transform;
            Vector3 local = t.InverseTransformPoint(worldPoint);
            float scale = Mathf.Max(0.0001f, t.lossyScale.x);

            for (int i = 0; i < p.baseVerts.Length; i++)
            {
                float d = Vector3.Distance(p.baseVerts[i], local);
                float f = 1f - Mathf.SmoothStep(0f, r / scale, d);
                f = Mathf.Pow(f, falloffPower);
                p.work[i] = p.baseVerts[i] + p.baseNormals[i] * (f * amount);
            }

            p.mesh.vertices = p.work;
            p.mesh.RecalculateNormals();
            p.mesh.RecalculateBounds();
        }
        lastApplied = worldPoint;
    }

    private void OnDrawGizmosSelected()
    {
        var pt = lump != null ? lump.position : manualPoint;
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.6f);
        Gizmos.DrawWireSphere(pt, radius);
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Swallows a parcel into a curved pipe: the box is pulled into the mouth, then a
/// single "lump" travels the elbow while <see cref="PipeBulge"/> swells the mesh.
/// Works without rigging; the pipe mesh only needs Read/Write enabled.
/// </summary>
[RequireComponent(typeof(PipeBulge))]
public class PipeSuck : MonoBehaviour
{
    [Header("Pipe")]
    public PipeBulge bulge;

    [Tooltip("Mesh used to find the mouth and the exit. Defaults to the first PipeBulge part.")]
    public MeshFilter pipeMesh;

    [Header("Timing")]
    [Tooltip("How long the parcel takes to be pulled into the mouth.")]
    public float pullDuration = 0.45f;

    [Tooltip("How long the lump takes to travel through the elbow.")]
    public float travelDuration = 0.9f;

    [Tooltip("Scale the parcel shrinks to while entering.")]
    [Range(0.05f, 1f)] public float entryScale = 0.3f;

    [Header("Look")]
    [Tooltip("Extra radius of the bulge at the lump.")]
    public float bulgeAmount = 0.09f;

    [Tooltip("How far along the elbow the bulge reaches.")]
    public float bulgeRadius = 0.35f;

    public bool IsBusy => queue.Count > 0 || running;

    private readonly Queue<GameObject> queue = new Queue<GameObject>();
    private bool running;
    private bool configured;

    private Vector3 mouth, exit, corner;
    private float tubeRadius;

    private void Awake()
    {
        if (bulge == null) bulge = GetComponent<PipeBulge>();
        Configure();
    }

    /// <summary>Finds the mouth (smaller ring), the exit (flange end) and a bend point.</summary>
    public void Configure()
    {
        if (configured) return;
        if (bulge == null) bulge = GetComponent<PipeBulge>();
        if (bulge == null) return;

        if (pipeMesh == null)
        {
            if (bulge.pipeParts == null || bulge.pipeParts.Length == 0) bulge.Initialize();
            if (bulge.pipeParts != null && bulge.pipeParts.Length > 0) pipeMesh = bulge.pipeParts[0];
        }
        if (pipeMesh == null || pipeMesh.sharedMesh == null) return;

        var mesh = pipeMesh.sharedMesh;
        var verts = mesh.vertices;
        if (verts.Length == 0) return;

        Transform t = pipeMesh.transform;
        var wp = new Vector3[verts.Length];
        for (int i = 0; i < verts.Length; i++) wp[i] = t.TransformPoint(verts[i]);

        Vector3 min = wp[0], max = wp[0];
        foreach (var p in wp) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
        Vector3 size = max - min;

        // slice along the axis with the largest extent; the ring at each end is an opening
        int axis = size.x >= size.y && size.x >= size.z ? 0 : (size.y >= size.z ? 1 : 2);
        float lo = axis == 0 ? min.x : axis == 1 ? min.y : min.z;
        float hi = axis == 0 ? max.x : axis == 1 ? max.y : max.z;
        float band = Mathf.Max(0.02f, (hi - lo) * 0.08f);

        Vector3 endLow = Vector3.zero, endHigh = Vector3.zero;
        float rLow = -1f, rHigh = -1f;
        int nLow = 0, nHigh = 0;

        for (int pass = 0; pass < 2; pass++)
        {
            float edge = pass == 0 ? lo : hi;
            Vector3 sum = Vector3.zero; int n = 0;
            foreach (var p in wp)
            {
                float val = axis == 0 ? p.x : axis == 1 ? p.y : p.z;
                if (Mathf.Abs(val - edge) <= band) { sum += p; n++; }
            }
            if (n == 0) continue;
            Vector3 c = sum / n;
            float r = 0f;
            foreach (var p in wp)
            {
                float val = axis == 0 ? p.x : axis == 1 ? p.y : p.z;
                if (Mathf.Abs(val - edge) <= band) r = Mathf.Max(r, Vector3.Distance(p, c));
            }
            if (pass == 0) { endLow = c; rLow = r; nLow = n; }
            else { endHigh = c; rHigh = r; nHigh = n; }
        }

        if (rLow < 0f || rHigh < 0f) return;

        // the flange end has the bigger ring; the parcel enters the smaller one
        if (rLow <= rHigh) { mouth = endLow; exit = endHigh; tubeRadius = rLow; }
        else { mouth = endHigh; exit = endLow; tubeRadius = rHigh; }

        // bend point: the vertex farthest from the straight line between the ends
        Vector3 ab = exit - mouth;
        float best = -1f;
        corner = (mouth + exit) * 0.5f;
        foreach (var p in wp)
        {
            Vector3 ap = p - mouth;
            float proj = Mathf.Clamp01(Vector3.Dot(ap, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            Vector3 closest = mouth + ab * proj;
            float d = Vector3.Distance(p, closest);
            if (d > best) { best = d; corner = p; }
        }

        configured = true;
    }

    public void Suck(GameObject package)
    {
        if (package == null) return;
        Configure();
        queue.Enqueue(package);
        if (!running) StartCoroutine(RunQueue());
    }

    private IEnumerator RunQueue()
    {
        running = true;
        while (queue.Count > 0)
        {
            var pkg = queue.Dequeue();
            if (pkg != null) yield return SuckOne(pkg);
        }
        running = false;
    }

    private IEnumerator SuckOne(GameObject package)
    {
        // let go of physics so the box can be animated
        var rb = package.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
        foreach (var c in package.GetComponentsInChildren<Collider>()) c.enabled = false;

        // also release it from the drop point bookkeeping
        var dp = BaseDropPoint.GetDropPointHolding(package);
        if (dp != null && dp.currentPlacedItem == package) dp.currentPlacedItem = null;

        Vector3 start = package.transform.position;
        Vector3 startScale = package.transform.localScale;
        Quaternion startRot = package.transform.rotation;

        // 1) pull into the mouth
        float t = 0f;
        while (t < pullDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / pullDuration);
            float e = k * k;                       // accelerate like a suction
            package.transform.position = Vector3.Lerp(start, mouth, e);
            package.transform.localScale = Vector3.Lerp(startScale, startScale * entryScale, e);
            package.transform.rotation = Quaternion.Slerp(startRot, Quaternion.LookRotation((exit - mouth).normalized), e * 0.5f);
            yield return null;
        }

        package.SetActive(false);

        // 2) the lump runs the elbow
        if (bulge != null)
        {
            bulge.amount = bulgeAmount;
            bulge.radius = Mathf.Max(bulgeRadius, tubeRadius * 1.2f);
        }

        Vector3 a = mouth, b = corner, c2 = exit;
        float travel = 0f;
        while (travel < travelDuration)
        {
            travel += Time.deltaTime;
            float k = Mathf.Clamp01(travel / travelDuration);
            float e = Mathf.SmoothStep(0f, 1f, k);
            // quadratic bezier through the bend
            Vector3 p = Vector3.Lerp(
                Vector3.Lerp(a, b, e),
                Vector3.Lerp(b, c2, e),
                e);
            if (bulge != null) bulge.SetPoint(p);
            yield return null;
        }

        if (bulge != null) bulge.ClearPoint();

        Object.Destroy(package);
    }

    private void OnDrawGizmosSelected()
    {
        if (!configured) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(mouth, Mathf.Max(0.05f, tubeRadius));
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(exit, Mathf.Max(0.05f, tubeRadius));
        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(corner, 0.05f);
        Gizmos.color = Color.white;
        Gizmos.DrawLine(mouth, corner);
        Gizmos.DrawLine(corner, exit);
    }
}

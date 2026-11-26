using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Swallows a parcel into a curved pipe: the box is pulled into the mouth, the
/// mouth squeezes (cartoon gulp), then a single round "lump" travels the elbow
/// while <see cref="PipeBulge"/> swells the mesh.
///
/// ทำงานได้โดยไม่ต้อง Rig — mesh ท่อต้องเปิด Read/Write Enabled
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

    [Header("Squeeze / gulp")]
    [Tooltip("ปากท่อรัดเข้าตอนกล่องมาถึง (เมตร). 0 = ไม่รัด")]
    public float gulpPinch = 0.06f;

    [Tooltip("เวลาที่ปากท่อค้างรัดก่อนคลาย (วินาที)")]
    public float gulpHold = 0.06f;

    [Tooltip("เวลาที่ปากท่อคลายตัวกลับ (วินาที)")]
    public float gulpRelease = 0.12f;

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
    private Vector3[] spine;

    private void Awake()
    {
        if (bulge == null) bulge = GetComponent<PipeBulge>();
        Configure();
    }

    /// <summary>หาปากท่อ (วงเล็ก), ทางออก (วงใหญ่) และเส้นกลางท่อ</summary>
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

        // ตัดตามแกนที่ยาวที่สุด — วงแหวนที่ปลายทั้งสองคือปากท่อ/ทางออก
        int axis = size.x >= size.y && size.x >= size.z ? 0 : (size.y >= size.z ? 1 : 2);
        float lo = axis == 0 ? min.x : axis == 1 ? min.y : min.z;
        float hi = axis == 0 ? max.x : axis == 1 ? max.y : max.z;
        float band = Mathf.Max(0.02f, (hi - lo) * 0.08f);

        Vector3 endLow = Vector3.zero, endHigh = Vector3.zero;
        float rLow = -1f, rHigh = -1f;

        for (int pass = 0; pass < 2; pass++)
        {
            float edge = pass == 0 ? lo : hi;
            Vector3 sum = Vector3.zero;
            int n = 0;

            foreach (var p in wp)
            {
                float val = axis == 0 ? p.x : axis == 1 ? p.y : p.z;
                if (Mathf.Abs(val - edge) <= band)
                {
                    sum += p;
                    n++;
                }
            }

            if (n == 0) continue;

            Vector3 c = sum / n;
            float r = 0f;

            foreach (var p in wp)
            {
                float val = axis == 0 ? p.x : axis == 1 ? p.y : p.z;
                if (Mathf.Abs(val - edge) <= band) r = Mathf.Max(r, Vector3.Distance(p, c));
            }

            if (pass == 0) { endLow = c; rLow = r; }
            else { endHigh = c; rHigh = r; }
        }

        if (rLow < 0f || rHigh < 0f) return;

        // ด้านที่มีหน้าแปลน = วงใหญ่ = ทางออก, วงเล็ก = ปากท่อ
        if (rLow <= rHigh) { mouth = endLow; exit = endHigh; tubeRadius = rLow; }
        else { mouth = endHigh; exit = endLow; tubeRadius = rHigh; }

        // --- เส้นกลางท่อ: ไล่หาจุดกึ่งกลางของวงตัดทีละช่วง ---
        spine = FitCenterline(wp, mouth, exit, 25, 8);
        corner = spine[spine.Length / 2];

        if (bulge != null) bulge.SetSpine(spine);

        configured = true;
    }

    /// <summary>
    /// หาเส้นกลางท่อจาก mesh เอง: เริ่มจากเส้นตรง mouth→exit แล้วเลื่อนแต่ละจุด
    /// ไปยัง centroid ของวงตัด (สไลซ์ตามแนว tangent) ทำซ้ำจนเข้าที่
    /// </summary>
    private static Vector3[] FitCenterline(Vector3[] wp, Vector3 mouth, Vector3 exit, int samples, int iterations)
    {
        const float slab = 0.14f;

        var sp = new Vector3[samples];
        for (int i = 0; i < samples; i++) sp[i] = Vector3.Lerp(mouth, exit, i / (float)(samples - 1));

        for (int iter = 0; iter < iterations; iter++)
        {
            var next = new Vector3[samples];

            for (int i = 0; i < samples; i++)
            {
                Vector3 tangent = sp[Mathf.Min(i + 1, samples - 1)] - sp[Mathf.Max(i - 1, 0)];
                if (tangent.sqrMagnitude < 1e-8f) { next[i] = sp[i]; continue; }
                tangent.Normalize();

                Vector3 sum = Vector3.zero;
                float wsum = 0f;

                for (int v = 0; v < wp.Length; v++)
                {
                    Vector3 rel = wp[v] - sp[i];
                    float along = Vector3.Dot(rel, tangent);
                    if (Mathf.Abs(along) > slab) continue;

                    float w = 1f - Mathf.Abs(along) / slab;
                    sum += (wp[v] - tangent * along) * w;
                    wsum += w;
                }

                next[i] = wsum > 3f ? sum / wsum : sp[i];
            }

            next[0] = mouth;
            next[samples - 1] = exit;

            for (int i = 1; i < samples - 1; i++)
                sp[i] = (next[i - 1] + next[i] * 2f + next[i + 1]) * 0.25f;

            sp[0] = mouth;
            sp[samples - 1] = exit;
        }

        return sp;
    }

    private Vector3 SampleSpine(float t)
    {
        if (spine == null || spine.Length < 2) return Vector3.Lerp(mouth, exit, t);

        float f = Mathf.Clamp01(t) * (spine.Length - 1);
        int i = Mathf.Min(Mathf.FloorToInt(f), spine.Length - 2);
        return Vector3.Lerp(spine[i], spine[i + 1], f - i);
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
        // ปลด physics ออกก่อน เพื่อ animate กล่องเอง
        var rb = package.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
        foreach (var c in package.GetComponentsInChildren<Collider>()) c.enabled = false;

        // ปลดจาก drop point ที่ถืออยู่
        var dp = BaseDropPoint.GetDropPointHolding(package);
        if (dp != null && dp.currentPlacedItem == package) dp.currentPlacedItem = null;

        Vector3 start = package.transform.position;
        Vector3 startScale = package.transform.localScale;
        Quaternion startRot = package.transform.rotation;

        if (bulge != null)
        {
            bulge.radius = Mathf.Max(bulgeRadius, tubeRadius * 1.2f);
            bulge.amount = 0f;
            bulge.SetPoint(mouth);
        }

        // 1) ดูดเข้าปาก + ปากท่อเริ่มรัดตอนกล่องใกล้ถึง
        float t = 0f;
        while (t < pullDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / pullDuration);
            float e = k * k;   // เร่งขึ้นเหมือนแรงดูด

            package.transform.position = Vector3.Lerp(start, mouth, e);
            package.transform.localScale = Vector3.Lerp(startScale, startScale * entryScale, e);
            package.transform.rotation = Quaternion.Slerp(startRot, Quaternion.LookRotation((exit - mouth).normalized), e * 0.5f);

            if (bulge != null && gulpPinch > 0f)
            {
                float p = Mathf.InverseLerp(0.55f, 1f, k);
                bulge.amount = -gulpPinch * p * p;
                bulge.SetPoint(mouth);
            }

            yield return null;
        }

        package.SetActive(false);

        // 2) กลืน: ค้างรัดนิดนึง → คลายพร้อมปล่อยลูกป่องออกจากปาก
        if (bulge != null)
        {
            float hold = Mathf.Max(0f, gulpHold);
            while (hold > 0f)
            {
                hold -= Time.deltaTime;
                yield return null;
            }

            float release = Mathf.Max(0.01f, gulpRelease);
            float rel = 0f;

            while (rel < release)
            {
                rel += Time.deltaTime;
                float blend = Mathf.Clamp01(rel / release);
                bulge.amount = Mathf.Lerp(-gulpPinch, bulgeAmount, blend);
                bulge.SetPoint(mouth);
                yield return null;
            }

            bulge.amount = bulgeAmount;
        }

        // 3) ลูกป่องวิ่งไปตามเส้นกลางท่อ
        float travel = 0f;
        while (travel < travelDuration)
        {
            travel += Time.deltaTime;
            float k = Mathf.Clamp01(travel / travelDuration);
            float e = Mathf.SmoothStep(0f, 1f, k);

            if (bulge != null) bulge.SetPoint(SampleSpine(e));
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

        if (spine != null && spine.Length > 1)
        {
            Gizmos.color = Color.white;
            for (int i = 0; i < spine.Length - 1; i++) Gizmos.DrawLine(spine[i], spine[i + 1]);
        }
    }
}

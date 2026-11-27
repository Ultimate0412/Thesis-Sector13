using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pool กลางสำหรับเอฟเฟกต์ที่ต้องเกิดซ้ำๆ (เช่น เอฟเฟกต์ตอนพุ่งชน)
/// หลักการ: สร้าง instance ไว้ล่วงหน้าครั้งเดียว แล้วสลับ "เปิด/ปิด" แทนการ Instantiate/Destroy
/// - Spawn()   = หยิบ instance ที่ปิดอยู่ -> ย้ายตำแหน่ง -> SetActive(true)
/// - Despawn() = SetActive(false) -> คืนเข้าคิว
/// แยกคิวตาม prefab ทำให้ใช้กับ VFX ตัวไหนก็ได้ในอนาคต
/// </summary>
[DisallowMultipleComponent]
public sealed class VfxPool : MonoBehaviour
{
    private static VfxPool instance;

    /// <summary>คืน pool ในฉาก (สร้างให้อัตโนมัติถ้ายังไม่มี)</summary>
    public static VfxPool Instance => instance != null ? instance : EnsureExists();

    /// <summary>คืน pool เฉพาะกรณีที่มีอยู่แล้ว (ไม่สร้างใหม่) — ใช้ตอนคืนของเข้า pool</summary>
    public static VfxPool Existing => instance;

    [Header("Pool Settings")]
    [Tooltip("จำนวน instance ที่สร้างรอไว้ต่อ prefab หนึ่งตัว (สร้างตอนเริ่มเกม ไม่มี alloc ตอนเล่น)")]
    [Min(0)] public int prewarmPerPrefab = 3;
    [Tooltip("เพดานจำนวน instance ต่อ prefab (กัน pool บวมถ้าเกิดถี่ผิดปกติ)")]
    [Min(1)] public int maxPerPrefab = 12;

    private readonly Dictionary<GameObject, Stack<GameObject>> freeByPrefab = new Dictionary<GameObject, Stack<GameObject>>();
    private readonly Dictionary<GameObject, GameObject> prefabByInstance = new Dictionary<GameObject, GameObject>();
    private readonly Dictionary<GameObject, int> totalByPrefab = new Dictionary<GameObject, int>();

    private Transform poolRoot;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        poolRoot = transform;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // ------------------------------------------------------------ Public API

    /// <summary>หา pool ในฉาก ถ้าไม่มีให้สร้าง GameObject ใหม่ให้</summary>
    public static VfxPool EnsureExists()
    {
        if (instance != null) return instance;

        VfxPool found = FindAnyObjectByType<VfxPool>(FindObjectsInactive.Include);
        if (found != null)
        {
            instance = found;
            return found;
        }

        GameObject root = new GameObject("VfxPool (Runtime)");
        instance = root.AddComponent<VfxPool>();
        return instance;
    }

    /// <summary>สร้าง instance รอไว้ตามจำนวน default (prewarmPerPrefab)</summary>
    public void Prewarm(GameObject prefab)
    {
        Prewarm(prefab, prewarmPerPrefab);
    }

    /// <summary>สร้าง instance รอไว้ล่วงหน้า count ตัว (เรียกครั้งเดียวตอนเริ่มเกม)</summary>
    public void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null || count <= 0) return;

        for (int i = 0; i < count; i++)
        {
            if (GetTotal(prefab) >= maxPerPrefab) break;

            GameObject instanceObject = CreateInstance(prefab);
            instanceObject.SetActive(false);
            Push(prefab, instanceObject);
        }
    }

    /// <summary>ขอ instance มาใช้ (เปิดใช้งาน) — ไม่มีการ Instantiate ถ้า pool ยังมีของ</summary>
    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float scale = 1f)
    {
        if (prefab == null) return null;

        GameObject instanceObject = Take(prefab);
        if (instanceObject == null)
        {
            // เกินเพดาน: ปล่อยผ่านไปเลย ดีกว่าสร้างของถาวรเพิ่ม
            return null;
        }

        Transform t = instanceObject.transform;
        t.SetParent(poolRoot != null ? poolRoot : transform, false);
        t.SetPositionAndRotation(position, rotation);
        t.localScale = Vector3.one * Mathf.Max(0.0001f, scale);

        // SetActive(true) จะไปกระตุ้น OnEnable ของ PooledParticleVfx ให้ Clear + Play เอง
        instanceObject.SetActive(true);
        return instanceObject;
    }

    /// <summary>คืน instance เข้าคิว (ปิดการใช้งาน ไม่ทำลาย)</summary>
    public void Despawn(GameObject instanceObject)
    {
        if (instanceObject == null) return;

        // ถ้าปิดอยู่แล้วแปลว่าอยู่ในคิวแล้ว กันการ push ซ้ำ
        if (!instanceObject.activeSelf) return;

        instanceObject.SetActive(false);

        if (prefabByInstance.TryGetValue(instanceObject, out GameObject prefab) && prefab != null)
        {
            instanceObject.transform.SetParent(poolRoot != null ? poolRoot : transform, false);
            Push(prefab, instanceObject);
        }
    }

    // ------------------------------------------------------------ Internal

    private GameObject Take(GameObject prefab)
    {
        if (freeByPrefab.TryGetValue(prefab, out Stack<GameObject> stack))
        {
            while (stack.Count > 0)
            {
                GameObject candidate = stack.Pop();
                if (candidate != null) return candidate;
            }
        }

        if (GetTotal(prefab) >= maxPerPrefab) return null;

        return CreateInstance(prefab);
    }

    private void Push(GameObject prefab, GameObject instanceObject)
    {
        if (!freeByPrefab.TryGetValue(prefab, out Stack<GameObject> stack))
        {
            stack = new Stack<GameObject>();
            freeByPrefab[prefab] = stack;
        }

        stack.Push(instanceObject);
    }

    private GameObject CreateInstance(GameObject prefab)
    {
        GameObject instanceObject = Instantiate(prefab, poolRoot != null ? poolRoot : transform);
        instanceObject.name = prefab.name + " (Pooled)";

        // ปิดทันทีในเฟรมเดียวกับ Instantiate เพื่อไม่ให้ OnEnable (Clear+Play) ทำงานตอนยังไม่วางตำแหน่ง
        // แล้ว Spawn() ค่อย SetActive(true) อีกครั้ง -> ได้ OnEnable ที่ตำแหน่งถูกต้อง
        instanceObject.SetActive(false);

        prefabByInstance[instanceObject] = prefab;
        totalByPrefab[prefab] = GetTotal(prefab) + 1;

        return instanceObject;
    }

    private int GetTotal(GameObject prefab)
    {
        return totalByPrefab.TryGetValue(prefab, out int total) ? total : 0;
    }
}

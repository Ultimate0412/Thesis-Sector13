using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// รายการ prefab ที่ระบบเซฟใช้แปลง "ชื่อ prefab" กลับเป็น asset จริง
/// สร้าง/อัปเดตได้จากเมนู Tools/Sector13/Save/Rebuild Prefab Database
/// asset ต้องอยู่ที่ Assets/Resources/SavePrefabDatabase.asset
/// </summary>
[CreateAssetMenu(fileName = "SavePrefabDatabase", menuName = "Sector13/Save Prefab Database", order = 10)]
public class SavePrefabDatabase : ScriptableObject
{
    [Tooltip("prefab ทั้งหมดที่อาจถูกเก็บลง Storage (มี PackageBox หรือ ItemObject)")]
    public GameObject[] prefabs;

    private Dictionary<string, GameObject> lookup;
    private static SavePrefabDatabase cached;

    private void OnEnable()
    {
        lookup = null;
    }

    public GameObject Get(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName)) return null;

        EnsureLookup();
        return lookup.TryGetValue(prefabName, out GameObject prefab) ? prefab : null;
    }

    private void EnsureLookup()
    {
        if (lookup != null && lookup.Count > 0) return;

        lookup = new Dictionary<string, GameObject>();

        if (prefabs == null) return;

        foreach (GameObject prefab in prefabs)
        {
            if (prefab == null) continue;
            if (!lookup.ContainsKey(prefab.name))
            {
                lookup[prefab.name] = prefab;
            }
        }
    }

    /// <summary>โหลด database จาก Resources (cache ไว้)</summary>
    public static SavePrefabDatabase Load()
    {
        if (cached != null) return cached;

        cached = Resources.Load<SavePrefabDatabase>("SavePrefabDatabase");
        if (cached == null)
        {
            Debug.LogWarning("[SavePrefabDatabase] ไม่พบ Assets/Resources/SavePrefabDatabase.asset — กรุณารันเมนู Tools/Sector13/Save/Rebuild Prefab Database");
        }

        return cached;
    }
}

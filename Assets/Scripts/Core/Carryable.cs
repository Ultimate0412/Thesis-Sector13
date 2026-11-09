using UnityEngine;

/// <summary>
/// Resolver กลางสำหรับหา ICarryable ของ object ที่จะเป็น heldObject
///
/// กติกา:
/// 1) ไล่จากตัวเองขึ้นไปหาบรรพบุรุษ "ตัวแรก" ที่มี carryable component
///    (ของที่ถูกวางใต้ openSlot ของกล่องจะเลือกตัวเอง ไม่ใช่กล่อง parent)
/// 2) ภายใน GameObject เดียวกัน เลือกชนิดตามลำดับคงที่ ห้ามสลับ
/// 3) ห้ามใช้ GetComponent&lt;ICarryable&gt;() เพราะลำดับ component ใน prefab ไม่รับประกัน
/// </summary>
public static class Carryable
{
    public static ICarryable Resolve(GameObject go)
    {
        if (go == null) return null;

        for (Transform t = go.transform; t != null; t = t.parent)
        {
            ICarryable c = Pick(t.gameObject);
            if (c != null) return c;
        }

        return null;
    }

    private static ICarryable Pick(GameObject go)
    {
        // ลำดับนี้คือสัญญา ห้ามสลับ
        if (go.TryGetComponent(out PackageBox box)) return box;        // กล่อง = ตัวที่ถูกถือ (ไม่ใช่ของข้างใน)
        if (go.TryGetComponent(out EnemyBase enemy)) return enemy;
        if (go.TryGetComponent(out MimicPackage mimic)) return mimic;
        if (go.TryGetComponent(out ItemObject item)) return item;      // ไอเท็มหลวม
        if (go.TryGetComponent(out MeleeWeapon weapon)) return weapon; // อาวุธ (D2: น้ำหนัก 0)
        return null;
    }

    public static float WeightOf(GameObject go)
    {
        ICarryable c = Resolve(go);
        return c != null ? c.Weight : 0f;
    }
}

using UnityEngine;

/// <summary>
/// แปะไว้บน root ของ prefab VFX ที่ถูกใช้ผ่าน VfxPool
/// - OnEnable  : ล้างเศษ particle เก่าแล้วเล่นใหม่ (ทุกครั้งที่ถูกหยิบจาก pool มาใช้)
/// - OnParticleSystemStopped : เอฟเฟกต์จบ -> คืนตัวเองเข้า pool (ปิด ไม่ทำลาย)
///
/// ต้องตั้ง ParticleSystem (ตัวที่อยู่บน GameObject เดียวกัน) ให้ stopAction = Callback
/// ไม่งั้นฟังก์ชัน OnParticleSystemStopped จะไม่ถูกเรียก
/// </summary>
[DisallowMultipleComponent]
public sealed class PooledParticleVfx : MonoBehaviour
{
    private ParticleSystem[] systems;
    private bool despawned;

    private void Awake()
    {
        CacheSystems();
    }

    private void OnEnable()
    {
        despawned = false;

        if (systems == null || systems.Length == 0)
        {
            CacheSystems();
        }

        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem ps = systems[i];
            if (ps == null) continue;

            // ล้าง particle ค้างจากรอบก่อน แล้วเริ่มเล่นใหม่ตั้งแต่ต้น
            ps.Clear(false);
            ps.Play(false);
        }
    }

    private void OnParticleSystemStopped()
    {
        if (despawned) return;
        despawned = true;

        VfxPool pool = VfxPool.Existing;
        if (pool != null)
        {
            pool.Despawn(gameObject);
            return;
        }

        // ไม่มี pool ในฉาน (เช่น เพลย์ prefab ตรงๆ ใน editor) -> ปิดตัวเองไว้
        gameObject.SetActive(false);
    }

    private void CacheSystems()
    {
        systems = GetComponentsInChildren<ParticleSystem>(true);
    }
}

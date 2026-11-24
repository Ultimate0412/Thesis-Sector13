using UnityEngine;

/// <summary>
/// ลบ GameObject ของเอฟเฟกต์อัตโนมัติหลังเล่นครบตามเวลาที่กำหนด
/// ใช้กับ VFX Prefab ที่เกิดชั่วคราว (เช่น ควันตอนศัตรูเกิด) เพื่อไม่ให้ Object ค้างในฉาก
/// </summary>
public class VfxAutoDestroy : MonoBehaviour
{
    [Tooltip("อายุของเอฟเฟกต์ก่อนถูกลบ (วินาที)")]
    [Min(0.1f)] public float lifetime = 3f;

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void OnValidate()
    {
        if (lifetime < 0.1f) lifetime = 0.1f;
    }
}

/// <summary>
/// สิ่งที่ผู้เล่นหยิบ/แบกได้ 1 อย่าง = 1 น้ำหนักทางกายภาพ (kg)
/// นี่คือ source of truth เดียวของน้ำหนัก — ห้ามคำนวณซ้ำที่อื่น
/// </summary>
public interface ICarryable
{
    /// <summary>น้ำหนักทางกายภาพ (kg) — ต้อง >= 0</summary>
    float Weight { get; }
}

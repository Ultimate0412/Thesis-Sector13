using UnityEngine;


public class HeldAnchor : MonoBehaviour
{
    [Header("จุดจับ (แนะนำ)")]
    [Tooltip("EmptyObject ลูกของวัตถุนี้ ใช้เป็นจุดที่มือผู้เล่นจับ (ถ้าเว้นว่างจะใช้ค่าตัวเลขด้านล่าง)")]
    public Transform anchorPoint;

    [Header("ค่าสำรอง (ใช้เมื่อไม่ได้กำหนด Anchor Point)")]
    [Tooltip("ตำแหน่งเมื่อถูกถือ (local เทียบกับจุดถือของผู้เล่น)")]
    public Vector3 localPosition = Vector3.zero;

    [Tooltip("การหมุนเมื่อถูกถือ (องศา)")]
    public Vector3 localEulerAngles = Vector3.zero;
}

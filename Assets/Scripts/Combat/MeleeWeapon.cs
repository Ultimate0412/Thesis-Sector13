using UnityEngine;
public class MeleeWeapon : MonoBehaviour, ICarryable
{
    [Header("Weapon Stats")]
    [Tooltip("ชื่ออาวุธ")]
    public string weaponName = "Wooden Stick";

    [HideInInspector]
    [Tooltip("น้ำหนักของอาวุธเมื่อหยิบถือ (kg)")]
    public float weaponWeight = 2f;


    public float Weight => 0f;

    [Tooltip("ปริมาณเกจสตั๊นที่เพิ่มให้กับศัตรูต่อการตี 1 ครั้ง ")]
    public float stunDamagePerHit = 35f;

    [Tooltip("ระยะการโจมตี (เมตร)")]
    public float attackRange = 2.4f;

    [Tooltip("เวลาระหว่างการโจมตีแต่ละครั้ง (วินาที)")]
    public float attackCooldown = 0.45f;

    [Header("Audio & Feedback")]
    public AudioClip swingSound;
    public AudioClip hitEnemySound;
    public AudioClip hitWallSound;

    [Header("Procedural Swing Settings")]
    [Tooltip("มุมเอียงของอาวุธตอนแกว่งตี")]
    public Vector3 swingRotationOffset = new Vector3(45f, -30f, 20f);
    [Tooltip("ระยะที่อาวุธขยับไปข้างหน้าตอนตี")]
    public Vector3 swingPositionOffset = new Vector3(0.1f, -0.1f, 0.25f);
}

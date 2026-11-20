using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ชุดบทสนทนาที่ใช้ซ้ำได้ (เช่น บทสนทนา NPC, Tutorial, เหตุการณ์พิเศษ)
/// ใช้กับ DialogueManager.Play(sequence)
/// </summary>
[CreateAssetMenu(fileName = "DialogueSequence", menuName = "Sector13/Dialogue Sequence", order = 1)]
public class DialogueSequence : ScriptableObject
{
    [Header("Speaker")]
    [Tooltip("ชื่อผู้พูดหลักของชุดบทสนทนานี้")]
    public string speakerName = "";

    [Tooltip("รูปผู้พูดหลักของชุดบทสนทนานี้")]
    public Sprite portrait;

    [Header("Timing")]
    [Tooltip("ความเร็วพิมพ์เริ่มต้น (ตัวอักษร/วินาที)")]
    [Min(1f)] public float charactersPerSecond = 35f;

    [Tooltip("เวลารอหลังพิมพ์จบก่อนไปบรรทัดถัดไป (วินาที)")]
    [Min(0f)] public float holdDuration = 2.5f;

    [Header("Lines")]
    [Tooltip("รายการบรรทัดบทสนทนา")]
    public List<DialogueLine> lines = new List<DialogueLine>();
}

using System;
using UnityEngine;

/// <summary>
/// 1 บรรทัดของบทสนทนา ใช้ได้ทั้งใน DayConfig และ DialogueSequence
/// ค่าที่ปล่อยเป็น 0 / ว่าง จะถูกแทนด้วยค่า default ของชุดบทสนทนา
/// </summary>
[Serializable]
public class DialogueLine
{
    [Tooltip("ชื่อผู้พูดของบรรทัดนี้ (เว้นว่าง = ใช้ชื่อหลักของชุดบทสนทนา)")]
    public string speakerName;

    [TextArea(2, 5)]
    [Tooltip("ข้อความที่พิมพ์ออกมา")]
    public string text;

    [Tooltip("รูปผู้พูดของบรรทัดนี้ (เว้นว่าง = ใช้รูปหลัก)")]
    public Sprite portrait;

    [Tooltip("ความเร็วพิมพ์ (ตัวอักษร/วินาที) — 0 = ใช้ค่า default")]
    [Min(0f)] public float charactersPerSecond;

    [Tooltip("เวลารอหลังพิมพ์จบก่อนไปบรรทัดถัดไป (วินาที) — 0 = ใช้ค่า default")]
    [Min(0f)] public float holdDuration;

    [Header("Voice Over")]
    [Tooltip("ไฟล์เสียงพากย์ของบรรทัดนี้ (เว้นว่าง = ไม่มีเสียง)")]
    public AudioClip voiceClip;

    [Range(0f, 1f)]
    [Tooltip("ระดับเสียงพากย์")]
    public float voiceVolume = 1f;

    [Tooltip("รอให้เสียงพากย์เล่นจบก่อนไปบรรทัดถัดไป")]
    public bool waitForVoiceToFinish = true;
}

using System;
using UnityEngine;

public enum TraceType
{
    ClawMarks,
    OilStain,
    WaterStain,
    CardboardScuff,
    AlienSlime_UV,
    ChemicalResidue_UV
}

public enum TraceVisibility
{
    NakedEye,
    RequiresUV
}

[System.Serializable]
public class CategoryTraceSettings
{
    [Header("Probability & Count")]
    [Tooltip("โอกาสที่จะมีร่องรอยบนกล่องประเภทนี้ (0 - 100%)")]
    [Range(0f, 100f)]
    public float spawnChancePercent = 80f;

    [Tooltip("จำนวนร่องรอยขั้นต่ำที่สุ่มเกิด")]
    [Range(0, 8)]
    public int minTraces = 1;

    [Tooltip("จำนวนร่องรอยสูงสุดที่สุ่มเกิด")]
    [Range(0, 8)]
    public int maxTraces = 3;

    [Header("UV Visibility Ratio")]
    [Tooltip("สัดส่วนโอกาสที่จะเป็นรอยแบบต้องใช้ไฟ UV เทียบกับรอยตาเปล่า (0% = ตาเปล่าล้วน, 100% = UV ล้วน)")]
    [Range(0f, 100f)]
    public float uvTraceRatioPercent = 50f;

    public CategoryTraceSettings(float spawnChance, int min, int max, float uvRatio)
    {
        spawnChancePercent = spawnChance;
        minTraces = min;
        maxTraces = max;
        uvTraceRatioPercent = uvRatio;
    }
}

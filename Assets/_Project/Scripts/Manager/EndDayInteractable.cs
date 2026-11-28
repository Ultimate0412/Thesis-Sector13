using UnityEngine;

/// <summary>
/// Interactable object that opens the daily summary / end-day flow.
/// Attach to a world object with a Collider (terminal, desk, wall button, etc.).
/// </summary>
public class EndDayInteractable : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [Tooltip("ข้อความที่แสดงตอนเล็ง object นี้")]
    [SerializeField] private string prompt = "[E] End Day";

    [Tooltip("ซ่อน prompt หลังจบ run แล้ว")]
    [SerializeField] private bool hidePromptWhenRunComplete = true;

    public string GetInteractPrompt()
    {
        if (GameDayManager.Instance == null) return prompt;

        if (hidePromptWhenRunComplete && GameDayManager.Instance.IsRunComplete)
        {
            return string.Empty;
        }

        return prompt;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (GameDayManager.Instance == null) return;

        // ใช้ flow เดียวกับปุ่ม END DAY บน HUD:
        // ตรวจเงื่อนไข -> เปิด Daily Summary หรือแจ้งเหตุที่ยังจบวันไม่ได้
        GameDayManager.Instance.RequestEndDay();
    }
}

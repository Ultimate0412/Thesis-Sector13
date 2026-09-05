using TMPro;
using UnityEngine;
using static Unity.Burst.Intrinsics.Arm;

public class ScoreUI : MonoBehaviour
{
    [Header("UI Reference")]
    public TextMeshProUGUI scoreText;

    private void Start()
    {
        // ย้ายมาไว้ที่ Start เพื่อให้มั่นใจว่า ScoreManager ถูกสร้างและพร้อมใช้งานแล้ว
        if (ScoreManager.Instance != null)
        {
            // อัปเดตแสดงผลคะแนนเริ่มต้นทันที
            UpdateScoreText(ScoreManager.Instance.totalScore);

            // สมัครรับ Event การเปลี่ยนแปลงคะแนน
            ScoreManager.Instance.OnScoreChanged += UpdateScoreText;
        }
        else
        {
            Debug.LogError("ไม่พบ ScoreManager.Instance ในฉาก!");
        }
    }

    private void OnDestroy()
    {
        // ยกเลิกการรับ Event เมื่อสคริปต์ถูกทำลาย
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnScoreChanged -= UpdateScoreText;
        }
    }

    private void UpdateScoreText(int newScore)
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + newScore.ToString();
        }
    }
}
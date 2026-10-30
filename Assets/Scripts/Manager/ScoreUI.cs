using TMPro;
using UnityEngine;

public class ScoreUI : MonoBehaviour
{
    [Header("UI Reference")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI RejectText;

    [Header("Open Quota UI Reference")]
    [Tooltip("TextMeshProUGUI สำหรับแสดงโควต้าการเปิดกล่องตรวจประจำวัน (หากเว้นว่างจะค้นหาหรือสร้างให้อัตโนมัติใต้ scoreText)")]
    public TextMeshProUGUI openQuotaText;

    [Tooltip("เปิดใช้งานการสร้าง UI แสดงโควต้าอัตโนมัติหากยังไม่ได้ลากใส่ใน Inspector")]
    public bool autoCreateQuotaUIIfMissing = true;

    [Tooltip("คำนำหน้าข้อความ เช่น 'Open Quota: ' หรือ 'โควต้าเปิดกล่อง: '")]
    public string quotaPrefix = "Open Quota: ";

    private void Start()
    {
        // ย้ายมาไว้ที่ Start เพื่อให้มั่นใจว่า ScoreManager ถูกสร้างและพร้อมใช้งานแล้ว
        if (ScoreManager.Instance != null)
        {
            // อัปเดตแสดงผลคะแนนเริ่มต้นทันที
            UpdateScoreText(ScoreManager.Instance.totalScore);
            UpdateRejectText(ScoreManager.Instance.rejectedPackagesCount);

            // สมัครรับ Event การเปลี่ยนแปลงคะแนน
            ScoreManager.Instance.OnScoreChanged += UpdateScoreText;
            ScoreManager.Instance.rejectedPackagesCountChanged += UpdateRejectText;
        }
        else
        {
            Debug.LogError("ไม่พบ ScoreManager.Instance ในฉาก!");
        }

        // เตรียม UI แสดงโควต้าการเปิดกล่องตรวจประจำวัน
        SetupOpenQuotaUI();

        if (GameDayManager.Instance != null)
        {
            UpdateOpenQuotaText(GameDayManager.Instance.RemainingQuotaToday);
            GameDayManager.Instance.OnQuotaChanged += UpdateOpenQuotaText;
            GameDayManager.Instance.OnDayStarted += HandleDayStarted;
        }
    }

    private void OnDestroy()
    {
        // ยกเลิกการรับ Event เมื่อสคริปต์ถูกทำลาย
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnScoreChanged -= UpdateScoreText;
            ScoreManager.Instance.rejectedPackagesCountChanged -= UpdateRejectText;
        }

        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.OnQuotaChanged -= UpdateOpenQuotaText;
            GameDayManager.Instance.OnDayStarted -= HandleDayStarted;
        }
    }

    private void HandleDayStarted(int newDay)
    {
        if (GameDayManager.Instance != null)
        {
            UpdateOpenQuotaText(GameDayManager.Instance.RemainingQuotaToday);
        }
    }

    private void SetupOpenQuotaUI()
    {
        if (openQuotaText != null) return;
        if (!autoCreateQuotaUIIfMissing) return;

        // 1. ค้นหาว่ามีลูกชื่อ OpenQuotaText อยู่แล้วหรือไม่
        Transform parentTransform = scoreText != null ? scoreText.transform.parent : transform;
        Transform existing = parentTransform.Find("OpenQuotaText");
        if (existing != null)
        {
            openQuotaText = existing.GetComponent<TextMeshProUGUI>();
            if (openQuotaText != null) return;
        }

        // 2. หากยังไม่มี และมี scoreText ให้ Clone จาก scoreText เพื่อให้ Font / Settings ตรงกัน
        if (scoreText != null)
        {
            GameObject quotaObj = Instantiate(scoreText.gameObject, scoreText.transform.parent);
            quotaObj.name = "OpenQuotaText";
            RectTransform rect = quotaObj.GetComponent<RectTransform>();
            RectTransform scoreRect = scoreText.GetComponent<RectTransform>();
            if (rect != null && scoreRect != null)
            {
                rect.anchoredPosition = scoreRect.anchoredPosition + new Vector2(0f, -44f);
                rect.sizeDelta = new Vector2(scoreRect.sizeDelta.x + 100f, scoreRect.sizeDelta.y);
            }

            openQuotaText = quotaObj.GetComponent<TextMeshProUGUI>();
            if (openQuotaText != null)
            {
                openQuotaText.color = new Color(0.35f, 0.9f, 1f, 1f); // สีฟ้าอ่อนคมชัด
            }
        }
    }

    private void UpdateScoreText(int newScore)
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + newScore.ToString();
        }
    }

    private void UpdateRejectText(int newReject)
    {
        if (RejectText != null)
        {
            RejectText.text = newReject.ToString();
        }    
    }

    public void UpdateOpenQuotaText(int remaining)
    {
        if (openQuotaText == null)
        {
            SetupOpenQuotaUI();
            if (openQuotaText == null) return;
        }

        int total = GameDayManager.Instance != null ? GameDayManager.Instance.dailyOpenQuota : remaining;

        if (remaining <= 0)
        {
            openQuotaText.text = $"{quotaPrefix}<color=#FF4444>0/{total}</color>";
        }
        else if (remaining <= 3)
        {
            openQuotaText.text = $"{quotaPrefix}<color=#FFCC00>{remaining}/{total}</color>";
        }
        else
        {
            openQuotaText.text = $"{quotaPrefix}<color=#55FF88>{remaining}</color>/{total}";
        }
    }
}

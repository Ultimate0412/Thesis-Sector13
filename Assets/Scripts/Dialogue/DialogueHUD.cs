using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD แสดงบทสนทนาแบบบับเบิ้ลล่างกลางจอ
/// - เด้งขึ้น (pop/bounce) พร้อม fade in
/// - พิมพ์ข้อความแบบ typewriter (ใช้ maxVisibleCharacters ของ TMP)
/// - มีช่อง Portrait และป้ายชื่อผู้พูด
/// - เล่นเสียงพากย์ (Voice Over) ต่อบรรทัด และรอเสียงจบได้
/// ตัวควบคุมลำดับบทสนทนาอยู่ที่ DialogueManager
/// </summary>
public class DialogueHUD : MonoBehaviour
{
    public static DialogueHUD Instance { get; private set; }

    [Header("Root")]
    [Tooltip("แผงบับเบิ้ลบทสนทนา (จะถูกซ่อนเมื่อไม่ได้ใช้งาน)")]
    public GameObject dialoguePanel;

    [Tooltip("CanvasGroup ของแผง (ใช้ fade in/out)")]
    public CanvasGroup canvasGroup;

    [Tooltip("RectTransform ที่ใช้ทำอนิเมชันเด้ง (ปกติคือตัวแผงเอง)")]
    public RectTransform bubbleRoot;

    [Header("Content")]
    public Image portraitFrame;
    public Image portraitImage;
    public GameObject speakerNamePlate;
    public TextMeshProUGUI speakerNameText;
    public TextMeshProUGUI dialogueText;

    [Header("Animation")]
    [Tooltip("ระยะเวลาเด้งขึ้น (วินาที)")]
    [Min(0.05f)] public float popInDuration = 0.28f;

    [Tooltip("ระยะเวลาจางหายตอนปิด (วินาที)")]
    [Min(0.05f)] public float fadeOutDuration = 0.18f;

    [Range(0.5f, 1f)]
    [Tooltip("ขนาดเริ่มต้นก่อนเด้งขึ้น")]
    public float hiddenScale = 0.8f;

    [Header("Audio")]
    [Tooltip("AudioSource สำหรับเสียงพากย์ (2D) — เว้นว่างจะสร้างให้อัตโนมัติ")]
    public AudioSource voiceSource;

    [Header("Auto Setup")]
    [Tooltip("สร้าง UI ให้อัตโนมัติหากยังไม่ได้ผูก reference")]
    public bool autoCreateIfMissing = true;

    [Header("Layout")]
    [Tooltip("ระยะเยื้องซ้ายของข้อความเมื่อมีช่อง Portrait")]
    [Min(0f)] public float portraitTextInset = 176f;

    [Tooltip("ระยะเยื้องซ้ายของข้อความเมื่อไม่มีรูปผู้พูด")]
    [Min(0f)] public float noPortraitTextInset = 28f;

    private Coroutine punchCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        EnsureUIHierarchy();

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ------------------------------------------------------------------
    // API ที่ DialogueManager เรียกใช้
    // ------------------------------------------------------------------

    /// <summary>เด้งบับเบิ้ลขึ้นมา</summary>
    public IEnumerator ShowBubbleRoutine()
    {
        EnsureUIHierarchy();
        if (dialoguePanel == null || canvasGroup == null) yield break;

        dialoguePanel.SetActive(true);
        dialoguePanel.transform.SetAsLastSibling();

        float duration = Mathf.Max(0.05f, popInDuration);
        float elapsed = 0f;
        canvasGroup.alpha = 0f;
        if (bubbleRoot != null) bubbleRoot.localScale = Vector3.one * hiddenScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / duration);

            canvasGroup.alpha = k;
            if (bubbleRoot != null)
            {
                // EaseOutBack ให้ความรู้สึก "เด้ง" ตอนจบ
                float s = Mathf.LerpUnclamped(hiddenScale, 1f, EaseOutBack(k));
                bubbleRoot.localScale = Vector3.one * s;
            }

            yield return null;
        }

        canvasGroup.alpha = 1f;
        if (bubbleRoot != null) bubbleRoot.localScale = Vector3.one;
    }

    /// <summary>ซ่อนบับเบิ้ล (fade + ย่อลงเล็กน้อย)</summary>
    public IEnumerator HideBubbleRoutine()
    {
        if (dialoguePanel == null || canvasGroup == null) yield break;

        float duration = Mathf.Max(0.05f, fadeOutDuration);
        float elapsed = 0f;
        float startAlpha = canvasGroup.alpha;
        Vector3 startScale = bubbleRoot != null ? bubbleRoot.localScale : Vector3.one;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / duration);

            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, k);
            if (bubbleRoot != null)
            {
                bubbleRoot.localScale = Vector3.Lerp(startScale, Vector3.one * 0.94f, k);
            }

            yield return null;
        }

        canvasGroup.alpha = 0f;
        if (bubbleRoot != null) bubbleRoot.localScale = Vector3.one;
        dialoguePanel.SetActive(false);
    }

    /// <summary>
    /// แสดง 1 บรรทัด: ตั้งชื่อ/รูป → พิมพ์ → รอเสียงจบ → รอ holdDuration
    /// </summary>
    public IEnumerator PlayLineRoutine(
        DialogueLine line,
        string defaultSpeaker,
        Sprite defaultPortrait,
        float defaultCharactersPerSecond,
        float defaultHoldDuration)
    {
        if (line == null) yield break;

        EnsureUIHierarchy();

        string speaker = !string.IsNullOrWhiteSpace(line.speakerName) ? line.speakerName : defaultSpeaker;
        Sprite portrait = line.portrait != null ? line.portrait : defaultPortrait;
        float charactersPerSecond = line.charactersPerSecond > 0f ? line.charactersPerSecond : defaultCharactersPerSecond;
        float holdDuration = line.holdDuration > 0f ? line.holdDuration : defaultHoldDuration;

        ApplySpeaker(speaker);
        ApplyPortrait(portrait);

        if (dialogueText != null)
        {
            dialogueText.text = line.text;
            dialogueText.maxVisibleCharacters = 0;
            dialogueText.ForceMeshUpdate();

            int totalCharacters = dialogueText.textInfo.characterCount;

            if (punchCoroutine != null) StopCoroutine(punchCoroutine);
            punchCoroutine = StartCoroutine(PunchRoutine());

            PlayVoice(line);

            float visibleCharacters = 0f;
            while (visibleCharacters < totalCharacters)
            {
                visibleCharacters += Mathf.Max(1f, charactersPerSecond) * Time.deltaTime;
                dialogueText.maxVisibleCharacters = Mathf.Min(totalCharacters, Mathf.FloorToInt(visibleCharacters));
                yield return null;
            }

            dialogueText.maxVisibleCharacters = totalCharacters;
        }

        // รอเสียงพากย์จบ (ถ้าเปิดไว้และมีเสียงจริง)
        if (line.waitForVoiceToFinish && line.voiceClip != null && voiceSource != null)
        {
            while (voiceSource != null && voiceSource.isPlaying)
            {
                yield return null;
            }
        }

        if (holdDuration > 0f)
        {
            yield return new WaitForSeconds(holdDuration);
        }

        StopVoice();
    }

    /// <summary>หยุดเสียงพากย์ทันที</summary>
    public void StopVoice()
    {
        if (voiceSource != null)
        {
            voiceSource.Stop();
        }
    }

    /// <summary>ปิดบับเบิ้ลทันที (ไม่เล่นอนิเมชัน)</summary>
    public void StopImmediate()
    {
        StopAllCoroutines();
        punchCoroutine = null;
        StopVoice();

        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (bubbleRoot != null) bubbleRoot.localScale = Vector3.one;
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
    }

    // ------------------------------------------------------------------
    // ภายใน
    // ------------------------------------------------------------------

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }

    private void ApplySpeaker(string speaker)
    {
        bool hasSpeaker = !string.IsNullOrWhiteSpace(speaker);

        if (speakerNamePlate != null) speakerNamePlate.SetActive(hasSpeaker);
        if (speakerNameText != null) speakerNameText.text = hasSpeaker ? speaker : string.Empty;
    }

    private void ApplyPortrait(Sprite portrait)
    {
        bool hasPortrait = portrait != null;

        if (portraitFrame != null) portraitFrame.gameObject.SetActive(hasPortrait);
        if (portraitImage != null)
        {
            portraitImage.sprite = portrait;
            portraitImage.enabled = hasPortrait;
        }

        // ไม่มีรูป → ดึงข้อความชิดซ้าย เพื่อไม่ให้เหลือช่องว่างของ portrait
        if (dialogueText != null)
        {
            RectTransform textRect = dialogueText.rectTransform;
            Vector2 offsetMin = textRect.offsetMin;
            offsetMin.x = hasPortrait ? portraitTextInset : noPortraitTextInset;
            textRect.offsetMin = offsetMin;
        }
    }

    private void PlayVoice(DialogueLine line)
    {
        if (voiceSource == null) return;

        voiceSource.Stop();

        if (line.voiceClip == null)
        {
            voiceSource.clip = null;
            return;
        }

        voiceSource.clip = line.voiceClip;
        voiceSource.volume = Mathf.Clamp01(line.voiceVolume);
        voiceSource.Play();
    }

    private IEnumerator PunchRoutine()
    {
        if (bubbleRoot == null) yield break;

        const float duration = 0.12f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / duration);
            bubbleRoot.localScale = Vector3.one * (1f + 0.02f * Mathf.Sin(k * Mathf.PI));
            yield return null;
        }

        bubbleRoot.localScale = Vector3.one;
        punchCoroutine = null;
    }

    /// <summary>
    /// ตรวจและสร้างโครงสร้าง UI ให้อัตโนมัติหากยังไม่ได้ผูก reference
    /// (ใช้ทั้งตอน runtime และตอนสร้าง prefab)
    /// </summary>
    public void EnsureUIHierarchy()
    {
        if (dialoguePanel != null)
        {
            if (canvasGroup == null) canvasGroup = dialoguePanel.GetComponent<CanvasGroup>();
            if (bubbleRoot == null) bubbleRoot = dialoguePanel.GetComponent<RectTransform>();
            EnsureVoiceSource();
            return;
        }

        if (!autoCreateIfMissing) return;

        Canvas targetCanvas = GetComponentInParent<Canvas>();
        if (targetCanvas == null) targetCanvas = FindFirstObjectByType<Canvas>();

        if (targetCanvas == null)
        {
            GameObject canvasObj = new GameObject("DialogueCanvas");
            targetCanvas = canvasObj.AddComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // --- แผงบับเบิ้ลหลัก ---
        GameObject panelObj = new GameObject("DialoguePanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panelObj.transform.SetParent(targetCanvas.transform, false);

        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.sizeDelta = new Vector2(760f, 180f);
        panelRect.anchoredPosition = new Vector2(0f, 48f);

        Image background = panelObj.GetComponent<Image>();
        background.color = new Color(0.04f, 0.07f, 0.12f, 0.95f);
        background.raycastTarget = false;

        canvasGroup = panelObj.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        dialoguePanel = panelObj;
        bubbleRoot = panelRect;

        // --- เส้น accent ด้านบน ---
        GameObject accentObj = new GameObject("AccentBar", typeof(RectTransform), typeof(Image));
        accentObj.transform.SetParent(panelObj.transform, false);
        RectTransform accentRect = accentObj.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 1f);
        accentRect.anchorMax = new Vector2(1f, 1f);
        accentRect.pivot = new Vector2(0.5f, 1f);
        accentRect.sizeDelta = new Vector2(0f, 3f);
        accentRect.anchoredPosition = Vector2.zero;
        Image accentImage = accentObj.GetComponent<Image>();
        accentImage.color = new Color(0.2f, 0.85f, 1f);
        accentImage.raycastTarget = false;

        // --- ช่อง Portrait ---
        GameObject portraitObj = new GameObject("PortraitFrame", typeof(RectTransform), typeof(Image));
        portraitObj.transform.SetParent(panelObj.transform, false);
        RectTransform portraitRect = portraitObj.GetComponent<RectTransform>();
        portraitRect.anchorMin = new Vector2(0f, 0.5f);
        portraitRect.anchorMax = new Vector2(0f, 0.5f);
        portraitRect.pivot = new Vector2(0f, 0.5f);
        portraitRect.sizeDelta = new Vector2(140f, 140f);
        portraitRect.anchoredPosition = new Vector2(18f, 0f);
        portraitFrame = portraitObj.GetComponent<Image>();
        portraitFrame.color = new Color(0.08f, 0.16f, 0.24f, 1f);
        portraitFrame.raycastTarget = false;

        GameObject portraitImageObj = new GameObject("PortraitImage", typeof(RectTransform), typeof(Image));
        portraitImageObj.transform.SetParent(portraitObj.transform, false);
        RectTransform portraitImageRect = portraitImageObj.GetComponent<RectTransform>();
        portraitImageRect.anchorMin = Vector2.zero;
        portraitImageRect.anchorMax = Vector2.one;
        portraitImageRect.offsetMin = new Vector2(4f, 4f);
        portraitImageRect.offsetMax = new Vector2(-4f, -4f);
        portraitImage = portraitImageObj.GetComponent<Image>();
        portraitImage.preserveAspect = true;
        portraitImage.raycastTarget = false;

        // --- ป้ายชื่อผู้พูด ---
        GameObject plateObj = new GameObject("SpeakerNamePlate", typeof(RectTransform), typeof(Image));
        plateObj.transform.SetParent(panelObj.transform, false);
        RectTransform plateRect = plateObj.GetComponent<RectTransform>();
        plateRect.anchorMin = new Vector2(0f, 1f);
        plateRect.anchorMax = new Vector2(0f, 1f);
        plateRect.pivot = new Vector2(0f, 0f);
        plateRect.sizeDelta = new Vector2(220f, 34f);
        plateRect.anchoredPosition = new Vector2(170f, 6f);
        Image plateImage = plateObj.GetComponent<Image>();
        plateImage.color = new Color(0.1f, 0.35f, 0.5f, 1f);
        plateImage.raycastTarget = false;
        speakerNamePlate = plateObj;

        GameObject speakerTextObj = new GameObject("SpeakerNameText", typeof(RectTransform), typeof(TextMeshProUGUI));
        speakerTextObj.transform.SetParent(plateObj.transform, false);
        RectTransform speakerTextRect = speakerTextObj.GetComponent<RectTransform>();
        speakerTextRect.anchorMin = Vector2.zero;
        speakerTextRect.anchorMax = Vector2.one;
        speakerTextRect.offsetMin = new Vector2(10f, 0f);
        speakerTextRect.offsetMax = new Vector2(-10f, 0f);
        speakerNameText = speakerTextObj.GetComponent<TextMeshProUGUI>();
        speakerNameText.fontSize = 18f;
        speakerNameText.fontStyle = FontStyles.Bold;
        speakerNameText.alignment = TextAlignmentOptions.Left;
        speakerNameText.color = Color.white;
        speakerNameText.raycastTarget = false;
        speakerNameText.text = "ผู้พูด";

        // --- ข้อความบทสนทนา ---
        GameObject textObj = new GameObject("DialogueText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(panelObj.transform, false);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(portraitTextInset, 24f);
        textRect.offsetMax = new Vector2(-24f, -18f);
        dialogueText = textObj.GetComponent<TextMeshProUGUI>();
        dialogueText.fontSize = 22f;
        dialogueText.alignment = TextAlignmentOptions.Left;
        dialogueText.color = new Color(0.9f, 0.95f, 1f);
        dialogueText.raycastTarget = false;
        dialogueText.text = "...";

        // --- หางบับเบิ้ล ---
        GameObject tailObj = new GameObject("BubbleTail", typeof(RectTransform), typeof(Image));
        tailObj.transform.SetParent(panelObj.transform, false);
        RectTransform tailRect = tailObj.GetComponent<RectTransform>();
        tailRect.anchorMin = new Vector2(0f, 0f);
        tailRect.anchorMax = new Vector2(0f, 0f);
        tailRect.pivot = new Vector2(0.5f, 0.5f);
        tailRect.sizeDelta = new Vector2(26f, 26f);
        tailRect.anchoredPosition = new Vector2(60f, -8f);
        tailRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Image tailImage = tailObj.GetComponent<Image>();
        tailImage.color = new Color(0.04f, 0.07f, 0.12f, 0.95f);
        tailImage.raycastTarget = false;

        EnsureVoiceSource();
        panelObj.SetActive(false);
    }

    private void EnsureVoiceSource()
    {
        if (voiceSource != null) return;

        voiceSource = GetComponent<AudioSource>();
        if (voiceSource == null)
        {
            voiceSource = gameObject.AddComponent<AudioSource>();
        }

        voiceSource.playOnAwake = false;
        voiceSource.loop = false;
        voiceSource.spatialBlend = 0f;
    }
}

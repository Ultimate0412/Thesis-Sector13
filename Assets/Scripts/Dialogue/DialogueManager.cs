using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Script ตัวกลางสำหรับระบบบทสนทนาทั้งหมดในเกม
/// - จัดลำดับบรรทัด, คิวบทสนทนา, callback/event
/// - สั่งงาน DialogueHUD (view) โดยไม่หยุดเวลาเกม
/// - แสดงบทสนทนาเริ่มวันจาก DayConfig (พร้อมหน่วงเวลา startDelay)
/// ระบบอื่นสามารถเรียกใช้ได้ผ่าน DialogueManager.Instance.Play(...)
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("References")]
    [Tooltip("ปล่อยว่างได้ ระบบจะหา DialogueHUD ในฉากให้อัตโนมัติ")]
    public DialogueHUD dialogueHUD;

    [Header("Debug")]
    [Tooltip("แสดง log เมื่อเริ่ม/จบบทสนทนา")]
    public bool logDialogueEvents = false;

    /// <summary>กำลังเล่นบทสนทนาอยู่หรือไม่</summary>
    public bool IsDialogueActive { get; private set; }

    /// <summary>เริ่มแสดงบรรทัดใหม่</summary>
    public event Action<DialogueLine> OnLineStarted;

    /// <summary>บทสนทนาจบตามปกติ (ไม่นับกรณีถูก Stop)</summary>
    public event Action OnDialogueFinished;

    private Coroutine playCoroutine;
    private Coroutine pendingDayDialogueCoroutine;
    private readonly Queue<DialogueRequest> queuedDialogues = new Queue<DialogueRequest>();

    private class DialogueRequest
    {
        public string speakerName;
        public Sprite portrait;
        public List<DialogueLine> lines;
        public float charactersPerSecond;
        public float holdDuration;
        public Action onComplete;
    }

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
    }

    private void Start()
    {
        ResolveHUD();

        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.OnDayStarted += HandleDayStarted;
        }

        // Day 1 ไม่มีการยิง OnDayStarted จึงต้องแสดงเองหลัง Start ของทุกตัวทำงานครบ
        StartCoroutine(ShowDayOneDialogueRoutine());
    }

    private void OnDestroy()
    {
        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.OnDayStarted -= HandleDayStarted;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ------------------------------------------------------------------
    // API สำหรับระบบอื่นในอนาคต
    // ------------------------------------------------------------------

    /// <summary>เล่นบทสนทนาจาก DialogueSequence asset</summary>
    public void Play(DialogueSequence sequence, Action onComplete = null)
    {
        if (sequence == null)
        {
            onComplete?.Invoke();
            return;
        }

        Play(sequence.speakerName, sequence.portrait, sequence.lines,
             sequence.charactersPerSecond, sequence.holdDuration, onComplete, true);
    }

    /// <summary>
    /// เล่นบทสนทนาแบบกำหนดเอง
    /// interrupt = true → ตัดบทสนทนาที่เล่นอยู่ทันที, false → ต่อคิวไว้
    /// </summary>
    public void Play(
        string speakerName,
        Sprite portrait,
        IList<DialogueLine> lines,
        float charactersPerSecond,
        float holdDuration,
        Action onComplete = null,
        bool interrupt = true)
    {
        if (lines == null || lines.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        ResolveHUD();
        if (dialogueHUD == null)
        {
            Debug.LogWarning("[DialogueManager] ไม่พบ DialogueHUD ในฉาก");
            onComplete?.Invoke();
            return;
        }

        DialogueRequest request = new DialogueRequest
        {
            speakerName = speakerName,
            portrait = portrait,
            lines = new List<DialogueLine>(lines),
            charactersPerSecond = charactersPerSecond,
            holdDuration = holdDuration,
            onComplete = onComplete
        };

        if (interrupt)
        {
            StopCurrentPlayback(false);
            queuedDialogues.Clear();
            playCoroutine = StartCoroutine(PlayRoutine(request));
        }
        else if (IsDialogueActive || playCoroutine != null)
        {
            queuedDialogues.Enqueue(request);
        }
        else
        {
            playCoroutine = StartCoroutine(PlayRoutine(request));
        }
    }

    /// <summary>เล่นบทสนทนาสั้น ๆ จากข้อความหลายบรรทัด (ใช้ชื่อ/รูป default)</summary>
    public void PlayLines(params string[] lines)
    {
        if (lines == null || lines.Length == 0) return;

        List<DialogueLine> list = new List<DialogueLine>(lines.Length);
        foreach (string text in lines)
        {
            list.Add(new DialogueLine { text = text });
        }

        Play(string.Empty, null, list, 35f, 2.5f);
    }

    /// <summary>หยุดบทสนทนาทั้งหมด (immediate = true → ซ่อนทันที)</summary>
    public void Stop(bool immediate = true)
    {
        StopCurrentPlayback(immediate);
        queuedDialogues.Clear();
    }

    // ------------------------------------------------------------------
    // บทสนทนาเริ่มวัน
    // ------------------------------------------------------------------

    private IEnumerator ShowDayOneDialogueRoutine()
    {
        // รอ 1 เฟรมเพื่อให้ GameDayManager.Start() ทำงานครบก่อน
        yield return null;

        if (GameDayManager.Instance == null) yield break;
        yield return ShowDayDialogueRoutine(GameDayManager.Instance.CurrentDayConfig);
    }

    private void HandleDayStarted(int newDay)
    {
        if (pendingDayDialogueCoroutine != null)
        {
            StopCoroutine(pendingDayDialogueCoroutine);
            pendingDayDialogueCoroutine = null;
        }

        DayConfig config = GameDayManager.Instance != null ? GameDayManager.Instance.CurrentDayConfig : null;
        pendingDayDialogueCoroutine = StartCoroutine(ShowDayDialogueRoutine(config));
    }

    private IEnumerator ShowDayDialogueRoutine(DayConfig config)
    {
        DayDialogueSettings settings = config != null ? config.dialogue : null;

        if (settings == null || !settings.enableDialogue) yield break;
        if (settings.lines == null || settings.lines.Length == 0) yield break;

        if (settings.startDelay > 0f)
        {
            yield return new WaitForSeconds(settings.startDelay);
        }

        pendingDayDialogueCoroutine = null;

        Play(settings.speakerName, settings.portrait, settings.lines,
             settings.charactersPerSecond, settings.holdDuration, null, true);
    }

    // ------------------------------------------------------------------
    // ภายใน
    // ------------------------------------------------------------------

    private IEnumerator PlayRoutine(DialogueRequest request)
    {
        IsDialogueActive = true;

        if (logDialogueEvents)
        {
            Debug.Log($"[DialogueManager] เริ่มบทสนทนา ({request.lines.Count} บรรทัด)");
        }

        yield return dialogueHUD.ShowBubbleRoutine();

        foreach (DialogueLine line in request.lines)
        {
            if (line == null || string.IsNullOrWhiteSpace(line.text)) continue;

            OnLineStarted?.Invoke(line);
            yield return dialogueHUD.PlayLineRoutine(
                line,
                request.speakerName,
                request.portrait,
                request.charactersPerSecond,
                request.holdDuration);
        }

        yield return dialogueHUD.HideBubbleRoutine();

        IsDialogueActive = false;
        playCoroutine = null;

        if (logDialogueEvents)
        {
            Debug.Log("[DialogueManager] บทสนทนาจบ");
        }

        request.onComplete?.Invoke();
        OnDialogueFinished?.Invoke();

        TryPlayNextQueued();
    }

    private void TryPlayNextQueued()
    {
        if (queuedDialogues.Count == 0 || playCoroutine != null) return;

        DialogueRequest next = queuedDialogues.Dequeue();
        playCoroutine = StartCoroutine(PlayRoutine(next));
    }

    private void StopCurrentPlayback(bool hidePanelImmediately)
    {
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }

        IsDialogueActive = false;

        if (dialogueHUD != null)
        {
            dialogueHUD.StopVoice();
            if (hidePanelImmediately)
            {
                dialogueHUD.StopImmediate();
            }
        }
    }

    private void ResolveHUD()
    {
        if (dialogueHUD != null) return;

        dialogueHUD = DialogueHUD.Instance;
        if (dialogueHUD == null)
        {
            dialogueHUD = FindFirstObjectByType<DialogueHUD>(FindObjectsInactive.Include);
        }

        if (dialogueHUD == null)
        {
            GameObject hudObj = new GameObject("DialogueHUD");
            dialogueHUD = hudObj.AddComponent<DialogueHUD>();
        }
    }
}

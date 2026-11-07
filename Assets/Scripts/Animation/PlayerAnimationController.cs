using System;
using UnityEngine;

/// <summary>
/// ควบคุมแอนิเมชันของผู้เล่นมุมมองบุคคลที่หนึ่งแบบ Procedural
/// - Head Bob (เดิน/วิ่ง), Idle Breathing
/// - Jump Kick / Landing Dip
/// - Run FOV Kick
/// - ส่ง Event ก้าวเท้า (OnFootstep) ไปใช้กับระบบเสียงได้
/// - (ทางเลือก) ส่งค่า Parameter ไปยัง Animator หากมีโมเดล/แขนใส่ไว้
///
/// ปรับเฉพาะ localPosition ของกล้องและ FOV เท่านั้น
/// จึงไม่ชนกับ PlayerController (ซึ่งหมุนกล้องใน LateUpdate) และไม่ยุ่งกับของที่ถืออยู่
/// ของที่ถือ / ไฟฉาย UV ที่เป็นลูกของกล้องจะขยับตามกล้องโดยอัตโนมัติ
/// </summary>
[DefaultExecutionOrder(100)] // ให้ทำงานหลัง PlayerController
[RequireComponent(typeof(PlayerController))]
public class PlayerAnimationController : MonoBehaviour
{
    [Header("Head Bob")]
    public bool enableHeadBob = true;
    [Tooltip("ความถี่การโยก (รอบ/วินาที) ตอนเดิน")]
    public float walkBobFrequency = 7f;
    public float runBobFrequency = 10f;
    [Tooltip("ความสูงการโยกขึ้นลง")]
    public float walkBobVertical = 0.035f;
    public float runBobVertical = 0.065f;
    [Tooltip("ความกว้างการโยกซ้ายขวา")]
    public float walkBobHorizontal = 0.02f;
    public float runBobHorizontal = 0.035f;

    [Header("Idle Breathing")]
    public bool enableBreathing = true;
    public float breathAmplitude = 0.004f;
    public float breathSpeed = 1.2f;

    [Header("Jump / Landing")]
    [Tooltip("แรงกระตุกกล้องขึ้นตอนกระโดด")]
    public float jumpKick = 0.04f;
    [Tooltip("ตัวคูณความลึกของกล้องที่ยุบตอนลงพื้น (คูณกับความเร็วตก)")]
    public float landDipScale = 0.012f;
    public float maxLandDip = 0.14f;
    [Tooltip("ความเร็วตกขั้นต่ำที่จะนับว่าเป็นการลงพื้นแรง")]
    public float minLandVelocity = 3f;
    public float landRecoverSpeed = 8f;

    [Header("Run FOV Kick")]
    public bool enableFovKick = true;
    public float fovKickAmount = 6f;
    public float fovLerpSpeed = 6f;

    [Header("Smoothing")]
    public float positionSmoothing = 14f;
    [Tooltip("ความเร็วแนวราบขั้นต่ำที่ถือว่ากำลังเคลื่อนที่")]
    public float moveThreshold = 0.1f;

    [Header("Animator (Optional)")]
    [Tooltip("ใส่ Animator ของแขน/โมเดล หากมี (ปล่อยว่างได้)")]
    public Animator animator;

    /// <summary>ถูกเรียกทุกครั้งที่เท้าแตะพื้น (ใช้ผูกเสียงฝีเท้า)</summary>
    public event Action<bool> OnFootstep;   // พารามิเตอร์: true = กำลังวิ่ง
    public event Action OnJump;
    public event Action<float> OnLand;      // พารามิเตอร์: ความแรงของการลงพื้น (0-1)

    // Animator parameter hashes
    private static readonly int MoveZHash = Animator.StringToHash("MoveZ");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private static readonly int PickHash = Animator.StringToHash("Pick");

    private PlayerController controller;
    private Transform camTransform;
    private Camera cam;

    private Vector3 baseCamLocalPos;
    private float baseFov;

    private float bobTimer;
    private int lastStepIndex;
    private float landOffset;      // ค่าลบ = กล้องยุบลง
    private float jumpOffset;
    private bool wasGrounded = true;
    private float lastAirVelocityY;
    private Vector3 currentOffset;

    public float HorizontalSpeed { get; private set; }
    public bool IsMoving { get; private set; }
    public bool IsRunning { get; private set; }

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
    }

    private void Start()
    {
        camTransform = controller.playerCamera;
        if (camTransform == null)
        {
            Camera c = GetComponentInChildren<Camera>();
            if (c != null) camTransform = c.transform;
        }

        if (camTransform != null)
        {
            cam = camTransform.GetComponent<Camera>();
            baseCamLocalPos = camTransform.localPosition;
            if (cam != null) baseFov = cam.fieldOfView;
        }

        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false; // ให้ Rigidbody คุมการเคลื่อนที่
        wasGrounded = controller.isGrounded;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;

        UpdateMovementState();
        DetectJumpAndLand();
        UpdateAnimatorParameters();
    }

    private void LateUpdate()
    {
        if (camTransform == null || Time.timeScale <= 0f) return;

        Vector3 target = Vector3.zero;

        if (controller.isGrounded && IsMoving)
        {
            if (enableHeadBob) target += CalculateHeadBob();
        }
        else
        {
            // ค่อย ๆ รีเซ็ตจังหวะการเดิน ให้ก้าวแรกหลังหยุดเริ่มจากกลางจังหวะ
            bobTimer = Mathf.Lerp(bobTimer, Mathf.Round(bobTimer / Mathf.PI) * Mathf.PI, Time.deltaTime * 6f);

            if (controller.isGrounded && enableBreathing)
            {
                target.y += Mathf.Sin(Time.time * breathSpeed) * breathAmplitude;
            }
        }

        // Jump kick + Landing dip
        jumpOffset = Mathf.Lerp(jumpOffset, 0f, Time.deltaTime * landRecoverSpeed);
        landOffset = Mathf.Lerp(landOffset, 0f, Time.deltaTime * landRecoverSpeed);
        target.y += jumpOffset + landOffset;

        currentOffset = Vector3.Lerp(currentOffset, target, Time.deltaTime * positionSmoothing);
        camTransform.localPosition = baseCamLocalPos + currentOffset;

        UpdateFov();
    }

    // ---------------------------------------------------------------

    private void UpdateMovementState()
    {
#if UNITY_6000_0_OR_NEWER
        Vector3 vel = controller.rb.linearVelocity;
#else
        Vector3 vel = controller.rb.velocity;
#endif
        HorizontalSpeed = new Vector2(vel.x, vel.z).magnitude;
        IsMoving = HorizontalSpeed > moveThreshold;
        // ถือว่าวิ่งเมื่อความเร็วเกินเดินปกติอย่างชัดเจน
        IsRunning = IsMoving && HorizontalSpeed > (controller.walkSpeed + controller.runSpeed) * 0.5f;
    }

    private Vector3 CalculateHeadBob()
    {
        float freq = IsRunning ? runBobFrequency : walkBobFrequency;
        float vert = IsRunning ? runBobVertical : walkBobVertical;
        float horiz = IsRunning ? runBobHorizontal : walkBobHorizontal;

        // ปรับความถี่ตามความเร็วจริง (เช่น เดินชนกำแพงจะโยกช้าลง)
        float speedFactor = Mathf.Clamp01(HorizontalSpeed / Mathf.Max(0.01f, IsRunning ? controller.runSpeed : controller.walkSpeed));
        bobTimer += Time.deltaTime * freq * Mathf.Max(0.3f, speedFactor) * 0.5f;

        // เกิดเสียงก้าวเท้าทุกครั้งที่กล้องลงต่ำสุด (ทุก π)
        int stepIndex = Mathf.FloorToInt(bobTimer / Mathf.PI);
        if (stepIndex != lastStepIndex)
        {
            lastStepIndex = stepIndex;
            OnFootstep?.Invoke(IsRunning);
        }

        float x = Mathf.Cos(bobTimer) * horiz;
        float y = -Mathf.Abs(Mathf.Sin(bobTimer)) * vert;
        return new Vector3(x, y, 0f);
    }

    private void DetectJumpAndLand()
    {
        bool grounded = controller.isGrounded;

#if UNITY_6000_0_OR_NEWER
        float vy = controller.rb.linearVelocity.y;
#else
        float vy = controller.rb.velocity.y;
#endif

        if (!grounded) lastAirVelocityY = vy;

        // ออกจากพื้นด้วยความเร็วขึ้น = กระโดด
        if (wasGrounded && !grounded && vy > 0.5f)
        {
            jumpOffset = jumpKick;
            OnJump?.Invoke();
        }

        // แตะพื้นอีกครั้ง = ลงพื้น
        if (!wasGrounded && grounded)
        {
            float impact = Mathf.Abs(lastAirVelocityY);
            if (impact >= minLandVelocity)
            {
                landOffset = -Mathf.Min(impact * landDipScale, maxLandDip);
                OnLand?.Invoke(Mathf.Clamp01(impact / 12f));
            }
            lastAirVelocityY = 0f;
        }

        wasGrounded = grounded;
    }

    private void UpdateFov()
    {
        if (!enableFovKick || cam == null) return;

        float targetFov = baseFov + (IsRunning && controller.isGrounded ? fovKickAmount : 0f);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, Time.deltaTime * fovLerpSpeed);
    }

    /// <summary>เรียกจาก PlayerPickupSystem ตอนหยิบของ เพื่อเล่นท่า Pick</summary>
    public void TriggerPick()
    {
        if (animator != null) animator.SetTrigger(PickHash);
    }

    private void UpdateAnimatorParameters()
    {
        if (animator == null) return;

#if UNITY_6000_0_OR_NEWER
        Vector3 vel = controller.rb.linearVelocity;
#else
        Vector3 vel = controller.rb.velocity;
#endif
        // MoveZ: -1 = ถอยหลังเต็มที่, 0 = นิ่ง, 1 = วิ่งเต็มที่ (Blend Tree ใช้ค่านี้)
        Vector3 localVel = transform.InverseTransformDirection(new Vector3(vel.x, 0f, vel.z));
        float magnitude = localVel.magnitude / Mathf.Max(0.01f, controller.runSpeed);
        float moveZ = localVel.z < -0.1f ? -magnitude : magnitude;

        animator.SetFloat(MoveZHash, moveZ, 0.1f, Time.deltaTime);
        animator.SetBool(IsGroundedHash, controller.isGrounded);
        animator.SetBool(IsRunningHash, IsRunning);
    }
}

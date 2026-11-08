using System;
using UnityEngine;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(PlayerController))]
public class PlayerAnimationController : MonoBehaviour
{
    [Header("Head Bob")]
    public bool enableHeadBob = true;
    public float walkBobFrequency = 7f;
    public float runBobFrequency = 10f;
    public float walkBobVertical = 0.035f;
    public float runBobVertical = 0.065f;
    public float walkBobHorizontal = 0.02f;
    public float runBobHorizontal = 0.035f;

    [Header("Smoothing")]
    public float positionSmoothing = 14f;

    public float moveThreshold = 0.1f;

    [Header("Animator")]
    [Tooltip("Animator ของโมเดล (ปล่อยว่างได้ จะหาจากลูกของ Player)")]
    public Animator animator;

    public event Action<bool> OnFootstep;

    private static readonly int MoveZHash = Animator.StringToHash("MoveZ");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private static readonly int PickHash = Animator.StringToHash("Pick");

    private PlayerController controller;
    private Transform camTransform;
    private Vector3 baseCamLocalPos;
    private Vector3 currentOffset;
    private float bobTimer;
    private int lastStepIndex;

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
        if (camTransform != null) baseCamLocalPos = camTransform.localPosition;

        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null)
            Debug.LogError("[PlayerAnimationController] ไม่เจอ Animator ในลูกของ Player", this);
        else
            animator.applyRootMotion = false;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;

        Vector3 vel = GetVelocity();
        HorizontalSpeed = new Vector2(vel.x, vel.z).magnitude;
        IsMoving = HorizontalSpeed > moveThreshold;
        IsRunning = IsMoving && HorizontalSpeed > (controller.walkSpeed + controller.runSpeed) * 0.5f;

        UpdateAnimatorParameters(vel);
    }

    private void LateUpdate()
    {
        if (camTransform == null || Time.timeScale <= 0f) return;

        Vector3 target = Vector3.zero;

        if (enableHeadBob && controller.isGrounded && IsMoving)
        {
            target = CalculateHeadBob();
        }
        else
        {
            bobTimer = Mathf.Lerp(bobTimer, Mathf.Round(bobTimer / Mathf.PI) * Mathf.PI, Time.deltaTime * 6f);
        }

        currentOffset = Vector3.Lerp(currentOffset, target, Time.deltaTime * positionSmoothing);
        camTransform.localPosition = baseCamLocalPos + currentOffset;
    }

    private Vector3 CalculateHeadBob()
    {
        float freq = IsRunning ? runBobFrequency : walkBobFrequency;
        float vert = IsRunning ? runBobVertical : walkBobVertical;
        float horiz = IsRunning ? runBobHorizontal : walkBobHorizontal;

        float refSpeed = Mathf.Max(0.01f, IsRunning ? controller.runSpeed : controller.walkSpeed);
        float speedFactor = Mathf.Clamp01(HorizontalSpeed / refSpeed);
        bobTimer += Time.deltaTime * freq * Mathf.Max(0.3f, speedFactor) * 0.5f;

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


    [ContextMenu("Test Pick")]
    private void TestPick()
    {
        Debug.Log("[PlayerAnimationController] Test Pick, ความยาวท่า = " + TriggerPick() + " วินาที");
    }

    public float TriggerPick()
    {
        if (animator == null) return 0f;
        animator.SetTrigger(PickHash);
        return GetClipLength("Pick");
    }

    private float GetClipLength(string key)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return 0f;
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == key || clip.name.EndsWith("|" + key))
                return clip.length / Mathf.Max(0.01f, animator.speed);
        }
        return 0f;
    }

    private void UpdateAnimatorParameters(Vector3 vel)
    {
        if (animator == null) return;

        Vector3 localVel = transform.InverseTransformDirection(new Vector3(vel.x, 0f, vel.z));
        float magnitude = localVel.magnitude / Mathf.Max(0.01f, controller.runSpeed);
        float moveZ = localVel.z < -0.1f ? -magnitude : magnitude;

        animator.SetFloat(MoveZHash, moveZ, 0.1f, Time.deltaTime);
        animator.SetBool(IsGroundedHash, controller.isGrounded);
        animator.SetBool(IsRunningHash, IsRunning);
    }

    private Vector3 GetVelocity()
    {
#if UNITY_6000_0_OR_NEWER
        return controller.rb.linearVelocity;
#else
        return controller.rb.velocity;
#endif
    }
}
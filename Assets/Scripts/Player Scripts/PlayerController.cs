using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(PlayerStats))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Speeds")]
    public float walkSpeed = 4f;
    public float runSpeed = 7f;
    public float jumpHeight = 1.2f;

    [Header("Ground Check Settings")]
    public LayerMask groundMask = ~0;
    public float groundCheckDistance = 0.15f;
    [SerializeField] private bool _isGrounded;
    public bool isGrounded => _isGrounded;

    [Header("Camera Settings")]
    public Transform playerCamera;
    public float lookSpeed = 2f;
    public float lookXLimit = 45f;

    [HideInInspector] public Rigidbody rb;
    [HideInInspector] public CapsuleCollider capsuleCollider;
    [HideInInspector] public PlayerStats stats;
    [HideInInspector] public float rotationX = 0f;

    // Movement vectors
    private Vector3 targetMoveDirection = Vector3.zero;
    private bool jumpRequested = false;

    // States
    private PlayerStateBase currentState;
    public IdleState idleState;
    public WalkState walkState;
    public RunState runState;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();
        stats = GetComponent<PlayerStats>();

        // ตั้งค่า Rigidbody สำหรับ First-Person Controller
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // ป้องกันตัวละครติดขอบกำแพงเวลาเดินเบียดหรือกระโดดชนผนัง
        if (capsuleCollider.sharedMaterial == null)
        {
            PhysicsMaterial frictionless = new PhysicsMaterial("PlayerFrictionless")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounciness = 0f,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            capsuleCollider.sharedMaterial = frictionless;
        }

        // สร้าง States (Idle, Walk, Run)
        idleState = new IdleState(this);
        walkState = new WalkState(this);
        runState = new RunState(this);
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        SwitchState(idleState);
    }

    private void Update()
    {
        HandleCameraLook();
        CheckGrounded();

        // ตรวจสอบการกดปุ่มกระโดดใน Update เพื่อไม่ให้พลาด input ในแต่ละเฟรม
        if (Input.GetKeyDown(KeyCode.Space) && _isGrounded)
        {
            jumpRequested = true;
        }

        currentState?.UpdateState();
    }

    private void FixedUpdate()
    {
        ApplyPhysicsMovement();
    }

    public void SwitchState(PlayerStateBase newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState?.Enter();
    }

    private void HandleCameraLook()
    {
        if (playerCamera == null) return;

        rotationX += -Input.GetAxis("Mouse Y") * lookSpeed;
        rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
        playerCamera.localRotation = Quaternion.Euler(rotationX, 0, 0);
        transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSpeed, 0);
    }

    private void CheckGrounded()
    {
        if (capsuleCollider == null) return;

        Vector3 origin = transform.position + capsuleCollider.center;
        float radius = capsuleCollider.radius * 0.9f;
        float castDistance = (capsuleCollider.height * 0.5f) - radius + groundCheckDistance;

        RaycastHit[] hits = Physics.SphereCastAll(origin, radius, Vector3.down, castDistance, groundMask, QueryTriggerInteraction.Ignore);
        _isGrounded = false;
        foreach (var hit in hits)
        {
            if (hit.collider != capsuleCollider && !hit.collider.transform.IsChildOf(transform))
            {
                _isGrounded = true;
                break;
            }
        }
    }

    // เรียกใช้โดยแต่ละ State เพื่อกำหนดทิศทางและความเร็วที่ต้องการเคลื่อนที่
    public void ApplyMovement(float speed)
    {
        float curSpeedX = Input.GetAxisRaw("Vertical");
        float curSpeedY = Input.GetAxisRaw("Horizontal");

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        Vector3 inputDir = (forward * curSpeedX) + (right * curSpeedY);
        if (inputDir.sqrMagnitude > 1f)
        {
            inputDir.Normalize();
        }

        targetMoveDirection = inputDir * speed;
    }

    private void ApplyPhysicsMovement()
    {
#if UNITY_6000_0_OR_NEWER
        Vector3 currentVel = rb.linearVelocity;
#else
        Vector3 currentVel = rb.velocity;
#endif

        // ทำการกระโดด
        if (jumpRequested)
        {
            float jumpVelocity = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * jumpHeight);
            currentVel.y = jumpVelocity;
            jumpRequested = false;
        }

        // ควบคุมความเร็วแนวราบ (X และ Z)
        currentVel.x = targetMoveDirection.x;
        currentVel.z = targetMoveDirection.z;

#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = currentVel;
#else
        rb.velocity = currentVel;
#endif
    }

    private void OnDrawGizmosSelected()
    {
        if (capsuleCollider == null) return;

        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Vector3 origin = transform.position + capsuleCollider.center;
        float radius = capsuleCollider.radius * 0.9f;
        float castDistance = (capsuleCollider.height * 0.5f) - radius + groundCheckDistance;
        Gizmos.DrawWireSphere(origin + Vector3.down * castDistance, radius);
    }
}
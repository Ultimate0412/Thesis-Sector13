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

        // α╕òα╕▒α╣ëα╕çα╕äα╣êα╕▓ Rigidbody α╕¬α╕│α╕½α╕úα╕▒α╕Ü First-Person Controller
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // α╕¢α╣ëα╕¡α╕çα╕üα╕▒α╕Öα╕òα╕▒α╕ºα╕Ñα╕░α╕äα╕úα╕òα╕┤α╕öα╕éα╕¡α╕Üα╕üα╕│α╣üα╕₧α╕çα╣Çα╕ºα╕Ñα╕▓α╣Çα╕öα╕┤α╕Öα╣Çα╕Üα╕╡α╕óα╕öα╕½α╕úα╕╖α╕¡α╕üα╕úα╕░α╣éα╕öα╕öα╕èα╕Öα╕£α╕Öα╕▒α╕ç
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

        // α╕¬α╕úα╣ëα╕▓α╕ç States (Idle, Walk, Run)
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

        // α╕òα╕úα╕ºα╕êα╕¬α╕¡α╕Üα╕üα╕▓α╕úα╕üα╕öα╕¢α╕╕α╣êα╕íα╕üα╕úα╕░α╣éα╕öα╕öα╣âα╕Ö Update α╣Çα╕₧α╕╖α╣êα╕¡α╣äα╕íα╣êα╣âα╕½α╣ëα╕₧α╕Ñα╕▓α╕ö input α╣âα╕Öα╣üα╕òα╣êα╕Ñα╕░α╣Çα╕ƒα╕úα╕í
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

    // α╣Çα╕úα╕╡α╕óα╕üα╣âα╕èα╣ëα╣éα╕öα╕óα╣üα╕òα╣êα╕Ñα╕░ State α╣Çα╕₧α╕╖α╣êα╕¡α╕üα╕│α╕½α╕Öα╕öα╕ùα╕┤α╕¿α╕ùα╕▓α╕çα╣üα╕Ñα╕░α╕äα╕ºα╕▓α╕íα╣Çα╕úα╣çα╕ºα╕ùα╕╡α╣êα╕òα╣ëα╕¡α╕çα╕üα╕▓α╕úα╣Çα╕äα╕Ñα╕╖α╣êα╕¡α╕Öα╕ùα╕╡α╣ê
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

        // α╕ùα╕│α╕üα╕▓α╕úα╕üα╕úα╕░α╣éα╕öα╕ö
        if (jumpRequested)
        {
            float jumpVelocity = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * jumpHeight);
            currentVel.y = jumpVelocity;
            jumpRequested = false;
        }

        // α╕äα╕ºα╕Üα╕äα╕╕α╕íα╕äα╕ºα╕▓α╕íα╣Çα╕úα╣çα╕ºα╣üα╕Öα╕ºα╕úα╕▓α╕Ü (X α╣üα╕Ñα╕░ Z)
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

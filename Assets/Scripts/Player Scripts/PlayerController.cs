using NUnit.Framework.Interfaces;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerStats))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Speeds")]
    public float walkSpeed = 4f;
    public float runSpeed = 7f;
    public float gravity = -9.81f;
    public float jumpHeight = 1.2f;

    [Header("Camera Settings")]
    public Transform playerCamera;
    public float lookSpeed = 2f;
    public float lookXLimit = 45f;

    [HideInInspector] public CharacterController controller;
    [HideInInspector] public PlayerStats stats;
    [HideInInspector] public Vector3 moveDirection = Vector3.zero;
    [HideInInspector] public float rotationX = 0;

    // เหลือแค่ Idle, Walk, Run
    private PlayerStateBase currentState;
    public IdleState idleState;
    public WalkState walkState;
    public RunState runState;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        stats = GetComponent<PlayerStats>();

        // สร้าง States (ตัด Crouch และ Jump ออกจาก State)
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
        currentState?.UpdateState();
    }

    public void SwitchState(PlayerStateBase newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState?.Enter();
    }

    private void HandleCameraLook()
    {
        rotationX += -Input.GetAxis("Mouse Y") * lookSpeed;
        rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
        playerCamera.localRotation = Quaternion.Euler(rotationX, 0, 0);
        transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSpeed, 0);
    }

    // ฟังก์ชันคำนวณการเคลื่อนที่ + รองรับการกระโดดตลอดเวลา
    public void ApplyMovement(float speed)
    {
        float curSpeedX = speed * Input.GetAxis("Vertical");
        float curSpeedY = speed * Input.GetAxis("Horizontal");

        Vector3 forward = transform.TransformDirection(Vector3.forward);
        Vector3 right = transform.TransformDirection(Vector3.right);

        float movementDirectionY = moveDirection.y;
        moveDirection = (forward * curSpeedX) + (right * curSpeedY);
        moveDirection.Normalize();
        moveDirection *= speed;

        if (controller.isGrounded)
        {
            moveDirection.y = -0.5f; // กดตัวละครติดพื้นเวลาอยู่บนพื้น

            // เช็คปุ่มกระโดดตรงนี้ เพื่อให้กดกระโดดได้ทุก State (ไม่ว่าจะเดินหรือวิ่งอยู่)
            if (Input.GetKeyDown(KeyCode.Space))
            {
                moveDirection.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }
        else
        {
            // ถ้าอยู่กลางอากาศ แรงโน้มถ่วงจะดึงลงเรื่อยๆ แต่ยังรักษาทิศทางเดิน/วิ่งกลางอากาศได้ระดับหนึ่ง
            moveDirection.y = movementDirectionY + (gravity * Time.deltaTime);
        }

        controller.Move(moveDirection * Time.deltaTime);
    }
}
using UnityEngine;
using UnityEngine.InputSystem;

namespace ThesisSector13
{
    [RequireComponent(typeof(CharacterController))]
    public class FPSPlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float sprintSpeed = 8f;
        [SerializeField] private float crouchSpeed = 2.5f;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -19.62f;

        [Header("Look")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private float maxLookAngle = 85f;
        [SerializeField] private bool lockCursor = true;

        [Header("Crouch")]
        [SerializeField] private float standingHeight = 1.8f;
        [SerializeField] private float crouchHeight = 1f;
        [SerializeField] private float crouchTransitionSpeed = 10f;

        private CharacterController controller;
        private Vector3 velocity;
        private Vector2 moveInput;
        private float cameraPitch;
        private bool isSprinting;
        private bool isCrouching;
        private bool shouldJump;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
        }

        private void OnEnable()
        {
            SetCursorState(true);
        }

        private void OnDisable()
        {
            SetCursorState(false);
        }

        private void Update()
        {
            ReadInput();
            UpdateCursorLock();
            HandleMouseLook();
            HandleMovement();
            HandleGravity();
            HandleCrouch();
        }

        private void ReadInput()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard != null)
            {
                float x = 0f;
                float z = 0f;
                if (keyboard.aKey.isPressed) x -= 1f;
                if (keyboard.dKey.isPressed) x += 1f;
                if (keyboard.sKey.isPressed) z -= 1f;
                if (keyboard.wKey.isPressed) z += 1f;

                moveInput = Vector2.ClampMagnitude(new Vector2(x, z), 1f);
                isSprinting = keyboard.shiftKey.isPressed && !isCrouching && moveInput.y > 0f;
                isCrouching = keyboard.ctrlKey.isPressed;
                shouldJump = keyboard.spaceKey.wasPressedThisFrame;
            }

            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity * 0.1f;
                transform.Rotate(Vector3.up * delta.x);

                cameraPitch = Mathf.Clamp(cameraPitch - delta.y, -maxLookAngle, maxLookAngle);
            }
        }

        private void UpdateCursorLock()
        {
            if (!lockCursor)
                return;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                SetCursorState(false);

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                SetCursorState(true);
        }

        private void SetCursorState(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void HandleMouseLook()
        {
            if (cameraTransform == null)
                return;

            cameraTransform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
        }

        private void HandleMovement()
        {
            float speed = isCrouching ? crouchSpeed : (isSprinting ? sprintSpeed : walkSpeed);
            Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
            controller.Move(move * speed * Time.deltaTime);
        }

        private void HandleGravity()
        {
            if (controller.isGrounded && velocity.y < 0f)
                velocity.y = -2f;

            if (shouldJump && controller.isGrounded)
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
        }

        private void HandleCrouch()
        {
            float targetHeight = isCrouching ? crouchHeight : standingHeight;
            controller.height = Mathf.Lerp(controller.height, targetHeight, crouchTransitionSpeed * Time.deltaTime);
            controller.center = new Vector3(controller.center.x, controller.height * 0.5f, controller.center.z);
        }
    }
}
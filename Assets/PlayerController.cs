using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("이동 속도 설정")]
    public float walkSpeed = 3.5f;
    public float sprintSpeed = 6.0f;
    public float crouchSpeed = 2.0f;
    public float gravity = -20f;         // 체감상 덜 붕 뜨도록 중력 가속도를 현실감 있게 조정

    [Header("점프 설정")]
    public float jumpHeight = 1.5f;
    public float groundCheckDistance = 0.2f; // 바닥 감지 여유 거리
    public LayerMask groundMask = ~0;        // 기본값: 모든 레이어 감지

    [Header("웅크리기 높이 설정")]
    public float standingHeight = 2.0f;
    public float crouchHeight = 1.0f;
    public float standingCamY = 0.6f;
    public float crouchCamY = 0.0f;
    public float crouchTransitionSpeed = 10f;

    [Header("시점 회전 설정")]
    public Transform cameraTransform;
    public float mouseSensitivity = 0.1f;
    public float maxLookUpAngle = 80.0f;
    public float maxLookDownAngle = -80.0f;

    private CharacterController controller;
    private Vector3 verticalVelocity;
    private float cameraPitch = 0f;
    private bool isCrouching = false;
    private bool isGrounded = false;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        controller.height = standingHeight;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        CheckGrounded();
        HandleCrouch();
        LookAround();
        Move();
    }

    // CharacterController.isGrounded 대신 물리 구체 체크로 확실하게 바닥 감지
    void CheckGrounded()
    {
        Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - (controller.height / 2f) + controller.radius, transform.position.z);
        isGrounded = Physics.CheckSphere(spherePosition, controller.radius + groundCheckDistance, groundMask, QueryTriggerInteraction.Ignore);

        // 바닥에 안정적으로 붙어있을 때 아래 방향 누적 가속도 초기화
        if (isGrounded && verticalVelocity.y < 0)
        {
            verticalVelocity.y = -2f;
        }
    }

    void HandleCrouch()
    {
        if (Keyboard.current == null) return;

        isCrouching = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.cKey.isPressed;

        float targetHeight = isCrouching ? crouchHeight : standingHeight;
        float targetCamY = isCrouching ? crouchCamY : standingCamY;

        controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
        controller.center = new Vector3(0, controller.height / 2f, 0);

        if (cameraTransform != null)
        {
            Vector3 camPos = cameraTransform.localPosition;
            camPos.y = Mathf.Lerp(camPos.y, targetCamY, Time.deltaTime * crouchTransitionSpeed);
            cameraTransform.localPosition = camPos;
        }
    }

    void LookAround()
    {
        if (Mouse.current == null) return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue() * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseDelta.x);

        cameraPitch -= mouseDelta.y;
        cameraPitch = Mathf.Clamp(cameraPitch, maxLookDownAngle, maxLookUpAngle);

        if (cameraTransform != null)
        {
            cameraTransform.localEulerAngles = new Vector3(cameraPitch, 0f, 0f);
        }
    }

    void Move()
    {
        if (Keyboard.current == null) return;

        // WASD 수평 이동
        float x = 0f;
        float z = 0f;

        if (Keyboard.current.wKey.isPressed) z += 1f;
        if (Keyboard.current.sKey.isPressed) z -= 1f;
        if (Keyboard.current.aKey.isPressed) x -= 1f;
        if (Keyboard.current.dKey.isPressed) x += 1f;

        float currentSpeed = walkSpeed;
        if (isCrouching)
        {
            currentSpeed = crouchSpeed;
        }
        else if (Keyboard.current.leftShiftKey.isPressed && z > 0)
        {
            currentSpeed = sprintSpeed;
        }

        Vector3 move = (transform.right * x + transform.forward * z).normalized;
        controller.Move(move * currentSpeed * Time.deltaTime);

        // 점프: 바닥에 있고 웅크리지 않았을 때 발동
        if (Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded && !isCrouching)
        {
            verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // 중력 계산 및 수직 이동 적용
        verticalVelocity.y += gravity * Time.deltaTime;
        controller.Move(verticalVelocity * Time.deltaTime);
    }
}
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
        controller.center = Vector3.zero;

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

    // 캡슐 아래쪽 반구 중심에서 아래로 SphereCast 해서 바닥 감지
    // (SphereCast는 시작 시점에 겹쳐 있는 자기 자신의 콜라이더를 무시하므로 자기 몸에 걸리지 않음)
    void CheckGrounded()
    {
        float castRadius = controller.radius * 0.95f;
        Vector3 bottomSphereCenter = transform.TransformPoint(controller.center)
                                     + Vector3.down * (controller.height / 2f - controller.radius);
        float castDistance = controller.skinWidth + groundCheckDistance + (controller.radius - castRadius);

        isGrounded = controller.isGrounded ||
                     Physics.SphereCast(bottomSphereCenter, castRadius, Vector3.down, out _, castDistance, groundMask, QueryTriggerInteraction.Ignore);

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
        // 피벗은 서 있을 때 캡슐 중앙 → 높이가 줄어도 발바닥 위치가 고정되도록 중심을 내림
        controller.center = new Vector3(0, (controller.height - standingHeight) / 2f, 0);

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
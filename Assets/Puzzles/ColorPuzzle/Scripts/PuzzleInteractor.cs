using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HorrorPuzzle
{
    /// <summary>
    /// 플레이어 카메라에 붙이는 간단한 상호작용 스크립트.
    /// 커서가 잠겨 있으면(1인칭) 화면 중앙, 커서가 보이면 마우스 위치로 레이를 쏜다.
    /// 좌클릭 또는 E 키로 PuzzleButton을 누른다. 이미 자체 상호작용 시스템이 있으면 이 스크립트 대신
    /// 그 시스템에서 PuzzleButton.Interact()를 호출하면 된다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PuzzleInteractor : MonoBehaviour
    {
        public float maxDistance = 2.5f;
        public LayerMask layers = ~0;
        public bool useMouseClick = true;
        public bool useKeyE = true;

        public static bool AnyActive { get; private set; }
        public PuzzleButton Hovered { get; private set; }

        Camera cam;

        void Awake() { cam = GetComponent<Camera>(); }
        void OnEnable() { AnyActive = true; }
        void OnDisable() { AnyActive = false; }

        void Update()
        {
            Vector2 screen = Cursor.lockState == CursorLockMode.Locked
                ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
                : ReadMousePosition();
            Ray ray = cam.ScreenPointToRay(screen);
            Hovered = null;
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, layers, QueryTriggerInteraction.Collide))
                Hovered = hit.collider.GetComponentInParent<PuzzleButton>();

            if (Hovered != null && PressedThisFrame()) Hovered.Interact();
        }

        Vector2 ReadMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }

        bool PressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            bool click = useMouseClick && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            bool key = useKeyE && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
            return click || key;
#else
            return (useMouseClick && Input.GetMouseButtonDown(0)) || (useKeyE && Input.GetKeyDown(KeyCode.E));
#endif
        }
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class DoubleDoorController : MonoBehaviour
{
    [Header("문 오브젝트 연결 (비워두면 아래 이름으로 씬에서 자동 검색)")]
    public Transform leftDoor;
    public Transform rightDoor;
    public string leftDoorName = "Door_Entrance_L";
    public string rightDoorName = "Door_Entrance_R";

    [Header("회전 설정")]
    public float openAngle = 90f;   // 문이 열리는 각도 (방향은 플레이어 반대쪽으로 자동 결정)
    public float doorSpeed = 2.5f;

    [Header("상호작용 설정")]
    public float interactionDistance = 3.5f;

    // 모델의 피벗이 경첩 위치가 아닐 수 있으므로, 문 바깥쪽 모서리에 회전축(경첩)을 만들어 그걸 돌린다
    private Transform leftHinge;
    private Transform rightHinge;
    private Vector3 doorAxis;        // 왼쪽 문 → 오른쪽 문 방향 (수평)
    private Vector3 doorwayCenter;   // 두 문 사이 중앙 (거리 계산용)

    private Quaternion leftClosedRot;
    private Quaternion rightClosedRot;
    private Quaternion leftOpenRot;
    private Quaternion rightOpenRot;

    private bool isOpen = false;
    private bool isLocked = false;
    private Transform playerTransform;
    private Coroutine activeCoroutine;

    void Start()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null) playerTransform = player.transform;
        else Debug.LogWarning("[DoubleDoorController] 'Player' 태그가 붙은 오브젝트가 없습니다.");

        if (leftDoor == null) leftDoor = FindByName(leftDoorName);
        if (rightDoor == null) rightDoor = FindByName(rightDoorName);

        if (leftDoor == null || rightDoor == null)
        {
            Debug.LogWarning($"[DoubleDoorController] 문을 찾지 못했습니다. (L: {leftDoorName}, R: {rightDoorName})");
            enabled = false;
            return;
        }

        Bounds leftBounds = GetWorldBounds(leftDoor);
        Bounds rightBounds = GetWorldBounds(rightDoor);

        doorAxis = rightBounds.center - leftBounds.center;
        doorAxis.y = 0f;
        doorAxis.Normalize();
        doorwayCenter = (leftBounds.center + rightBounds.center) / 2f;

        // 각 문의 바깥쪽 모서리(서로 먼 쪽)를 경첩 위치로 사용
        Vector3 leftHingePos = leftBounds.center - doorAxis * ExtentAlong(leftBounds, doorAxis);
        Vector3 rightHingePos = rightBounds.center + doorAxis * ExtentAlong(rightBounds, doorAxis);

        leftHinge = CreateHinge("Hinge_" + leftDoor.name, leftDoor, leftHingePos);
        rightHinge = CreateHinge("Hinge_" + rightDoor.name, rightDoor, rightHingePos);

        leftClosedRot = leftHinge.localRotation;
        rightClosedRot = rightHinge.localRotation;
    }

    void Update()
    {
        if (isLocked || playerTransform == null) return;
        if (Keyboard.current == null) return;

        float dist = Vector3.Distance(doorwayCenter, playerTransform.position);
        if (dist <= interactionDistance && Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (isOpen) CloseDoors();
            else OpenDoors();
        }
    }

    public void OpenDoors()
    {
        if (isLocked || isOpen || leftHinge == null) return;
        isOpen = true;

        // 플레이어 반대쪽으로 열리도록 회전 방향 결정
        Vector3 doorNormal = Vector3.Cross(Vector3.up, doorAxis);
        Vector3 toPlayer = playerTransform != null ? playerTransform.position - doorwayCenter : doorNormal;
        float side = Vector3.Dot(toPlayer, doorNormal) >= 0f ? 1f : -1f;

        // 왼쪽 문 끝은 +doorAxis 방향 → Y축 +각도 회전 시 +doorNormal 쪽으로 이동 (Unity 왼손 좌표계)
        // 플레이어가 있는 쪽(side)의 반대로 열리도록 부호를 뒤집는다. 오른쪽 문은 거울 대칭.
        float leftAngle = -openAngle * side;
        leftOpenRot = leftClosedRot * Quaternion.Euler(0f, leftAngle, 0f);
        rightOpenRot = rightClosedRot * Quaternion.Euler(0f, -leftAngle, 0f);

        StartAnimation(leftOpenRot, rightOpenRot, doorSpeed);
    }

    public void CloseDoors()
    {
        if (isLocked || !isOpen || leftHinge == null) return;
        isOpen = false;
        StartAnimation(leftClosedRot, rightClosedRot, doorSpeed);
    }

    public void SlamAndLockDoors()
    {
        if (isLocked || leftHinge == null) return;
        isLocked = true;
        isOpen = false;
        StartAnimation(leftClosedRot, rightClosedRot, 10f);
    }

    void StartAnimation(Quaternion targetLeft, Quaternion targetRight, float speed)
    {
        if (activeCoroutine != null) StopCoroutine(activeCoroutine);
        activeCoroutine = StartCoroutine(AnimateDoors(targetLeft, targetRight, speed));
    }

    IEnumerator AnimateDoors(Quaternion targetLeft, Quaternion targetRight, float speed)
    {
        float t = 0f;
        Quaternion startLeft = leftHinge.localRotation;
        Quaternion startRight = rightHinge.localRotation;

        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.deltaTime * speed);
            leftHinge.localRotation = Quaternion.Slerp(startLeft, targetLeft, t);
            rightHinge.localRotation = Quaternion.Slerp(startRight, targetRight, t);
            yield return null;
        }
        activeCoroutine = null;
    }

    static Transform FindByName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return null;
        GameObject found = GameObject.Find(objectName);
        return found != null ? found.transform : null;
    }

    static Bounds GetWorldBounds(Transform target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(target.position, Vector3.zero);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    // AABB가 주어진 수평 방향으로 중심에서 얼마나 뻗어 있는지
    static float ExtentAlong(Bounds bounds, Vector3 dir)
    {
        return Mathf.Abs(dir.x) * bounds.extents.x + Mathf.Abs(dir.z) * bounds.extents.z;
    }

    static Transform CreateHinge(string hingeName, Transform door, Vector3 worldPos)
    {
        Transform hinge = new GameObject(hingeName).transform;
        hinge.SetParent(door.parent, false);
        hinge.position = worldPos;
        hinge.rotation = Quaternion.identity; // 경첩의 로컬 Y축 = 월드 위쪽 (모델 축 변환과 무관하게 수직 회전)
        door.SetParent(hinge, true);
        return hinge;
    }
}

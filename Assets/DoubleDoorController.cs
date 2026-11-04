using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class DoubleDoorController : MonoBehaviour
{
    [Header("문 오브젝트 연결")]
    public Transform leftDoor;
    public Transform rightDoor;

    [Header("회전 각도 설정 (안쪽으로 열림)")]
    public float leftOpenAngle = 90f;
    public float rightOpenAngle = -90f;
    public float doorSpeed = 2.5f;

    [Header("상호작용 설정")]
    public float interactionDistance = 6f;

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
        if (player != null)
        {
            playerTransform = player.transform;
        }

        // 로컬 Y축 기준으로 문 열림 각도 설정
        if (leftDoor != null)
        {
            leftClosedRot = leftDoor.localRotation;
            leftOpenRot = leftClosedRot * Quaternion.Euler(0f, leftOpenAngle, 0f);
        }

        if (rightDoor != null)
        {
            rightClosedRot = rightDoor.localRotation;
            rightOpenRot = rightClosedRot * Quaternion.Euler(0f, rightOpenAngle, 0f);
        }
    }

    void Update()
    {
        if (isLocked || isOpen || playerTransform == null) return;
        if (Keyboard.current == null) return;

        // 플레이어와 문 사이 거리 체크
        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist <= interactionDistance && Keyboard.current.eKey.wasPressedThisFrame)
        {
            OpenDoors();
        }
    }

    public void OpenDoors()
    {
        if (isLocked || isOpen) return;
        isOpen = true;

        if (activeCoroutine != null) StopCoroutine(activeCoroutine);
        activeCoroutine = StartCoroutine(AnimateDoors(leftOpenRot, rightOpenRot, doorSpeed));
    }

    public void SlamAndLockDoors()
    {
        if (isLocked) return;
        isLocked = true;
        isOpen = false;

        if (activeCoroutine != null) StopCoroutine(activeCoroutine);
        StartCoroutine(AnimateDoors(leftClosedRot, rightClosedRot, 10f));
    }

    IEnumerator AnimateDoors(Quaternion targetLeft, Quaternion targetRight, float speed)
    {
        float t = 0f;
        Quaternion startLeft = leftDoor != null ? leftDoor.localRotation : Quaternion.identity;
        Quaternion startRight = rightDoor != null ? rightDoor.localRotation : Quaternion.identity;

        while (t < 1f)
        {
            t += Time.deltaTime * speed;
            if (leftDoor != null) leftDoor.localRotation = Quaternion.Slerp(startLeft, targetLeft, t);
            if (rightDoor != null) rightDoor.localRotation = Quaternion.Slerp(startRight, targetRight, t);
            yield return null;
        }
    }
}
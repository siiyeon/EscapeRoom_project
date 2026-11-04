using UnityEngine;

public class SchoolEntranceTrigger : MonoBehaviour
{
    [Header("문 컨트롤러 연결")]
    public DoubleDoorController doorController;

    private bool triggered = false;

    private void OnTriggerEnter(Collider other)
    {
        // 들어온 대상이 플레이어이고, 아직 작동 안 했다면 실행
        if (!triggered && other.CompareTag("Player"))
        {
            triggered = true;
            if (doorController != null)
            {
                doorController.SlamAndLockDoors();
            }
            // 한 번 닫힌 후 트리거 비활성화
            gameObject.SetActive(false);
        }
    }
}
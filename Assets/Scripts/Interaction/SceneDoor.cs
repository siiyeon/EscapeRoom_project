using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// E키를 누르면 다른 씬(교실 맵)으로 이동하는 문.
/// - 학교 건물 문: schoolDoorName(예: Door_1F_C08)을 넣으면 그 문 위치를 사용하고, 도착 위치는 복도 쪽 문 앞.
/// - 교실 씬 출구: 이 오브젝트를 바닥의 문 위치에 두고, arrivalPoint에 교실 안쪽 도착 위치를 넣는다.
/// </summary>
public class SceneDoor : MonoBehaviour
{
    [Header("이 문의 이름 (다른 씬에서 여기로 도착할 때 사용)")]
    public string doorId = "Room_Exit";

    [Header("E키를 누르면 이동할 씬과, 그 씬에서 도착할 문 이름")]
    public string targetScene = "SampleScene";
    public string targetDoorId = "School_Science";

    [Header("학교 건물 문에 붙일 때만: 문 이름 (뒤에 _L, _R 이 붙은 두 짝)")]
    public string schoolDoorName = "";

    [Header("도착 위치 (비워두면 문 앞 1.3m)")]
    public Transform arrivalPoint;

    [Header("상호작용 설정")]
    public float interactionDistance = 2.5f;
    public bool isLocked = false; // 잠그면 E키로 안 열림 (열쇠 퍼즐용)

    // 학교 씬에 자동으로 붙는 교실 이동 문 (학교 문 이름, 이 문의 id, 이동할 씬)
    // 새 교실 씬을 만들면 여기에 한 줄 추가
    static readonly (string door, string id, string scene)[] SchoolDoors =
    {
        ("Door_1F_C08", "School_Science", "ScienceRoom"), // 과학실
        ("Door_2F_C06", "School_Music",   "MusicRoom"),   // 음악실
    };

    private Transform player;
    private bool ready;
    private Vector3 interactCenter;
    private Vector3 arrivalFloorPos;
    private Quaternion arrivalRotation;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // 학교 씬이 로드될 때마다 교실 이동 문을 자동으로 붙인다
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneDoor[] existing = FindObjectsByType<SceneDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var d in SchoolDoors)
        {
            bool alreadySet = false;
            foreach (SceneDoor e in existing) if (e.schoolDoorName == d.door) { alreadySet = true; break; }
            if (alreadySet || GameObject.Find(d.door + "_L") == null) continue;

            var go = new GameObject("SceneDoor_" + d.door);
            SceneManager.MoveGameObjectToScene(go, scene);
            var door = go.AddComponent<SceneDoor>();
            door.doorId = d.id;
            door.targetScene = d.scene;
            door.targetDoorId = "Room_Exit";
            door.schoolDoorName = d.door;
        }
    }

    void Update()
    {
        if (isLocked || SceneTravel.IsTraveling || Keyboard.current == null) return;
        if (!Keyboard.current.eKey.wasPressedThisFrame) return;

        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p == null) return;
            player = p.transform;
        }
        if (!EnsureReady()) return;
        if (Vector3.Distance(interactCenter, player.position) > interactionDistance) return;

        SceneTravel.Travel(targetScene, targetDoorId);
    }

    /// <summary>이 문으로 도착할 때의 바닥 위치와 바라볼 방향.</summary>
    public bool TryGetArrival(out Vector3 floorPos, out Quaternion rotation)
    {
        bool ok = EnsureReady();
        floorPos = arrivalFloorPos;
        rotation = arrivalRotation;
        return ok;
    }

    bool EnsureReady()
    {
        if (ready) return true;

        if (!string.IsNullOrEmpty(schoolDoorName))
        {
            GameObject l = GameObject.Find(schoolDoorName + "_L");
            GameObject r = GameObject.Find(schoolDoorName + "_R");
            if (l == null || r == null)
            {
                Debug.LogWarning($"[SceneDoor] 문을 찾지 못했습니다: {schoolDoorName}_L / _R");
                return false;
            }
            Bounds lb = GetWorldBounds(l.transform);
            Bounds rb = GetWorldBounds(r.transform);
            interactCenter = (lb.center + rb.center) / 2f;

            Vector3 axis = Flat(rb.center - lb.center).normalized;
            Vector3 intoRoom = Vector3.Cross(Vector3.up, axis);
            // 교실은 복도보다 정문(Door_Entrance_L) 쪽에 있다
            GameObject entrance = GameObject.Find("Door_Entrance_L");
            if (entrance != null && Vector3.Dot(entrance.transform.position - interactCenter, intoRoom) < 0f)
                intoRoom = -intoRoom;

            float floorY = Mathf.Min(lb.min.y, rb.min.y);
            Vector3 corridor = -intoRoom;
            arrivalFloorPos = new Vector3(interactCenter.x, floorY, interactCenter.z) + corridor * 1.3f;
            arrivalRotation = Quaternion.LookRotation(corridor);
        }
        else
        {
            interactCenter = transform.position + Vector3.up;
            Transform a = arrivalPoint;
            Vector3 forward = Flat(a != null ? a.forward : transform.forward);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            arrivalFloorPos = a != null ? a.position : transform.position + forward.normalized * 1.3f;
            arrivalRotation = Quaternion.LookRotation(forward);
        }

        ready = true;
        return true;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    static Bounds GetWorldBounds(Transform target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(target.position, Vector3.zero);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    void OnDrawGizmos()
    {
        if (!string.IsNullOrEmpty(schoolDoorName)) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + Vector3.up, interactionDistance);
        if (arrivalPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(arrivalPoint.position, 0.15f);
            Gizmos.DrawRay(arrivalPoint.position, arrivalPoint.forward);
        }
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 시작 지점(철문 바깥)에서 너무 멀리 못 가게 막는 보이지 않는 벽.
/// - 왼쪽/오른쪽: 철문에서 울타리 2칸 넘게 가면 막힘 → 열쇠 찾으라는 혼잣말
/// - 뒤쪽(학교 반대 방향): 울타리 3칸 거리에서 막힘 → 학교에 들어가야 한다는 혼잣말
/// 학교 씬이 로드되면 철문(Fence_Gate_L/R) 위치를 기준으로 자동으로 만들어진다.
/// </summary>
public class OutsideBoundary : MonoBehaviour
{
    [Header("크기 (울타리 한 칸 = 약 2.83m)")]
    public float fencePanelWidth = 2.83f;
    public int sidePanels = 2;     // 철문 양옆으로 울타리 몇 칸까지 갈 수 있는지
    public int backPanels = 3;     // 학교 반대쪽으로 울타리 몇 칸 거리까지 갈 수 있는지
    public float gateHalfWidth = 2.75f; // 철문 가운데 ~ 철문 기둥까지

    [Header("혼잣말")]
    public float warnDistance = 0.8f;   // 벽에 이만큼 가까워지면 말풍선
    public float noticeDuration = 2.5f;
    // 옆 벽에 다가갈 때마다 이 중 하나가 랜덤으로 나온다
    // 열쇠를 줍기 전: 옆/뒤 어느 벽이든 이 대사, 열쇠를 주운 뒤: withKeyMessage
    public string[] sideMessagesNoKey = { "철문 주변에 열쇠가 있다고 했지.." };
    public string withKeyMessage = "이제 학교에 들어가자";

    float sideLimit, backLimit;
    int nearWall; // 0 = 벽 근처 아님, 1 = 옆 벽, 2 = 뒤 벽 (벽에 새로 다가갈 때만 랜덤 멘트를 다시 고른다)
    Transform player;
    string notice;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindAnyObjectByType<OutsideBoundary>() != null) return;
        GameObject l = GameObject.Find("Fence_Gate_L"), r = GameObject.Find("Fence_Gate_R");
        if (l == null || r == null) return;

        Bounds lb = WorldBounds(l.transform), rb = WorldBounds(r.transform);
        Vector3 center = (lb.center + rb.center) / 2f;
        center.y = Mathf.Min(lb.min.y, rb.min.y);
        Vector3 axis = rb.center - lb.center; axis.y = 0f; axis.Normalize();
        Vector3 outward = Vector3.Cross(Vector3.up, axis);
        // 바깥 = 학교(정문 현관) 반대쪽
        GameObject school = GameObject.Find("Door_Entrance_L");
        if (school != null && Vector3.Dot(school.transform.position - center, outward) > 0f) outward = -outward;

        var go = new GameObject("[OutsideBoundary]");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.SetPositionAndRotation(center, Quaternion.LookRotation(outward));
        go.AddComponent<OutsideBoundary>();
    }

    void Start()
    {
        sideLimit = gateHalfWidth + sidePanels * fencePanelWidth;
        backLimit = backPanels * fencePanelWidth;

        // 로컬 좌표: x = 철문을 따라 옆으로, z = 학교 반대쪽(바깥)으로
        const float height = 6f, thick = 0.5f;
        AddWall("Wall_Left", new Vector3(-sideLimit - thick / 2f, height / 2f, backLimit / 2f), new Vector3(thick, height, backLimit + thick));
        AddWall("Wall_Right", new Vector3(sideLimit + thick / 2f, height / 2f, backLimit / 2f), new Vector3(thick, height, backLimit + thick));
        AddWall("Wall_Back", new Vector3(0f, height / 2f, backLimit + thick / 2f), new Vector3(sideLimit * 2f + thick * 2f, height, thick));
    }

    void AddWall(string wallName, Vector3 localCenter, Vector3 size)
    {
        var wall = new GameObject(wallName);
        wall.transform.SetParent(transform, false);
        var box = wall.AddComponent<BoxCollider>();
        box.center = localCenter;
        box.size = size;
    }

    void Update()
    {
        if (SceneTravel.IsTraveling || InventoryUI.IsOpen) return;
        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p == null) return;
            player = p.transform;
        }

        Vector3 local = transform.InverseTransformPoint(player.position);
        int wall = 0;
        if (local.z >= 0.3f) // 철문 안쪽(학교 쪽)에서는 상관없음
        {
            if (local.z > backLimit - warnDistance) wall = 2;
            else if (Mathf.Abs(local.x) > sideLimit - warnDistance) wall = 1;
        }

        if (wall != 0 && wall != nearWall)
        {
            // 벽에 새로 다가간 순간 멘트를 고른다
            if (KeyPickup.HasKey2 || sideMessagesNoKey.Length == 0) notice = withKeyMessage;
            else notice = sideMessagesNoKey[Random.Range(0, sideMessagesNoKey.Length)];
        }
        nearWall = wall;
        if (wall != 0) GameHUD.Say(notice, noticeDuration); // 벽 앞에 있는 동안 계속 보이고, 떠나면 잠시 후 사라짐
    }


    static Bounds WorldBounds(Transform t)
    {
        Renderer[] rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }
}

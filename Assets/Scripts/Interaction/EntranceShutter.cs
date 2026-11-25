using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 학교 현관 철제 셔터 (모델: Shutter_Curtain / Shutter_Housing / Shutter_Rails).
/// - 처음에는 셔터가 위(Shutter_Housing 안)에 감겨 있다 → 셔터 판은 안 보이고 지나갈 수 있다.
/// - 플레이어가 셔터를 지나 안으로 들어오면 셔터가 위에서 쾅 떨어져 현관을 막는다 (엔딩 전까지 계속 닫힘).
/// 학교 씬이 로드되면 자동으로 붙는다 (씬에 넣을 필요 없음).
/// </summary>
public class EntranceShutter : MonoBehaviour
{
    public string curtainName = "Shutter_Curtain";
    public string insideMarkerName = "Door_1F_C08"; // 건물 안쪽 방향 판단용
    public float dropDistanceInside = 0.8f;         // 셔터를 지나 이만큼 들어오면 떨어짐 (셔터에 끼지 않게)
    public float dropDuration = 0.5f;

    // 교실에 다녀와서 학교 씬이 다시 로드돼도 닫힌 상태 유지
    static bool closedUntilEnding;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnGameStart() => closedUntilEnding = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindAnyObjectByType<EntranceShutter>() != null) return;
        if (GameObject.Find("Shutter_Curtain") == null)
        {
            if (GameObject.Find("Fence_Gate_L") != null) // 학교 씬인데 셔터가 없으면 알려준다
                Debug.LogWarning("[EntranceShutter] 학교 모델에서 'Shutter_Curtain'을 찾지 못했습니다. 셔터가 있는 새 학교 모델인지 확인하세요.");
            return;
        }
        var go = new GameObject("EntranceShutter");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<EntranceShutter>();
    }

    Transform curtain;
    Renderer[] renderers;
    Collider[] colliders;
    Vector3 downPosition;     // 닫힌(내려온) 위치 = 모델 원래 위치
    float height;             // 셔터 판 높이 (이만큼 위에서 떨어진다)
    Vector3 planePoint, insideDir;
    bool closed;

    void Start()
    {
        GameObject go = GameObject.Find(curtainName);
        if (go == null) { enabled = false; return; }
        curtain = go.transform;
        renderers = go.GetComponentsInChildren<Renderer>(true);
        colliders = go.GetComponentsInChildren<Collider>(true);

        Bounds b = WorldBounds(renderers, curtain.position);
        downPosition = curtain.position;
        height = Mathf.Max(b.size.y, 0.5f);
        planePoint = b.center;

        // 셔터 면에 수직인 방향 = 셔터가 얇은 축, 그중 건물 안쪽
        insideDir = b.size.x < b.size.z ? Vector3.right : Vector3.forward;
        GameObject inside = GameObject.Find(insideMarkerName);
        if (inside != null && Vector3.Dot(inside.transform.position - planePoint, insideDir) < 0f) insideDir = -insideDir;

        closed = closedUntilEnding;
        SetVisible(closed);
    }

    void Update()
    {
        if (closed || curtain == null || SceneTravel.IsTraveling) return;
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return;
        if (Vector3.Dot(player.transform.position - planePoint, insideDir) < dropDistanceInside) return;

        closed = closedUntilEnding = true;
        Debug.Log("[EntranceShutter] 플레이어가 들어와서 현관 셔터가 내려옵니다.");
        StartCoroutine(Drop());
    }

    IEnumerator Drop()
    {
        // 셔터 판을 보이게 하고 위(감겨 있던 곳)에서 원래 위치까지 떨어뜨린다. 충돌은 다 내려온 뒤에 켠다.
        foreach (Renderer r in renderers) r.enabled = true;
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.deltaTime / dropDuration);
            float e = t * t; // 점점 빨라지며 떨어짐
            curtain.position = downPosition + Vector3.up * height * (1f - e);
            yield return null;
        }
        curtain.position = downPosition;
        foreach (Collider c in colliders) c.enabled = true;
        Physics.SyncTransforms();
    }

    void SetVisible(bool down)
    {
        curtain.position = downPosition;
        foreach (Renderer r in renderers) r.enabled = down;
        foreach (Collider c in colliders) c.enabled = down;
        Physics.SyncTransforms();
    }

    /// <summary>엔딩 때 호출: 셔터를 다시 올린다.</summary>
    public void OpenForEnding()
    {
        StopAllCoroutines();
        closed = closedUntilEnding = false;
        SetVisible(false);
    }

    static Bounds WorldBounds(Renderer[] rs, Vector3 fallback)
    {
        if (rs.Length == 0) return new Bounds(fallback, Vector3.zero);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }
}

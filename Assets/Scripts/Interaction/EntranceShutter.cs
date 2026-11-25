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
    public string insideMarkerName = "Door_1F_C08";
    public float dropDistanceInside = 0.8f;         // 셔터를 지나 이만큼 들어오면 떨어짐 (셔터에 끼지 않게)
    const float SourceFrameRate = 24f;
    const float ImpactFrame = 10f;
    const float SettleFrame = 18f;

    // 교실에 다녀와서 학교 씬이 다시 로드돼도 닫힌 상태 유지
    static bool closedUntilEnding;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    internal static void ResetOnGameStart() => closedUntilEnding = false;

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
    SkinnedMeshRenderer curtainRenderer;
    int openShapeIndex;
    Collider[] colliders;
    Bounds closedBounds;
    float openLift;
    Vector3 insideDir;
    CharacterController player;
    bool closed;

    void Start()
    {
        GameObject go = GameObject.Find(curtainName);
        if (go == null) { enabled = false; return; }
        curtain = go.transform;
        curtainRenderer = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
        openShapeIndex = FindOpenShape(curtainRenderer != null ? curtainRenderer.sharedMesh : null);
        if (openShapeIndex < 0)
        {
            Debug.LogWarning("[EntranceShutter] Could not find the Open blend shape on the shutter curtain.", this);
            enabled = false;
            return;
        }
        colliders = go.GetComponentsInChildren<Collider>(true);

        curtainRenderer.updateWhenOffscreen = true;
        var baked = new Mesh();
        SetPose(0f);
        closedBounds = BakeWorldBounds(baked);
        SetPose(1f);
        openLift = BakeWorldBounds(baked).min.y - closedBounds.min.y;
        Destroy(baked);

        var floor = GameObject.Find("Floor_1F")?.GetComponentInChildren<Collider>();
        if (floor != null && floor.Raycast(new Ray(closedBounds.center, Vector3.down), out RaycastHit floorHit, closedBounds.size.y))
        {
            float floorOffset = floorHit.point.y - closedBounds.min.y;
            curtain.position += Vector3.up * floorOffset;
            closedBounds.center += Vector3.up * floorOffset;
        }

        // 셔터 면에 수직인 방향 = 셔터가 얇은 축, 그중 건물 안쪽
        insideDir = closedBounds.size.x < closedBounds.size.z ? Vector3.right : Vector3.forward;
        GameObject inside = GameObject.Find(insideMarkerName) ?? GameObject.Find("Shutter_Trigger");
        if (inside != null && Vector3.Dot(inside.transform.position - closedBounds.center, insideDir) < 0f) insideDir = -insideDir;

        closed = closedUntilEnding;
        SetPose(closed ? 0f : 1f);
        SetCollision(closed);
    }

    void LateUpdate()
    {
        if (closed || curtain == null || SceneTravel.IsTraveling) return;
        if (player == null) player = GameObject.FindWithTag("Player")?.GetComponent<CharacterController>();
        if (player == null || !player.enabled) return;

        Bounds playerBounds = player.bounds;
        Vector3 center = playerBounds.center;
        float depth = SignedDepth(center);
        float clearance = Mathf.Abs(insideDir.x) * (playerBounds.extents.x + closedBounds.extents.x)
                        + Mathf.Abs(insideDir.z) * (playerBounds.extents.z + closedBounds.extents.z);
        float triggerStart = Mathf.Max(dropDistanceInside, clearance + 0.05f);
        if (depth < triggerStart || depth > triggerStart + 2f) return;

        // Project the player's capsule onto the shutter plane to check the doorway's width and height.
        Bounds projectedPlayer = playerBounds;
        projectedPlayer.center = center - insideDir * depth;
        if (!closedBounds.Intersects(projectedPlayer)) return;

        closed = true;
        StartCoroutine(Drop());
    }

    float SignedDepth(Vector3 point) => Vector3.Dot(point - closedBounds.center, insideDir);

    IEnumerator Drop()
    {
        float frame = 0f;
        bool impacted = false;
        while (frame < SettleFrame)
        {
            float nextFrame = Mathf.Min(frame + Time.deltaTime * SourceFrameRate, impacted ? SettleFrame : ImpactFrame);
            float openness = SampleSlam(nextFrame);
            if (!impacted && player != null && player.enabled)
            {
                Bounds playerBounds = player.bounds;
                if (SignedDepth(playerBounds.center) < 0f ||
                    (playerBounds.max.y >= closedBounds.min.y + openLift * openness && closedBounds.Intersects(playerBounds)))
                {
                    closed = false;
                    SetPose(1f);
                    yield break;
                }
            }

            frame = nextFrame;
            SetPose(openness);
            if (!impacted && frame >= ImpactFrame)
            {
                impacted = true;
                SetCollision(true);
                AudioClip impact = Resources.Load<AudioClip>("Audio/EntranceShutterClose");
                if (impact != null) AudioSource.PlayClipAtPoint(impact, closedBounds.center);
            }
            if (frame >= SettleFrame) break;
            yield return null;
        }
        closedUntilEnding = true;
    }

    // Shutter_Slam animates the Open shape key in Blender. Begin at its first moving key (frame 10).
    static float SampleSlam(float frame)
    {
        if (frame <= ImpactFrame) return 1f - Mathf.Pow(frame / ImpactFrame, 2f);
        if (frame <= 13f) return Mathf.SmoothStep(0f, 0.06f, (frame - ImpactFrame) / 3f);
        if (frame <= 15f) return Mathf.SmoothStep(0.06f, 0f, (frame - 13f) / 2f);
        if (frame <= 17f) return Mathf.SmoothStep(0f, 0.018f, (frame - 15f) / 2f);
        return Mathf.SmoothStep(0.018f, 0f, frame - 17f);
    }

    void SetPose(float openness)
    {
        curtainRenderer.SetBlendShapeWeight(openShapeIndex, openness * 100f);
    }

    void SetCollision(bool enabled)
    {
        foreach (Collider c in colliders) c.enabled = enabled;
        Physics.SyncTransforms();
    }

    /// <summary>엔딩 때 호출: 셔터를 다시 올린다.</summary>
    public void OpenForEnding()
    {
        StopAllCoroutines();
        closed = closedUntilEnding = false;
        if (curtainRenderer == null || openShapeIndex < 0) return;
        SetCollision(false);
        SetPose(1f);
    }

    static int FindOpenShape(Mesh mesh)
    {
        if (mesh == null) return -1;
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string name = mesh.GetBlendShapeName(i);
            if (name == "Open" || name.EndsWith(".Open", System.StringComparison.Ordinal)) return i;
        }
        return -1;
    }

    Bounds BakeWorldBounds(Mesh baked)
    {
        curtainRenderer.BakeMesh(baked);
        Vector3[] vertices = baked.vertices;
        Transform meshTransform = curtainRenderer.transform;
        var bounds = new Bounds(meshTransform.TransformPoint(vertices[0]), Vector3.zero);
        for (int i = 1; i < vertices.Length; i++) bounds.Encapsulate(meshTransform.TransformPoint(vertices[i]));
        return bounds;
    }
}

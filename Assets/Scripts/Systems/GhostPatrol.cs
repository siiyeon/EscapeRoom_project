using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class GhostPatrol : MonoBehaviour
{
    [Range(0.1f, 0.95f)] public float walkingSpeedRatio = 0.8f;
    [Min(0.1f)] public float fallbackSpeed = 2.8f;
    [Min(0f)] public float pauseAtDestination = 1f;
    [Range(0f, 1f)] public float floorChangeChance = 0.4f; // 다음 목적지를 다른 층으로 고를 확률
    public bool IsReady { get; private set; }
    public float FloorHeight { get; private set; }      // 2층 바닥 높이
    public float TopFloorHeight { get; private set; }   // 3층 바닥 높이 (2층과 3층을 오가며 순찰)
    public string allowedStairName = "Stair_2F_3F";     // 귀신이 오르내릴 수 있는 계단
    public int PatrolPointCount => patrolPoints.Count;

    const float FloorTolerance = 0.15f;
    readonly List<Vector3> patrolPoints = new List<Vector3>();
    NavMeshPath path;
    NavMeshData navigation;
    NavMeshDataInstance navigationInstance;
    NavMeshAgent agent;
    float nextMoveTime;
    float stuckTime;
    Vector3 lastPosition;
    Vector3 safePosition;
    PlayerController player;

    // 여자 귀신 애니메이션: 멈춰 있으면 Idle, 움직이면 Walk (HorrorGirlImporter가 만든 애니메이터 사용)
    [Header("애니메이션")]
    public string animatorResource = "HorrorGirlAnimator";
    [Min(0.01f)] public float walkClipSpeed = 0.25f;   // Walk 애니메이션이 발이 안 미끄러지는 이동 속도 (README 기준)
    [Min(1f)] public float maxWalkPlaybackSpeed = 2.5f; // 실제 이동이 더 빠를 때 애니메이션을 최대 몇 배까지 빠르게 재생할지
    Animator animator;
    static readonly int SpeedParam = Animator.StringToHash("Speed");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        SceneManager.sceneLoaded -= BindScene;
        SceneManager.sceneLoaded += BindScene;
    }

    static void BindScene(Scene scene, LoadSceneMode mode)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
        {
            if (item.name != "Ghost" || item.GetComponent<GhostPatrol>() != null) continue;
            item.gameObject.AddComponent<GhostPatrol>();
        }
    }

    void Start()
    {
        path = new NavMeshPath();
        Transform floor2 = null, floor3 = null;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        foreach (Transform item in root.GetComponentsInChildren<Transform>())
        {
            if (item.name == "Floor_2F") floor2 = item;
            else if (item.name == "Floor_3F") floor3 = item;
        }
        if (floor2 == null || !TryGetBounds(floor2, out Bounds bounds))
        { Fail("Cannot find the second-floor model (Floor_2F)."); return; }
        FloorHeight = bounds.max.y;
        TopFloorHeight = floor3 != null && TryGetBounds(floor3, out Bounds b3) ? b3.max.y : FloorHeight;
        Transform school = floor2.root;

        // 2층 ~ 3층 높이만 길로 만든다. 2층↔3층 계단만 걸을 수 있고, 다른 계단(1층↔2층 등)은 빼서
        // 귀신이 1층으로 내려가지 않게 한다.
        var markups = new List<NavMeshBuildMarkup>();
        foreach (Transform item in school.GetComponentsInChildren<Transform>())
            if (item.name.StartsWith("Stair_") && item.name != allowedStairName)
                markups.Add(new NavMeshBuildMarkup { root = item, ignoreFromBuild = true });
        var sources = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(school, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
        settings.agentRadius = 0.3f;
        settings.agentHeight = 2.0f;  // 귀신 키 약 1.9m (Scale 1.19) + 여유
        settings.agentClimb = 0.25f;  // 계단 한 칸 0.2m
        settings.agentSlope = 45f;    // 계단 경사 약 37도
        settings.overrideVoxelSize = true;
        settings.voxelSize = 0.1f;
        float bandBottom = FloorHeight - 0.3f, bandTop = TopFloorHeight + 2.9f;
        Bounds buildBounds = new Bounds(new Vector3(bounds.center.x, (bandBottom + bandTop) / 2f, bounds.center.z),
            new Vector3(bounds.size.x, bandTop - bandBottom, bounds.size.z));
        navigation = NavMeshBuilder.BuildNavMeshData(settings, sources, buildBounds, Vector3.zero, Quaternion.identity);
        if (navigation == null) { Fail("Could not build second/third-floor navigation."); return; }
        navigationInstance = NavMesh.AddNavMeshData(navigation);

        // Seed in the corridor between a classroom door and the lockers opposite it.
        Transform classroom = FindNamed(school, "Door_2F_C01");
        Transform locker = FindNamed(school, "LockerDoor_2F_B01_C1L");
        Vector3 seed = classroom != null && locker != null
            ? (classroom.position + locker.position) * 0.5f : bounds.center;
        seed.y = FloorHeight;
        if (!SampleFloor(seed, 1.5f, FloorHeight, out Vector3 spawn))
        { Fail("Could not find a second-floor corridor spawn."); return; }

        // 2층과 3층 모두에 순찰 지점을 깐다 → 다른 층 지점이 뽑히면 계단으로 오르내린다
        int upstairs = 0;
        foreach (float floorY in TopFloorHeight > FloorHeight + 1f ? new[] { FloorHeight, TopFloorHeight } : new[] { FloorHeight })
        for (float x = bounds.min.x + 0.75f; x < bounds.max.x - 0.75f; x += 3f)
        for (float z = bounds.min.z + 0.75f; z < bounds.max.z - 0.75f; z += 1.5f)
        {
            if (!SampleFloor(new Vector3(x, floorY, z), 0.5f, floorY, out Vector3 point)) continue;
            if (Vector3.Distance(spawn, point) < 1f || !SafePath(spawn, point)) continue;
            patrolPoints.Add(point);
            if (floorY != FloorHeight) upstairs++;
        }
        if (patrolPoints.Count < 2) { Fail("Not enough connected patrol points."); return; }
        if (TopFloorHeight > FloorHeight + 1f && upstairs == 0)
            Debug.LogWarning("[GhostPatrol] 3층으로 가는 길을 찾지 못해 2층만 순찰합니다. (계단 '" + allowedStairName + "' 충돌체 확인)", this);

        foreach (Animator a in GetComponentsInChildren<Animator>()) a.applyRootMotion = false;
        animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.runtimeAnimatorController == null && !string.IsNullOrEmpty(animatorResource))
        {
            animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>(animatorResource);
            if (animator.runtimeAnimatorController == null)
                Debug.LogWarning("[GhostPatrol] 애니메이터 '" + animatorResource + "'를 찾지 못했습니다. 귀신이 애니메이션 없이 움직입니다.", this);
        }
        transform.position = spawn;
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();
        agent.agentTypeID = settings.agentTypeID;
        // NavMeshAgent 크기는 오브젝트 Scale이 곱해지므로 나눠서 길 설정과 실제 크기를 맞춘다
        float scale = Mathf.Max(transform.lossyScale.y, 0.01f);
        agent.radius = settings.agentRadius / scale;
        agent.height = settings.agentHeight / scale;
        agent.baseOffset = 0f;
        agent.angularSpeed = 180f;
        agent.acceleration = 5f;
        agent.stoppingDistance = 0.25f;
        agent.autoTraverseOffMeshLink = false;
        agent.autoRepath = false;
        agent.Warp(spawn);
        safePosition = lastPosition = spawn;
        player = FindFirstObjectByType<PlayerController>();
        UpdateSpeed();
        IsReady = true;
        ChooseDestination();
    }

    void Update()
    {
        if (!IsReady || agent == null || !agent.isOnNavMesh) return;
        UpdateSpeed();
        agent.isStopped = SceneTravel.IsTraveling;
        UpdateAnimation();
        if (agent.isStopped) return;

        // 2층~3층 범위(계단 포함)를 벗어나면 마지막 안전한 위치로 되돌린다
        if (!InBand(transform.position.y))
        {
            agent.ResetPath();
            agent.Warp(safePosition);
        }
        safePosition = transform.position;
        if (!agent.pathPending && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.1f))
        {
            if (nextMoveTime <= 0f) nextMoveTime = Time.time + pauseAtDestination;
            if (Time.time >= nextMoveTime) ChooseDestination();
        }
        else nextMoveTime = 0f;

        stuckTime = agent.hasPath && Vector3.Distance(lastPosition, transform.position) < 0.05f * Time.deltaTime
            ? stuckTime + Time.deltaTime : 0f;
        lastPosition = transform.position;
        if (stuckTime > 3f) { agent.ResetPath(); stuckTime = 0f; nextMoveTime = 0f; }
    }

    void UpdateAnimation()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        float moving = agent.isStopped ? 0f : agent.velocity.magnitude;
        animator.SetFloat(SpeedParam, moving);
        animator.speed = moving > 0.1f ? Mathf.Clamp(moving / (walkClipSpeed * transform.lossyScale.y), 1f, maxWalkPlaybackSpeed) : 1f; // 키가 크면 보폭도 커짐
    }

    void UpdateSpeed()
    {
        agent.speed = player != null ? Mathf.Max(0.1f, player.walkSpeed * walkingSpeedRatio) : fallbackSpeed;
    }

    void ChooseDestination()
    {
        nextMoveTime = 0f;
        bool wantOtherFloor = TopFloorHeight > FloorHeight + 1f && Random.value < floorChangeChance;
        for (int attempt = 0; attempt < 30; attempt++)
        {
            Vector3 point = patrolPoints[Random.Range(0, patrolPoints.Count)];
            // 일정 확률로 다른 층 지점을 골라 계단을 오르내리게 한다 (20번 안에 못 찾으면 아무 지점이나)
            bool otherFloor = Mathf.Abs(point.y - transform.position.y) > 1f;
            if (attempt < 20 && otherFloor != wantOtherFloor) continue;
            if (Vector3.Distance(transform.position, point) < 4f || !SafePath(transform.position, point)) continue;
            agent.SetPath(path);
            return;
        }
        nextMoveTime = Time.time + 1f;
    }

    bool InBand(float y) => y >= FloorHeight - FloorTolerance && y <= Mathf.Max(FloorHeight, TopFloorHeight) + FloorTolerance;

    bool SampleFloor(Vector3 point, float radius, float floorY, out Vector3 result)
    {
        bool found = NavMesh.SamplePosition(point, out NavMeshHit hit, radius, NavMesh.AllAreas)
            && Mathf.Abs(hit.position.y - floorY) <= FloorTolerance;
        result = hit.position;
        return found;
    }

    bool SafePath(Vector3 from, Vector3 to)
    {
        if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return false;
        foreach (Vector3 corner in path.corners)
            if (!InBand(corner.y)) return false;
        return true;
    }

    static Transform FindNamed(Transform root, string objectName)
    {
        foreach (Transform item in root.GetComponentsInChildren<Transform>())
            if (item.name == objectName) return item;
        return null;
    }

    static bool TryGetBounds(Transform target, out Bounds bounds)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        bounds = new Bounds();
        if (renderers.Length == 0) return false;
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return true;
    }

    void Fail(string reason)
    {
        Debug.LogError("[GhostPatrol] " + reason, this);
        enabled = false;
    }

    void OnDestroy()
    {
        if (navigationInstance.valid) navigationInstance.Remove();
        if (navigation != null) Destroy(navigation);
    }
}

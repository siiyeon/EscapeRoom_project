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
    public bool IsReady { get; private set; }
    public float FloorHeight { get; private set; }
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
        Transform floor = null;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        foreach (Transform item in root.GetComponentsInChildren<Transform>())
            if (item.name == "Floor_2F") { floor = item; break; }
        if (floor == null || !TryGetBounds(floor, out Bounds bounds))
        { Fail("Cannot find the second-floor model (Floor_2F)."); return; }
        FloorHeight = bounds.max.y;
        Transform school = floor.root;

        // Bake only the second-floor height band. Stairs contribute no walkable geometry.
        var markups = new List<NavMeshBuildMarkup>();
        foreach (Transform item in school.GetComponentsInChildren<Transform>())
            if (item.name.StartsWith("Stair_"))
                markups.Add(new NavMeshBuildMarkup { root = item, ignoreFromBuild = true });
        var sources = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(school, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
        settings.agentRadius = 0.3f;
        settings.agentHeight = 1.8f;
        settings.agentClimb = 0.2f;
        settings.agentSlope = 35f;
        settings.overrideVoxelSize = true;
        settings.voxelSize = 0.1f;
        Bounds buildBounds = new Bounds(new Vector3(bounds.center.x, FloorHeight + 1.25f, bounds.center.z),
            new Vector3(bounds.size.x, 2.9f, bounds.size.z));
        navigation = NavMeshBuilder.BuildNavMeshData(settings, sources, buildBounds, Vector3.zero, Quaternion.identity);
        if (navigation == null) { Fail("Could not build second-floor navigation."); return; }
        navigationInstance = NavMesh.AddNavMeshData(navigation);

        // Seed in the corridor between a classroom door and the lockers opposite it.
        Transform classroom = FindNamed(school, "Door_2F_C01");
        Transform locker = FindNamed(school, "LockerDoor_2F_B01_C1L");
        Vector3 seed = classroom != null && locker != null
            ? (classroom.position + locker.position) * 0.5f : bounds.center;
        seed.y = FloorHeight;
        if (!SampleFloor(seed, 1.5f, out Vector3 spawn))
        { Fail("Could not find a second-floor corridor spawn."); return; }

        for (float x = bounds.min.x + 0.75f; x < bounds.max.x - 0.75f; x += 3f)
        for (float z = bounds.min.z + 0.75f; z < bounds.max.z - 0.75f; z += 1.5f)
        {
            if (!SampleFloor(new Vector3(x, FloorHeight, z), 0.5f, out Vector3 point)) continue;
            if (Vector3.Distance(spawn, point) < 1f || !SafePath(spawn, point)) continue;
            patrolPoints.Add(point);
        }
        if (patrolPoints.Count < 2) { Fail("Not enough connected second-floor patrol points."); return; }

        foreach (Animator animator in GetComponentsInChildren<Animator>()) animator.applyRootMotion = false;
        transform.position = spawn;
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();
        agent.agentTypeID = settings.agentTypeID;
        agent.radius = settings.agentRadius;
        agent.height = settings.agentHeight;
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
        if (agent.isStopped) return;

        // Guard against any externally assigned path or vertical displacement leaving this floor.
        if (Mathf.Abs(transform.position.y - FloorHeight) > FloorTolerance)
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

    void UpdateSpeed()
    {
        agent.speed = player != null ? Mathf.Max(0.1f, player.walkSpeed * walkingSpeedRatio) : fallbackSpeed;
    }

    void ChooseDestination()
    {
        nextMoveTime = 0f;
        for (int attempt = 0; attempt < 30; attempt++)
        {
            Vector3 point = patrolPoints[Random.Range(0, patrolPoints.Count)];
            if (Vector3.Distance(transform.position, point) < 4f || !SafePath(transform.position, point)) continue;
            agent.SetPath(path);
            return;
        }
        nextMoveTime = Time.time + 1f;
    }

    bool SampleFloor(Vector3 point, float radius, out Vector3 result)
    {
        bool found = NavMesh.SamplePosition(point, out NavMeshHit hit, radius, NavMesh.AllAreas)
            && Mathf.Abs(hit.position.y - FloorHeight) <= FloorTolerance;
        result = hit.position;
        return found;
    }

    bool SafePath(Vector3 from, Vector3 to)
    {
        if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return false;
        foreach (Vector3 corner in path.corners)
            if (Mathf.Abs(corner.y - FloorHeight) > FloorTolerance) return false;
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

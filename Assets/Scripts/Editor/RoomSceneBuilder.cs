using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// 교실 모델로 교실 씬(Assets/Scenes/Rooms/*.unity)을 만든다.
/// 메뉴: Tools > Escape Room > Build Room Scenes
/// - 플레이어는 넣지 않는다. 교실 씬에서 바로 테스트하려면 'Player' 태그를 붙인 플레이어를 직접 넣는다.
///   (학교에서 문으로 들어오면 학교 플레이어가 이어지고, 교실 씬에 있던 Player는 자동으로 지워진다)
/// - 이미 있는 씬은 건드리지 않는다. 처음부터 다시 만들려면 씬 파일을 지우고 실행.
/// - 모델에 충돌체(Generate Colliders)를 켜고, 출구 문(SceneDoor)과 조명을 넣고, Build Settings에 등록한다.
/// </summary>
public static class RoomSceneBuilder
{
    const string SceneDir = "Assets/Scenes/Rooms";
    const string SchoolSceneName = "SampleScene";

    class RoomSpec
    {
        public string SceneName;     // 씬 파일 이름 (SceneDoor의 targetScene과 같아야 함)
        public string ModelPath;
        public string DoorPrefix;    // 모델에서 출입문 틀 오브젝트 이름 시작
        public string FloorName;     // 모델에서 바닥 오브젝트 이름
        public string SchoolDoorId;  // 학교 쪽 SceneDoor id (나갈 때 도착할 곳)
    }

    // 새 교실 모델이 생기면 여기에 추가하고, SceneDoor.SchoolDoors에도 학교 문을 추가
    static readonly RoomSpec[] Rooms =
    {
        new RoomSpec { SceneName = "ScienceRoom", ModelPath = "Assets/Models/Environment/Science Class.fbx",
                       DoorPrefix = "DoorFrame_", FloorName = "Floor", SchoolDoorId = "School_Science" },
        new RoomSpec { SceneName = "MusicRoom", ModelPath = "Assets/Models/Environment/Music Class.fbx",
                       DoorPrefix = "MR_DoorFrame", FloorName = "MR_Floor", SchoolDoorId = "School_Music" },
    };

    [MenuItem("Tools/Escape Room/Build Room Scenes")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string originalScene = SceneManager.GetActiveScene().path;

        var created = new List<string>();
        var untouched = new List<string>();
        try
        {
            EnsureFolder(SceneDir);
            foreach (RoomSpec spec in Rooms)
            {
                string path = $"{SceneDir}/{spec.SceneName}.unity";
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    EnableColliders(spec.ModelPath);
                    BuildScene(spec, path);
                    created.Add(spec.SceneName);
                }
                else untouched.Add(spec.SceneName);
                AddToBuildSettings(path);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Room Scenes", "씬을 만드는 중 오류가 났어요:\n" + e.Message, "확인");
            return;
        }
        finally
        {
            if (!string.IsNullOrEmpty(originalScene)) EditorSceneManager.OpenScene(originalScene);
        }

        string msg = "";
        if (created.Count > 0) msg += "새로 만든 씬: " + string.Join(", ", created) + "\n";
        if (untouched.Count > 0) msg += "이미 있어서 그대로 둔 씬: " + string.Join(", ", untouched) + "\n";
        msg += $"\n씬 위치: {SceneDir}";
        EditorUtility.DisplayDialog("Room Scenes", msg, "확인");
    }

    static void BuildScene(RoomSpec spec, string path)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(spec.ModelPath);
        if (model == null) throw new System.Exception($"{spec.ModelPath} 파일을 찾지 못했어요.");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var room = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
        room.name = spec.SceneName + "_Model";
        room.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        Bounds floor = BoundsOf(room.transform, t => t.name == spec.FloorName)
                       ?? throw new System.Exception($"{spec.ModelPath}에서 바닥 '{spec.FloorName}'을 찾지 못했어요.");
        Bounds door = BoundsOf(room.transform, t => t.name.StartsWith(spec.DoorPrefix))
                      ?? throw new System.Exception($"{spec.ModelPath}에서 문 '{spec.DoorPrefix}'를 찾지 못했어요.");
        float floorY = floor.max.y;

        // 문에서 교실 안쪽 방향: 문틀이 얇은 축(= 벽에 수직)을 따라 교실 중심 쪽
        // (문이 교실 모서리 근처에 있어도 벽을 따라가는 방향으로 잘못 잡히지 않도록)
        Vector3 d = floor.center - door.center;
        Vector3 inward = door.size.x < door.size.z
            ? new Vector3(Mathf.Sign(d.x), 0f, 0f)
            : new Vector3(0f, 0f, Mathf.Sign(d.z));

        // 출구 문: 바닥의 문 위치, 앞(+Z)이 교실 안쪽
        var exit = new GameObject("Exit_Door");
        SceneManager.MoveGameObjectToScene(exit, scene);
        exit.transform.SetPositionAndRotation(new Vector3(door.center.x, floorY, door.center.z), Quaternion.LookRotation(inward));
        var sceneDoor = exit.AddComponent<SceneDoor>();
        sceneDoor.doorId = "Room_Exit";
        sceneDoor.targetScene = SchoolSceneName;
        sceneDoor.targetDoorId = spec.SchoolDoorId;
        sceneDoor.interactionDistance = 2.5f;

        var arrival = new GameObject("ArrivalPoint");
        arrival.transform.SetParent(exit.transform, false);
        arrival.transform.localPosition = new Vector3(0f, 0f, 1.3f);
        sceneDoor.arrivalPoint = arrival.transform;

        // 조명: 어두운 공포 분위기 + 천장 근처 희미한 전등 하나
        var lightGo = new GameObject("Room Light");
        SceneManager.MoveGameObjectToScene(lightGo, scene);
        lightGo.transform.position = new Vector3(floor.center.x, floorY + 2.6f, floor.center.z);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 14f;
        light.intensity = 3f;
        light.color = new Color(0.85f, 0.9f, 1f);
        light.shadows = LightShadows.Soft;

        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.05f, 0.05f, 0.06f);

        if (!EditorSceneManager.SaveScene(scene, path))
            throw new System.Exception($"{path} 저장에 실패했어요.");
    }

    // 플레이어가 바닥을 뚫고 떨어지지 않도록 충돌체 생성을 켜고,
    // 모델에 딸려온 Blender 카메라가 플레이어 화면을 덮지 않도록 카메라 가져오기를 끈다
    static void EnableColliders(string modelPath)
    {
        var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (importer == null || (importer.addCollider && !importer.importCameras)) return;
        importer.addCollider = true;
        importer.importCameras = false;
        importer.SaveAndReimport();
    }

    static void AddToBuildSettings(string path)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == path)) return;
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    static Bounds? BoundsOf(Transform root, System.Func<Transform, bool> match)
    {
        Bounds? result = null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (!match(t)) continue;
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (result == null) result = r.bounds;
                else { var b = result.Value; b.Encapsulate(r.bounds); result = b; }
            }
        }
        return result;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}

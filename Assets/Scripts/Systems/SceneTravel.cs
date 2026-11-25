using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 이동 담당. 화면을 어둡게 → 씬 로드 → 도착할 문(SceneDoor) 앞에 플레이어 배치 → 화면 밝게.
/// 플레이어(태그 Player)는 씬이 바뀌어도 유지된다 (주운 열쇠 등 상태 그대로).
/// 새 씬에 들어 있는 다른 Player는 자동으로 지운다.
/// </summary>
public class SceneTravel : MonoBehaviour
{
    public static bool IsTraveling { get; private set; }

    const float FadeDuration = 0.35f;
    const string RoomStartDoorId = "Room_Exit"; // 교실 씬 출구 문 = 교실 씬에서 바로 시작할 때 시작 지점

    static SceneTravel runner;
    static GameObject persistentPlayer;
    static Texture2D blackTexture;
    static bool playerDead, restarting;

    float fadeAlpha;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        IsTraveling = false;
        playerDead = restarting = false;
        runner = null;
        persistentPlayer = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // 씬이 로드될 때마다 플레이어를 하나만 남긴다
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (restarting)
        {
            restarting = playerDead = IsTraveling = false;
            Time.timeScale = 1f;
        }
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        if (persistentPlayer == null && players.Length > 0)
        {
            persistentPlayer = players[0];
            DontDestroyOnLoad(persistentPlayer);

            // 교실 씬에서 바로 플레이를 시작했으면, 플레이어를 어디에 놓았든 출구 문 앞에서 시작
            if (!IsTraveling) PlacePlayerAt(RoomStartDoorId, warnIfMissing: false);
        }

        foreach (GameObject p in players)
        {
            if (p == persistentPlayer) continue;
            p.SetActive(false); // 같은 프레임에 다른 스크립트가 찾지 못하도록 먼저 끔
            Destroy(p);
        }

        if (persistentPlayer == null)
            Debug.LogWarning("[SceneTravel] 'Player' 태그가 붙은 플레이어가 없습니다. 이 씬에서 바로 테스트하려면 플레이어를 넣고 태그를 Player로 설정하세요.");
    }

    internal static bool TryBeginDeath(PlayerController player)
    {
        if (IsTraveling || InventoryUI.IsOpen || player == null) return false;
        playerDead = IsTraveling = true;
        player.enabled = false;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        return true;
    }

    internal static void RestartAfterDeath()
    {
        if (!playerDead || restarting) return;
        restarting = true;

        KeyPickup.ResetInventory();
        IronGateController.ResetOnGameStart();
        DoubleDoorController.ResetOnGameStart();
        EntranceShutter.ResetOnGameStart();
        Inventory.ResetOnGameStart();
        Mission.ResetOnGameStart();

        if (persistentPlayer != null)
        {
            persistentPlayer.SetActive(false);
            Destroy(persistentPlayer);
            persistentPlayer = null;
        }
        AudioListener.pause = false;
        SceneManager.LoadScene(0, LoadSceneMode.Single);
    }

    /// <summary>sceneName 씬으로 이동해서 doorId 문 앞에 도착한다.</summary>
    public static void Travel(string sceneName, string arrivalDoorId)
    {
        if (IsTraveling) return;
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[SceneTravel] '{sceneName}' 씬을 불러올 수 없습니다. File > Build Profiles(Build Settings)의 씬 목록에 추가되어 있는지 확인하세요.");
            return;
        }

        if (runner == null)
        {
            var go = new GameObject("[SceneTravel]");
            DontDestroyOnLoad(go);
            runner = go.AddComponent<SceneTravel>();
        }
        runner.StartCoroutine(runner.TravelRoutine(sceneName, arrivalDoorId));
    }

    IEnumerator TravelRoutine(string sceneName, string arrivalDoorId)
    {
        IsTraveling = true;
        SetPlayerControl(false);

        yield return Fade(0f, 1f);

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone) yield return null;
        yield return null; // 새 씬의 문들이 자리 잡을 때까지 한 프레임 대기

        PlacePlayerAt(arrivalDoorId);
        SetPlayerControl(true);

        yield return Fade(1f, 0f);
        IsTraveling = false;
    }

    static void PlacePlayerAt(string doorId, bool warnIfMissing = true)
    {
        if (persistentPlayer == null) return;

        SceneDoor target = null;
        foreach (SceneDoor d in FindObjectsByType<SceneDoor>(FindObjectsSortMode.None))
            if (d.doorId == doorId) { target = d; break; }

        if (target == null || !target.TryGetArrival(out Vector3 floorPos, out Quaternion rotation))
        {
            if (warnIfMissing) Debug.LogWarning($"[SceneTravel] 도착할 문 '{doorId}'를 찾지 못했습니다.");
            return;
        }

        // floorPos는 바닥 위치 → 캡슐 바닥이 바닥 살짝 위에 오도록 피벗 높이를 맞춘다
        var controller = persistentPlayer.GetComponent<CharacterController>();
        float pivotAboveFloor = controller != null ? controller.height / 2f - controller.center.y + 0.05f : 1f;

        if (controller != null) controller.enabled = false;
        persistentPlayer.transform.SetPositionAndRotation(floorPos + Vector3.up * pivotAboveFloor, rotation);
        Physics.SyncTransforms();
        if (controller != null) controller.enabled = true;
    }

    static void SetPlayerControl(bool on)
    {
        if (persistentPlayer == null) return;
        var pc = persistentPlayer.GetComponent<PlayerController>();
        if (pc != null) pc.enabled = on;
    }

    IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / FadeDuration);
            fadeAlpha = Mathf.Lerp(from, to, t);
            yield return null;
        }
    }

    void OnGUI()
    {
        if (fadeAlpha <= 0f) return;
        if (blackTexture == null)
        {
            blackTexture = new Texture2D(1, 1);
            blackTexture.SetPixel(0, 0, Color.black);
            blackTexture.Apply();
        }
        GUI.depth = -1000;
        GUI.color = new Color(0f, 0f, 0f, fadeAlpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), blackTexture);
        GUI.color = Color.white;
    }
}

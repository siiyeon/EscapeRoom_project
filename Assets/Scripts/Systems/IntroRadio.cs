using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임 시작 연출 (학교 씬을 처음 열 때 한 번만):
/// 1) 왼손에 무전기를 들어 올리고, 화면이 부드럽게 무전기 쪽으로 돌아간다
/// 2) 무전기 소리(Resources/Audio/Radio_1) 재생 + 무전 대사를 순서대로 화면 아래에 보여준다
/// 3) 무전기를 내리고 다시 정면을 본 뒤 조작할 수 있게 된다
/// 학교 씬이 로드되면 자동으로 시작된다 (씬에 넣을 필요 없음). 대사는 아래 lines에 순서대로 추가하면 된다.
/// </summary>
public class IntroRadio : MonoBehaviour
{
    public static bool IsPlaying { get; private set; }
    static bool played;

    [Header("무전 대사 (순서대로, 각 줄 표시 시간)")]
    public string speaker = "무전기";   // 대사 앞에 붙는 말하는 사람 (예: "무전기 : 산 너머...")
    public string[] lines =
    {
        "산 너머 폐교에서 이상한 소리난다고 또 전화하셨다",
        "주변에 열쇠 놔뒀으니까 대충 한바퀴 둘러보고 철수해",
    };
    public float lineSeconds = 3f;

    [Header("무전기")]
    public string radioModel = "PoliceRadio";          // Resources 폴더의 모델 이름
    public string radioSound = "Audio/Radio_1";        // Resources 폴더의 소리
    public Vector3 radioOffset = new Vector3(-0.24f, -0.33f, 0.42f); // 눈 위치 기준 (왼쪽, 아래, 앞)
    public float radioScale = 1.3f;

    [Header("연출 속도")]
    public float lookTime = 1.3f;    // 무전기 쪽으로 고개 돌리는 시간
    public float returnTime = 1.1f;  // 다시 정면으로 돌아오는 시간

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnGameStart() { played = false; IsPlaying = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // 철문이 있는 학교 씬에서, 게임 처음 한 번만
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (played || GameObject.Find("Fence_Gate_L") == null) return;
        played = true;
        var go = new GameObject("IntroRadio");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<IntroRadio>();
    }

    IEnumerator Start()
    {
        yield return null; // 플레이어 배치 등이 끝날 때까지 한 프레임 대기

        GameObject playerGo = GameObject.FindWithTag("Player");
        PlayerController pc = playerGo != null ? playerGo.GetComponent<PlayerController>() : null;
        Transform cam = pc != null && pc.cameraTransform != null ? pc.cameraTransform : (Camera.main != null ? Camera.main.transform : null);
        if (playerGo == null || cam == null) { Destroy(gameObject); yield break; }
        Transform player = playerGo.transform;

        IsPlaying = true;
        if (pc != null) pc.enabled = false;

        // 무전기: 몸(플레이어)에 붙여서 왼쪽 아래에 든다 → 카메라가 그쪽을 보면 화면 가운데로 들어온다
        Vector3 eyeLocal = player.InverseTransformPoint(cam.position);
        Vector3 holdLocal = eyeLocal + radioOffset;
        Vector3 hiddenLocal = holdLocal + Vector3.down * 0.35f;
        Transform radio = CreateRadio(player, eyeLocal, holdLocal);
        if (radio != null) radio.localPosition = hiddenLocal;

        Quaternion front = Quaternion.identity; // 정면 (PlayerController의 시작 시점과 같음)
        Vector3 lookTarget = player.TransformPoint(holdLocal + Vector3.up * 0.05f);
        Quaternion lookAtRadio = Quaternion.Inverse(player.rotation) * Quaternion.LookRotation(lookTarget - cam.position, player.up);
        Quaternion camStart = cam.localRotation;

        // 1) 무전기를 들어 올리며 고개를 돌린다
        for (float t = 0f; t < 1f; )
        {
            t = Mathf.Min(1f, t + Time.deltaTime / lookTime);
            float e = Mathf.SmoothStep(0f, 1f, t);
            cam.localRotation = Quaternion.Slerp(camStart, lookAtRadio, e);
            if (radio != null) radio.localPosition = Vector3.Lerp(hiddenLocal, holdLocal, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 1.4f)));
            yield return null;
        }

        // 2) 무전 소리 + 대사
        AudioClip clip = string.IsNullOrEmpty(radioSound) ? null : Resources.Load<AudioClip>(radioSound);
        float soundEnd = Time.time;
        if (clip != null)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.spatialBlend = 0f;
            source.Play();
            soundEnd = Time.time + clip.length;
        }
        yield return new WaitForSeconds(0.3f);
        foreach (string line in lines)
        {
            if (string.IsNullOrEmpty(line)) continue;
            GameHUD.Say(string.IsNullOrEmpty(speaker) ? line : speaker + " : " + line, lineSeconds);
            yield return new WaitForSeconds(lineSeconds + 0.25f);
        }
        while (Time.time < soundEnd) yield return null; // 무전 소리가 끝날 때까지

        // 3) 무전기를 내리고 정면으로
        Quaternion camFrom = cam.localRotation;
        for (float t = 0f; t < 1f; )
        {
            t = Mathf.Min(1f, t + Time.deltaTime / returnTime);
            float e = Mathf.SmoothStep(0f, 1f, t);
            cam.localRotation = Quaternion.Slerp(camFrom, front, e);
            if (radio != null) radio.localPosition = Vector3.Lerp(holdLocal, hiddenLocal, e);
            yield return null;
        }
        cam.localRotation = front;
        if (radio != null) Destroy(radio.gameObject);

        if (pc != null) pc.enabled = true;
        IsPlaying = false;
        GameHUD.Hint("[WASD] 이동", 3.5f);
        Destroy(gameObject);
    }

    Transform CreateRadio(Transform player, Vector3 eyeLocal, Vector3 holdLocal)
    {
        GameObject prefab = string.IsNullOrEmpty(radioModel) ? null : Resources.Load<GameObject>(radioModel);
        if (prefab == null) { Debug.LogWarning("[IntroRadio] 무전기 모델 '" + radioModel + "'을 Resources에서 찾지 못했습니다."); return null; }

        GameObject model = Instantiate(prefab);
        model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (Light l in model.GetComponentsInChildren<Light>(true)) Destroy(l);
        foreach (Camera c in model.GetComponentsInChildren<Camera>(true)) Destroy(c.gameObject);

        Bounds all = new Bounds(); bool any = false;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (!any) { all = r.bounds; any = true; } else all.Encapsulate(r.bounds);
        }

        // 무전기 앞면(화면·버튼, 모델의 +Z)이 눈을 보게, 살짝 비스듬히
        var anchor = new GameObject("Radio_LeftHand").transform;
        anchor.SetParent(player, false);
        anchor.localPosition = holdLocal;
        Vector3 toEye = eyeLocal - holdLocal;
        anchor.localRotation = Quaternion.LookRotation(new Vector3(toEye.x, 0f, toEye.z), Vector3.up) * Quaternion.Euler(-12f, 14f, 6f);
        anchor.localScale = Vector3.one * radioScale;
        model.transform.SetParent(anchor, false);
        model.transform.localPosition = -all.center;
        return anchor;
    }
}

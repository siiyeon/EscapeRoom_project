using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PrologueCutscene
{
// 프롤로그 시안용 컷신 진행 스크립트 (Space / Esc 로 스킵)
// 소리: PrologueAudio (코드 합성), 무전 송신 시 손에 든 무전기 마이크가 시야에 올라온다.
public class PrologueDirector : MonoBehaviour
{
    Camera cam;
    Transform car, camSeat, station; Transform[] wiperPivots;
    Transform pStation, pSchool, pStand;
    Light lightning;
    GameObject rain, ghost;
    Renderer ghostWindow;
    Material windowDim, windowLit;
    Image fade;
    Text subtitle, title;
    PrologueAudio sfx;

    // 손에 든 무전기 마이크
    Transform handMic; GameObject handParts, micCord;
    Vector3 micRestLocalPos; Quaternion micRestLocalRot;
    float micBlend, micTarget;
    bool listening; float listenPitch;   // 무전을 들을 때 시선을 아래로
    static readonly Vector3 MicHeldPos = new Vector3(0.14f, -0.12f, 0.38f);
    static readonly Vector3 MicHeldRot = new Vector3(-8f, -15f, 6f);
    static readonly Vector3 MicRestPos = new Vector3(0.22f, -0.42f, 0.30f);
    static readonly Vector3 MicRestRot = new Vector3(30f, -30f, 20f);

    Vector3 seatPos; Quaternion seatRot;
    bool wiping, shaking, ended, radioTalking;
    float wiperT, prevWiperDir;
    const float DriveFog = 0.03f, SchoolFog = 0.012f;

    void Awake()
    {
        cam = GameObject.Find("Main Camera").GetComponent<Camera>();
        camSeat = cam.transform.parent;
        seatPos = cam.transform.localPosition; seatRot = cam.transform.localRotation;
        car = GameObject.Find("PatrolCar").transform;
        var wpl = new System.Collections.Generic.List<Transform>();
        foreach (var t in car.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("WiperPivot_")) wpl.Add(t);
        wiperPivots = wpl.ToArray();
        lightning = GameObject.Find("LightningFlash").GetComponent<Light>();
        rain = GameObject.Find("Rain");
        station = GameObject.Find("PoliceStation_Interior").transform;
        pStation = GameObject.Find("CamPoint_Station").transform;
        pSchool = GameObject.Find("CamPoint_School").transform;
        pStand = GameObject.Find("CamPoint_Stand").transform;
        SetupGameSchool();
        ghost = GameObject.Find("Ghost_Silhouette");
        SetupGhost();
        // 임시 학교의 창문(Win_F3_5)이 있으면 그걸 밝히고, 없으면(게임 학교) 귀신 뒤 교실 조명으로 창을 밝힌다
        var winGo = GameObject.Find("Win_F3_5");
        ghostWindow = winGo != null ? winGo.GetComponent<Renderer>() : null;
        if (ghostWindow != null)
        {
            windowDim = ghostWindow.sharedMaterial;
            windowLit = new Material(windowDim);   // 원래 재질을 복제해서 어떤 렌더 파이프라인에서도 동작
            windowLit.color = Color.black;
            windowLit.EnableKeyword("_EMISSION");
            windowLit.SetColor("_EmissionColor", new Color(0.55f, 0.62f, 0.75f) * 1.3f);
        }
        fade = GameObject.Find("Fade").GetComponent<Image>();
        subtitle = GameObject.Find("Subtitle").GetComponent<Text>();
        title = GameObject.Find("Title").GetComponent<Text>();
        var font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 40);
        subtitle.font = font; title.font = font;
        ghost.SetActive(false);

        sfx = GetComponent<PrologueAudio>(); if (sfx == null) sfx = gameObject.AddComponent<PrologueAudio>();

        var hm = GameObject.Find("HeldRadio");
        if (hm != null) { handMic = hm.transform; hm.SetActive(false); }
    }

    // ---------- 게임 학교 / 여자 귀신 ----------
    Light ghostRoomLight;   // 번개 칠 때 귀신 뒤 교실을 밝혀서 창에 실루엣이 비치게

    // 프롤로그에서는 진짜 게임 학교 모델(School_Game)을 쓴다.
    // 게임 스크립트들이 이름으로 찾아서 동작하는 오브젝트(철문·현관 셔터·교실 문 등)는 이름을 바꿔서,
    // 프롤로그 씬에서는 철문 열기·무전 연출·교실 이동 같은 게임 기능이 켜지지 않게 한다.
    // (Awake는 게임 스크립트들의 씬 로드 처리보다 먼저 실행된다)
    void SetupGameSchool()
    {
        var school = GameObject.Find("School_Game");
        if (school == null) return;
        string[] gameNames = { "Fence_Gate_L", "Fence_Gate_R", "Shutter_Curtain", "Door_1F_C08_L", "Door_2F_C06_L", "Floor_2F", "Key2" };
        foreach (var t in school.GetComponentsInChildren<Transform>(true))
        {
            if (System.Array.IndexOf(gameNames, t.name) >= 0) t.name += "_Prologue";
            // 게임 학교의 넓은 땅은 프롤로그 산길 도로를 덮어버리므로 끈다 (프롤로그 땅을 그대로 사용)
            if (t.name == "Ground") t.gameObject.SetActive(false);
        }
    }

    // 3층 창가의 여자 귀신: 검은 실루엣 + 고개 숙이고 서 있는 Idle 애니메이션
    void SetupGhost()
    {
        if (ghost == null) return;
        var anim = ghost.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.applyRootMotion = false;
            if (anim.runtimeAnimatorController == null)
                anim.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("HorrorGirlAnimator");
        }
        foreach (var r in ghost.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i].color = Color.black;
                if (mats[i].HasProperty("_BaseColor")) mats[i].SetColor("_BaseColor", Color.black);
                if (mats[i].HasProperty("_Smoothness")) mats[i].SetFloat("_Smoothness", 0f);
                if (mats[i].HasProperty("_Metallic")) mats[i].SetFloat("_Metallic", 0f);
            }
            r.materials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        // 귀신 뒤(교실 안쪽) 천장 근처의 차가운 빛 → 창 안이 밝아지면서 귀신이 검은 그림자로 보인다
        var lg = new GameObject("Ghost_RoomLight");
        lg.transform.position = ghost.transform.position - ghost.transform.forward * 2.6f + Vector3.up * 1.7f;
        ghostRoomLight = lg.AddComponent<Light>();
        ghostRoomLight.type = LightType.Point;
        ghostRoomLight.range = 7f;
        ghostRoomLight.intensity = 0f;
        ghostRoomLight.color = new Color(0.62f, 0.7f, 0.85f);
        ghostRoomLight.shadows = LightShadows.None;
    }

    void SetWindowLit(bool lit)
    {
        if (ghostWindow != null) ghostWindow.sharedMaterial = lit ? windowLit : windowDim;
        if (ghostRoomLight != null) ghostRoomLight.intensity = lit ? 14f : 0f;
    }

    // 도로 경로(RoadPath)를 따라 오른쪽 차로에 차를 놓고 진행 방향으로 돌린다
    void PlaceCar(float z)
    {
        var rp = GameObject.Find("RoadPath");
        if (rp == null) { car.position = new Vector3(1.6f, 0, z); return; }
        Vector3 p, fwd; rp.GetComponent<RoadPath>().SampleAtZ(z, out p, out fwd);
        Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
        car.position = p + right * 1.6f;
        car.rotation = Quaternion.LookRotation(fwd, Vector3.up);
    }

    void Start() { StartCoroutine(Run()); StartCoroutine(RadioBlink()); }

    // 무전 중에 차량 무전기 LED 깜빡임
    IEnumerator RadioBlink()
    {
        var lg = GameObject.Find("RadioLED_Light"); var l = lg ? lg.GetComponent<Light>() : null;
        var rr = GameObject.Find("Radio_LED"); var r = rr ? rr.GetComponent<Renderer>() : null;
        Color baseE = r ? r.sharedMaterial.GetColor("_EmissionColor") : Color.black;
        while (true)
        {
            float k = radioTalking ? (Random.value > 0.45f ? 1.6f : 0.15f) : 1f;
            if (l) l.intensity = 0.6f * k;
            if (r) r.material.SetColor("_EmissionColor", baseE * k);
            yield return new WaitForSeconds(radioTalking ? 0.06f : 0.2f);
        }
    }

    void Update()
    {
        if (!ended && SkipPressed()) SkipToEnd();

        if (wiping)
        {
            wiperT += Time.deltaTime * 2.6f;
            float ang = (1f - Mathf.Cos(wiperT)) * 0.5f * 85f;
            foreach (var wp in wiperPivots) wp.localRotation = Quaternion.Euler(0, 0, ang);
            float dir = Mathf.Sign(Mathf.Sin(wiperT));
            if (dir != prevWiperDir) { sfx.Wiper(); prevWiperDir = dir; }
        }
        if (shaking && cam.transform.parent == camSeat)
        {
            float t = Time.time * 7f;
            cam.transform.localPosition = seatPos + new Vector3((Mathf.PerlinNoise(t, 0) - 0.5f) * 0.012f, (Mathf.PerlinNoise(0, t) - 0.5f) * 0.02f, 0);
        }
    }

    void LateUpdate()
    {
        // 휴대용 무전기를 든 손: 화면 아래(보이지 않는 곳)에서 오른쪽 아래 시야로 올라온다
        if (handMic == null) return;
        micBlend = Mathf.MoveTowards(micBlend, micTarget, Time.deltaTime / 0.55f);
        float s = micBlend * micBlend * (3f - 2f * micBlend);
        float sway = Time.time * 1.7f;
        Vector3 restPos = cam.transform.TransformPoint(MicRestPos);
        Vector3 heldPos = cam.transform.TransformPoint(MicHeldPos + new Vector3(Mathf.Sin(sway) * 0.004f, Mathf.Sin(sway * 1.3f) * 0.005f, 0));
        Quaternion faceCam = cam.transform.rotation * Quaternion.Euler(0, 180, 0);
        handMic.position = Vector3.Lerp(restPos, heldPos, s);
        handMic.rotation = Quaternion.Slerp(faceCam * Quaternion.Euler(MicRestRot), faceCam * Quaternion.Euler(MicHeldRot), s);
        bool lifted = micBlend > 0.02f;
        if (handMic.gameObject.activeSelf != lifted) handMic.gameObject.SetActive(lifted);
    }

    // ---------- 메인 시퀀스 ----------
    IEnumerator Run()
    {
        SetA(fade, 1); title.text = ""; subtitle.text = "";

        // #1 검은 화면 + 자막 (빗소리가 먼저 들린다)
        sfx.SetRain(PrologueAudio.RainMode.Outside);
        yield return new WaitForSeconds(1f);
        sfx.Thunder(2.0f, 0.5f);
        yield return ShowTitle("20여 년 전, 불미스러운 사건 이후 문을 닫은\n산골 마을의 고등학교", 3.5f);
        yield return ShowTitle("비가 거세게 쏟아지던 자정 무렵\n관할 지서", 2.5f);

        // #2 지서 내부: 게시판 → 벽시계 → 책상 으로 천천히 움직이는 카메라
        rain.SetActive(false);
        RenderSettings.fogDensity = DriveFog;
        sfx.SetRain(PrologueAudio.RainMode.Indoor);
        cam.transform.SetParent(null, true);
        var stationCam = StartCoroutine(StationCamera(18f));
        yield return Fade(1, 0, 1.5f);
        sfx.Ring();
        yield return Say("(따르르릉… 따르르릉…)", 2.4f);
        yield return MicUp();
        listening = true;
        yield return new WaitForSeconds(0.5f);
        yield return RadioIn("상황실 : 산 너머 폐교에서 또 여자애 비명 소리가 들린다고, 이장님이 신고하셨다.", 4.2f);
        yield return RadioIn("상황실 : 별일 없을 거다. 한 바퀴만 둘러보고 철수해.", 2.8f);
        listening = false;
        yield return new WaitForSeconds(0.5f);
        yield return MicDown();
        yield return Say("박진혁 순경 : …또 애들 장난이겠지.", 2.4f);
        yield return Fade(0, 1, 1f);

        StopCoroutine(stationCam);
        // #3 순찰차 운전석 시점 + 상황실과 무전
        rain.SetActive(true);
        SitInCar();
        PlaceCar(0f);
        sfx.SetRain(PrologueAudio.RainMode.InCar);
        sfx.SetEngine(0.45f, 1.15f);
        wiping = true; shaking = true;
        StartCoroutine(Drive(0f, 150f, 21f, false));
        yield return Fade(1, 0, 1.2f);
        yield return new WaitForSeconds(1.5f);
        yield return RadioIn("상황실 : 순찰 3호, 순찰 3호. 현재 위치 보고 바란다.", 3.2f);
        yield return MicUp();
        yield return RadioOut("박진혁 순경 : 순찰 3호 수신. 폐교 진입로 이동 중, 10분 내 도착 예정.", 3.6f);
        yield return RadioIn("상황실 : 알았다. 도착하면 다시 보고해.", 2.4f);
        yield return MicDown();
        yield return new WaitForSeconds(1.0f);
        StartCoroutine(Flash(false, 0.25f));
        yield return Say("(빗줄기가 점점 거세진다)", 2.5f);
        yield return new WaitForSeconds(0.8f);
        yield return Say("박진혁 순경 : 하필 이런 날씨에 폐교라니…", 2.5f);
        yield return new WaitForSeconds(0.8f);
        yield return Fade(0, 1, 0.8f);
        wiping = false; shaking = false;

        // #4 폐교 원경 + 3층 창가 실루엣
        RenderSettings.fogDensity = SchoolFog;
        sfx.SetRain(PrologueAudio.RainMode.Outside);
        sfx.SetEngine(0f, 1f);
        Place(pSchool); cam.fieldOfView = 30f;
        yield return Fade(1, 0, 1.5f);
        yield return new WaitForSeconds(2f);
        ghost.SetActive(true);
        yield return Flash(true, 0.15f);    // 번개 → 3층 창가에 누군가 서 있다
        yield return new WaitForSeconds(2.2f);
        ghost.SetActive(false);
        yield return Flash(true, 0.5f);     // 다시 번개 → 아무도 없다
        yield return new WaitForSeconds(1.5f);
        yield return Fade(0, 1, 1f);

        // #5 정문 앞 정차 → 도착 보고 → 하차 → 플레이 시작
        cam.fieldOfView = 62f;
        RenderSettings.fogDensity = DriveFog;
        SitInCar();
        PlaceCar(255f);
        sfx.SetRain(PrologueAudio.RainMode.InCar);
        sfx.SetEngine(0.45f, 1.1f);
        wiping = true; shaking = true;
        StartCoroutine(Drive(255f, 296f, 5f, true));
        yield return Fade(1, 0, 1f);
        yield return new WaitForSeconds(2.5f);
        sfx.SetEngine(0.3f, 0.8f);           // 감속 → 공회전
        yield return new WaitForSeconds(2.0f);
        shaking = false;
        yield return MicUp();
        yield return RadioOut("박진혁 순경 : 상황실, 순찰 3호. 현장 도착했습니다. 확인하고 복귀하겠습니다.", 3.6f);
        yield return RadioIn("상황실 : 수신. 철문 열쇠는 이장님이 정문 근처에 두셨다더라. 조심해서 둘러봐.", 3.6f);
        yield return MicDown();
        wiping = false;
        yield return new WaitForSeconds(0.4f);
        yield return Fade(0, 1, 0.7f);
        sfx.SetRain(PrologueAudio.RainMode.Outside);
        sfx.SetEngine(0.12f, 0.8f);          // 등 뒤에서 공회전하는 순찰차
        Place(pStand);
        yield return Fade(1, 0, 1.2f);
        End();
    }

    // ---------- 2번 장면 카메라 ----------
    IEnumerator StationCamera(float dur)
    {
        // 지서 로컬 좌표: (위치, 바라보는 지점, 시간 비율)
        Vector3[] pos = { new Vector3(-1.25f, 1.55f, -1.95f), new Vector3(-1.1f, 1.52f, -1.7f), new Vector3(-0.95f, 1.48f, -1.45f), new Vector3(-0.8f, 1.42f, -1.2f) };
        Vector3[] look = { new Vector3(-2.5f, 1.5f, 0.3f), new Vector3(-1.9f, 1.75f, 1.6f), new Vector3(-0.9f, 1.95f, 2.43f), new Vector3(0f, 0.95f, 1.45f) };
        float[] at = { 0f, 0.28f, 0.5f, 1f };
        float e = 0;
        while (e < dur)
        {
            e += Time.deltaTime; float u = Mathf.Clamp01(e / dur);
            int i = 0; while (i < at.Length - 2 && u > at[i + 1]) i++;
            float k = Mathf.InverseLerp(at[i], at[i + 1], u); k = k * k * (3f - 2f * k);
            Vector3 p = station.TransformPoint(Vector3.Lerp(pos[i], pos[i + 1], k));
            Vector3 l = station.TransformPoint(Vector3.Lerp(look[i], look[i + 1], k));
            listenPitch = Mathf.MoveTowards(listenPitch, listening ? 14f : 0f, Time.deltaTime * 18f);
            cam.transform.position = p; cam.transform.rotation = Quaternion.LookRotation(l - p) * Quaternion.Euler(listenPitch, 0, 0);
            yield return null;
        }
    }

    // ---------- 무전 ----------
    IEnumerator RadioIn(string s, float t)
    {
        radioTalking = true; sfx.RadioStart(false);
        yield return Say(s, t);
        sfx.RadioEnd(); radioTalking = false;
        yield return new WaitForSeconds(0.25f);
    }
    IEnumerator RadioOut(string s, float t)
    {
        radioTalking = true; sfx.RadioStart(true);
        yield return Say(s, t);
        sfx.RadioEnd(); radioTalking = false;
        yield return new WaitForSeconds(0.25f);
    }
    IEnumerator MicUp() { micTarget = 1f; yield return new WaitForSeconds(0.6f); }
    IEnumerator MicDown() { micTarget = 0f; yield return new WaitForSeconds(0.6f); }

    // ---------- 도우미 ----------
    // 프롤로그가 끝나면 다음 씬으로 넘어간다.
    // nextSceneName 이 비어 있으면 빌드 순서상 바로 다음 씬(프롤로그가 0번이면 1번 씬)을 연다.
    public string nextSceneName = "SampleScene";   // 프롤로그가 끝나면 열 게임 본편 씬

    void End()
    {
        ended = true;
        StartCoroutine(GoNext());
    }

    IEnumerator GoNext()
    {
        var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        int next = active.buildIndex + 1;
        bool byName = !string.IsNullOrEmpty(nextSceneName);
        bool hasNext = byName || (active.buildIndex >= 0 && next < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings);
        if (!hasNext) yield break;
        yield return new WaitForSeconds(1.2f);
        yield return Fade(0, 1, 0.8f);
        if (byName) UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName);
        else UnityEngine.SceneManagement.SceneManager.LoadScene(next);
    }

    // Space / Esc 스킵: 예전 입력(Input Manager)과 새 Input System 둘 다 지원
    static bool SkipPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape)) return true;
#endif
        return false;
    }

    // 스킵하면 그 자리에서 바로 화면을 까맣게 하고 소리를 끈 뒤 게임 본편으로 넘어간다
    void SkipToEnd()
    {
        ended = true;
        StopAllCoroutines();
        wiping = shaking = radioTalking = false;
        micTarget = micBlend = 0f; listening = false; listenPitch = 0f;
        if (handMic != null) handMic.gameObject.SetActive(false);
        lightning.intensity = 0;
        if (ghost != null) ghost.SetActive(false);
        SetWindowLit(false);
        subtitle.text = ""; title.text = "";
        SetA(fade, 1);                 // 즉시 검은 화면
        sfx.StopAllNow();
        StartCoroutine(LoadNextNow());
    }

    IEnumerator LoadNextNow()
    {
        yield return null;             // 검은 화면이 한 번 그려진 뒤에 넘어간다
        var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        int next = active.buildIndex + 1;
        if (!string.IsNullOrEmpty(nextSceneName)) UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName);
        else if (active.buildIndex >= 0 && next < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings)
            UnityEngine.SceneManagement.SceneManager.LoadScene(next);
    }

    void Place(Transform p)
    {
        cam.transform.SetParent(p, false);
        cam.transform.localPosition = Vector3.zero;
        cam.transform.localRotation = Quaternion.identity;
    }

    void SitInCar()
    {
        cam.transform.SetParent(camSeat, false);
        cam.transform.localPosition = seatPos;
        cam.transform.localRotation = seatRot;
    }

    IEnumerator Drive(float z0, float z1, float dur, bool easeOut)
    {
        float e = 0;
        while (e < dur)
        {
            e += Time.deltaTime;
            float t = Mathf.Clamp01(e / dur);
            if (easeOut) t = 1f - (1f - t) * (1f - t);
            PlaceCar(Mathf.Lerp(z0, z1, t));
            yield return null;
        }
    }

    IEnumerator Flash(bool litWindow, float thunderDelay)
    {
        sfx.Thunder(thunderDelay, 0.9f);
        Color bg = cam.backgroundColor;
        Color flashBg = new Color(0.5f, 0.55f, 0.68f);
        if (litWindow) SetWindowLit(true);
        float[] seq = { 2.2f, 0.2f, 1.6f, 0.4f, 0f };
        foreach (float i in seq)
        {
            lightning.intensity = i;
            cam.backgroundColor = Color.Lerp(bg, flashBg, i / 2.2f);
            yield return new WaitForSeconds(0.07f);
        }
        lightning.intensity = 0; cam.backgroundColor = bg;
        yield return new WaitForSeconds(0.25f);
        SetWindowLit(false);
    }

    IEnumerator ShowTitle(string s, float hold)
    {
        // 컴퓨터로 타이핑하듯 한 글자씩 + 키보드 소리. 아직 안 나온 글자는 투명하게 둬서 줄 위치가 흔들리지 않게 한다
        SetA(title, 1f);
        for (int i = 0; i <= s.Length; i++)
        {
            bool blink = i < s.Length;
            title.text = s.Substring(0, i) + (blink ? "<color=#FFFFFFAA>_</color>" : "") + "<color=#00000000>" + s.Substring(i) + "</color>";
            if (i == s.Length) break;
            char c = s[i];
            if (c == '\n') { sfx.Key(true); yield return new WaitForSeconds(0.38f); continue; }
            sfx.Key(c == ' ');
            float wait = Random.Range(0.06f, 0.13f);
            if (c == ',' || c == '.') wait += 0.22f;
            yield return new WaitForSeconds(wait);
        }
        title.text = s;
        yield return new WaitForSeconds(hold);
        yield return FadeGraphic(title, 1, 0, 1f);
        title.text = "";
    }

    IEnumerator Say(string s, float t)
    {
        subtitle.text = s;
        yield return new WaitForSeconds(t);
        subtitle.text = "";
    }

    IEnumerator Fade(float from, float to, float d) { yield return FadeGraphic(fade, from, to, d); }

    IEnumerator FadeGraphic(Graphic g, float from, float to, float d)
    {
        float e = 0;
        while (e < d) { e += Time.deltaTime; SetA(g, Mathf.Lerp(from, to, e / d)); yield return null; }
        SetA(g, to);
    }

    static void SetA(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }
}
}

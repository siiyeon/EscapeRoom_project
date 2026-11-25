using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 학교 앞 철문(Fence_Gate_L / Fence_Gate_R).
/// 쇠사슬과 자물쇠로 잠겨 있고, Key2 열쇠를 주운 뒤 E키를 누르면 자물쇠가 풀리며 학교 안쪽으로 열린다.
/// 학교 씬이 로드되면 자동으로 붙는다 (씬에 따로 넣을 필요 없음).
/// </summary>
public class IronGateController : MonoBehaviour
{
    [Header("철문 오브젝트 이름")]
    public string leftGateName = "Fence_Gate_L";
    public string rightGateName = "Fence_Gate_R";
    public string chainName = "Gate_Chain";
    public string padlockName = "Gate_Padlock";
    public string insideMarkerName = "Door_Entrance_L"; // 학교 쪽(안쪽) 방향 판단용

    [Header("설정")]
    public float interactionDistance = 2.5f;
    public float openAngle = 100f;
    public float openSpeed = 0.8f;
    public float noticeDuration = 2.5f;

    // 교실에 다녀와서 학교 씬이 다시 로드돼도 열린 상태 유지
    static bool gateOpened;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnGameStart() { gateOpened = false; runHintShown = false; }

    [Header("철문을 지나면 달리기 안내")]
    public float runHintDistance = 2f;   // 철문을 지나 학교 쪽으로 몇 m 들어오면 안내
    public float runHintDuration = 2f;
    static bool runHintShown;
    Vector3 insideDir;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindAnyObjectByType<IronGateController>() != null) return;
        if (GameObject.Find("Fence_Gate_L") == null) return;
        var go = new GameObject("IronGate");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<IronGateController>();
    }

    Transform leftHinge, rightHinge;
    GameObject chain, padlock;
    Vector3 gateCenter, gateNormal;
    Quaternion leftClosed, rightClosed;
    bool isOpen, inRange;
    string notice;
    float noticeUntil;
    GUIStyle style;
    Font font;

    void Start()
    {
        Transform left = FindByName(leftGateName);
        Transform right = FindByName(rightGateName);
        if (left == null || right == null)
        {
            Debug.LogWarning($"[IronGate] 철문을 찾지 못했습니다. ({leftGateName}, {rightGateName})");
            enabled = false;
            return;
        }
        chain = FindByName(chainName)?.gameObject;
        padlock = FindByName(padlockName)?.gameObject;

        Bounds lb = GetWorldBounds(left);
        Bounds rb = GetWorldBounds(right);
        Vector3 axis = rb.center - lb.center;
        axis.y = 0f;
        axis.Normalize();
        gateCenter = (lb.center + rb.center) / 2f;
        gateNormal = Vector3.Cross(Vector3.up, axis);

        // 각 문짝의 바깥쪽 끝(기둥 쪽)을 경첩으로 사용
        leftHinge = CreateHinge("Hinge_" + left.name, left, lb.center - axis * ExtentAlong(lb, axis));
        rightHinge = CreateHinge("Hinge_" + right.name, right, rb.center + axis * ExtentAlong(rb, axis));
        leftClosed = leftHinge.localRotation;
        rightClosed = rightHinge.localRotation;

        if (gateOpened) Open(instant: true);
    }

    void Update()
    {
        inRange = false;
        if (SceneTravel.IsTraveling || InventoryUI.IsOpen) return;

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return;

        if (isOpen)
        {
            // 철문을 지나 학교 쪽으로 2m 들어오면 달리기 안내 (게임 중 한 번만)
            if (!runHintShown && Vector3.Dot(player.transform.position - gateCenter, insideDir) >= runHintDistance)
            {
                runHintShown = true;
                ShowNotice("Shift : 달리기", runHintDuration);
            }
            return;
        }

        Vector3 d = player.transform.position - gateCenter;
        d.y = 0f;
        inRange = d.magnitude <= interactionDistance;
        if (!inRange) return;

        if (KeyPickup.ConsumedInteractionThisFrame) return; // 같은 E키로 열쇠를 주운 프레임
        if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;

        if (!KeyPickup.HasKey2)
        {
            ShowNotice("쇠사슬과 자물쇠로 단단히 잠겨 있다. 열쇠가 필요하다.");
            return;
        }

        ShowNotice("열쇠로 자물쇠를 풀었다.");
        Inventory.Remove(KeyPickup.ItemId); // 사용한 아이템은 사라진다
        gateOpened = true;
        Open(instant: false);
    }

    void Open(bool instant)
    {
        isOpen = true;
        if (chain != null) chain.SetActive(false);
        if (padlock != null) padlock.SetActive(false);

        // 학교 안쪽으로 열린다
        float side = -1f;
        GameObject inside = string.IsNullOrEmpty(insideMarkerName) ? null : GameObject.Find(insideMarkerName);
        if (inside != null)
            side = -Mathf.Sign(Vector3.Dot(inside.transform.position - gateCenter, gateNormal));
        insideDir = gateNormal * -side; // 학교 안쪽 방향

        // 왼쪽 문짝 끝은 +axis 방향 → Y축 +각도 회전 시 +gateNormal 쪽으로 이동. 오른쪽은 거울 대칭.
        float leftAngle = -openAngle * side;
        Quaternion leftOpen = leftClosed * Quaternion.Euler(0f, leftAngle, 0f);
        Quaternion rightOpen = rightClosed * Quaternion.Euler(0f, -leftAngle, 0f);

        if (instant)
        {
            leftHinge.localRotation = leftOpen;
            rightHinge.localRotation = rightOpen;
        }
        else
        {
            AudioClip openingSound = Resources.Load<AudioClip>("Audio/IronGateOpen");
            if (openingSound != null) AudioSource.PlayClipAtPoint(openingSound, gateCenter);
            StartCoroutine(Swing(leftOpen, rightOpen));
        }
    }

    IEnumerator Swing(Quaternion leftOpen, Quaternion rightOpen)
    {
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.deltaTime * openSpeed);
            float e = 1f - (1f - t) * (1f - t); // 처음엔 빠르게, 끝에서 천천히 (무거운 철문 느낌)
            leftHinge.localRotation = Quaternion.Slerp(leftClosed, leftOpen, e);
            rightHinge.localRotation = Quaternion.Slerp(rightClosed, rightOpen, e);
            yield return null;
        }
    }

    void ShowNotice(string message, float duration = -1f)
    {
        notice = message;
        noticeUntil = Time.unscaledTime + (duration > 0f ? duration : noticeDuration);
    }

    void OnGUI()
    {
        if (SceneTravel.IsTraveling || InventoryUI.IsOpen) return;
        string message = Time.unscaledTime < noticeUntil ? notice : (inRange && !isOpen ? "[E] 철문 열기" : null);
        if (message == null) return;
        if (style == null)
        {
            font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 22);
            style = new GUIStyle(GUI.skin.box) { font = font, fontSize = 22, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = Color.white;
        }
        // KeyPickup 안내문(화면 75% 높이)과 겹치지 않도록 조금 위에 표시
        GUI.Box(new Rect((Screen.width - 560f) / 2f, Screen.height * 0.66f, 560f, 54f), message, style);
    }

    void OnDestroy() { if (font != null) Destroy(font); }

    static Transform FindByName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return null;
        GameObject found = GameObject.Find(objectName);
        return found != null ? found.transform : null;
    }

    static Bounds GetWorldBounds(Transform target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(target.position, Vector3.zero);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static float ExtentAlong(Bounds bounds, Vector3 dir)
    {
        return Mathf.Abs(dir.x) * bounds.extents.x + Mathf.Abs(dir.z) * bounds.extents.z;
    }

    static Transform CreateHinge(string hingeName, Transform gate, Vector3 worldPos)
    {
        Transform hinge = new GameObject(hingeName).transform;
        hinge.SetParent(gate.parent, false);
        hinge.position = worldPos;
        hinge.rotation = Quaternion.identity;
        gate.SetParent(hinge, true);
        return hinge;
    }
}

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(BoxCollider))]
public class KeyPickup : MonoBehaviour
{
    // Session state survives scene loads, including returning to SampleScene.
    public static bool HasKey2 { get; private set; }
    public static bool ConsumedInteractionThisFrame => pickupFrame == Time.frameCount;
    public const string ItemId = "GateKey"; // 인벤토리 아이템 id
    static int pickupFrame = -1;
    [Min(0.1f)] public float interactionDistance = 1.8f;
    [Range(5f, 90f)] public float viewAngle = 20f; // 화면 가운데에서 이 각도 안에 열쇠가 보이면 집을 수 있음
    [Min(0.1f)] public float noticeDuration = 2.5f;
    bool collected, focused;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetInventory() { HasKey2 = false; pickupFrame = -1; }

    // Also covers scenes already open in the editor before the scene file was updated.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RegisterSceneBinding()
    {
        SceneManager.sceneLoaded -= BindScene;
        SceneManager.sceneLoaded += BindScene;
    }

    static void BindScene(Scene scene, LoadSceneMode mode)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
        {
            if (item.name != "Key2" || item.GetComponent<KeyPickup>() != null) continue;
            item.gameObject.AddComponent<KeyPickup>();
        }
    }
    void Awake()
    {
        var box = GetComponent<BoxCollider>();
        bool initialized = false;
        Bounds localBounds = new Bounds();
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            Bounds b = renderer.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = transform.InverseTransformPoint(b.center + Vector3.Scale(b.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                if (!initialized) { localBounds = new Bounds(p, Vector3.zero); initialized = true; }
                else localBounds.Encapsulate(p);
            }
        }
        if (initialized)
        {
            box.center = localBounds.center;
            // A small world-space margin makes the thin key practical to aim at.
            Vector3 scale = transform.lossyScale;
            Vector3 margin = new Vector3(0.08f / Mathf.Max(Mathf.Abs(scale.x), 0.0001f),
                0.08f / Mathf.Max(Mathf.Abs(scale.y), 0.0001f),
                0.08f / Mathf.Max(Mathf.Abs(scale.z), 0.0001f));
            box.size = localBounds.size + margin;
        }
        box.isTrigger = true;
        Physics.SyncTransforms();
        if (HasKey2) HideKey();
    }

    void Update()
    {
        focused = false;
        if (collected || SceneTravel.IsTraveling || InventoryUI.IsOpen) return;
        if (HasKey2) { HideKey(); return; }
        Camera view = Camera.main;
        if (view == null) return;
        var player = view.GetComponentInParent<PlayerController>();
        if (player == null || !player.isActiveAndEnabled) return;
        focused = CanReach(view, player.transform);
        if (focused) GameHUD.Prompt("[E] 열쇠 줍기");
        if (!focused || Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;
        pickupFrame = Time.frameCount;
        HasKey2 = true;
        GameHUD.Say("정문 열쇠를 획득했다.", noticeDuration);   // 대사 (화면 아래)
        GameHUD.Hint("[TAB] 인벤토리", noticeDuration);       // 조작 안내 (화면 가운데 아래)
        StartCoroutine(SayAfter("이제 학교에 들어가자", noticeDuration)); // 이어서 다음 혼잣말
        // 인벤토리에 넣기 (아이콘은 숨기기 전에 열쇠 모델을 찍어서 만든다)
        Inventory.Add(new InventoryItem
        {
            Id = ItemId,
            SlotName = "열쇠",
            SlotSubName = "Key",
            Title = "정문 열쇠 (Key)",
            Description = "철문의 자물쇠를 열 수 있을 것 같다.",
            Icon = ItemIconRenderer.Render(gameObject),
            OnUse = IronGateController.TryUseKey, // 철문 앞에서 [마우스 좌클릭]으로 사용
        });
        HideKey();
    }

    // 열쇠가 가까이 있고 화면 가운데 근처에 보이기만 하면 집을 수 있다 (정확히 조준할 필요 없음)
    bool CanReach(Camera view, Transform player)
    {
        Vector3 eye = view.transform.position;
        Vector3 keyCenter = transform.TransformPoint(GetComponent<BoxCollider>().center);
        Vector3 toKey = keyCenter - eye;
        if (toKey.magnitude > interactionDistance) return false;
        if (Vector3.Angle(view.transform.forward, toKey) > viewAngle) return false;
        if (toKey.magnitude < 0.8f) return true; // 바로 앞이면 시야 검사 생략

        // 벽 너머 열쇠는 집지 않도록: 열쇠 중심이나 위쪽 중 하나라도 가리는 것 없이 보이면 OK
        Vector3[] targets = { keyCenter, keyCenter + Vector3.up * 0.1f };
        foreach (Vector3 target in targets)
        {
            bool blocked = false;
            foreach (var hit in Physics.RaycastAll(eye, target - eye, Vector3.Distance(eye, target), ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(player) || hit.collider.transform.IsChildOf(transform)) continue;
                blocked = true;
                break;
            }
            if (!blocked) return true;
        }
        return false;
    }

    System.Collections.IEnumerator SayAfter(string line, float delay)
    {
        yield return new WaitForSeconds(delay);
        GameHUD.Say(line, noticeDuration);
    }

    void HideKey()
    {
        collected = true;
        focused = false;
        foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = false;
    }

    void OnGUI()
    {
        if (SceneTravel.IsTraveling || InventoryUI.IsOpen || collected) return;
        // 화면 가운데 조준점 (열쇠를 집을 수 있으면 초록색)
        Color previous = GUI.color;
        GUI.color = focused ? Color.green : Color.white;
        GUI.DrawTexture(new Rect(Screen.width * 0.5f - 2f, Screen.height * 0.5f - 2f, 4f, 4f), Texture2D.whiteTexture);
        GUI.color = previous;
    }

}

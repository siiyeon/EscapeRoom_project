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
    [Min(0.1f)] public float noticeDuration = 2.5f;
    bool collected, focused;
    CharacterController playerBody;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    internal static void ResetInventory() { HasKey2 = false; pickupFrame = -1; }

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

    void Start()
    {
        var boundary = FindFirstObjectByType<OutsideBoundary>();
        if (collected || boundary == null || boundary.gameObject.scene != gameObject.scene) return;

        // Keep the pickup outside the fence volume even in an older copy of the school scene.
        Transform perimeter = boundary.transform;
        Vector3 center = GetComponent<BoxCollider>().bounds.center;
        float outsideDistance = Vector3.Dot(center - perimeter.position, perimeter.forward);
        if (outsideDistance >= 0.45f) return;
        transform.position += perimeter.forward * (0.45f - outsideDistance);
        Physics.SyncTransforms();
    }

    void Update()
    {
        focused = false;
        if (collected || SceneTravel.IsTraveling || InventoryUI.IsOpen || IntroRadio.IsPlaying) return;
        if (HasKey2) { HideKey(); return; }
        if (playerBody == null) playerBody = GameObject.FindWithTag("Player")?.GetComponent<CharacterController>();
        if (playerBody == null || !playerBody.enabled || !playerBody.gameObject.activeInHierarchy) return;
        focused = CanReach(playerBody);
        if (focused) GameHUD.Prompt("[E] 열쇠 줍기");
        if (!focused || Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;
        pickupFrame = Time.frameCount;
        var item = new InventoryItem
        {
            Id = ItemId,
            SlotName = "열쇠",
            SlotSubName = "Key",
            Title = "정문 열쇠 (Key)",
            Description = "철문의 자물쇠를 열 수 있을 것 같다.",
            OnUse = IronGateController.TryUseKey, // 철문 앞에서 [마우스 좌클릭]으로 사용
        };
        if (!Inventory.Add(item)) return;
        HasKey2 = true;
        GameHUD.Say("정문 열쇠를 획득했다.", noticeDuration);
        GameHUD.Hint("[TAB] 인벤토리", noticeDuration);
        StartCoroutine(SayAfter("이제 학교에 들어가자", noticeDuration));
        try { item.Icon = ItemIconRenderer.Render(gameObject); }
        catch (System.Exception error) { Debug.LogWarning("[KeyPickup] " + error.Message, this); }
        HideKey();
    }

    bool CanReach(CharacterController player)
    {
        Bounds body = player.bounds;
        Vector3 keyCenter = GetComponent<BoxCollider>().bounds.center;
        Vector3 offset = keyCenter - body.center;
        offset.y = 0f;
        if (offset.sqrMagnitude > interactionDistance * interactionDistance ||
            keyCenter.y < body.min.y - 0.35f || keyCenter.y > body.max.y + 0.35f) return false;

        // 벽 너머 열쇠는 집지 않도록: 열쇠 중심이나 위쪽 중 하나라도 가리는 것 없이 보이면 OK
        Vector3 start = body.center;
        Vector3[] targets = { keyCenter, keyCenter + Vector3.up * 0.1f };
        foreach (Vector3 target in targets)
        {
            bool blocked = false;
            foreach (var hit in Physics.RaycastAll(start, target - start, Vector3.Distance(start, target), ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(player.transform) || hit.collider.transform.IsChildOf(transform)) continue;
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
        if (SceneTravel.IsTraveling || InventoryUI.IsOpen || IntroRadio.IsPlaying || collected) return;
        // 화면 가운데 조준점 (열쇠를 집을 수 있으면 초록색)
        Color previous = GUI.color;
        GUI.color = focused ? Color.green : Color.white;
        GUI.DrawTexture(new Rect(Screen.width * 0.5f - 2f, Screen.height * 0.5f - 2f, 4f, 4f), Texture2D.whiteTexture);
        GUI.color = previous;
    }

}

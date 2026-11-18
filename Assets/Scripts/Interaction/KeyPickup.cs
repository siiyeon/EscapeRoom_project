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
    static int pickupFrame = -1;
    [Min(0.1f)] public float interactionDistance = 2f;
    [Min(0.1f)] public float noticeDuration = 2.5f;
    bool collected, focused;
    float noticeUntil;
    GUIStyle style;
    Font font;

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
        if (collected || SceneTravel.IsTraveling) return;
        if (HasKey2) { HideKey(); return; }
        Camera view = Camera.main;
        if (view == null) return;
        var player = view.GetComponentInParent<PlayerController>();
        if (player == null || !player.isActiveAndEnabled) return;
        Ray ray = new Ray(view.transform.position, view.transform.forward);
        float distance = float.PositiveInfinity;
        Collider nearest = null;
        foreach (var hit in Physics.RaycastAll(ray, interactionDistance, ~0, QueryTriggerInteraction.Collide))
        {
            if (hit.collider.transform.IsChildOf(player.transform)) continue;
            if (hit.collider.isTrigger && hit.collider.GetComponentInParent<KeyPickup>() == null) continue;
            if (hit.distance >= distance) continue;
            distance = hit.distance;
            nearest = hit.collider;
        }
        focused = nearest != null && nearest.GetComponentInParent<KeyPickup>() == this;
        if (!focused || Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;
        pickupFrame = Time.frameCount;
        HasKey2 = true;
        noticeUntil = Time.unscaledTime + noticeDuration;
        HideKey();
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
        if (SceneTravel.IsTraveling) return;
        if (!collected)
        {
            Color previous = GUI.color;
            GUI.color = focused ? Color.green : Color.white;
            GUI.DrawTexture(new Rect(Screen.width * 0.5f - 2f, Screen.height * 0.5f - 2f, 4f, 4f), Texture2D.whiteTexture);
            GUI.color = previous;
        }
        string message = Time.unscaledTime < noticeUntil ? "Key2 열쇠를 획득했습니다." : focused ? "[E] Key2 열쇠 줍기" : null;
        if (message == null) return;
        if (style == null)
        {
            font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 22);
            style = new GUIStyle(GUI.skin.box) { font = font, fontSize = 22, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = Color.white;
        }
        GUI.Box(new Rect((Screen.width - 440f) / 2f, Screen.height * 0.75f, 440f, 54f), message, style);
    }

    void OnDestroy() { if (font != null) Destroy(font); }
}

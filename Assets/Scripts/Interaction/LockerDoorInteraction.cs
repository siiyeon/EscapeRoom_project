using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// A single raycast selects one panel, even when many locker doors are within reach.
[DefaultExecutionOrder(-50)]
public class LockerDoorInteraction : MonoBehaviour
{
    [Min(0.1f)] public float interactionDistance = 2f;
    static LockerDoorInteraction instance;
    LockerDoorController focusedDoor;
    GUIStyle style;
    Font font;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetInstance() { instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        SceneManager.sceneLoaded -= BindScene;
        SceneManager.sceneLoaded += BindScene;
    }

    static void BindScene(Scene scene, LoadSceneMode mode)
    {
        bool found = false;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
        {
            if (!Regex.IsMatch(item.name, @"^LockerDoor_\d+F_B\d+_C\d+[LU]$")) continue;
            if (item.GetComponentInChildren<Renderer>(true) == null) continue;
            if (item.GetComponent<LockerDoorController>() == null) item.gameObject.AddComponent<LockerDoorController>();
            found = true;
        }
        if (!found || instance != null) return;
        var manager = new GameObject("[LockerDoorInteraction]");
        DontDestroyOnLoad(manager);
        instance = manager.AddComponent<LockerDoorInteraction>();
    }

    void Update()
    {
        focusedDoor = null;
        if (SceneTravel.IsTraveling || KeyPickup.ConsumedInteractionThisFrame || InventoryUI.IsOpen) return;
        Camera view = Camera.main;
        if (view == null) return;
        var player = view.GetComponentInParent<PlayerController>();
        if (player == null || !player.isActiveAndEnabled) return;
        Collider nearest = null;
        float distance = interactionDistance;
        foreach (RaycastHit hit in Physics.RaycastAll(view.transform.position, view.transform.forward,
                     interactionDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(player.transform) || hit.distance > distance) continue;
            distance = hit.distance;
            nearest = hit.collider;
        }
        if (nearest == null) return;
        focusedDoor = nearest.GetComponentInParent<LockerDoorController>();
        if (focusedDoor == null || focusedDoor.IsMoving || Keyboard.current == null) return;
        if (Keyboard.current.eKey.wasPressedThisFrame) focusedDoor.Toggle(view.transform.position);
    }

    void OnGUI()
    {
        if (focusedDoor == null || SceneTravel.IsTraveling || KeyPickup.ConsumedInteractionThisFrame || InventoryUI.IsOpen) return;
        if (style == null)
        {
            font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 20);
            style = new GUIStyle(GUI.skin.box) { font = font, fontSize = 20, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = Color.white;
        }
        Color previous = GUI.color;
        GUI.color = Color.green;
        GUI.DrawTexture(new Rect(Screen.width * 0.5f - 2f, Screen.height * 0.5f - 2f, 4f, 4f), Texture2D.whiteTexture);
        GUI.color = previous;
        string message = focusedDoor.IsMoving ? "문 이동 중..." : focusedDoor.IsOpen ? "[E] 사물함 닫기" : "[E] 사물함 열기";
        GUI.Box(new Rect((Screen.width - 360f) / 2f, Screen.height * 0.85f, 360f, 48f), message, style);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (font != null) Destroy(font);
    }
}

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Batch entry: -executeMethod LockerDoorValidation.Run
public static class LockerDoorValidation
{
    const string Running = "EscapeRoom.LockerDoorValidation";
    static LockerDoorController[] doors;
    static Quaternion[] closed;
    static Vector3[] pivots;
    static LockerDoorController selected;
    static int stage;
    static double next, deadline;

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        SessionState.SetBool(Running, true);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    static void Resume()
    {
        if (!SessionState.GetBool(Running, false)) return;
        deadline = EditorApplication.timeSinceStartup + 90;
        next = EditorApplication.timeSinceStartup + 3;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (now > deadline) throw new Exception("Locker validation timed out at stage " + stage);
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || now < next) return;
            if (stage == 0)
            {
                doors = UnityEngine.Object.FindObjectsByType<LockerDoorController>(FindObjectsSortMode.None);
                if (doors.Length != 216) throw new Exception("Expected 216 individual locker doors, got " + doors.Length);
                closed = doors.Select(d => d.transform.localRotation).ToArray();
                pivots = doors.Select(d => d.transform.position).ToArray();
                foreach (var door in doors)
                {
                    if (door.GetComponentsInChildren<Collider>().Length == 0) throw new Exception("No collider: " + door.name);
                    Vector3 offset = door.GetComponentInChildren<Renderer>().bounds.center - door.transform.position;
                    door.Open(door.transform.position + Vector3.Cross(Vector3.up, offset).normalized);
                }
                stage = 1; next = now + 1;
            }
            else if (stage == 1)
            {
                for (int i = 0; i < doors.Length; i++)
                {
                    if (!doors[i].IsOpen || doors[i].IsMoving || Quaternion.Angle(closed[i], doors[i].transform.localRotation) < 90f)
                        throw new Exception("Failed to open: " + doors[i].name);
                    if (Vector3.Distance(pivots[i], doors[i].transform.position) > 0.001f)
                        throw new Exception("Hinge shifted: " + doors[i].name);
                    doors[i].Close();
                }
                stage = 2; next = now + 1;
            }
            else if (stage == 2)
            {
                for (int i = 0; i < doors.Length; i++)
                    if (doors[i].IsOpen || doors[i].IsMoving || Quaternion.Angle(closed[i], doors[i].transform.localRotation) > 0.01f)
                        throw new Exception("Failed to close: " + doors[i].name);
                selected = doors.First(d => d.name == "LockerDoor_1F_B01_C1U");
                PositionPlayerAt(selected);
                stage = 3; next = now + 0.2;
            }
            else if (stage == 3)
            {
                var interaction = UnityEngine.Object.FindFirstObjectByType<LockerDoorInteraction>();
                interaction.SendMessage("Update");
                var focused = typeof(LockerDoorInteraction).GetField("focusedDoor",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(interaction);
                if (!ReferenceEquals(focused, selected)) throw new Exception("Ray selection did not focus the intended panel.");
                selected.Toggle(Camera.main.transform.position);
                stage = 4; next = now + 0.2;
            }
            else
            {
                if (!selected.IsOpen) throw new Exception("Selected locker did not open.");
                if (doors.Count(d => d.IsOpen) != 1) throw new Exception("Toggling the selected panel affected another locker door.");
                Debug.Log("LOCKER_DOOR_VALIDATION_PASS: 216 doors opened and closed with fixed hinges; ray selection and toggling affected exactly one panel.");
                Finish(0);
            }
        }
        catch (Exception error) { Debug.LogException(error); Finish(1); }
    }

    static void PositionPlayerAt(LockerDoorController door)
    {
        Camera camera = Camera.main;
        var player = camera.GetComponentInParent<PlayerController>();
        Vector3 center = door.GetComponentInChildren<Renderer>().bounds.center;
        Vector3 normal = Vector3.Cross(Vector3.up, center - door.transform.position).normalized;
        foreach (float sign in new[] { 1f, -1f })
        {
            Vector3 origin = center + normal * sign * 0.8f;
            Vector3 direction = -normal * sign;
            var hits = Physics.RaycastAll(origin, direction, 1.5f, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => !h.collider.transform.IsChildOf(player.transform)).OrderBy(h => h.distance).ToArray();
            if (hits.Length == 0 || hits[0].collider.GetComponentInParent<LockerDoorController>() != door) continue;
            var body = player.GetComponent<CharacterController>();
            body.enabled = false;
            player.transform.rotation = Quaternion.LookRotation(direction);
            camera.transform.localRotation = Quaternion.identity;
            camera.transform.localPosition = new Vector3(0f, player.standingCamY, 0f);
            player.transform.position = origin - Vector3.up * player.standingCamY;
            body.enabled = true;
            Physics.SyncTransforms();
            return;
        }
        throw new Exception("No unobstructed viewing position found for input test.");
    }

    static void Finish(int result)
    {
        SessionState.SetBool(Running, false);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(result);
    }
}


using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class LockerDoorController : MonoBehaviour
{
    [Range(30f, 120f)] public float openAngle = 100f;
    [Min(0.1f)] public float animationDuration = 0.35f;
    public bool IsOpen { get; private set; }
    public bool IsMoving => motion != null;

    Quaternion closedRotation;
    Coroutine motion;
    Vector3 panelCenterOffset;

    void Awake()
    {
        // The imported locker panel already has its pivot on the hinge edge.
        closedRotation = transform.localRotation;
        Renderer panel = GetComponentInChildren<Renderer>();
        panelCenterOffset = panel != null ? panel.bounds.center - transform.position : transform.right;
    }

    public void Toggle(Vector3 viewerPosition)
    {
        if (IsOpen) Close();
        else Open(viewerPosition);
    }

    public void Open(Vector3 viewerPosition)
    {
        if (IsOpen || IsMoving) return;
        // Swing toward the player standing in front of the locker, out of the cabinet.
        Vector3 tangent = Vector3.Cross(Vector3.up, panelCenterOffset);
        float sign = Vector3.Dot(tangent, viewerPosition - transform.position) >= 0f ? 1f : -1f;
        Quaternion parentRotation = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
        Quaternion target = Quaternion.Inverse(parentRotation) * Quaternion.AngleAxis(sign * openAngle, Vector3.up)
            * parentRotation * closedRotation;
        IsOpen = true;
        motion = StartCoroutine(Animate(target));
    }

    public void Close()
    {
        if (!IsOpen || IsMoving) return;
        IsOpen = false;
        motion = StartCoroutine(Animate(closedRotation));
    }

    IEnumerator Animate(Quaternion target)
    {
        Quaternion start = transform.localRotation;
        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / animationDuration));
            transform.localRotation = Quaternion.Slerp(start, target, progress);
            yield return null;
        }
        transform.localRotation = target;
        motion = null;
    }

    void OnDisable()
    {
        if (motion != null) StopCoroutine(motion);
        motion = null;
        transform.localRotation = closedRotation;
        IsOpen = false;
    }
}

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 씬에 있는 아이템 모델을 위에서 찍어서 인벤토리 아이콘 이미지로 만든다.
/// 원본 오브젝트의 메쉬만 복사해서 아무도 안 보이는 곳(지하 1000m)에서 촬영하므로 원본에는 영향이 없다.
/// </summary>
public static class ItemIconRenderer
{
    const int IconLayer = 31;
    static readonly Vector3 StudioPosition = new Vector3(0f, -1000f, 0f);

    public static Texture Render(GameObject source, int size = 256)
    {
        if (source == null) return null;

        var root = new GameObject("[ItemIcon]");
        root.transform.position = StudioPosition;
        bool any = false;
        foreach (MeshFilter mf in source.GetComponentsInChildren<MeshFilter>(true))
        {
            var mr = mf.GetComponent<MeshRenderer>();
            if (mf.sharedMesh == null || mr == null) continue;
            if (mf.name.Contains("Preview")) continue; // 모델링할 때 쓴 미리보기용 바닥판 등은 빼고 찍는다
            // 원본 루트 기준 상대 위치/회전/크기 그대로 복사 (루트의 크기·회전은 빼서 원래 모양으로)
            Matrix4x4 rel = source.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            var part = new GameObject(mf.name) { layer = IconLayer };
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = rel.GetColumn(3);
            part.transform.localRotation = rel.rotation;
            part.transform.localScale = rel.lossyScale;
            part.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var r = part.AddComponent<MeshRenderer>();
            r.sharedMaterials = mr.sharedMaterials;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            any = true;
        }
        if (!any) { Object.Destroy(root); return null; }

        Renderer[] parts = root.GetComponentsInChildren<Renderer>();
        Bounds b = parts[0].bounds;
        for (int i = 1; i < parts.Length; i++) b.Encapsulate(parts[i].bounds);

        // 모델이 비스듬히 놓여 있어도 되도록, 부품 위치들로 "긴 축 / 중간 축 / 얇은 축"을 구한다
        var pts = new System.Collections.Generic.List<Vector3>();
        foreach (Renderer r in parts) AddCorners(pts, r.bounds);
        Vector3 longAxis = PrincipalAxis(pts, b.center, Vector3.zero);
        Vector3 midAxis = PrincipalAxis(pts, b.center, longAxis);
        Vector3 thinAxis = Vector3.Cross(longAxis, midAxis).normalized;

        // 넓은 면을 정면이 아니라 살짝 비스듬하게(옆으로 35도, 긴 축 쪽으로 15도) 바라보고,
        // 긴 축이 화면에서 대각선(45도)으로 놓이게 한다
        Vector3 viewDir = (thinAxis * Mathf.Cos(35f * Mathf.Deg2Rad) + midAxis * Mathf.Sin(35f * Mathf.Deg2Rad) + longAxis * 0.27f).normalized;
        if (Vector3.Dot(viewDir, Vector3.up) < -0.2f) viewDir = -viewDir; // 아래에서 올려다보지 않게
        Quaternion look = Quaternion.LookRotation(-viewDir, longAxis);
        look = look * Quaternion.Euler(0f, 0f, -45f);
        Vector3 up = look * Vector3.up;
        float dist = Mathf.Max(b.size.magnitude, 0.05f) * 2f;

        // 화면에 꽉 차게: 카메라 기준으로 부품 모서리들이 차지하는 범위를 재서 중심과 크기를 맞춘다
        Vector3 right = look * Vector3.right;
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        foreach (Vector3 p in pts)
        {
            Vector3 d = p - b.center;
            float x = Vector3.Dot(d, right), y = Vector3.Dot(d, up);
            minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
        }
        Vector3 center = b.center + right * (minX + maxX) * 0.5f + up * (minY + maxY) * 0.5f;
        float half = Mathf.Max(maxX - minX, maxY - minY) * 0.5f;

        var camGo = new GameObject("[ItemIconCamera]");
        camGo.transform.SetPositionAndRotation(center + viewDir * dist, look);
        var cam = camGo.AddComponent<Camera>();
        cam.enabled = false;
        cam.orthographic = true;
        cam.orthographicSize = Mathf.Max(half * 1.04f, 0.001f);
        cam.nearClipPlane = dist * 0.1f;
        cam.farClipPlane = dist * 3f;
        cam.cullingMask = 1 << IconLayer;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.allowHDR = false;
        cam.allowMSAA = false;
        var camData = cam.GetUniversalAdditionalCameraData();
        camData.renderPostProcessing = false;
        camData.renderShadows = false;

        // 아이콘용 조명 2개 (지하 1000m라 게임 화면에는 영향 없음).
        // 너무 가까우면 하얗게 날아가서 텍스처가 안 보이므로 3m 떨어진 곳에서 비춘다.
        var lightGo = new GameObject("[ItemIconLight]");
        AddPointLight(lightGo, b.center + (viewDir + up * 0.6f).normalized * 3f, 9f);
        AddPointLight(lightGo, b.center + (viewDir - up * 0.4f + Vector3.Cross(viewDir, up) * 0.8f).normalized * 3f, 4f);

        // 금속 재질은 반사할 주변 환경이 없으면 검게 보이므로, 아이콘 촬영용 복사본은 금속 느낌을 줄인다
        var tempMaterials = new System.Collections.Generic.List<Material>();
        foreach (Renderer r in parts)
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                var m = new Material(mats[i]);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", Mathf.Min(m.GetFloat("_Metallic"), 0.2f));
                if (m.HasProperty("_MetallicGlossMap")) m.SetTexture("_MetallicGlossMap", null);
                m.DisableKeyword("_METALLICSPECGLOSSMAP");
                tempMaterials.Add(m);
                mats[i] = m;
            }
            r.sharedMaterials = mats;
        }

        var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { name = "Icon_" + source.name };
        rt.Create();
        cam.targetTexture = rt;
        try { cam.Render(); }
        catch (System.Exception e) { Debug.LogWarning("[ItemIconRenderer] 아이콘을 만들지 못했습니다: " + e.Message); }
        cam.targetTexture = null;

        Object.Destroy(camGo);
        Object.Destroy(lightGo);
        Object.Destroy(root);
        foreach (Material m in tempMaterials) Object.Destroy(m);
        return rt;
    }

    static void AddPointLight(GameObject parent, Vector3 position, float intensity)
    {
        var go = new GameObject("Light");
        go.transform.SetParent(parent.transform, false);
        go.transform.position = position;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 8f;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
    }

    static void AddCorners(System.Collections.Generic.List<Vector3> pts, Bounds bb)
    {
        for (int i = 0; i < 8; i++)
            pts.Add(bb.center + Vector3.Scale(bb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
    }

    // 점들이 가장 길게 퍼진 방향 (exclude 방향 성분은 빼고 계산). 간단한 거듭제곱법.
    static Vector3 PrincipalAxis(System.Collections.Generic.List<Vector3> pts, Vector3 c, Vector3 exclude)
    {
        float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
        foreach (Vector3 p in pts)
        {
            Vector3 d = p - c;
            if (exclude != Vector3.zero) d -= exclude * Vector3.Dot(d, exclude);
            xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z; yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
        }
        Vector3 v = exclude == Vector3.zero ? new Vector3(1f, 0.7f, 0.4f) : Vector3.Cross(exclude, new Vector3(0.3f, 1f, 0.5f));
        for (int i = 0; i < 40; i++)
        {
            v = new Vector3(xx * v.x + xy * v.y + xz * v.z, xy * v.x + yy * v.y + yz * v.z, xz * v.x + yz * v.y + zz * v.z);
            if (exclude != Vector3.zero) v -= exclude * Vector3.Dot(v, exclude);
            if (v.sqrMagnitude < 1e-12f) return exclude == Vector3.zero ? Vector3.right : Vector3.Cross(exclude, Vector3.up).normalized;
            v.Normalize();
        }
        return v;
    }
}

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

        // 가장 얇은 축 방향에서 내려다보고, 긴 축이 화면에서 대각선으로 놓이게 한다
        Vector3 s = b.size;
        int thin = s.x <= s.y && s.x <= s.z ? 0 : (s.y <= s.z ? 1 : 2);
        int a1 = (thin + 1) % 3, a2 = (thin + 2) % 3;
        int longAxis = s[a1] >= s[a2] ? a1 : a2;
        int midAxis = longAxis == a1 ? a2 : a1;
        Vector3 viewDir = Axis(thin);
        Vector3 up = (Axis(longAxis) + Axis(midAxis)).normalized;
        float half = (s[longAxis] + s[midAxis]) * 0.5f / Mathf.Sqrt(2f);
        float dist = Mathf.Max(s.magnitude, 0.05f) * 2f;

        var camGo = new GameObject("[ItemIconCamera]");
        camGo.transform.SetPositionAndRotation(b.center + viewDir * dist, Quaternion.LookRotation(-viewDir, up));
        var cam = camGo.AddComponent<Camera>();
        cam.enabled = false;
        cam.orthographic = true;
        cam.orthographicSize = Mathf.Max(half * 1.15f, 0.001f);
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

    static Vector3 Axis(int i) => i == 0 ? Vector3.right : (i == 1 ? Vector3.up : Vector3.forward);
}

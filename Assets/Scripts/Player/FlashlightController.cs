using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class FlashlightController : MonoBehaviour
{
    // 씬이 바뀌어도 플레이어와 함께 유지되는 손전등 (인벤토리의 손전등/배터리 장비가 사용)
    public static FlashlightController Instance { get; private set; }
    public float BatteryPercent => batteryPercent;
    public bool IsOn => isOn;

    [Header("켜면 화면 오른쪽 아래에 보이는 손전등 모델 (Resources 폴더의 모델 이름)")]
    public string viewModelResource = "Flashlight";
    public Vector3 viewModelOffset = new Vector3(0.31f, -0.29f, 0.55f); // 카메라 기준 위치 (오른쪽, 아래, 앞) — 손잡이 절반은 화면 밖
    public Vector3 viewModelAngles = new Vector3(4f, -10f, 0f);         // 살짝 안쪽을 비추듯 기울임
    public float viewModelScale = 1.4f;
    public float beamFocusDistance = 8f; // 불빛이 화면 가운데 몇 m 앞을 향하게 할지

    [Header("불빛 모양")]
    public float beamSpotAngle = 58f;    // 빛 원뿔 각도 (좁을수록 손전등 느낌)
    [Range(0.1f, 0.95f)] public float beamSoftness = 0.3f; // 가운데 밝은 부분 비율 (작을수록 가장자리가 부드럽게 번짐)
    public float beamRange = 25f;
    public float beamIntensity = 6f;
    public Color beamColor = new Color(1f, 0.94f, 0.82f); // 살짝 따뜻한 손전등 빛
    public bool beamPattern = false;     // 켜면 실제 손전등처럼 가운데 밝은 점 + 반사판 고리 무늬 (플레이 중에 바꿔도 바로 적용)
    [Range(0f, 0.3f)] public float beamConeAlpha = 0f; // 렌즈에서 뻗는 빛줄기 (안개가 없으면 어색해서 기본은 끔)
    public float beamConeLength = 3f;
    private GameObject viewModel;

    [Header("손전등 연결")]
    public GameObject flashlightLight;     // 껐다 켤 Spot Light 오브젝트
    private Light spotLightComponent;

    [Header("UI 연결 (선택 사항)")]
    public TextMeshProUGUI batteryText;    // 화면에 띄울 배터리 잔량 텍스트

    [Header("손전등 상태")]
    public bool isOn = false;              // 시작할 때는 꺼져 있고 F키를 눌러야 켜진다

    [Header("배터리 수치 설정")]
    [Range(0f, 100f)]
    public float batteryPercent = 100f;
    public float drainSpeed = 100f / 1200f; // 초당 소모율 (100% / 1200초 = 20분 지속)

    [Header("배터리 부족 연출: 이 % 아래로 내려가면 불빛이 점점 어두워진다")]
    public float lowBatteryThreshold = 20f;
    private float defaultIntensity = 3.5f;

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        CreateViewModel();
        if (flashlightLight != null)
        {
            spotLightComponent = flashlightLight.GetComponent<Light>();
            if (spotLightComponent != null)
            {
                defaultIntensity = spotLightComponent.intensity;
            }
            flashlightLight.SetActive(isOn);
        }
        UpdateUI();
    }

    void Update()
    {
        HandleInput();
        HandleBattery();
        if (viewModel != null && viewModel.activeSelf != isOn) viewModel.SetActive(isOn);
        if (spotLightComponent != null && appliedPattern != beamPattern) ApplyBeamPattern(spotLightComponent);
    }

    /// <summary>손전등 켜기/끄기 (F키, 인벤토리에서 손전등 사용). 배터리가 없으면 켜지지 않는다.</summary>
    public bool Toggle()
    {
        isOn = batteryPercent > 0f && !isOn;
        if (flashlightLight != null) flashlightLight.SetActive(isOn);
        UpdateUI();
        return batteryPercent > 0f;
    }

    // 손전등 모델을 카메라 오른쪽 아래에 붙인다. 모델이 어떻게 놓여 있든 머리(FL_Head) 쪽이 앞을 보게 돌린다.
    void CreateViewModel()
    {
        Transform cam = flashlightLight != null ? flashlightLight.transform.parent : null;
        GameObject prefab = string.IsNullOrEmpty(viewModelResource) ? null : Resources.Load<GameObject>(viewModelResource);
        if (cam == null || prefab == null) return;

        GameObject model = Instantiate(prefab);
        model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (Light l in model.GetComponentsInChildren<Light>(true)) Destroy(l);
        foreach (Camera c in model.GetComponentsInChildren<Camera>(true)) Destroy(c.gameObject);

        Bounds all = new Bounds(); bool any = false;
        Vector3 head = Vector3.zero, tail = Vector3.zero;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            if (r.name.Contains("Preview")) { r.gameObject.SetActive(false); continue; } // 미리보기용 바닥판
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (!any) { all = r.bounds; any = true; } else all.Encapsulate(r.bounds);
            if (r.name == "FL_Head") head = r.bounds.center;
            if (r.name == "FL_TailCap") tail = r.bounds.center;
        }
        if (!any) { Destroy(model); return; }
        Vector3 axis = head != tail ? (head - tail).normalized : Vector3.forward;
        Quaternion fix = Quaternion.FromToRotation(axis, Vector3.forward);

        var anchor = new GameObject("FlashlightViewModel").transform;
        anchor.SetParent(cam, false);
        anchor.localPosition = viewModelOffset;
        anchor.localRotation = Quaternion.Euler(viewModelAngles);
        anchor.localScale = Vector3.one * viewModelScale;
        model.transform.SetParent(anchor, false);
        model.transform.localRotation = fix;
        model.transform.localPosition = -(fix * all.center);

        // 불빛이 손전등 헤드(렌즈)에서 나오게: 조명을 렌즈 바로 앞으로 옮기고, 화면 가운데 앞쪽을 비추게 한다
        if (flashlightLight != null)
        {
            Transform lens = null;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
                if (r.name == "FL_Lens" || (lens == null && r.name == "FL_Head")) lens = r.transform;
            Vector3 lensPos = lens != null ? lens.GetComponent<Renderer>().bounds.center : anchor.TransformPoint(Vector3.forward * 0.1f);
            lensPos += anchor.forward * 0.02f; // 렌즈 표면 살짝 앞 (모델에 가려지지 않게)
            Transform beam = flashlightLight.transform;
            beam.SetParent(cam, true);
            beam.position = lensPos;
            beam.rotation = Quaternion.LookRotation(cam.position + cam.forward * beamFocusDistance - lensPos, cam.up);

            // 원래 조명은 134도로 너무 넓어서 플레이어 몸에서 빛이 퍼지는 것처럼 보였다 → 손전등다운 좁은 원뿔로
            Light spot = flashlightLight.GetComponent<Light>();
            if (spot != null)
            {
                spot.type = LightType.Spot;
                spot.spotAngle = beamSpotAngle;
                spot.innerSpotAngle = beamSpotAngle * beamSoftness;
                spot.range = beamRange;
                spot.intensity = beamIntensity;
                spot.color = beamColor;
                ApplyBeamPattern(spot);
            }

            // 손전등 머리가 빛나 보이게: 렌즈/LED 재질을 발광시키고, 머리 주변을 비추는 아주 작은 빛 추가
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name != "FL_Lens" && r.name != "FL_LED" && r.name != "FL_Reflector") continue;
                Material[] mats = r.materials; // 이 손전등 전용 복사본
                foreach (Material m in mats)
                {
                    m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    m.SetColor("_EmissionColor", new Color(1f, 0.95f, 0.85f) * (r.name == "FL_Reflector" ? 1.5f : 4f));
                }
                r.materials = mats;
            }
            var glow = new GameObject("LensGlow").AddComponent<Light>();
            glow.transform.SetParent(anchor, false);
            glow.transform.position = lensPos;
            glow.type = LightType.Point;
            glow.range = 0.35f;
            glow.intensity = 1.2f;
            glow.color = new Color(1f, 0.95f, 0.85f);
            glow.shadows = LightShadows.None;

            CreateBeamCone(beam);
        }

        viewModel = anchor.gameObject;
        viewModel.SetActive(isOn);
    }

    Texture2D beamCookie;
    bool appliedPattern;

    void ApplyBeamPattern(Light spot)
    {
        if (beamPattern && beamCookie == null) beamCookie = MakeBeamCookie();
        spot.cookie = beamPattern ? beamCookie : null;
        appliedPattern = beamPattern;
    }

    // 손전등 불빛 무늬: 가운데 밝은 점 → 반사판이 만드는 옅은 고리 → 부드럽게 사라지는 가장자리 (+ 살짝 얼룩)
    static Texture2D MakeBeamCookie()
    {
        const int s = 256;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "FlashlightCookie" };
        var px = new Color[s * s];
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float u = x / (float)(s - 1) * 2f - 1f, v = y / (float)(s - 1) * 2f - 1f;
            float r = Mathf.Sqrt(u * u + v * v);
            float hot = Mathf.Exp(-r * r / 0.05f);                          // 가운데 밝은 점
            float body = Mathf.Clamp01(1f - Mathf.Pow(r / 0.92f, 3f)) * 0.55f; // 넓게 퍼지는 빛
            float ring = Mathf.Exp(-Mathf.Pow((r - 0.62f) / 0.05f, 2f)) * 0.18f; // 반사판 고리
            float edge = Mathf.Clamp01((1f - r) / 0.18f);                   // 가장자리 부드럽게
            float smudge = 0.94f + Mathf.PerlinNoise(u * 3f + 7f, v * 3f + 3f) * 0.12f; // 렌즈 얼룩
            float k = Mathf.Clamp01((hot * 0.7f + body + ring) * edge * smudge);
            px[y * s + x] = new Color(k, k, k, k);
        }
        tex.SetPixels(px);
        tex.Apply(false, true);
        return tex;
    }

    // 렌즈에서 앞으로 뻗는 아주 옅은 빛줄기 (먼지 낀 공기에 비친 느낌). 렌즈 쪽은 보이고 멀어질수록 사라진다.
    void CreateBeamCone(Transform beam)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null || beamConeAlpha <= 0f) return;

        const int sides = 24;
        float length = beamConeLength;
        float farRadius = Mathf.Tan(beamSpotAngle * 0.5f * Mathf.Deg2Rad) * length * 0.6f;
        const float nearRadius = 0.025f;
        var verts = new Vector3[sides * 2];
        var colors = new Color[sides * 2];
        var tris = new int[sides * 6];
        for (int i = 0; i < sides; i++)
        {
            float a = i * Mathf.PI * 2f / sides;
            var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            verts[i] = dir * nearRadius;
            verts[i + sides] = dir * farRadius + Vector3.forward * length;
            colors[i] = new Color(1f, 0.96f, 0.88f, beamConeAlpha);
            colors[i + sides] = new Color(1f, 0.96f, 0.88f, 0f);
            int n = (i + 1) % sides;
            tris[i * 6 + 0] = i; tris[i * 6 + 1] = i + sides; tris[i * 6 + 2] = n;
            tris[i * 6 + 3] = n; tris[i * 6 + 4] = i + sides; tris[i * 6 + 5] = n + sides;
        }
        var mesh = new Mesh { name = "FlashlightBeamCone", vertices = verts, colors = colors, triangles = tris };
        mesh.RecalculateBounds();

        var mat = new Material(shader);
        mat.SetFloat("_Surface", 1f);                 // 투명
        mat.SetFloat("_Blend", 2f);                   // 더하기(빛처럼 밝게)
        mat.SetFloat("_Cull", 0f);                    // 안팎 모두 그리기
        mat.SetFloat("_ZWrite", 0f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.SetColor("_BaseColor", Color.white);

        var cone = new GameObject("BeamCone");
        cone.transform.SetParent(beam, false);
        cone.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = cone.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    void HandleInput()
    {
        if (Keyboard.current == null || InventoryUI.IsOpen || IntroRadio.IsPlaying) return;

        // F키를 누르면 손전등 켜기/끄기 토글
        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            if (batteryPercent > 0f)
            {
                isOn = !isOn;
                flashlightLight.SetActive(isOn);
            }
            else
            {
                isOn = false;
                flashlightLight.SetActive(false);
            }
            UpdateUI();
        }
    }

    void HandleBattery()
    {
        if (!isOn) return;

        if (batteryPercent > 0f)
        {
            batteryPercent -= drainSpeed * Time.deltaTime;
            batteryPercent = Mathf.Clamp(batteryPercent, 0f, 100f);

            // 배터리가 20% 아래로 내려갈수록 불빛이 점점 어두워짐 (20% → 원래 밝기, 0% → 꺼짐)
            if (spotLightComponent != null)
            {
                float k = batteryPercent < lowBatteryThreshold ? batteryPercent / lowBatteryThreshold : 1f;
                spotLightComponent.intensity = defaultIntensity * k;
            }
        }
        else
        {
            // 방전 시 자동 소등
            isOn = false;
            flashlightLight.SetActive(false);
        }

        UpdateUI();
    }

    void UpdateUI()
    {
        if (batteryText != null)
        {
            if (isOn)
            {
                batteryText.text = $"[F] LIGHT: {Mathf.CeilToInt(batteryPercent)}%";
            }
            else
            {
                batteryText.text = $"[F] LIGHT: OFF ({Mathf.CeilToInt(batteryPercent)}%)";
            }
        }
    }

    // 필드에서 건전지 주웠을 때 배터리 충전용 함수
    public void RechargeBattery(float amount)
    {
        batteryPercent = Mathf.Clamp(batteryPercent + amount, 0f, 100f);
        if (spotLightComponent != null)
        {
            spotLightComponent.intensity = defaultIntensity;
        }
        UpdateUI();
    }
}
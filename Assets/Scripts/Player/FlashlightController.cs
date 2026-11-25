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
        }

        viewModel = anchor.gameObject;
        viewModel.SetActive(isOn);
    }

    void HandleInput()
    {
        if (Keyboard.current == null || InventoryUI.IsOpen) return;

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
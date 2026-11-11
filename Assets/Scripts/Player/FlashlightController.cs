using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class FlashlightController : MonoBehaviour
{
    [Header("손전등 연결")]
    public GameObject flashlightLight;     // 껐다 켤 Spot Light 오브젝트
    private Light spotLightComponent;

    [Header("UI 연결 (선택 사항)")]
    public TextMeshProUGUI batteryText;    // 화면에 띄울 배터리 잔량 텍스트

    [Header("손전등 상태")]
    public bool isOn = true;

    [Header("배터리 수치 설정")]
    [Range(0f, 100f)]
    public float batteryPercent = 100f;
    public float drainSpeed = 1.0f;        // 초당 소모율 (1.0이면 100초 지속)

    [Header("배터리 부족 깜빡임 연출")]
    public float lowBatteryThreshold = 20f;
    private float defaultIntensity = 3.5f;
    private float flickerTimer = 0f;

    void Start()
    {
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
    }

    void HandleInput()
    {
        if (Keyboard.current == null) return;

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

            // 배터리가 20% 이하일 때 불규칙하게 깜빡거림 연출
            if (batteryPercent <= lowBatteryThreshold && spotLightComponent != null)
            {
                flickerTimer -= Time.deltaTime;
                if (flickerTimer <= 0f)
                {
                    spotLightComponent.intensity = Random.Range(defaultIntensity * 0.1f, defaultIntensity);
                    flickerTimer = Random.Range(0.05f, 0.2f);
                }
            }
            else if (spotLightComponent != null)
            {
                spotLightComponent.intensity = defaultIntensity;
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
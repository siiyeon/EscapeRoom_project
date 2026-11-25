using UnityEngine;

/// <summary>
/// 게임 시작 때 플레이어가 기본으로 가지고 있는 경찰 장비를 인벤토리 장비(GEAR) 칸에 넣는다.
/// 장비 모델은 Resources 폴더에 있어야 한다 (예: Assets/Models/Props/Resources/Flashlight.fbx).
/// 새 장비를 추가하려면 Give(...) 한 줄을 더 쓰면 된다.
/// </summary>
public static class PoliceGear
{
    public const string FlashlightId = "Flashlight";
    public const string BatteryId = "FlashlightBattery";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void GiveStartingGear()
    {
        InventoryItem flashlight = Give(FlashlightId, "Flashlight", "손전등", "Flashlight", "손전등 (Flashlight)",
            "경찰 지급용 손전등.\nF키를 눌러서 사용할 수 있다.");
        if (flashlight != null)
        {
            flashlight.OnUse = UseFlashlight;
            flashlight.Badge = () => FlashlightController.Instance != null
                ? Mathf.CeilToInt(FlashlightController.Instance.BatteryPercent) + "%" : null;
        }

        InventoryItem battery = Give(BatteryId, "FlashlightBattery", "배터리", "Battery", "손전등 배터리 (Battery)",
            "손전등 배터리가 다 닳았을 때 사용할 수 있다.\n배터리가 없을 때 사용해보자.");
        if (battery != null) battery.OnUse = UseBattery;
    }

    // 손전등: 켜기/끄기 (F키와 같음)
    static bool UseFlashlight()
    {
        FlashlightController light = FlashlightController.Instance;
        return light != null && light.Toggle();
    }

    // 배터리: 손전등 배터리를 100%로 채우고 사라진다. 이미 가득 차 있으면 사용할 수 없다.
    static bool UseBattery()
    {
        FlashlightController light = FlashlightController.Instance;
        if (light == null || light.BatteryPercent >= 99.5f) return false;
        light.RechargeBattery(100f);
        Inventory.Remove(BatteryId);
        return true;
    }

    static InventoryItem Give(string id, string modelResource, string shortName, string englishName, string title, string description)
    {
        if (Inventory.Has(id)) return null;
        var model = Resources.Load<GameObject>(modelResource);
        if (model == null) Debug.LogWarning($"[PoliceGear] 장비 모델 '{modelResource}'를 Resources 폴더에서 찾지 못했습니다. 아이콘 없이 넣습니다.");

        var item = new InventoryItem
        {
            Id = id,
            SlotName = shortName,
            SlotSubName = englishName,
            Title = title,
            Description = description,
            Icon = model != null ? ItemIconRenderer.Render(model) : null,
        };
        return Inventory.AddGear(item) ? item : null;
    }
}

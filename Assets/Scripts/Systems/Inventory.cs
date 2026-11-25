using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>인벤토리에 들어가는 아이템(또는 장비) 하나.</summary>
public class InventoryItem
{
    public string Id;           // 코드에서 찾을 때 쓰는 이름 (예: "GateKey")
    public string SlotName;     // 아이콘이 없을 때 칸에 표시할 짧은 이름 (예: "열쇠")
    public string SlotSubName;  // 짧은 영어 이름 (예: "Key")
    public string Title;        // 오른쪽 설명 제목 (예: "정문 열쇠 (Key)")
    public string Description;  // 오른쪽 설명
    public Texture Icon;        // 아이콘 (모델을 찍어서 만든 이미지)

    /// <summary>
    /// [마우스 좌클릭]으로 사용했을 때 할 일. 사용에 성공하면 true (인벤토리가 닫힌다).
    /// 비워두면 "지금은 사용할 수 없다."가 표시된다.
    /// </summary>
    public Func<bool> OnUse;

    /// <summary>칸 오른쪽 아래에 작게 표시할 글자 (예: 손전등 배터리 "100%"). 비워두면 표시 안 함.</summary>
    public Func<string> Badge;
}

/// <summary>
/// 플레이어 인벤토리. 씬이 바뀌어도 유지되고, 게임을 새로 시작하면 비워진다.
/// - Items: 획득한 아이템 & 단서 (가운데 칸)
/// - Gear : 경찰 장비 (왼쪽 칸)
/// 화면 표시는 InventoryUI가 담당한다 (TAB키).
/// </summary>
public static class Inventory
{
    public const int Capacity = 16;     // 아이템 & 단서 칸 수 (4 x 4)
    public const int GearCapacity = 6;  // 장비 칸 수

    static readonly List<InventoryItem> items = new List<InventoryItem>();
    static readonly List<InventoryItem> gear = new List<InventoryItem>();
    public static IReadOnlyList<InventoryItem> Items => items;
    public static IReadOnlyList<InventoryItem> Gear => gear;

    /// <summary>인벤토리가 비워진 횟수 (게임 시작·귀신에게 잡혀 재시작할 때 늘어남). 시작 장비를 다시 줄 때 사용.</summary>
    public static int ResetCount { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    internal static void ResetOnGameStart()
    {
        ResetCount++;
        while (items.Count > 0) Remove(items[items.Count - 1].Id);
        while (gear.Count > 0) Remove(gear[gear.Count - 1].Id);
    }

    public static bool Has(string id) => items.Exists(i => i.Id == id) || gear.Exists(i => i.Id == id);

    public static bool Add(InventoryItem item)
    {
        if (item == null || Has(item.Id) || items.Count >= Capacity) return false;
        items.Add(item);
        return true;
    }

    public static bool AddGear(InventoryItem item)
    {
        if (item == null || Has(item.Id) || gear.Count >= GearCapacity) return false;
        gear.Add(item);
        return true;
    }

    /// <summary>아이템을 없앤다 (사용한 아이템은 소멸).</summary>
    public static bool Remove(string id)
    {
        InventoryItem item = items.Find(i => i.Id == id) ?? gear.Find(i => i.Id == id);
        if (item == null) return false;
        items.Remove(item);
        gear.Remove(item);
        if (item.Icon is RenderTexture rt && rt != null) { rt.Release(); UnityEngine.Object.Destroy(rt); }
        return true;
    }
}

/// <summary>
/// 현재 임무. 인벤토리 오른쪽 위에 "[임무: ...]"로 표시된다.
/// 진행에 따라 Mission.Set("새 임무")로 바꾸면 된다.
/// </summary>
public static class Mission
{
    public const string Initial = "폐교 탐색하기";
    public static string Current { get; private set; } = Initial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    internal static void ResetOnGameStart() => Current = Initial;

    public static void Set(string mission) => Current = mission;
}

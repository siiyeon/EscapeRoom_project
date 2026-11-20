using System.Collections.Generic;
using UnityEngine;

/// <summary>인벤토리에 들어가는 아이템 하나.</summary>
public class InventoryItem
{
    public string Id;           // 코드에서 찾을 때 쓰는 이름 (예: "GateKey")
    public string SlotName;     // 왼쪽 칸 아래 이름 (예: "열쇠")
    public string SlotSubName;  // 왼쪽 칸 아래 작은 영어 이름 (예: "Key")
    public string Title;        // 오른쪽 설명 제목 (예: "정문 열쇠 (Key)")
    public string Description;  // 오른쪽 설명
    public Texture Icon;        // 아이콘 (모델을 찍어서 만든 이미지)
}

/// <summary>
/// 플레이어 인벤토리. 씬이 바뀌어도 유지되고, 게임을 새로 시작하면 비워진다.
/// 화면 표시는 InventoryUI가 담당한다 (TAB키).
/// </summary>
public static class Inventory
{
    public const int Capacity = 12;

    static readonly List<InventoryItem> items = new List<InventoryItem>();
    public static IReadOnlyList<InventoryItem> Items => items;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnGameStart() => items.Clear();

    public static bool Has(string id) => items.Exists(i => i.Id == id);

    public static bool Add(InventoryItem item)
    {
        if (item == null || Has(item.Id) || items.Count >= Capacity) return false;
        items.Add(item);
        return true;
    }

    /// <summary>아이템을 없앤다 (사용한 아이템은 소멸).</summary>
    public static bool Remove(string id)
    {
        InventoryItem item = items.Find(i => i.Id == id);
        if (item == null) return false;
        items.Remove(item);
        if (item.Icon is RenderTexture rt) { rt.Release(); Object.Destroy(rt); }
        return true;
    }
}

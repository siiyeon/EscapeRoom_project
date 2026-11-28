using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// TAB키로 여는 인벤토리 화면.
/// - 배경: 멈춘 게임 화면 위에 어두운 막을 씌운다 (Time.timeScale = 0 → 귀신도 멈춤, 플레이어 조작 정지, 소리 일시정지).
/// - 왼쪽: 장비 (GEAR) / 가운데: 획득한 아이템 & 단서 (4 x 4) / 오른쪽: 선택한 아이템 설명
/// - 오른쪽 위: 현재 임무 (Mission.Current)
/// - 조작: [WASD] 이동, [TAB] 닫기, [마우스 좌클릭] 사용 (마우스 커서는 나타나지 않음)
/// 게임이 시작되면 자동으로 만들어진다 (씬에 넣을 필요 없음).
/// </summary>
public class InventoryUI : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    // 화면 비율이 달라도 같은 모양이 되도록 1600 x 900 기준으로 그린 뒤 크기를 맞춘다
    const float RefW = 1600f, RefH = 900f;
    const int Columns = 4;
    const float Slot = 112f, Gap = 10f;

    enum Section { Gear, Items }

    static InventoryUI instance;
    Section section = Section.Items;
    int selected;
    float previousTimeScale = 1f;
    PlayerController pausedPlayer;
    string feedback;
    float feedbackUntil;

    Texture2D white, vignette;
    Font font;
    GUIStyle badgeStyle, titleStyle, missionStyle, headerStyle, itemTitleStyle, descStyle, hintStyle, slotTextStyle, feedbackStyle, emptyStyle;

    // 색
    static readonly Color TextMain = new Color(0.86f, 0.86f, 0.84f);
    static readonly Color TextDim = new Color(0.62f, 0.62f, 0.60f);
    static readonly Color MissionRed = new Color(0.86f, 0.25f, 0.20f);
    static readonly Color Line = new Color(1f, 1f, 1f, 0.22f);
    static readonly Color SlotFill = new Color(0.55f, 0.58f, 0.62f, 0.12f);
    static readonly Color SlotBorder = new Color(1f, 1f, 1f, 0.16f);
    static readonly Color SelFill = new Color(0.75f, 0.78f, 0.82f, 0.22f);
    static readonly Color SelBorder = new Color(1f, 1f, 1f, 0.75f);

    // 장비 칸 배치: 첫 줄 정사각형 3칸, 아래로 넓은 칸 3줄 (참고 이미지와 같은 모양)
    static readonly Rect[] GearSlots =
    {
        new Rect(0f, 0f, Slot, Slot), new Rect(Slot + Gap, 0f, Slot, Slot), new Rect((Slot + Gap) * 2f, 0f, Slot, Slot),
        new Rect(0f, (Slot + Gap) * 1f, Slot * 2f + Gap, Slot),
        new Rect(0f, (Slot + Gap) * 2f, Slot * 2f + Gap, Slot),
        new Rect(0f, (Slot + Gap) * 3f, Slot * 2f + Gap, Slot),
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { IsOpen = false; instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (instance != null) return;
        if (Time.timeScale == 0f) Time.timeScale = 1f;
        var go = new GameObject("[InventoryUI]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<InventoryUI>();
    }

    // ───────────────────────── 입력 ─────────────────────────

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.tabKey.wasPressedThisFrame)
        {
            if (IsOpen) Close();
            else Open();
            return;
        }
        if (!IsOpen) return;
        if (kb.escapeKey.wasPressedThisFrame) { Close(); return; }

        if (kb.wKey.wasPressedThisFrame) Move(0, -1);
        if (kb.sKey.wasPressedThisFrame) Move(0, 1);
        if (kb.aKey.wasPressedThisFrame) Move(-1, 0);
        if (kb.dKey.wasPressedThisFrame) Move(1, 0);

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) UseSelected();
    }

    void Move(int dx, int dy)
    {
        if (section == Section.Items)
        {
            int col = selected % Columns, row = selected / Columns;
            if (dx < 0 && col == 0)
            {
                // 아이템 칸 맨 왼쪽에서 A → 장비 칸으로
                section = Section.Gear;
                selected = row == 0 ? 2 : Mathf.Min(2 + row, GearSlots.Length - 1);
                return;
            }
            col = Mathf.Clamp(col + dx, 0, Columns - 1);
            row = Mathf.Clamp(row + dy, 0, Inventory.Capacity / Columns - 1);
            selected = row * Columns + col;
            return;
        }

        // 장비 칸 (0,1,2 = 첫 줄, 3,4,5 = 넓은 칸)
        if (dx > 0)
        {
            if (selected < 2) selected++;
            else { section = Section.Items; selected = (selected <= 2 ? 0 : selected - 2) * Columns; }
        }
        else if (dx < 0) { if (selected > 0 && selected <= 2) selected--; }
        else if (dy > 0) selected = selected <= 2 ? 3 : Mathf.Min(selected + 1, GearSlots.Length - 1);
        else if (dy < 0)
        {
            if (selected == 3) selected = 0;        // 첫 넓은 칸 → 첫 줄
            else if (selected > 3) selected--;      // 넓은 칸끼리 위로
        }
    }

    InventoryItem SelectedItem()
    {
        IReadOnlyList<InventoryItem> list = section == Section.Gear ? Inventory.Gear : Inventory.Items;
        return selected < list.Count ? list[selected] : null;
    }

    void UseSelected()
    {
        InventoryItem item = SelectedItem();
        if (item == null) return;
        if (item.OnUse != null)
        {
            Close(); // 사용 효과가 게임 안에서 바로 보이도록 먼저 닫는다
            if (item.OnUse()) return;
            Open();  // 사용할 수 없으면 다시 연다
        }
        feedback = "지금은 사용할 수 없다.";
        feedbackUntil = Time.unscaledTime + 2f;
    }

    void Open()
    {
        if (SceneTravel.IsTraveling || IsOpen || IntroRadio.IsPlaying) return; // 시작 무전 연출 중에는 안 열림
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == SceneTravel.PrologueSceneName) return; // 프롤로그 컷신 중에는 안 열림
        IsOpen = true;
        IReadOnlyList<InventoryItem> list = section == Section.Gear ? Inventory.Gear : Inventory.Items;
        if (selected >= list.Count) { section = Section.Items; selected = 0; }

        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        GameObject player = GameObject.FindWithTag("Player");
        pausedPlayer = player != null ? player.GetComponent<PlayerController>() : null;
        if (pausedPlayer != null && pausedPlayer.enabled) pausedPlayer.enabled = false;
        else pausedPlayer = null;
        // 마우스 커서는 숨긴 채로 둔다 (클릭은 '사용'으로만 쓰임)
    }

    void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        Time.timeScale = previousTimeScale;
        AudioListener.pause = false;
        if (pausedPlayer != null && !SceneTravel.IsTraveling) pausedPlayer.enabled = true;
        pausedPlayer = null;
    }

    void OnDestroy()
    {
        if (IsOpen) { Time.timeScale = previousTimeScale; AudioListener.pause = false; IsOpen = false; }
        if (white != null) Destroy(white);
        if (vignette != null) Destroy(vignette);
        if (font != null) Destroy(font);
    }

    // ───────────────────────── 그리기 ─────────────────────────

    void OnGUI()
    {
        if (!IsOpen) return;
        EnsureResources();
        GUI.depth = -500;

        // 멈춘 게임 화면 위에 어두운 막 + 가장자리 더 어둡게
        Color prev = GUI.color;
        GUI.color = new Color(0.02f, 0.03f, 0.04f, 0.62f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);
        GUI.color = prev;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), vignette);

        // 글씨가 깨지지 않도록 화면 전체를 늘리지(GUI.matrix) 않고,
        // 위치/크기와 글자 크기를 화면 해상도에 맞춰 직접 계산해서 그린다.
        UpdateScale();

        // 위쪽: 제목 / 임무
        Label(new Rect(70f, 40f, 600f, 60f), "INVENTORY", titleStyle);
        Label(new Rect(900f, 48f, 630f, 40f), "[임무: " + Mission.Current + "]", missionStyle);

        DrawGear(new Vector2(70f, 160f));
        DrawItems(new Vector2(560f, 160f));
        Fill(new Rect(1090f, 205f, 1f, 500f), Line); // 가운데와 오른쪽 사이 세로선
        DrawDetail(new Rect(1120f, 205f, 410f, 560f));

        // 아래쪽: 조작 안내 (화면 맨 아래 띠에만 그린다 → 설명 칸에는 절대 겹치지 않음)
        Fill(new Rect(40f, 800f, 1520f, 1f), Line);
        Label(new Rect(900f, 815f, 630f, 34f), "[WASD] 이동    [TAB] 닫기    [마우스 좌클릭] 사용", hintStyle);
    }

    void DrawGear(Vector2 origin)
    {
        Label(new Rect(origin.x, origin.y, 400f, 34f), "장비 (GEAR)", headerStyle);
        Fill(new Rect(origin.x, origin.y + 38f, Slot * 3f + Gap * 2f + 20f, 1f), Line);

        for (int i = 0; i < GearSlots.Length; i++)
        {
            Rect r = GearSlots[i];
            r.x += origin.x; r.y += origin.y + 45f;
            InventoryItem item = i < Inventory.Gear.Count ? Inventory.Gear[i] : null;
            DrawSlot(r, item, section == Section.Gear && selected == i, iconSquare: true);
        }
    }

    void DrawItems(Vector2 origin)
    {
        Label(new Rect(origin.x, origin.y, 500f, 34f), "획득한 아이템 & 단서", headerStyle);
        Fill(new Rect(origin.x, origin.y + 38f, Columns * Slot + (Columns - 1) * Gap + 20f, 1f), Line);

        for (int i = 0; i < Inventory.Capacity; i++)
        {
            int col = i % Columns, row = i / Columns;
            Rect r = new Rect(origin.x + col * (Slot + Gap), origin.y + 45f + row * (Slot + Gap), Slot, Slot);
            InventoryItem item = i < Inventory.Items.Count ? Inventory.Items[i] : null;
            DrawSlot(r, item, section == Section.Items && selected == i, iconSquare: true);
        }
    }

    void DrawSlot(Rect r, InventoryItem item, bool isSelected, bool iconSquare)
    {
        Fill(r, isSelected ? SelFill : SlotFill);
        Frame(r, isSelected ? 2f : 1f, isSelected ? SelBorder : SlotBorder);
        if (item == null) return;

        // 넓은 칸은 왼쪽 정사각형 부분에 아이콘
        Rect iconArea = iconSquare ? new Rect(r.x, r.y, Mathf.Min(r.width, r.height), r.height) : r;
        if (item.Icon != null)
            Tex(new Rect(iconArea.x + 4f, iconArea.y + 4f, iconArea.width - 8f, iconArea.height - 8f), item.Icon);
        else
            Label(iconArea, item.SlotName, slotTextStyle);

        // 칸 오른쪽 아래 작은 글자 (예: 손전등 배터리 100%)
        string badge = item.Badge?.Invoke();
        if (!string.IsNullOrEmpty(badge))
            Label(new Rect(iconArea.x, iconArea.yMax - 26f, iconArea.width - 8f, 22f), badge, badgeStyle);
    }

    void DrawDetail(Rect area)
    {
        Rect preview = new Rect(area.x, area.y, area.width, 170f);
        Fill(preview, SlotFill);
        Frame(preview, 1f, SlotBorder);

        InventoryItem item = SelectedItem();
        if (item == null)
        {
            Label(new Rect(area.x, area.y + 190f, area.width, 40f),
                Inventory.Items.Count == 0 && Inventory.Gear.Count == 0 ? "아직 가진 물건이 없다." : "빈 칸", emptyStyle);
        }
        else
        {
            if (item.Icon != null)
                Tex(new Rect(preview.center.x - 82f, preview.y + 4f, 164f, 162f), item.Icon);
            Label(new Rect(area.x, area.y + 190f, area.width, 40f), item.Title + "  —", itemTitleStyle);
            Label(new Rect(area.x, area.y + 240f, area.width, 260f), item.Description, descStyle);
        }

        if (Time.unscaledTime < feedbackUntil)
            Label(new Rect(area.x, area.yMax - 40f, area.width, 34f), feedback, feedbackStyle);
    }

    // ── 1600x900 기준 좌표 → 실제 화면 픽셀 (정수로 맞춰서 선과 글씨가 또렷하게) ──
    float k = 1f, ox, oy;
    int scaledFor = -1;

    void UpdateScale()
    {
        k = Mathf.Min(Screen.width / RefW, Screen.height / RefH);
        ox = Mathf.Round((Screen.width - RefW * k) / 2f);
        oy = Mathf.Round((Screen.height - RefH * k) / 2f);
        int key = Screen.width * 10000 + Screen.height;
        if (key == scaledFor) return;
        scaledFor = key;
        foreach (var pair in baseSizes) pair.Key.fontSize = Mathf.Max(8, Mathf.RoundToInt(pair.Value * k));
    }

    Rect T(Rect r)
    {
        float x = Mathf.Round(ox + r.x * k), y = Mathf.Round(oy + r.y * k);
        return new Rect(x, y, Mathf.Max(1f, Mathf.Round(ox + r.xMax * k) - x), Mathf.Max(1f, Mathf.Round(oy + r.yMax * k) - y));
    }

    void Label(Rect r, string text, GUIStyle style) => GUI.Label(T(r), text, style);
    void Tex(Rect r, Texture tex) => GUI.DrawTexture(T(r), tex, ScaleMode.ScaleToFit, true);

    void Fill(Rect r, Color c)
    {
        Color prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(T(r), white);
        GUI.color = prev;
    }

    // 테두리 두께는 화면 크기와 상관없이 1~2픽셀로 또렷하게
    void Frame(Rect r, float t, Color c)
    {
        Rect s = T(r);
        float px = Mathf.Max(1f, Mathf.Round(t * Mathf.Min(k, 1.5f)));
        Color prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(new Rect(s.x, s.y, s.width, px), white);
        GUI.DrawTexture(new Rect(s.x, s.yMax - px, s.width, px), white);
        GUI.DrawTexture(new Rect(s.x, s.y + px, px, s.height - px * 2f), white);
        GUI.DrawTexture(new Rect(s.xMax - px, s.y + px, px, s.height - px * 2f), white);
        GUI.color = prev;
    }

    // ───────────────────────── 리소스 (코드로 생성) ─────────────────────────

    void EnsureResources()
    {
        if (white != null) return;

        white = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
        white.SetPixel(0, 0, Color.white);
        white.Apply();
        vignette = MakeVignette();

        // 맑은 고딕에는 기울임꼴이 없고 굵은 글씨는 별도 글꼴이라, 가짜로 기울이거나 굵게 만들면 글씨가 깨진다
        font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 24);
        titleStyle = Style(40, FontStyle.Normal, TextAnchor.MiddleLeft, TextMain);
        missionStyle = Style(22, FontStyle.Normal, TextAnchor.MiddleRight, MissionRed);
        headerStyle = Style(21, FontStyle.Normal, TextAnchor.MiddleLeft, TextDim);
        itemTitleStyle = Style(28, FontStyle.Normal, TextAnchor.MiddleLeft, TextMain);
        descStyle = Style(21, FontStyle.Normal, TextAnchor.UpperLeft, TextDim);
        descStyle.wordWrap = true;
        hintStyle = Style(18, FontStyle.Normal, TextAnchor.MiddleRight, TextDim);
        slotTextStyle = Style(16, FontStyle.Normal, TextAnchor.MiddleCenter, TextMain);
        feedbackStyle = Style(18, FontStyle.Normal, TextAnchor.MiddleLeft, MissionRed);
        emptyStyle = Style(20, FontStyle.Normal, TextAnchor.MiddleLeft, TextDim);
        badgeStyle = Style(15, FontStyle.Normal, TextAnchor.LowerRight, TextMain);
    }

    readonly Dictionary<GUIStyle, int> baseSizes = new Dictionary<GUIStyle, int>();

    GUIStyle Style(int size, FontStyle fs, TextAnchor anchor, Color color)
    {
        // 모든 글씨는 일반 맑은 고딕 하나로 그린다.
        // ("Malgun Gothic Bold"라는 글꼴은 따로 불러올 수 없어서, 그걸 쓰던 제목 칸에 다른 글씨가 대신 그려지는 문제가 있었다)
        var s = new GUIStyle { font = font, fontSize = size, fontStyle = FontStyle.Normal, alignment = anchor, richText = false };
        s.normal.textColor = color;
        s.clipping = TextClipping.Clip;
        s.name = "InventoryUI_" + baseSizes.Count;
        baseSizes[s] = size;
        return s;
    }

    // 화면 가장자리를 어둡게 하는 비네팅
    static Texture2D MakeVignette()
    {
        const int s = 128;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var px = new Color[s * s];
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float u = x / (float)(s - 1) * 2f - 1f, v = y / (float)(s - 1) * 2f - 1f;
            float d = Mathf.Clamp01((Mathf.Sqrt(u * u + v * v) - 0.55f) / 0.9f);
            px[y * s + x] = new Color(0f, 0f, 0f, d * d * 0.7f);
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}

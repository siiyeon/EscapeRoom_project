using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// TAB키로 여는 출석부 느낌의 인벤토리 화면.
/// - 열려 있는 동안 게임이 멈춘다 (Time.timeScale = 0 → 귀신도 멈춤, 플레이어 조작 정지, 소리 일시정지).
/// - 왼쪽 페이지: 아이템 칸 12개 (4 x 3). WASD로 칸 선택 (마우스 커서는 나타나지 않음).
/// - 오른쪽 페이지: 선택한 아이템의 큰 아이콘, 이름, 설명.
/// - TAB 또는 ESC로 닫는다.
/// 게임이 시작되면 자동으로 만들어진다 (씬에 넣을 필요 없음).
/// </summary>
public class InventoryUI : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    // 화면 비율이 달라도 같은 모양이 되도록 1600 x 900 기준으로 그린 뒤 크기를 맞춘다
    const float RefW = 1600f, RefH = 900f;
    const int Columns = 4, Rows = 3;

    static InventoryUI instance;
    int selected;
    float previousTimeScale = 1f;
    PlayerController pausedPlayer;

    Texture2D white, leftPage, rightPage, leather, spine;
    Font koreanFont, titleFont;
    GUIStyle titleStyle, closeStyle, slotNameStyle, slotSubStyle, itemTitleStyle, descStyle, emptyStyle;

    // 색
    static readonly Color Ink = new Color(0.13f, 0.09f, 0.06f);
    static readonly Color InkFaded = new Color(0.13f, 0.09f, 0.06f, 0.6f);
    static readonly Color Rule = new Color(0.30f, 0.23f, 0.16f, 0.30f);
    static readonly Color SlotBorder = new Color(0.17f, 0.12f, 0.08f, 0.9f);
    static readonly Color SlotFill = new Color(0.20f, 0.15f, 0.10f, 0.22f);
    static readonly Color SelectedBorder = new Color(0.45f, 0.05f, 0.04f, 1f);

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

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.tabKey.wasPressedThisFrame)
        {
            if (IsOpen) Close();
            else Open();
        }
        else if (IsOpen && kb.escapeKey.wasPressedThisFrame) Close();

        if (!IsOpen) return;
        // WASD로 칸 이동 (열려 있는 동안 플레이어 조작은 꺼져 있어서 캐릭터는 안 움직인다)
        if (kb.dKey.wasPressedThisFrame) selected = Mathf.Min(selected + 1, Inventory.Capacity - 1);
        if (kb.aKey.wasPressedThisFrame) selected = Mathf.Max(selected - 1, 0);
        if (kb.sKey.wasPressedThisFrame) selected = Mathf.Min(selected + Columns, Inventory.Capacity - 1);
        if (kb.wKey.wasPressedThisFrame) selected = Mathf.Max(selected - Columns, 0);
    }

    void Open()
    {
        if (SceneTravel.IsTraveling) return;
        IsOpen = true;
        if (selected >= Inventory.Items.Count) selected = 0;

        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        GameObject player = GameObject.FindWithTag("Player");
        pausedPlayer = player != null ? player.GetComponent<PlayerController>() : null;
        if (pausedPlayer != null && pausedPlayer.enabled) pausedPlayer.enabled = false;
        else pausedPlayer = null;
        // 마우스 커서는 숨긴 채로 둔다 (조작은 WASD만)
    }

    void Close()
    {
        IsOpen = false;
        Time.timeScale = previousTimeScale;
        AudioListener.pause = false;
        if (pausedPlayer != null && !SceneTravel.IsTraveling) pausedPlayer.enabled = true;
        pausedPlayer = null;
    }

    void OnDestroy()
    {
        if (IsOpen) { Time.timeScale = previousTimeScale; AudioListener.pause = false; IsOpen = false; }
        foreach (Object o in new Object[] { white, leftPage, rightPage, leather, spine, koreanFont, titleFont })
            if (o != null) Destroy(o);
    }

    // ───────────────────────── 그리기 ─────────────────────────

    void OnGUI()
    {
        if (!IsOpen) return;
        EnsureResources();
        GUI.depth = -500;

        // 뒤 배경 어둡게
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);
        GUI.color = Color.white;

        float scale = Mathf.Min(Screen.width / RefW, Screen.height / RefH);
        Matrix4x4 old = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - RefW * scale) / 2f, (Screen.height - RefH * scale) / 2f, 0f),
                                   Quaternion.identity, new Vector3(scale, scale, 1f));

        Rect book = new Rect(200f, 85f, 1200f, 730f);
        DrawBook(book, out Rect left, out Rect right);
        DrawLeftPage(left);
        DrawRightPage(right);

        GUI.matrix = old;
    }

    void DrawBook(Rect book, out Rect left, out Rect right)
    {
        // 그림자 + 가죽 표지
        Fill(new Rect(book.x + 10, book.y + 14, book.width, book.height), new Color(0, 0, 0, 0.45f));
        Rect cover = new Rect(book.x - 16, book.y - 14, book.width + 32, book.height + 28);
        GUI.DrawTexture(cover, leather);
        Frame(cover, 2f, new Color(0.08f, 0.03f, 0.02f, 0.9f));

        float half = book.width / 2f;
        left = new Rect(book.x, book.y, half, book.height);
        right = new Rect(book.x + half, book.y, half, book.height);
        GUI.DrawTexture(left, leftPage);
        GUI.DrawTexture(right, rightPage);

        // 가운데 접히는 부분
        GUI.DrawTexture(new Rect(book.x + half - 28f, book.y, 56f, book.height), spine);
        Fill(new Rect(book.x + half - 1f, book.y, 2f, book.height), new Color(0.15f, 0.1f, 0.07f, 0.5f));
    }

    void DrawLeftPage(Rect page)
    {
        GUI.Label(new Rect(page.x + 44f, page.y + 26f, 400f, 64f), "Inventory", titleStyle);
        Fill(new Rect(page.x + 40f, page.y + 92f, page.width - 84f, 2f), new Color(Ink.r, Ink.g, Ink.b, 0.55f));

        const float slotW = 118f, slotH = 132f, gapX = 16f, gapY = 22f;
        float gridW = Columns * slotW + (Columns - 1) * gapX;
        float startX = page.x + (page.width - gridW) / 2f - 6f;
        float startY = page.y + 128f;

        for (int i = 0; i < Inventory.Capacity; i++)
        {
            int col = i % Columns, row = i / Columns;
            Rect slot = new Rect(startX + col * (slotW + gapX), startY + row * (slotH + gapY), slotW, slotH);
            InventoryItem item = i < Inventory.Items.Count ? Inventory.Items[i] : null;

            Fill(slot, SlotFill);
            Frame(slot, 2f, SlotBorder);
            Frame(new Rect(slot.x + 4f, slot.y + 4f, slot.width - 8f, slot.height - 8f), 1f, new Color(SlotBorder.r, SlotBorder.g, SlotBorder.b, 0.35f));
            if (i == selected) Frame(new Rect(slot.x - 3f, slot.y - 3f, slot.width + 6f, slot.height + 6f), 3f, SelectedBorder);

            if (item != null)
            {
                if (item.Icon != null)
                    GUI.DrawTexture(new Rect(slot.x + 16f, slot.y + 8f, slot.width - 32f, slot.width - 32f), item.Icon, ScaleMode.ScaleToFit, true);
                GUI.Label(new Rect(slot.x, slot.y + slotH - 44f, slotW, 24f), item.SlotName, slotNameStyle);
                if (!string.IsNullOrEmpty(item.SlotSubName))
                    GUI.Label(new Rect(slot.x, slot.y + slotH - 24f, slotW, 18f), "(" + item.SlotSubName + ")", slotSubStyle);
            }
        }
    }

    void DrawRightPage(Rect page)
    {
        // 공책 줄
        for (float y = page.y + 96f; y < page.yMax - 28f; y += 38f)
            Fill(new Rect(page.x + 34f, y, page.width - 64f, 1f), Rule);
        foreach (float x in new[] { 52f, 60f, 92f })
            Fill(new Rect(page.x + x, page.y + 60f, 1f, page.height - 90f), Rule);

        GUI.Label(new Rect(page.xMax - 200f, page.y + 30f, 160f, 34f), "[TAB] 닫기", closeStyle);

        InventoryItem item = selected < Inventory.Items.Count ? Inventory.Items[selected] : null;
        if (item == null)
        {
            GUI.Label(new Rect(page.x + 60f, page.y + 300f, page.width - 120f, 60f),
                Inventory.Items.Count == 0 ? "아직 가진 물건이 없다." : "빈 칸", emptyStyle);
            return;
        }

        if (item.Icon != null)
            GUI.DrawTexture(new Rect(page.center.x - 140f, page.y + 92f, 280f, 280f), item.Icon, ScaleMode.ScaleToFit, true);
        GUI.Label(new Rect(page.x + 120f, page.y + 410f, page.width - 170f, 48f), item.Title, itemTitleStyle);
        GUI.Label(new Rect(page.x + 120f, page.y + 470f, page.width - 170f, 200f), item.Description, descStyle);
    }

    void Fill(Rect r, Color c)
    {
        Color prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, white);
        GUI.color = prev;
    }

    void Frame(Rect r, float t, Color c)
    {
        Fill(new Rect(r.x, r.y, r.width, t), c);
        Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
        Fill(new Rect(r.x, r.y, t, r.height), c);
        Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    // ───────────────────────── 리소스 (코드로 생성) ─────────────────────────

    void EnsureResources()
    {
        if (white != null) return;

        white = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
        white.SetPixel(0, 0, Color.white);
        white.Apply();

        leftPage = MakePaper(spineOnRight: true, seed: 3.1f);
        rightPage = MakePaper(spineOnRight: false, seed: 7.7f);
        leather = MakeLeather();
        spine = MakeSpine();

        koreanFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 24);
        titleFont = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Times New Roman", "Malgun Gothic" }, 48);

        titleStyle = Style(titleFont, 50, FontStyle.BoldAndItalic, TextAnchor.MiddleLeft, Ink);
        closeStyle = Style(koreanFont, 20, FontStyle.Normal, TextAnchor.MiddleRight, Ink);
        slotNameStyle = Style(koreanFont, 16, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
        slotSubStyle = Style(koreanFont, 12, FontStyle.Normal, TextAnchor.MiddleCenter, InkFaded);
        itemTitleStyle = Style(koreanFont, 30, FontStyle.Bold, TextAnchor.MiddleLeft, Ink);
        descStyle = Style(koreanFont, 22, FontStyle.Normal, TextAnchor.UpperLeft, Ink);
        descStyle.wordWrap = true;
        emptyStyle = Style(koreanFont, 22, FontStyle.Italic, TextAnchor.MiddleCenter, InkFaded);
    }

    static GUIStyle Style(Font font, int size, FontStyle fs, TextAnchor anchor, Color color)
    {
        var s = new GUIStyle { font = font, fontSize = size, fontStyle = fs, alignment = anchor, richText = false };
        s.normal.textColor = color;
        return s;
    }

    static float Fbm(float x, float y)
    {
        return Mathf.PerlinNoise(x, y) * 0.55f + Mathf.PerlinNoise(x * 2.3f, y * 2.3f) * 0.3f + Mathf.PerlinNoise(x * 6f, y * 6f) * 0.15f;
    }

    // 오래되어 바랜 종이:
    // 누렇게 바랜 색 + 얼룩 + 검은 점 + 군데군데 그을린 테두리 + 찢어진 테두리와 뜯겨나간 모서리.
    // 찢어진 부분은 투명해서 뒤의 어두운 표지가 보인다. 가운데(접히는 쪽)는 찢지 않는다.
    static Texture2D MakePaper(bool spineOnRight, float seed)
    {
        const int w = 384, h = 468;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var px = new Color[w * h];
        Color baseCol = new Color(0.64f, 0.56f, 0.41f);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float u = x / (float)(w - 1), v = y / (float)(h - 1);
            float n = Fbm(u * 5f + seed, v * 6f + seed);

            // 얼룩: 넓게 번진 얼룩 + 검은 점
            float blot = Mathf.PerlinNoise(u * 2.2f + seed * 3f, v * 2.2f + seed);
            float stain = Mathf.Clamp01((blot - 0.58f) * 3f);
            float speck = Mathf.PerlinNoise(u * 90f + seed, v * 90f) > 0.82f ? 0.18f : 0f;

            // 가장자리까지 거리 (접히는 쪽은 찢지 않으므로 바깥 세 변만)
            float spineDist = spineOnRight ? 1f - u : u;
            float outerX = spineOnRight ? u : 1f - u;
            float edge = Mathf.Min(outerX, Mathf.Min(v, 1f - v));

            // 테두리를 따라가는 좌표 (변마다 따로)
            float along = (outerX < Mathf.Min(v, 1f - v)) ? v * 40f : u * 32f + 100f;
            // 군데군데만 그을린 구간 (0 = 멀쩡, 1 = 심하게 탐)
            float scorch = Mathf.Clamp01((Mathf.PerlinNoise(along * 0.12f + seed * 7f, seed * 2f) - 0.5f) * 4f);
            // 찢어진 테두리: 들쭉날쭉한 깊이 (탄 곳은 더 깊이 먹혀 들어감)
            float tear = 0.010f + Fbm(along + seed * 5f, seed) * 0.03f + scorch * 0.025f;
            // 뜯겨나간 모서리 (왼쪽 페이지는 왼쪽 위, 오른쪽 페이지는 오른쪽 아래)
            float cornerDist = spineOnRight ? u + (1f - v) : (1f - u) + v;
            float corner = (spineOnRight ? 0.09f : 0.13f) + Fbm(u * 25f + seed, v * 25f) * 0.05f;
            bool missing = edge < tear || cornerDist < corner;

            // 그을음: 탄 구간에서만, 찢어진 끝에서 안쪽으로 번지듯 (모서리 뜯긴 곳은 약하게)
            float burnEdge = Mathf.Pow(Mathf.Clamp01(1f - (edge - tear) / 0.06f), 1.5f) * scorch * 0.75f;
            float burnCorner = Mathf.Clamp01(1f - (cornerDist - corner) / 0.03f) * 0.3f;
            float burn = Mathf.Max(burnEdge, burnCorner);
            float spineShade = Mathf.Pow(Mathf.Clamp01(1f - spineDist / 0.13f), 2f) * 0.4f;
            float edgeShade = Mathf.Pow(Mathf.Clamp01(1f - edge / 0.08f), 2f) * 0.12f;

            float k = 0.80f + n * 0.22f - stain * 0.2f - speck - burn - spineShade - edgeShade;
            k = Mathf.Max(k, 0.05f);
            px[y * w + x] = new Color(baseCol.r * k, baseCol.g * k * (1f - stain * 0.05f), baseCol.b * k * (1f - stain * 0.15f),
                                      missing ? 0f : 1f);
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // 어두운 출석부 표지 (거의 검은 고동색)
    static Texture2D MakeLeather()
    {
        const int s = 192;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var px = new Color[s * s];
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float u = x / (float)(s - 1), v = y / (float)(s - 1);
            float n = Fbm(u * 9f + 11f, v * 9f + 5f);
            float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            float k = 0.7f + n * 0.45f - Mathf.Clamp01(1f - edge / 0.05f) * 0.35f;
            px[y * s + x] = new Color(0.11f * k, 0.065f * k, 0.05f * k, 1f);
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    static Texture2D MakeSpine()
    {
        const int w = 64;
        var tex = new Texture2D(w, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        for (int x = 0; x < w; x++)
        {
            float t = Mathf.Abs(x / (float)(w - 1) - 0.5f) * 2f; // 가운데 0 → 양 끝 1
            tex.SetPixel(x, 0, new Color(0.12f, 0.07f, 0.04f, Mathf.Pow(1f - t, 1.6f) * 0.55f));
        }
        tex.Apply();
        return tex;
    }
}

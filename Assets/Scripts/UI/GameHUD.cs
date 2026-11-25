using UnityEngine;

/// <summary>
/// 게임 화면 글씨를 한 곳에서 그린다. 모든 글씨는 배경 상자 없이 글자만 (읽기 쉽게 그림자만 살짝).
/// - Say(...)    : 대사 / 혼잣말 / 무전 → 화면 아래 가운데
/// - Hint(...)   : 조작 안내 (예: "Shift : 달리기") → 화면 가운데에서 살짝 아래
/// - Prompt(...) : 지금 누를 수 있는 키 (예: "[E] 철문 열기") → 조작 안내와 같은 자리. 보여줄 동안 매 프레임 호출
/// 게임이 시작되면 자동으로 만들어진다 (씬에 넣을 필요 없음).
/// </summary>
public class GameHUD : MonoBehaviour
{
    static GameHUD instance;

    string sayText, hintText, promptText;
    float sayUntil, hintUntil, sayStart, hintStart;
    int promptFrame = -10;

    Font font;
    GUIStyle sayStyle, hintStyle, shadowStyle;
    int styledHeight = -1;

    const float FadeTime = 0.25f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;

    // 배터리 표시가 항상 보이도록 게임 시작 때 바로 만든다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateOnStart() => Get();

    static GameHUD Get()
    {
        if (instance == null)
        {
            var go = new GameObject("[GameHUD]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<GameHUD>();
        }
        return instance;
    }

    /// <summary>대사/혼잣말을 화면 아래 가운데에 seconds초 동안 보여준다.</summary>
    public static void Say(string text, float seconds = 3f)
    {
        GameHUD h = Get();
        if (h.sayText != text || Time.unscaledTime >= h.sayUntil) h.sayStart = Time.unscaledTime;
        h.sayText = text;
        h.sayUntil = Time.unscaledTime + seconds;
    }

    /// <summary>조작 안내를 화면 가운데 살짝 아래에 seconds초 동안 보여준다.</summary>
    public static void Hint(string text, float seconds = 2.5f)
    {
        GameHUD h = Get();
        if (h.hintText != text || Time.unscaledTime >= h.hintUntil) h.hintStart = Time.unscaledTime;
        h.hintText = text;
        h.hintUntil = Time.unscaledTime + seconds;
    }

    /// <summary>지금 누를 수 있는 키 안내. 보여줄 동안 매 프레임(Update) 호출한다.</summary>
    public static void Prompt(string text)
    {
        GameHUD h = Get();
        h.promptText = text;
        h.promptFrame = Time.frameCount;
    }

    public static void ClearSay() { if (instance != null) instance.sayUntil = 0f; }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        if (InventoryUI.IsOpen || SceneTravel.IsTraveling) return;
        EnsureStyles();
        float w = Screen.width, h = Screen.height;

        // 조작 안내: 화면 가운데에서 살짝 아래 (Hint가 있으면 Hint, 없으면 Prompt)
        float hintAlpha = Alpha(hintStart, hintUntil);
        if (hintAlpha > 0f) Draw(new Rect(0, h * 0.58f, w, h * 0.06f), hintText, hintStyle, hintAlpha);
        else if (Time.frameCount - promptFrame <= 1 && !string.IsNullOrEmpty(promptText))
            Draw(new Rect(0, h * 0.58f, w, h * 0.06f), promptText, hintStyle, 1f);

        // 대사: 화면 아래 가운데
        float sayAlpha = Alpha(sayStart, sayUntil);
        if (sayAlpha > 0f) Draw(new Rect(w * 0.1f, h * 0.80f, w * 0.8f, h * 0.12f), sayText, sayStyle, sayAlpha);

        DrawBattery(w, h);
    }

    // 손전등 배터리: 화면 오른쪽 아래에 배터리 모양 + 남은 % (배경 없이). 시작 무전 연출 중에는 숨김.
    void DrawBattery(float w, float h)
    {
        FlashlightController light = FlashlightController.Instance;
        if (light == null || IntroRadio.IsPlaying) return;
        if (white == null)
        {
            white = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
            white.SetPixel(0, 0, Color.white);
            white.Apply();
        }

        float pct = Mathf.Clamp(light.BatteryPercent, 0f, 100f);
        bool low = pct <= light.lowBatteryThreshold;
        Color main = low ? new Color(0.92f, 0.32f, 0.26f) : new Color(0.88f, 0.88f, 0.85f);
        float alpha = light.IsOn ? 0.95f : 0.55f; // 꺼져 있으면 흐리게

        float u = Mathf.Round(h * 0.012f);              // 기준 크기
        float bw = u * 4.2f, bh = u * 2f;               // 배터리 몸통
        float x = Mathf.Round(w - u * 3f - bw - u * 0.5f), y = Mathf.Round(h - u * 3.2f - bh);
        float line = Mathf.Max(1f, Mathf.Round(u * 0.18f));

        // 그림자 → 테두리 → 볼록 단자 → 남은 양
        Box(new Rect(x + line, y + line, bw, bh), new Color(0f, 0f, 0f, 0.5f * alpha), line);
        Box(new Rect(x, y, bw, bh), new Color(main.r, main.g, main.b, alpha), line);
        Fill(new Rect(x + bw, y + bh * 0.3f, Mathf.Max(line * 2f, u * 0.35f), bh * 0.4f), new Color(main.r, main.g, main.b, alpha));
        float inner = bw - line * 4f;
        Fill(new Rect(x + line * 2f, y + line * 2f, Mathf.Round(inner * pct / 100f), bh - line * 4f), new Color(main.r, main.g, main.b, alpha * 0.9f));

        // 숫자는 배터리 왼쪽에
        string label = Mathf.CeilToInt(pct) + "%";
        batteryStyle.fontSize = Mathf.Max(11, Mathf.RoundToInt(h * 0.022f));
        batteryStyle.normal.textColor = new Color(main.r, main.g, main.b, alpha);
        shadowStyle.fontSize = batteryStyle.fontSize;
        shadowStyle.alignment = TextAnchor.MiddleRight;
        shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.6f * alpha);
        Rect tr = new Rect(x - u * 7f - u * 0.6f, y - u, u * 7f, bh + u * 2f);
        GUI.Label(new Rect(tr.x + line, tr.y + line, tr.width, tr.height), label, shadowStyle);
        GUI.Label(tr, label, batteryStyle);
    }

    Texture2D white;
    GUIStyle batteryStyle;

    void Fill(Rect r, Color c)
    {
        Color prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, white);
        GUI.color = prev;
    }

    void Box(Rect r, Color c, float t)
    {
        Fill(new Rect(r.x, r.y, r.width, t), c);
        Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
        Fill(new Rect(r.x, r.y, t, r.height), c);
        Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    static float Alpha(float start, float until)
    {
        float now = Time.unscaledTime;
        if (now >= until) return 0f;
        return Mathf.Clamp01(Mathf.Min((now - start) / FadeTime, (until - now) / FadeTime));
    }

    void Draw(Rect r, string text, GUIStyle style, float alpha)
    {
        if (string.IsNullOrEmpty(text)) return;
        // 배경 없이 글자만: 뒤에 어두운 그림자를 살짝 깔아서 밝은 곳에서도 읽히게
        shadowStyle.fontSize = style.fontSize;
        shadowStyle.alignment = style.alignment;
        shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.75f * alpha);
        float o = Mathf.Max(1f, Mathf.Round(style.fontSize * 0.07f));
        GUI.Label(new Rect(r.x + o, r.y + o, r.width, r.height), text, shadowStyle);

        Color c = style.normal.textColor;
        style.normal.textColor = new Color(c.r, c.g, c.b, alpha);
        GUI.Label(r, text, style);
        style.normal.textColor = c;
    }

    void EnsureStyles()
    {
        if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 24);
        if (sayStyle == null)
        {
            sayStyle = new GUIStyle { font = font, alignment = TextAnchor.UpperCenter, wordWrap = true, richText = false };
            sayStyle.normal.textColor = new Color(0.95f, 0.95f, 0.93f);
            hintStyle = new GUIStyle { font = font, alignment = TextAnchor.MiddleCenter, richText = false };
            hintStyle.normal.textColor = new Color(0.85f, 0.85f, 0.82f);
            shadowStyle = new GUIStyle { font = font, wordWrap = true, richText = false };
            batteryStyle = new GUIStyle { font = font, alignment = TextAnchor.MiddleRight, richText = false };
        }
        // 화면 높이에 맞춰 글자 크기 (늘려서 그리지 않으므로 글씨가 깨지지 않음)
        if (styledHeight != Screen.height)
        {
            styledHeight = Screen.height;
            sayStyle.fontSize = Mathf.Max(14, Mathf.RoundToInt(Screen.height * 0.032f));
            hintStyle.fontSize = Mathf.Max(12, Mathf.RoundToInt(Screen.height * 0.026f));
        }
    }

    void OnDestroy() { if (font != null) Destroy(font); if (white != null) Destroy(white); }
}

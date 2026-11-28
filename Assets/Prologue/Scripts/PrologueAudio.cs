using System.Collections;
using UnityEngine;

namespace PrologueCutscene
{
// 프롤로그 시안용 효과음 — 외부 사운드 파일 없이 코드로 합성한다.
// 빗소리, 천둥, 다이얼 전화벨, 무전 잡음/스켈치/송신음, 와이퍼, 엔진
public class PrologueAudio : MonoBehaviour
{
    public enum RainMode { Outside, InCar, Indoor, Silent }

    const int SR = 22050;
    System.Random rng = new System.Random(7);
    AudioClip keyClick, keyThock, rain, thunder, ring, staticLoop, squelch, beep, wiper, engine;
    AudioSource rainSrc, engineSrc, staticSrc, sfx, keySrc;
    AudioLowPassFilter rainLP;
    float rainVolTarget, rainCutTarget = 22000f, engineVolTarget, enginePitchTarget = 1f, staticVolTarget;

    float N() { return (float)(rng.NextDouble() * 2.0 - 1.0); }
    AudioClip Make(string name, float[] d)
    {
        float peak = 0.0001f; foreach (var v in d) peak = Mathf.Max(peak, Mathf.Abs(v));
        for (int i = 0; i < d.Length; i++) d[i] = d[i] / peak * 0.9f;
        var c = AudioClip.Create(name, d.Length, 1, SR, false); c.SetData(d, 0); return c;
    }
    static float[] LoopFade(float[] d, float sec)
    {
        int n = Mathf.Min((int)(sec * SR), d.Length / 2);
        for (int i = 0; i < n; i++) { float t = i / (float)n; int j = d.Length - n + i; d[i] = d[i] * t + d[j] * (1 - t); }
        System.Array.Resize(ref d, d.Length - n); return d;
    }

    void Awake()
    {
        BuildClips();
        rainSrc = NewSource("Rain", rain, true); rainLP = rainSrc.gameObject.AddComponent<AudioLowPassFilter>(); rainLP.cutoffFrequency = 22000;
        engineSrc = NewSource("Engine", engine, true);
        staticSrc = NewSource("RadioStatic", staticLoop, true);
        sfx = NewSource("SFX", null, false);
        keySrc = NewSource("Keyboard", null, false);
        rainSrc.volume = engineSrc.volume = staticSrc.volume = 0;
        rainSrc.Play(); engineSrc.Play(); staticSrc.Play();
    }

    AudioSource NewSource(string n, AudioClip c, bool loop)
    {
        var g = new GameObject("Audio_" + n); g.transform.SetParent(transform, false);
        var s = g.AddComponent<AudioSource>(); s.clip = c; s.loop = loop; s.playOnAwake = false; s.spatialBlend = 0; return s;
    }

    void Update()
    {
        float k = Time.deltaTime * 2.5f;
        rainSrc.volume = Mathf.Lerp(rainSrc.volume, rainVolTarget, k);
        rainLP.cutoffFrequency = Mathf.Lerp(rainLP.cutoffFrequency, rainCutTarget, k);
        engineSrc.volume = Mathf.Lerp(engineSrc.volume, engineVolTarget, k);
        engineSrc.pitch = Mathf.Lerp(engineSrc.pitch, enginePitchTarget, k);
        staticSrc.volume = Mathf.Lerp(staticSrc.volume, staticVolTarget * (0.75f + 0.25f * Mathf.PerlinNoise(Time.time * 9f, 0)), Time.deltaTime * 12f);
    }

    // ---------- 외부에서 부르는 함수 ----------
    public void SetRain(RainMode m)
    {
        switch (m)
        {
            case RainMode.Outside: rainVolTarget = 0.75f; rainCutTarget = 20000f; break;
            case RainMode.InCar: rainVolTarget = 0.6f; rainCutTarget = 2600f; break;   // 차 지붕/유리에 떨어지는 소리
            case RainMode.Indoor: rainVolTarget = 0.35f; rainCutTarget = 750f; break;  // 창밖에서 먹먹하게
            default: rainVolTarget = 0f; break;
        }
    }
    public void SetEngine(float vol, float pitch) { engineVolTarget = vol; enginePitchTarget = pitch; }
    public void Thunder(float delay, float vol) { StartCoroutine(Delayed(delay, () => sfx.PlayOneShot(thunder, vol))); }
    public void Ring() { sfx.PlayOneShot(ring, 0.55f); }
    public void Wiper() { sfx.PlayOneShot(wiper, 0.35f); }
    public void RadioStart(bool outgoing)
    {
        if (outgoing) sfx.PlayOneShot(beep, 0.35f);
        sfx.PlayOneShot(squelch, outgoing ? 0.25f : 0.5f);
        staticVolTarget = outgoing ? 0.10f : 0.22f;
    }
    public void RadioEnd()
    {
        sfx.PlayOneShot(squelch, 0.45f);
        staticVolTarget = 0f;
    }
    // 컴퓨터 키보드 타이핑 소리 (글자 하나마다)
    public void Key(bool space)
    {
        keySrc.pitch = Random.Range(0.88f, 1.12f);
        keySrc.PlayOneShot(space ? keyThock : keyClick, Random.Range(0.35f, 0.5f));
    }

    public void StopAllNow() { staticVolTarget = 0; staticSrc.volume = 0; }

    IEnumerator Delayed(float d, System.Action a) { yield return new WaitForSeconds(d); a(); }

    // ---------- 합성 ----------
    void BuildClips()
    {
        // 키보드: 딸깍(일반 키) / 툭(스페이스·엔터)
        {
            int n = (int)(SR * 0.07f); var d = new float[n]; float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR; float w = N(); lp += (w - lp) * 0.5f;
                float click = (w - lp) * Mathf.Exp(-t / 0.004f) * 1.2f;
                float body = Mathf.Sin(2 * Mathf.PI * 1900f * t) * Mathf.Exp(-t / 0.01f) * 0.35f;
                float bottom = t > 0.012f ? (Mathf.Sin(2 * Mathf.PI * 420f * t) * 0.5f + lp * 0.6f) * Mathf.Exp(-(t - 0.012f) / 0.012f) : 0f;
                d[i] = click + body + bottom;
            }
            keyClick = Make("KeyClick", d);
            int m = (int)(SR * 0.1f); var e = new float[m]; lp = 0;
            for (int i = 0; i < m; i++)
            {
                float t = i / (float)SR; float w = N(); lp += (w - lp) * 0.25f;
                e[i] = (w - lp) * Mathf.Exp(-t / 0.005f) * 0.9f + (Mathf.Sin(2 * Mathf.PI * 210f * t) * 0.7f + lp * 0.8f) * Mathf.Exp(-t / 0.025f);
            }
            keyThock = Make("KeyThock", e);
        }
        // 빗소리 (6초 루프): 핑크 노이즈 + 빗방울 톡톡
        {
            int n = SR * 6; var d = new float[n]; float lp = 0, lp2 = 0;
            for (int i = 0; i < n; i++) { float w = N(); lp += (w - lp) * 0.25f; lp2 += (lp - lp2) * 0.08f; d[i] = lp * 0.7f + lp2 * 0.6f; }
            for (int k = 0; k < 2600; k++)
            {
                int s = rng.Next(n - 400); float a = (float)rng.NextDouble() * 0.9f + 0.1f; float h = 0;
                for (int j = 0; j < 220; j++) { float w = N(); h += (w - h) * 0.6f; d[s + j] += (w - h) * a * Mathf.Exp(-j / 35f); }
            }
            d = LoopFade(d, 0.4f); rain = Make("Rain", d);
        }
        // 천둥 (7초): 날카로운 첫 소리 + 길게 우르릉
        {
            int n = SR * 7; var d = new float[n]; float br = 0, lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR; float w = N();
                br = br * 0.995f + w * 0.05f; lp += (br - lp) * 0.04f;
                float swell = 0.55f + 0.45f * Mathf.Sin(t * 2.3f) * Mathf.Sin(t * 0.9f + 1.1f);
                float env = Mathf.Clamp01(t / 0.25f) * Mathf.Exp(-t / 2.4f) * swell;
                float crack = t < 0.35f ? w * Mathf.Exp(-t / 0.06f) * 0.8f : 0f;
                d[i] = lp * 6f * env + crack;
            }
            thunder = Make("Thunder", d);
        }
        // 다이얼 전화벨 (따르르릉 x2)
        {
            int n = (int)(SR * 2.4f); var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR; bool on = (t < 0.95f) || (t > 1.35f && t < 2.3f);
                if (!on) continue;
                float strike = Mathf.Repeat(t * 22f, 1f); float hammer = Mathf.Exp(-strike * 6f);
                float bell = Mathf.Sin(2 * Mathf.PI * 1180f * t) * 0.6f + Mathf.Sin(2 * Mathf.PI * 1430f * t) * 0.4f + Mathf.Sin(2 * Mathf.PI * 2710f * t) * 0.25f;
                d[i] = bell * (0.35f + 0.65f * hammer);
            }
            ring = Make("PhoneRing", d);
        }
        // 무전 잡음 루프 / 스켈치 / 송신음
        {
            int n = SR * 2; var d = new float[n]; float lp = 0, hp = 0;
            for (int i = 0; i < n; i++)
            {
                float w = N(); lp += (w - lp) * 0.45f; hp += (lp - hp) * 0.05f; float band = lp - hp;
                if (rng.NextDouble() < 0.0015) band += N() * 3f;
                d[i] = band;
            }
            d = LoopFade(d, 0.2f); staticLoop = Make("RadioStatic", d);
            int m = (int)(SR * 0.32f); var q = new float[m]; lp = 0; hp = 0;
            for (int i = 0; i < m; i++) { float t = i / (float)SR; float w = N(); lp += (w - lp) * 0.6f; hp += (lp - hp) * 0.08f; q[i] = (lp - hp) * Mathf.Exp(-t / 0.09f); }
            squelch = Make("Squelch", q);
            int b = (int)(SR * 0.11f); var bb = new float[b];
            for (int i = 0; i < b; i++) { float t = i / (float)SR; bb[i] = Mathf.Sin(2 * Mathf.PI * 1250f * t) * Mathf.Clamp01(t / 0.005f) * Mathf.Clamp01((0.11f - t) / 0.01f); }
            beep = Make("PTTBeep", bb);
        }
        // 와이퍼 (고무 쓸리는 소리 + 끝에서 툭)
        {
            int n = (int)(SR * 0.5f); var d = new float[n]; float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR; float w = N(); lp += (w - lp) * 0.12f;
                float swish = lp * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.42f)) * 0.8f;
                float squeak = Mathf.Sin(2 * Mathf.PI * (620f + 90f * Mathf.Sin(t * 30f)) * t) * 0.06f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.42f));
                float thump = t > 0.40f ? Mathf.Sin(2 * Mathf.PI * 70f * t) * Mathf.Exp(-(t - 0.40f) / 0.03f) * 0.9f : 0f;
                d[i] = swish + squeak + thump;
            }
            wiper = Make("Wiper", d);
        }
        // 엔진 공회전/주행 루프 (2초, 33Hz 기본음 → 이음새 없음)
        {
            int n = SR * 2; var d = new float[n]; float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR; float f = 33f;
                float h = Mathf.Sin(2 * Mathf.PI * f * t) + 0.7f * Mathf.Sin(2 * Mathf.PI * 2 * f * t + 0.3f) + 0.45f * Mathf.Sin(2 * Mathf.PI * 3 * f * t + 1.1f) + 0.25f * Mathf.Sin(2 * Mathf.PI * 4 * f * t);
                float firing = 0.75f + 0.25f * Mathf.Sin(2 * Mathf.PI * 66f * t);
                float w = N(); lp += (w - lp) * 0.05f;
                d[i] = h * firing * 0.6f + lp * 0.5f;
            }
            d = LoopFade(d, 0.15f); engine = Make("Engine", d);
        }
    }
}
}

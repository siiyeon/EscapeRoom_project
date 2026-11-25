using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorPuzzle
{
    public enum PuzzleColor { Red, Blue, Yellow, Green }

    /// <summary>
    /// 색 버튼 순서 퍼즐. 색 버튼을 정답 순서대로 누르고 ENTER(옵션)를 누르면 풀린다.
    /// 다른 상호작용 시스템에서 쓰려면 PressColor(), PressEnter()를 직접 호출하면 된다.
    /// </summary>
    public class ColorButtonPuzzle : MonoBehaviour
    {
        [Header("정답")]
        [Tooltip("눌러야 하는 색 순서")]
        public PuzzleColor[] solution = { PuzzleColor.Red, PuzzleColor.Blue, PuzzleColor.Yellow, PuzzleColor.Green };
        [Tooltip("켜면 ENTER를 눌러야 확인, 끄면 정답 길이만큼 누르는 순간 자동 확인")]
        public bool requireEnter = true;

        [Header("파츠 (프리팹 생성 시 자동 연결)")]
        public Renderer body;
        public Renderer lamp;
        public Light lampLight;
        public Transform needle;
        public Renderer gaugeFace;
        [Tooltip("바늘이 움직이는 전체 각도 (블렌더 기준 -50도 ~ +50도)")]
        public float needleSweep = 100f;

        [Header("표시등 색")]
        public Color idleColor = new Color(0.25f, 0.02f, 0.02f);
        public Color successColor = new Color(0.1f, 1f, 0.2f);
        public Color failColor = new Color(1f, 0.05f, 0.02f);
        public float emissionIntensity = 4f;

        [Header("소리 (선택)")]
        public AudioSource audioSource;
        public AudioClip pressClip;
        public AudioClip enterClip;
        public AudioClip successClip;
        public AudioClip failClip;

        [Header("이벤트")]
        public UnityEvent onSolved;
        public UnityEvent onFailed;
        public UnityEvent onButtonPressed;

        /// <summary>코드용 이벤트: 색 버튼이 눌릴 때마다</summary>
        public event Action<PuzzleColor> ColorPressed;

        public bool IsSolved { get; private set; }
        public bool IsBusy { get; private set; }
        public IReadOnlyList<PuzzleColor> CurrentInput => input;

        readonly List<PuzzleColor> input = new List<PuzzleColor>();
        Material lampMat;
        Quaternion needleRest;
        Vector3 needleAxisLocal;   // in needle-parent space
        float needleShown, needleTarget;
        Vector3 pressDirWorld;
        bool initialized;

        void Awake() { Init(); }

        public void Init()
        {
            if (initialized) return;
            initialized = true;
            if (lamp != null)
            {
                lampMat = lamp.material; // instance
                lampMat.EnableKeyword("_EMISSION");
                lampMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            SetLamp(idleColor, false);
            pressDirWorld = ComputePressDirection();
            SetupNeedle();
            foreach (var b in GetComponentsInChildren<PuzzleButton>(true)) b.Bind(this);
        }

        /// <summary>버튼이 들어가는 방향(박스 안쪽, 월드 기준)</summary>
        public Vector3 PressDirectionWorld { get { if (!initialized) Init(); return pressDirWorld; } }

        // 박스 몸체 메시의 가장 얇은 축 = 앞뒤 축. 회전·스케일된 프리팹에서도 동작
        Vector3 ComputePressDirection()
        {
            if (body == null) return -transform.forward;
            var mf = body.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return -transform.forward;
            Vector3 s = mf.sharedMesh.bounds.size;
            Vector3 axis = s.x <= s.y && s.x <= s.z ? Vector3.right : (s.y <= s.z ? Vector3.up : Vector3.forward);
            Vector3 w = body.transform.TransformDirection(axis).normalized;
            Vector3 center = body.transform.TransformPoint(mf.sharedMesh.bounds.center);
            // 버튼(앞면)에서 몸체 중심 쪽이 안쪽
            Vector3 front = center;
            var anyButton = GetComponentInChildren<PuzzleButton>(true);
            if (anyButton != null) front = anyButton.transform.position;
            if (Vector3.Dot(center - front, w) < 0) w = -w;
            return w;
        }

        void SetupNeedle()
        {
            if (needle == null) return;
            needleRest = needle.localRotation;
            Vector3 axisW = pressDirWorld;
            // 방향 결정: 바늘을 +sweep/2 돌렸을 때 눈금판 중심(위쪽)을 향하는 부호를 고른다
            var mf = needle.GetComponent<MeshFilter>();
            Vector3 tip = mf != null && mf.sharedMesh != null
                ? needle.TransformPoint(mf.sharedMesh.bounds.center) - needle.position
                : needle.up;
            Vector3 up = transform.up;
            if (gaugeFace != null)
            {
                var gmf = gaugeFace.GetComponent<MeshFilter>();
                if (gmf != null && gmf.sharedMesh != null)
                    up = gaugeFace.transform.TransformPoint(gmf.sharedMesh.bounds.center) - needle.position;
            }
            up = Vector3.ProjectOnPlane(up, axisW).normalized;
            float a = Vector3.Dot(Quaternion.AngleAxis(needleSweep * 0.5f, axisW) * tip, up);
            float b = Vector3.Dot(Quaternion.AngleAxis(-needleSweep * 0.5f, axisW) * tip, up);
            if (b > a) axisW = -axisW;
            needleAxisLocal = needle.parent != null ? needle.parent.InverseTransformDirection(axisW) : axisW;
        }

        void Update()
        {
            if (needle == null) return;
            needleShown = Mathf.MoveTowards(needleShown, needleTarget, Time.deltaTime * 2.5f);
            needle.localRotation = Quaternion.AngleAxis(needleShown * needleSweep, needleAxisLocal) * needleRest;
        }

        // ---------- 입력 ----------
        public void PressColor(PuzzleColor c)
        {
            if (IsSolved || IsBusy) return;
            if (input.Count >= solution.Length) { Play(pressClip); return; } // 가득 차면 ENTER 대기
            input.Add(c);
            Play(pressClip);
            needleTarget = solution.Length > 0 ? (float)input.Count / solution.Length : 0f;
            ColorPressed?.Invoke(c);
            onButtonPressed?.Invoke();
            if (!requireEnter && input.Count >= solution.Length) Check();
        }

        // 인스펙터 UnityEvent에서 연결하기 쉽게
        public void PressRed() => PressColor(PuzzleColor.Red);
        public void PressBlue() => PressColor(PuzzleColor.Blue);
        public void PressYellow() => PressColor(PuzzleColor.Yellow);
        public void PressGreen() => PressColor(PuzzleColor.Green);

        public void PressEnter()
        {
            if (IsSolved || IsBusy) return;
            Play(enterClip);
            Check();
        }

        void Check()
        {
            bool ok = input.Count == solution.Length;
            for (int i = 0; ok && i < solution.Length; i++) ok = input[i] == solution[i];
            if (ok)
            {
                IsSolved = true;
                needleTarget = 1f;
                SetLamp(successColor, true);
                Play(successClip);
                onSolved?.Invoke();
            }
            else StartCoroutine(FailRoutine());
        }

        IEnumerator FailRoutine()
        {
            IsBusy = true;
            Play(failClip);
            onFailed?.Invoke();
            for (int i = 0; i < 3; i++)
            {
                SetLamp(failColor, true); yield return new WaitForSeconds(0.18f);
                SetLamp(idleColor, false); yield return new WaitForSeconds(0.14f);
            }
            input.Clear();
            needleTarget = 0f;
            IsBusy = false;
        }

        /// <summary>퍼즐을 처음 상태로 되돌린다</summary>
        public void ResetPuzzle()
        {
            StopAllCoroutines();
            input.Clear(); IsSolved = false; IsBusy = false; needleTarget = 0f;
            SetLamp(idleColor, false);
        }

        void SetLamp(Color c, bool on)
        {
            if (lampMat != null)
            {
                Color e = on ? c * emissionIntensity : Color.black;
                if (lampMat.HasProperty("_BaseColor")) lampMat.SetColor("_BaseColor", c);
                if (lampMat.HasProperty("_Color")) lampMat.SetColor("_Color", c);
                if (lampMat.HasProperty("_EmissionColor")) lampMat.SetColor("_EmissionColor", e);
                if (lampMat.HasProperty("_EmissiveColor")) lampMat.SetColor("_EmissiveColor", e); // HDRP
            }
            if (lampLight != null) { lampLight.enabled = on; lampLight.color = c; }
        }

        void Play(AudioClip clip)
        {
            if (clip == null) return;
            if (audioSource != null) audioSource.PlayOneShot(clip);
            else AudioSource.PlayClipAtPoint(clip, transform.position);
        }
    }
}

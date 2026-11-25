using System.Collections;
using UnityEngine;

namespace HorrorPuzzle
{
    /// <summary>
    /// 눌리는 버튼 하나. Interact()를 호출하면 눌리는 애니메이션 후 퍼즐에 입력이 전달된다.
    /// PuzzleInteractor(카메라 레이캐스트)나 다른 상호작용 시스템에서 Interact()를 부르면 된다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PuzzleButton : MonoBehaviour
    {
        public enum Kind { Color, Enter }

        public Kind kind = Kind.Color;
        public PuzzleColor color = PuzzleColor.Red;
        [Tooltip("눌리는 깊이 (미터)")]
        public float pressDepth = 0.008f;
        public float pressTime = 0.07f;

        ColorButtonPuzzle puzzle;
        Vector3 restLocalPos;
        bool animating;

        public void Bind(ColorButtonPuzzle p)
        {
            puzzle = p;
            restLocalPos = transform.localPosition;
        }

        void Start()
        {
            if (puzzle == null)
            {
                var p = GetComponentInParent<ColorButtonPuzzle>();
                if (p != null) p.Init();
            }
        }

        public bool CanInteract => puzzle != null && !puzzle.IsSolved && !puzzle.IsBusy;

        public void Interact()
        {
            if (puzzle == null || animating) return;
            StartCoroutine(PressRoutine());
            if (kind == Kind.Enter) puzzle.PressEnter();
            else puzzle.PressColor(color);
        }

        // 마우스로 바로 클릭하는 경우(구 입력 시스템 + 커서 보일 때)
        void OnMouseDown() { if (!PuzzleInteractor.AnyActive) Interact(); }

        IEnumerator PressRoutine()
        {
            animating = true;
            Vector3 dirW = puzzle.PressDirectionWorld;
            Vector3 dirL = transform.parent != null ? transform.parent.InverseTransformDirection(dirW) : dirW;
            Vector3 pressed = restLocalPos + dirL.normalized * pressDepth;
            for (float t = 0; t < 1f; t += Time.deltaTime / pressTime)
            { transform.localPosition = Vector3.Lerp(restLocalPos, pressed, t); yield return null; }
            transform.localPosition = pressed;
            yield return new WaitForSeconds(0.05f);
            for (float t = 0; t < 1f; t += Time.deltaTime / (pressTime * 1.5f))
            { transform.localPosition = Vector3.Lerp(pressed, restLocalPos, t); yield return null; }
            transform.localPosition = restLocalPos;
            animating = false;
        }
    }
}

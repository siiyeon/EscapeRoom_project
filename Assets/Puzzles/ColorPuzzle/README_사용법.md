# 컬러 버튼 퍼즐 (공포게임용) - 유니티 사용법

## 넣는 방법
1. `ColorPuzzle` 폴더를 통째로 유니티 프로젝트의 `Assets/` 아래에 복사한다.
2. 임포트가 끝나면 자동으로 `Assets/ColorPuzzle/ColorPuzzle.prefab`과 `Materials/`가 만들어진다.
   - 자동으로 안 생기면 메뉴 **Tools > Color Puzzle > Rebuild Prefab** 실행.
   - 렌더 파이프라인(Built-in / URP / HDRP)을 자동으로 감지해서 머티리얼을 만든다.
3. `ColorPuzzle.prefab`을 씬의 벽에 배치한다. 프리팹 원점은 박스 뒷면 가운데라 벽면에 딱 붙이면 된다.
   (앞면 = 버튼이 튀어나온 쪽)
4. 플레이어 카메라에 `PuzzleInteractor` 컴포넌트를 붙인다.
   - 1인칭(커서 잠김): 화면 중앙으로 조준 + 좌클릭 또는 E
   - 커서가 보일 때: 마우스로 버튼 클릭
   - 이미 상호작용 시스템이 있으면 `PuzzleInteractor` 대신 그 시스템에서 `PuzzleButton.Interact()`를 호출.

## 퍼즐 설정 (ColorButtonPuzzle 컴포넌트, 프리팹 루트)
- **Solution**: 정답 색 순서 (기본: 빨강 → 파랑 → 노랑 → 초록). 길이 자유.
- **Require Enter**: 켜면 색을 다 누르고 ENTER로 확인, 끄면 개수가 차는 순간 자동 확인.
- **On Solved**: 풀렸을 때 실행할 것 (문 열기, 아이템 활성화 등)을 연결.
- **On Failed**: 틀렸을 때 (귀신 등장 등).
- **On Button Pressed**: 버튼 누를 때마다.
- 사운드: Press / Enter / Success / Fail 클립을 넣으면 재생됨 (비워둬도 동작).

## 동작
- 색 버튼: 눌리는 애니메이션 + 게이지 바늘이 입력 개수만큼 올라감.
- 정답: 표시등 초록으로 켜짐, 더 이상 입력 안 받음.
- 오답: 표시등 빨강 3번 깜빡임 → 입력 초기화, 바늘 0으로.
- `ResetPuzzle()`로 처음 상태로 되돌릴 수 있음.

## 코드에서 쓰기
```csharp
using HorrorPuzzle;
var puzzle = GetComponent<ColorButtonPuzzle>();
puzzle.PressColor(PuzzleColor.Red);   // 버튼 대신 직접 입력
puzzle.PressEnter();
bool solved = puzzle.IsSolved;
puzzle.ColorPressed += c => Debug.Log(c);
```

## 파일 구성
- `Models/ColorPuzzle.fbx` : 모델 (실제 크기, 약 42 x 11 x 22cm + 위쪽 전선관)
- `Models/ColorPuzzle_BaseColor.png`, `ColorPuzzle_MetallicSmoothness.png` : 2048 구운 텍스처 (박스·버튼 전부 한 장)
- `Models/ColorPuzzle_Roughness.png`, `ColorPuzzle_Metallic.png` : 다른 셰이더용 원본
- `Scripts/` : ColorButtonPuzzle, PuzzleButton, PuzzleInteractor (네임스페이스 HorrorPuzzle)
- `Editor/ColorPuzzleSetup.cs` : 프리팹 자동 생성

## 파츠 이름 (FBX 안)
ColorPuzzle_Body, _Bezels, _GaugeFace, _Needle(게이지 바늘), _Button_Red/Blue/Yellow/Green, _Button_Enter,
_Lamp(표시등, 별도 머티리얼), _Conduit(위쪽 전선관, 필요 없으면 프리팹에서 꺼도 됨)

## 원본
`컬러버튼퍼즐.blend` (절차적 원본 재질은 CP_* 이름으로 보존)

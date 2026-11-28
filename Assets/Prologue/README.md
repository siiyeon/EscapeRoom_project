# 프롤로그 컷신 패키지 (붉은 출석부)

게임을 시작하면 가장 먼저 나오는 약 90초짜리 프롤로그 컷신입니다.
검은 화면 타이핑 자막 → 지서 무전 → 빗속 운전 → 폐교 3층 실루엣 → 정문 도착 순서로 진행되고,
끝나면 자동으로 다음 씬(게임 본편)으로 넘어갑니다.

## 설치 (파일만 넣으면 끝)
1. 유니티에서 게임 프로젝트를 엽니다.
2. `Prologue.unitypackage` 를 Project 창에 끌어다 놓거나, Assets > Import Package > Custom Package 로 엽니다.
3. 모든 항목이 체크된 상태로 **Import** 를 누릅니다.
4. 자동 설치가 한 번 실행됩니다. (Console 창에 `[Prologue] 설치 완료` 가 뜨면 끝)
   - Prologue 씬을 **빌드 순서 0번(게임 첫 씬)** 으로 등록합니다. 원래 0번이던 씬은 1번으로 밀립니다.
   - 프롤로그가 끝나면 **빌드 순서 1번 씬**(원래 첫 씬)으로 넘어갑니다.
   - 색 공간을 Linear 로 맞춥니다.
   - 렌더 파이프라인에 맞춰 자동으로 설정합니다. (아래 표)
5. 다시 실행하고 싶으면 메뉴 **Tools > Prologue > Install (첫 씬으로 설정)**

| 게임 프로젝트의 렌더러 | 자동 처리 |
|---|---|
| Built-in (기본) | Post Processing 패키지 자동 설치 후 그대로 사용. 만든 컴퓨터와 똑같이 보입니다. |
| URP | 재질 38개를 URP Lit 으로 변환, 비 파티클 교체, 포스트 프로세싱을 URP Volume 으로 다시 만들고 조명 밝기를 보정합니다. 앰비언트 오클루전은 URP Renderer 설정에서 **SSAO** 를 켜면 같아집니다. |
| HDRP | 자동 변환 없음. 재질이 분홍색으로 보이면 Edit > Rendering > Materials > Convert 를 사용하세요. |

## 다음 씬을 직접 지정하려면
Prologue 씬의 `== PROLOGUE == / PrologueDirector` 오브젝트를 선택하고
Inspector 의 **Next Scene Name** 에 씬 이름을 적으면 그 씬으로 넘어갑니다. (빌드 설정에 그 씬이 있어야 함)

## 들어 있는 것 (Assets/Prologue)
| 폴더 | 내용 |
|---|---|
| Scenes | Prologue.unity (컷신 전체, 조명, 카메라 연출) |
| Scripts | PrologueDirector (장면 진행, 자막 타이핑, 무전 연출, 와이퍼, 번개, 스킵), PrologueAudio (빗소리·천둥·전화벨·무전·와이퍼·엔진·키보드 소리를 코드로 합성 → 소리 파일 없음), RoadPath (차 주행 경로) |
| Models | 순찰차 실내·와이퍼·무전기, 산길 도로, 나무 3종, 전봇대, 지서 방·소품, 손에 든 무전기(HeldRadio) |
| Textures | Poly Haven 실사 텍스처 6종 (CC0, 상업 사용 가능, 출처 표기 불필요) + 플레이어 손 텍스처 |
| Materials | 모든 재질 (색, 반사, 요철 설정 포함) |
| PostFX | 포스트 프로세싱 프로필 (톤매핑 ACES, 색 보정, 블룸, 비네팅, 필름 그레인, AO, 모션 블러) |
| Editor | 자동 설치 스크립트 (게임 빌드에는 포함되지 않음) |

## 조작
- Space 또는 Esc: 프롤로그 건너뛰기 (예전 Input Manager, 새 Input System 둘 다 지원)

## 학교·정문을 게임 모델로 바꿀 때 (Claude 에게 부탁할 때 이 부분을 같이 읽혀 주세요)
- 임시 학교: `== PROLOGUE == / Environment / School_Placeholder`
  - 3층 창가 귀신 실루엣: `School_Placeholder / Ghost_Silhouette` (번개 칠 때 켜짐)
  - 불 켜지는 창문: `School_Placeholder / Win_F3_5` (스크립트가 이름으로 찾음 → 새 학교의 창문 오브젝트 이름을 Win_F3_5 로 하거나 같은 위치에 둘 것)
- 임시 정문: `Environment / SchoolGate`
- 학교를 바꾸면 함께 맞출 카메라 위치
  - `CamPoint_School`: 4번 장면, 학교를 멀리서 보는 카메라 (FOV 30)
  - `CamPoint_Stand`: 마지막 하차 시점 (정문 앞)
  - 차가 멈추는 위치: PrologueDirector 의 `PlaceCar(296f)` / `Drive(255f, 296f, ...)` (RoadPath 위의 거리 값)

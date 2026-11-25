# 다이얼 자물쇠 3종 (공포게임용) - 유니티 작업 안내

## 파일
- DialLock_Number.fbx : 4자리 숫자 다이얼 (각 다이얼 0~9)
- DialLock_Letter.fbx : 4자리 영문 다이얼 (다이얼마다 글자 10개)
- DialLock_Round.fbx  : 원형 회전식 다이얼 (0~39, 가운데 손잡이)
- 자물쇠마다 텍스처 4장 (2048px, 구워둔 것)
  - *_BaseColor.png : Albedo (sRGB)
  - *_MetallicSmoothness.png : 유니티 Standard/URP Lit의 Metallic 슬롯용 (R=Metallic, A=Smoothness). 임포트 시 sRGB 체크 해제
  - *_Roughness.png, *_Metallic.png : 다른 셰이더용 원본
- 자물쇠 하나당 재질 1개만 쓰면 됨 (모든 파츠가 같은 텍스처 아틀라스 공유)
- 노멀맵 없음 (글자, 눈금, 손잡이 홈 모두 실제 지오메트리)

## 크기
실제 크기(미터). 사각형 약 6x2.5x9cm, 원형 약 5x3x9cm.
FBX 내보내기: Apply Scalings = FBX All, Forward -Z, Up Y → 유니티 Scale Factor 1 그대로 사용.

## 계층 구조
DialLock_XXX (빈 오브젝트, 루트)
 ├ _Body, _Marker(빨간 기준 표시), _Axle, _Spacer_N, _Shackle(고리)
 └ _Dial_1 ~ _Dial_4 (사각형) / _Dial (원형)  ← 돌리는 부분

## 다이얼 회전 규칙 (블렌더 기준, 모두 회전값 0 = 첫 글자가 기준 표시 위치)
- 사각형(Number/Letter): 블렌더 로컬 X축 회전. index * 36도 → chars[index]가 빨간 표시 위치에 옴
  - Number 각 다이얼 chars = "0123456789"
  - Letter 다이얼1 "BCDFHLMPST", 2 "AEIOURLHNY", 3 "ADEILNORST", 4 "EDKLNSTYHR"
- 원형(Round): 블렌더 로컬 Y축(앞뒤 축) 회전. 숫자 n이 위쪽 빨간 표시에 오려면 rotation.y = -n * 9도 (40칸, 한 칸 9도)
- 블렌더→유니티 좌표 변환 때문에 유니티에서는 축 이름이나 +/- 방향이 바뀔 수 있음. 유니티에서 한 칸 돌려보고 맞는 축과 부호를 확인할 것.
- 다이얼 오브젝트에 커스텀 속성(chars, step_deg)을 넣어 FBX에 포함함 (Custom Properties 켬). 유니티는 기본으로 버리므로 AssetPostprocessor.OnPostprocessGameObjectWithUserProperties로 읽거나, 위 값을 컴포넌트에 직접 입력.

## 고리 열림 애니메이션
_Shackle의 원점이 왼쪽 다리 축에 있음 → 위로 약 0.012m 올린 뒤 세로축으로 약 90도 회전하면 열림.

## 원본
원본 블렌더 파일: 다이얼자물쇠.blend (절차적 녹 재질은 Lock_* 이름으로 보존, 다시 구울 때 사용)

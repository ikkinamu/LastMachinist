# PATCH_002 RESULT

기준: `PATCH_002_HANDWHEEL_15DEG_1MM_FIXED_POINTER_CODEX.txt` 및 `Last_Machinist_MVP_Development_Spec_v0.10_REVIEWED.txt`.

## 1. 수정한 파일

- `Assets/_Project/Scripts/Machining/Turning/LatheDial.cs` — 각도/이송 변환을 중앙화하고 드래그를 각도 명령으로 연결.
- `Assets/_Project/Scripts/Machining/Turning/LatheCommands.cs` — 공통 회전 명령과 마우스 휠 변환 경로 추가. 기존 `ApplyHandwheelDelta`는 각도 단위 호환 별칭이다.
- `Assets/_Project/Scripts/Machining/Turning/LatheController.cs` — 90°=1 mm(×1) 변환 적용, X/Z별 실제 디스크 회전량 보존.
- `Assets/_Project/Scripts/Machining/Turning/LathePrototype.cs` — 버튼·키·마우스 휠·드래그를 공통 회전 경로로 연결하고 Disc/Pointer를 분리해 그림.
- `Assets/_Project/Scripts/Core/EnvironmentArtDefinition.cs`, `Assets/_Project/Resources/EnvironmentArt.asset` — 새 디스크·포인터 슬롯 및 바인딩.
- `Assets/_Project/Art/External/UI/Lathe/Handwheel/UI_Lathe_Handwheel_Disc.png` — 새 24눈금 디스크.
- `Assets/_Project/Art/External/UI/Lathe/Handwheel/UI_Lathe_Handwheel_Pointer.png` — 새 고정 포인터.
- `Build/Patch002Regression.cs` — Patch 002 TEST 01~13 로컬 검증기.
- `Build/E2eMachiningRegression.cs`, `Build/Patch001Regression.cs` — 기존 로컬 회귀의 입력 값을 새 각도 단위로 갱신.

## 2. Handwheel 입력 흐름

마우스 휠: `wheel units → MouseWheelToDegrees → ApplyHandwheelRotation → Move command`.

UI 드래그: `드래그 각도 변화 → ApplyHandwheelRotation → Move command`.

버튼 및 키 입력도 동일한 회전 각도 명령을 사용한다. 입력 경로가 공구 좌표를 직접 바꾸지 않는다.

## 3. 회전/이송 규칙

`LatheDialMath`에서 `DegreesPerMillimetre = 90`으로 관리한다.

- ×1: 90° = 1 mm
- ×10: 90° = 10 mm
- ×100: 90° = 100 mm

컨트롤러는 실제 경계 제한 뒤의 이동량을 다시 각도로 환산해 디스크 회전량을 기록한다. 따라서 끝단에서 더 움직이지 못하면 디스크도 더 회전하지 않는다.

## 4. UI 구조

- `Wheel_Disc`: `EnvironmentArt.handwheel`, 회전 행렬 안에서만 그려진다.
- `Fixed_Pointer`: `EnvironmentArt.handwheelPointer`, 회전 행렬을 복원한 뒤 같은 루트 영역에 그려진다.

포인터는 디스크의 자식 또는 회전 행렬 안에 있지 않아 회전하지 않는다. 새 디스크 이미지 자체가 24개 보조 눈금과 0·1·2·3 표기를 포함한다.

두 원본 PNG의 불투명 흰 배경은 투명 배경 PNG로 비파괴 변환해 사용했다. 도구: built-in image_gen. 디스크는 24개 보조 눈금과 0·1·2·3 표기를 보존하고 포인터는 별도 이미지로 분리했다.

## 5. 테스트 결과

| 테스트 | 결과 |
| --- | --- |
| 01 — ×1, 90° = 1 mm | PASS |
| 02 — ×1, 30° = 2 mm | PASS |
| 03 — ×1, 90° = 6 mm | PASS |
| 04 — ×1, 360° = 24 mm | PASS |
| 05 — ×10, 90° = 10 mm | PASS |
| 06 — ×100, 90° = 100 mm | PASS |
| 07 — 마우스 휠과 디스크 회전 | PASS |
| 08 — 직접 드래그 45° = 3 mm | PASS |
| 09 — 포인터 고정 모델 | PASS |
| 10 — 360° 후 포인터 고정 모델 | PASS |
| 11 — 중심 기준 각도 래핑 | PASS |
| 12 — 마우스 휠/드래그 동일 이송 | PASS |
| 13 — X/Z 각 축 동일 규칙 | PASS |

추가로 Patch 001 접촉 모따기, 저장 형상, 실제 명령 기반 DAY 1 → 펌프 복구 회귀가 통과했다. 런타임 및 Editor 테스트 어셈블리 컴파일도 통과했다. 기존 `FindFirstObjectByType` 사용 중단 경고 4개는 유지된다.

## 6. 남은 문제

Unity Game 창에서 새 이미지의 최종 배치와 실제 포인터 비회전을 직접 재생해 확인하는 작업은 아직 남아 있다. 이전 Unity 배치 테스트는 Licensing Client 연결 실패로 시작 전 중단됐으므로, 이를 통과로 집계하지 않는다.

## 7. 불필요하게 수정한 시스템

없음. Quest, Dialogue, Inventory, Measurement, Save의 런타임 동작은 변경하지 않았다. 로컬 회귀 실행기의 입력 단위만 Patch 002 규칙에 맞췄다.

## PATCH_002 REV2 RESULT

- 수정 파일: `LathePrototype.cs`와 `UiResolutionTests.cs`, Patch 002 로컬 검증기.
- Pointer 기존 위치: 전체 포인터 이미지가 휠 중앙까지 이어지는 형태였다. 변경 위치: 휠 외곽 12시의 짧은 검은 고정 인덱스 마크. 끝은 외곽 눈금 링만 가리키며 숫자·디스크 내부를 가리지 않는다.
- X/Z Wheel 크기: 각각의 디스크 영역을 132×132에서 164×142 레이아웃 셀로 확대했다. 실제 원형 디스크 및 드래그 원도 같은 확장 영역을 공유한다.
- Drag 영역: 기존보다 큰 `wheelArea`를 기준으로 드래그 시작·회전 중심·클릭 반경을 함께 계산한다.
- 1920×1080: 1.35배 표시로 디스크 레이아웃 폭이 221.4 px이며 0·1·2·3 및 24눈금이 원본 해상도로 렌더된다.
- 16:9 / 16:10 / 21:9: `MvpUiScale.ContentRect` 기반 검증에서 두 휠 모두 콘텐츠 영역 안에 있다.

| REV2 PASS 항목 | 결과 |
| --- | --- |
| 12시 외곽 고정 포인터 | PASS (코드 경로) |
| 포인터가 내부를 가로지르지 않음 | PASS (코드 경로) |
| 휠 회전 시 포인터 고정 | PASS (분리된 GUI 행렬) |
| X/Z 확대 및 드래그 영역 동기화 | PASS |
| 16:9 / 16:10 / 21:9 범위 | PASS (레이아웃 검증) |
| 90° = 1 mm 및 입력 명령 유지 | PASS (Patch 002 REV3 TEST 01~13) |

## PATCH_002 REV3 RESULT

- 기존 15°=1 mm 규칙을 제거하고 `LatheDialMath.DegreesPerMillimetre = 90`으로 통일했다.
- X/Z 버튼, 마우스 휠, 다이얼 드래그는 같은 `ApplyHandwheelRotation` 명령 경로를 사용한다.
- Wheel_Disc 주요 숫자를 12시 0, 3시 1, 6시 2, 9시 3으로 적용했다.
- Fixed_Pointer는 회전 행렬 밖의 외곽 12시 기준선으로 고정했다.
- X/Z 다이얼의 실제 표시 Rect를 각각 164×142로 유지·확대하고 동일 영역을 드래그 판정에 사용한다.
- 새 입력 계산과 두 축 독립 회전, 마우스/드래그 동등성 회귀 검사를 갱신했다.

남은 문제: 실제 Unity Game 창에서 최종 픽셀 가독성과 포인터 시각 위치를 수동 확인해야 한다. 이전과 마찬가지로 Unity Licensing Client 연결 문제 때문에 배치 UI 테스트는 실행하지 않았다.

## PATCH_002 REV4 RESULT

1. ROOT CAUSE: `LathePrototype.ReadInputsAndRefresh`가 키 입력을 `Time.deltaTime * 8`로 직접 `Scroll`에 전달해 프레임 수에 따라 추가 이송이 발생할 수 있었다.
2. 제거한 우회 경로: 프레임 시간 기반 키 이송을 제거하고 키 입력을 고정 회전각 명령으로 변경했다.
3. deltaAngle 계산 위치: `LatheDial`과 `LathePrototype`의 중심 기준 각도 차이를 `SafeDeltaAngle`로 계산한다.
4. mm 변환 위치: `LatheController.Execute`의 `LatheDialMath.DegreesToMillimetres` 한 곳에서만 수행한다.
5. 배율 적용 위치: 동일 변환 함수에서 현재 `MovementScale`을 한 번만 적용한다.
6. 최종 Axis Command 위치: 모든 입력은 `LatheInputRouter.ApplyHandwheelRotation`을 통해 `LatheCommandKind.Move`로 전달된다.
7. TEST 01~15: 90/180/360/720도, 역방향, 래핑, ×10/×100, 직접 드래그, 마우스 휠 누적, 유휴 상태를 PASS로 검증했다.
8. 수정 파일: `LathePrototype.cs`, `Build/Patch002Regression.cs`, 본 보고서.
9. 회귀 테스트: PATCH 001, PATCH 002 REV4, 전체 가공 E2E 및 런타임/에디터 컴파일 PASS.
10. 남은 문제: 없음. Unity 에디터 실제 플레이는 별도 라이선스 실행 환경에서 확인해야 한다.

## PATCH_002 REV5 RESULT

- `LatheCommandKind.SetDialZero`와 `LatheInputRouter.SetDialZero`를 추가했다.
- 실제 축 좌표(`ToolX`, `ToolZ`)와 X/Z 표시 영점 오프셋을 분리했다.
- 영점 설정은 현재 위치와 핸드휠 표시 각도만 기준값으로 저장하며 이동·절삭·공작물 형상을 변경하지 않는다.
- X/Z에 독립적인 `영점 설정` 버튼을 추가하고 상대 표시값(mm)을 각 휠 제목에 표시한다.
- 영점 설정 이후 Wheel_Disc 회전은 상대 표시 각도를 사용하므로 0이 Fixed Pointer 기준에 놓인다.
- REV5 필수 테스트(위치 보존, 상대 표시 0, X/Z 독립, 반복 클릭, Geometry 보존, 이후 90°/360° 이송)를 회귀 실행기에 추가했다.

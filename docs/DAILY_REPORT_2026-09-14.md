# Last Machinist 일일 개발 보고서

작성일: 2026-09-14  
기준: `Last_Machinist_MVP_Development_Spec_v0.6.txt`, `AGENTS.md`  
범위: 기존 MVP/PHASE 8 통합 보강

## 오늘 반영한 내용

### 측정

- `TrueDimension`, `InstrumentReading`, `RecordedMeasurement`를 분리한 구조를 유지하고, 측정 위치 선택 → 기기 눈금 갱신 → 플레이어 직접 입력 → 기록 흐름을 정리했다.
- `VernierDisplayModel`과 `VernierView`를 추가했다. 본척·부척·죠 위치가 `InstrumentReading`에 따라 바뀐다.
- 측정 입력칸 초기값을 빈칸으로 변경했다. 측정 위치를 선택해도 정답이 자동 입력되지 않는다.
- 잘못 입력한 유한 값은 그대로 저장하며 실제 Geometry에는 반영하지 않는다.
- 측정 위치별 강조, 다시 측정, 기록 목록, 가공 복귀를 연결했다.
- 고장 축 L3 끝부분의 직경 마모와 2.5 mm 축방향 마모를 표현했다. L3 길이 측정은 현재 남은 재료를 기준으로 계산된다.

### 화면·연출

- `DialoguePage`를 추가해 시작 선택지와 NPC 대화를 화면 하단 약 33% 영역에 배치했다.
- 대화 중 선반 입력을 잠그고 대화 종료 후 이전 가공 상태를 복귀한다.
- 작업장에 작업대, 공구 보관대, 소재 보관대, 김영호 기록 공간, 선반 주변 요소를 추가했다.
- 농장에 작물 구역, 수조, 배관, 물 공급 맥락을 추가했다.
- 펌프에 모터 핀, 커플링, 배관 연결, 고정 요소를 보강했다.
- 의뢰·아이템 획득 알림을 일시적 Toast로 표시하고, 상세 의뢰 내용은 별도 페이지에서 열도록 했다.
- 선반의 네 고정 시점이 갱신 시에도 실제 Orthographic Projection을 유지하도록 했다.
- 측정 중에는 주축·공구 이동 입력을 차단하고, 카메라 휠 줌과 핸드휠 입력이 충돌하지 않도록 했다.
- 새 UI 문자열과 핸들 안내를 한국어로 정리했다.

## 코드 및 문서 변경

- 추가: `Assets/_Project/Scripts/Core/DialoguePage.cs`
- 추가: `Assets/_Project/Scripts/Measurement/VernierView.cs`
- 추가: `Assets/_Project/Tests/Editor/V04IntegrationTests.cs`
- 수정: `Assets/_Project/Scripts/Core/MvpWorldPanel.cs`
- 수정: `Assets/_Project/Scripts/Core/MvpStartupOverlay.cs`
- 수정: `Assets/_Project/Scripts/Machining/Turning/LathePrototype.cs`
- 수정: `Assets/_Project/Scripts/Measurement/MeasurementModels.cs`
- 수정: `Assets/_Project/Scripts/Integration/Quest01Integration.cs`
- 수정: `Assets/_Project/Scripts/Machining/Turning/LatheCommands.cs`
- 수정: `Assets/_Project/Scripts/Save/SaveGameStore.cs`
- 수정: `Assets/_Project/Tests/Editor/BootPlayModeTests.cs`
- 추가: [`V04_IMPLEMENTATION_STATUS.md`](V04_IMPLEMENTATION_STATUS.md)

## 오늘 확인된 결과

- Unity 6000.6.0f1 EditMode 테스트: **79 통과 / 0 실패 / 0 건너뜀**
  - 근거: `Build/v04-validation/unity-results.xml`
- Unity 런타임 및 Editor 테스트 어셈블리 컴파일: 오류 없음
  - 신규 `DialoguePage`, `VernierView`, `V04IntegrationTests` 포함
- 기존 도메인 회귀 검증 5종: 모두 통과
  - 퀘스트 게이트·대화
  - 측정 기록 분리
  - 저장·복원
  - 챔퍼·재가공
  - DAY 1 전체 가공 및 펌프 복구
- 개발용 Windows 실행 파일 빌드: 성공

## 직접 확인이 필요한 사항

다음은 코드 테스트만으로 완료 판정할 수 없다.

- 실제 Unity 창에서 시작 화면 → 대화 → 작업장 → 농장 전환이 자연스럽게 보이는지
- 대화창이 항상 하단 30~35%에 있고 NPC와 배경을 가리지 않는지
- 작업장 선반·작업대·공구·소재·기록 공간이 서로 구분되는지
- 농장이 펌프만 있는 화면이 아니라 농업 시설과 물 공급 문제로 읽히는지
- 펌프 고장 전후의 회전, 물 흐름, 소리 변화가 플레이어에게 명확한지
- 버니어 본척·부척·죠가 확대 화면에서 실제로 읽히는지
- 측정 위치 강조와 L3 마모 형상이 화면에서 이해되는지
- 네 카메라 시점에서 공작물과 공구가 가려지지 않는지
- 저장/불러오기 버튼을 실제 UI로 사용했을 때 진행도·측정 기록·방향·펌프 상태가 유지되는지
- 일반 빌드에서 Debug 수치와 개발 패널이 노출되지 않는지
- 한글 줄바꿈과 버튼 폭이 1280×800 외 해상도에서도 깨지지 않는지

## 현재 완료 판정

v0.4의 측정·입력·상태·저장 연동은 컴파일 및 자동 테스트 근거가 있다. 환경과 UI는 명세에 맞는 구조와 임시 표현을 반영했지만, 실제 창에서의 시각 가독성·배치·음향 품질은 아직 확인 전이다. 따라서 오늘 기준으로는 **v0.4 통합 구현 진행 중**이며, 전체 MVP 완성으로 보고하지 않는다.
## v0.6 반영 (17:50 KST)

- 외부 이미지 루트를 `Assets/_Project/Art/External/` 아래에 만들고, NPC별 `Characters/NPC/<Id>/Dialogue`·`World` 구조와 Background/Props/UI/Temporary 하위 폴더를 추가했다.
- 사용자가 제공한 `NPC_Seongcheol_Dialogue_Default.png`, `NPC_Seoyeon_Dialogue_Default.png`를 규칙에 맞는 Dialogue 폴더에 복사하고 Boot 씬의 `GameBootstrap`에 연결했다.
- `CharacterIllustrationBinding`/`CharacterIllustrationResolver`를 추가해 대화 데이터는 캐릭터 ID만 전달하고 이미지 교체는 씬 바인딩으로 처리하도록 분리했다. 대화 일러스트는 하단 대화창과 분리된 상단 캐릭터 레이어에서 Preserve Aspect로 표시된다.
- 최초 타이틀에서 `멈춰버린 펌프`를 제거했다. Quest가 활성화된 뒤에만 시작된 QuestTracker와 의뢰 Toast가 표시되도록 조정했다.

검증: Unity 런타임 어셈블리를 Unity 6 컴파일러 응답 파일로 재컴파일해 오류가 없음을 확인했다. 기존 Unity EditMode 결과는 79 PASS/0 FAIL이며, 이번 v0.6 변경 후 전체 Unity 실행 검증은 실행 환경 제한으로 재실행하지 못했다.

확인 필요: 실제 Unity 실행 화면에서 두 NPC PNG가 투명 배경과 원본 비율로 표시되는지, 타이틀→DAY 1→박성철 대화→이서연 등장→QuestPopup 순서가 유지되는지 확인해야 한다. World용 NPC 이미지는 아직 제공되지 않아 현재 월드에는 기존 Placeholder가 남아 있다.

## NPC 이미지 표시 보정

- 임의로 그리던 도형 NPC(`DialoguePage.DrawCharacter`)와 호출부를 제거했다.
- 두 PNG를 RGBA Sprite로 유지하고 Bilinear 필터·원본 비율 표시를 적용했다.
- 런타임/Editor 컴파일을 다시 통과했다.

## 해상도 대응 보정

- 1280×800 기준 UI의 가로·세로를 별도 배율로 늘리던 방식을 제거했다.
- 화면에 맞는 균일 배율과 중앙 레터박스를 적용해 NPC 일러스트와 대화창이 16:9·16:10·4:3 해상도에서 찌그러지지 않도록 했다.

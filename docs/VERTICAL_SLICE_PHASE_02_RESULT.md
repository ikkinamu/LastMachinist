# VERTICAL SLICE PHASE 02 RESULT

2026-09-25 · Item Inspection & Recovery v0.1

후속 검증 업데이트: 아래에 기록한 전체 테스트 실패 14개는 원인 분석 후 해결했다. 최신 전체 결과는 **110/110 PASS**다. 상세 원인과 수정은 `REGRESSION_REPAIR_2026-09-25.md` 참조. 아래 최초 검증 기록은 이력으로 보존한다. 실제 화면/장치 수동 확인 범위는 동일하다.

## AUDIT와 범위

기존 탐색은 FirstPersonExplorer의 InputActionMap → InteractRequested 이벤트 구조다. UI는 OnGUI/IMGUI이며 Canvas/EventSystem 기반이 아니다. 기존 공통 Inspectable/Interactable 구조는 없어 작은 데이터 바인딩 컴포넌트를 추가했다. 기존 이벤트를 재사용하며 중복 입력 Manager나 Inventory 시스템을 만들지 않았다. 사용자 지정 Phase 02만 구현한다.

## 조사와 데이터

First Person Camera의 Viewport 중심에서 Ray를 발사한다. 첫 Collider가 벽/일반 물체라면 조사하지 않으므로 벽 뒤 물체를 찾는 RaycastAll 방식이 아니다. Trigger는 제외한다. 범위는 TestYard 지역 에셋의 InteractionRange(기본 3 Unity 단위)에서 조정한다.

InspectableItemDefinition은 ItemId, DisplayName, InspectionProperties, CanRecover, RecoveryId를 보유한다. ActualMaterialType/ActualDimensions는 내부 데이터로 별도 보관하며 UI에서 읽지 않는다. 특성 창은 DisplayName과 PropertyLabel/PropertyValue 배열만 읽는다. 특성 개수는 고정하지 않고 스크롤 목록으로 표시한다.

InspectableItem은 월드 Collider가 있는 GameObject와 데이터 에셋을 연결한다. 이번 테스트 배치의 ItemId는 월드 아이템별로 고유하다. 중복 ID를 넣으면 같은 아이템으로 취급하므로, 추가 배치에도 고유 ID를 사용해야 한다.

## 창과 회수

좌클릭은 조사만 한다. 회수 가능한 대상에서 [회수]를 눌렀을 때만 세션 HashSet에 ItemId를 기록하고 해당 월드 아이템을 비활성화한다. 현재 대상/활성 상태/CanRecover/중복 여부를 다시 확인한다. [닫기]나 Esc는 대상을 변경하지 않고 탐색으로 돌아간다.

InspectionOpen 동안 이동·중력 이동·Mouse Look·월드 상호작용을 차단하고 커서를 해제한다. 창이 닫힌 프레임도 상호작용을 차단해 UI 클릭이 월드 조사로 전달되지 않도록 한다. 포커스를 잃어 탐색 메뉴도 열린 경우에는 특성 창을 닫아도 메뉴를 유지한다.

탐색 진입마다 순수 C# ExplorationRecoveryState에 새 회수 목록을 만들며 작업장 영구 인벤토리나 저장 데이터에는 기록하지 않는다. 재진입하면 테스트 아이템이 다시 나타난다. 화면 왼쪽 위에는 이번 탐색의 회수 개수만 표시한다.

## 테스트 배치와 조작

작업장 → 탐색 → 지역 선택 → 탐색 시작. 시작 지점 앞쪽 좌우에 녹슨 금속 봉(4개 특성), 긁힌 금속 판(2개 특성)을 배치했다. 가까이 가서 물체를 화면 중앙에 두고 좌클릭한다. 바닥 쪽 물체이므로 시선을 아래로 내려야 한다. 모두 Greybox이며 외관은 최종 아트가 아니다.

## 파일

신규:
- Scripts/Core/InspectableItemDefinition.cs, InspectableItem.cs, ExplorationInspection.cs, ExplorationRecoveryState.cs 및 meta
- Resources/ExplorationRegions/RustyRod.asset, WornPlate.asset 및 meta
- Tests/Editor/ExplorationInspectionTests.cs 및 meta
- 본 보고서

수정(위 경로는 모두 Assets/_Project 기준):
- Scripts/Core/ExplorationRegionDefinition.cs: 범위/아이템 배치 데이터
- Scripts/Core/FirstPersonExplorer.cs: 조사 모달 입력 상태/클릭 차단
- Scripts/Core/ExplorationSession.cs: 데이터 기반 아이템 배치/조사 창 연결
- Resources/ExplorationRegions/TestYard.asset: 아이템 2개와 범위
- Tests/Editor/ExplorationFoundationTests.cs: 메뉴 중 상호작용 차단 정책에 맞춰 메뉴 종료 후 이벤트 검사

## 검증

Unity 6000.6.0f1에서 별도 프로젝트 복사본으로 검증했다. 원본에서 열려 있는 Scene은 건드리지 않았다. 런타임/테스트 컴파일 통과. Phase 01 + Phase 02 집중 테스트 **4/4 PASS** (`Build/phase02-focused-results.xml`).

배치 실행은 Game 창 포커스/커서 잠금을 유지하지 못해 최초 테스트에서 탐색 메뉴가 자동으로 열렸다. 배치 환경에서만 포커스 상실에 따른 자동 메뉴 열림을 제외하고 재검증했다. 일반 실행의 포커스 상실 처리는 유지한다.

| 항목 | 결과/검증 범위 |
| --- | --- |
| TEST 01 조사만 수행 | PASS: 실제 Collider/Ray + 좌클릭이 호출하는 이벤트 경로, 월드 아이템 유지 |
| TEST 02 빈 공간 | PASS: 위쪽 빈 공간 Ray에 창 열림 없음 |
| TEST 03 범위 밖 | PASS: 범위 밖 배치에서 조사 안 됨 |
| TEST 04 벽 차폐 | PASS: 벽 Collider가 먼저 맞으면 조사 안 됨 |
| TEST 05 표시 정보 | 데이터 바인딩/화면 코드 확인: 관찰 배열만 표시. 실제 한국어 렌더링 수동 확인 필요 |
| TEST 06 입력 잠금 | PASS: 창이 열린 동안 이동/Look 처리 호출에도 위치/회전 불변, 커서 표시/해제 |
| TEST 07 닫기 | PASS: 대상 유지, 모달 해제 |
| TEST 08 재조사 | PASS: 닫은 대상 다시 조사 |
| TEST 09 회수 | PASS: 비활성화 + ItemId 기록 + 모달 해제 |
| TEST 10 중복 회수 | PASS: 중복/빈 ID 거부, CanRecover=false 거부 |
| TEST 11 다른 특성 개수 | PASS: 4개/2개 데이터에 같은 조사 경로 및 회수 사용. 시각적 배치는 수동 확인 필요 |
| TEST 12 클릭 충돌 | PASS: 모달 중/닫은 프레임에 world 이벤트 차단. 실제 버튼 포인터 조작은 수동 확인 필요 |
| TEST 13 복귀 | PASS: Workshop 활성/공작물 참조 유지, 새 세션 회수 목록 초기화. 전체 기존 기능 결과는 별도 기재 |

자동 테스트는 실제 장치 대신 동일 이벤트와 처리 메서드를 호출한다. OnGUI 창이 열린 상태의 프레임도 실행했지만 픽셀/실제 마우스 클릭을 검증한 것은 아니다. 따라서 위 결과를 모든 수동 인수 테스트 완료로 해석하지 않는다.

## 기존 문제와 제한

이전 작업에서 Boot 통합 테스트의 최종 평가가 Perfect 예상과 달리 Reworkable로 나오는 실패가 이미 확인됐다. 이번 변경은 가공/측정/퀘스트 로직을 수정하지 않는다. 영구 인벤토리, 재료 판별, 측정 도구, NPC/전투, QUEST 02 및 최종 아트는 범위 밖이다.

최종 전체 회귀: **102개 중 88 PASS / 14 FAIL** (`Build/phase02-final-results.xml`, `Build/phase02-final-unity.log`). 새 탐색/조사 테스트는 모두 통과했다. 남은 실패는 다음과 같다.

| 기존 테스트 그룹 | 실패 수 | 내용 |
| --- | ---: | --- |
| BootPlayModeTests | 1 | 최종 품질 Perfect / Reworkable 불일치(이전 작업에서도 재현) |
| LatheTests | 4 | 이송량/절삭 후 반경 예상 불일치 |
| Phase3Tests | 6 | 모따기 접촉/깊이/직렬화/재클램핑 예상 불일치 |
| Phase5Tests | 1 | Acceptable / Reworkable 품질 판정 불일치 |
| Phase7Tests | 1 | Perfect / Reworkable 펌프 통합 판정 불일치 |
| V04IntegrationTests | 1 | 대화/측정 영역 겹침 예상 불일치 |

실패를 숨기기 위해 기존 가공/품질 판정이나 테스트 기대값을 바꾸지 않았다. Boot 외 나머지 실패 전체에 대해 Phase 02 이전 동일 스냅샷 대조까지 수행한 것은 아니므로, 모두 기존 결함이라고 단정하지 않는다. 전체 회귀 없음/Console Error 0/명세의 최종 PASS는 아직 선언할 수 없다. Phase 02 기능 검증과 별개로 위 회귀 실패 및 실제 화면 조작 확인이 남아 있다.

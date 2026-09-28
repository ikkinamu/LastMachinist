# PATCH_003 REV3 RESULT

> 최신 적용: 아래 REV4 결과가 작업 준비, 메모, 닫기 배치에 관한 REV3 내용을 대체한다.

작성일: 2026-09-24

## UI audit 및 범위

현재 UI는 Canvas/RectTransform 기반 프리팹이 아니라 IMGUI 및 GUILayout이다. MvpUiScale이 1280×800 작성 좌표를 화면에 비례 배치한다. 따라서 프리팹 전면 교체 없이 같은 작성 좌표에 공통 컨테이너 규칙을 적용했다. 새 단계나 제외 시스템은 추가하지 않았다. 최신 사용자 지정 PATCH_003은 기존 자동 시작 흐름을 명시적으로 변경하는 요청이며, 기존 계산/데이터 소유권 규칙과 충돌하지 않는다.

- 선반: X/Z 조작을 동일 DrawAxisControls 그룹에 배치. 기존 DrawDial의 영점 버튼 상대 위치와 휠 크기 유지.
- 공구: 한 세로 컨테이너에 장착/선택 버튼 배치, 공통 간격 적용.
- 카메라: 정면/평면/우측/등각 순서의 공통 ButtonRow, 동일 간격.
- 측정/메모/인벤토리/결과/장비/퀘스트: 공통 페이지 우측 상단 닫기, 작업장으로 복귀. 기존 콜백·단축키 유지.
- 대화: 기존 스토리/선택지 유지, 다음 버튼은 공통 하단 Actions 컨테이너에 정렬.
- 일시정지: 기존 선반 사이드바 버튼 유지. 별도 설정/확인·취소 모달은 현재 없으므로 새로 만들지 않음.
- Margin 16, Gap 8, 기본 버튼 높이 42. MvpPanelLayout에 페이지/닫기/목록/상세/수락 위치를 중앙화.

## 퀘스트 시작과 저장

기존 CompleteIntro → TryAdvance(FarmerArrived) 자동 시작 경로를 CompleteIntro → Offer()로 변경했다. 기존 서연 도착 대화는 유지하며 별도 제안 팝업은 없다.

상태 의미: NotStarted=미제안, Offered=수락 가능, FarmerArrived~Installing=Active, Completed=완료. Offered는 enum 마지막에 추가하여 기존 저장된 상태 숫자를 바꾸지 않았다. 기존 진행 저장은 그대로 Active로 복원된다. 새 Offered 상태도 기존 QuestRuntimeState/SaveGameData로 저장되며 자동 체크포인트에 포함된다.

기존 퀘스트 창 목록에서 의뢰 선택 → 상세 내용/상태 확인 → 하단 고정 영역의 수락 버튼 → QuestStateMachine.Accept(). 선택/닫기는 UI 상태만 변경하고 의뢰 상태에는 영향을 주지 않는다. Offer/Accept는 상태 검사 후에만 이벤트를 발생시키므로 반복 요청은 무시한다.

CanAdvance의 IsActive 검사로 미수락 단계의 모든 목표 전이를 차단한다. 기존 측정·소재·회수·설치 관련 동작의 단계별 guard도 유지한다. Offered의 CurrentObjective는 비어 있고, Tracker는 IsActive일 때만 표시한다. 기존 퀘스트 내용과 완료 조건은 유지했다.

## 검증

| 항목 | 결과 및 검증 범위 |
| --- | --- |
| Q01 | PASS: 시작 대화 완료 후 Offered, IsActive=false |
| Q02 | PASS(상태): Offered에서 현재 목표 없음. 목록 선택은 UI bool만 변경 |
| Q03 | PASS: Accept 후 FarmerArrived 및 Active, 이벤트 1회 |
| Q04 | PASS: 수락 전 농장/조사/회수/버니어/가공/검사/설치 진행 거부 |
| Q05 | PASS: 수락 후 같은 농장 이동·조사 정상 진행 |
| Q06 | PASS(저장): JSON 직렬화/복원 후 Offered 유지. 닫기/재열기 실제 클릭은 미검증 |
| Q07 | PASS: Active 재제안/재수락 거부, 이벤트 중복 없음 |
| Q08 | PASS: Completed 재제안/재수락 거부 |
| Q09 | PASS: 수락 → 측정 기록 → 양면 가공 → 모따기 → 형상 판정 → 설치/펌프 복구 |

1920×1080, 1920×1200, 2560×1080에서 목록/상세/수락/닫기 영역의 화면 이탈 및 겹침 계산 검증 PASS. 실제 Unity 렌더링에서 텍스트 잘림, 클릭 영역, 카메라 전환은 별도 플레이 확인 필요.

PATCH001 A–H 및 PATCH002 REV4/REV5 회귀 실행 PASS. 실제 형상과 플레이어 기록 분리, 소재 소모, 펌프 복구 E2E PASS. 런타임과 Editor 테스트 어셈블리 컴파일 PASS. 기존 FindFirstObjectByType 사용 중단 경고 4건. Unity Test Runner 실행과 Game 창 수동 검증은 이번 작업에서 수행하지 않았다.

## 파일

Scene/Prefab 수정 없음. 이미지 수정 없음. 새 플레이스홀더 없음.

수정 스크립트:
- Assets/_Project/Scripts/Quest/QuestAndDialogue.cs
- Assets/_Project/Scripts/Core/MvpUiScale.cs
- Assets/_Project/Scripts/Core/MvpStartupOverlay.cs
- Assets/_Project/Scripts/Core/MvpWorldPanel.cs
- Assets/_Project/Scripts/Core/DialoguePage.cs
- Assets/_Project/Scripts/Machining/Turning/LathePrototype.cs
- Assets/_Project/Scripts/Save/SaveGameComposer.cs

수정 테스트:
- Assets/_Project/Tests/Editor/Phase6Tests.cs
- Assets/_Project/Tests/Editor/Phase7Tests.cs
- Assets/_Project/Tests/Editor/V03QuestFlowTests.cs
- Assets/_Project/Tests/Editor/BootPlayModeTests.cs
- Assets/_Project/Tests/Editor/UiResolutionTests.cs
- Build/E2eMachiningRegression.cs (로컬 회귀 실행기)

신규 문서: 본 보고서.

남은 확인: Unity Game 창에서 의뢰 선택 → 닫기 → 재열기 → 수락, 해상도별 글자/버튼 가독성, 측정 및 카메라 직접 조작. 구현 및 자동 검증 완료와 실제 플레이 확인을 구분한다.

## PATCH_003 REV4 RESULT (2026-09-24)

### 작업 준비

이전: 작업장에서 탄소강 선택 → 장비 선택. 변경: 작업 준비 → 선반 선택 → 탄소강 선택 → 선택 내용 확정/1개 소모 → 가공 진입. 기존 미구현 밀링/드릴링 버튼은 비활성 표시 그대로 유지하며 새 가공 방식이나 재료는 추가하지 않았다.

QuestRuntimeState.TurningSelected에 방식 선택 상태를 저장한다. QuestStateMachine.SelectTurning은 ShapeInferred에서만 허용된다. SelectMaterial과 SelectStock에서 방식 미선택을 거부한다. UI의 재료 선택은 임시 선택이며 최종 확정 전에는 재고를 소모하지 않는다. 기존 재가공/소재 재시도 경로는 유지한다. 이전 저장의 MaterialSelected 이후 단계는 재선택을 강요하지 않는다.

### 형상 추론

WorkMemoState.BindInferenceSource가 TurningProfile의 Segments/Chamfers/LinearCuts를 읽어 구간/모따기/경사 탭을 생성한다. 현재 데이터에는 구간 이름이 없어 순서 기반 한국어 이름을 사용한다. QUEST 01 전용 고정 L1/L2/L3 탭은 없다. 펌프의 손상된 형상은 4개의 Segment를 포함하므로 4개 구간 탭이 생성된다.

PlayerShapeInference에는 대상 ID·구간 키·표시 이름과 플레이어의 직경/길이/메모 문자열만 저장한다. 실제 치수는 복사하지 않는다. 측정 데이터와 별개이며 잘못된 추론도 그대로 유지된다. 탭 전환 및 다시 바인딩할 때 같은 키의 기록을 재사용한다. 원형봉으로 교체해도 기존 추론 기록은 보존한다. 기존 저장에 추론 탭 데이터가 없으면 복원된 현재 공작물 구조에서 빈 탭을 생성한다. 기존 메모 화면의 고정 정답 치수 표시는 제거했다. 기존 형상 추론 완료/퀘스트 판정 조건은 유지한다.

### Save / Load / Close

MvpPanelLayout.SessionActions(990,15,274,30)을 3개 동일 너비 셀로 분할한다. 순서는 저장/불러오기/닫기, 간격 8, 높이 30으로 통일했다. Tracker를 왼쪽으로 옮겨 겹침을 피했다. LathePrototype의 기존 독립 닫기 버튼을 제거하고 MvpStartupOverlay에서 기존 OpenPage(Workshop)를 호출한다. 이미 작업장이거나 대화 중/농장 화면이면 닫기만 비활성화한다. 저장·불러오기 콜백은 변경하지 않았다.

### TEST 01~12

| 테스트 | 결과 |
| --- | --- |
| 01 방식 우선 표시 | PASS(코드 검토): 작업 준비의 첫 단계는 방식 선택 |
| 02 미선택 시 재료 차단 | PASS(실행): 재료 선택 실패, 재고 3개 유지 |
| 03 선택 후 재료 활성 | PASS(실행): SelectTurning 후 재료 선택 성공 |
| 04 기존 가공 진입 | PASS(실행): 방식/재료/가공 후 펌프 복구 E2E |
| 05 펌프 구성 탭 | PASS(실행): 원본 Segment 수와 동일 |
| 06 다른 형상 | PASS(실행): 1구간+1모따기 입력에서 2개 생성 |
| 07 입력 유지 | PASS(실행): 재바인딩 및 JSON 저장/복원 후 999 유지 |
| 08 정답 노출 방지 | PASS(실행/검토): 새 입력은 빈 문자열, 실제 치수 자동 복사 없음 |
| 09 그룹 순서 | PASS(코드/영역): 동일 컨테이너의 0/1/2 셀, 실제 렌더링 미확인 |
| 10 닫기 | 기존 콜백 연결 검토 PASS; Unity 실제 클릭 미실행 |
| 11 저장/불러오기 | 선택/추론 기록 JSON 복원 PASS; Unity 버튼 실제 클릭 미실행 |
| 12 중복 닫기 | PASS(코드 검색): 독립 버튼 제거, 공통 그룹에만 1개 |

자동 회귀: PATCH001, PATCH002 REV4/REV5, PATCH003 수락 guard/저장/E2E 모두 PASS. 1920×1080, 1920×1200, 2560×1080 영역 범위 검증 PASS. 런타임/Editor 테스트 어셈블리 컴파일 PASS(기존 API 경고 4개). Unity Test Runner는 실행하지 않았다.

수정 파일: QuestAndDialogue.cs, Quest01Integration.cs, LathePrototype.cs, MvpWorldPanel.cs, MvpStartupOverlay.cs, MvpUiScale.cs, Phase6Tests.cs, Phase7Tests.cs, BootPlayModeTests.cs, UiResolutionTests.cs, Build/E2eMachiningRegression.cs, 본 보고서. Scene/Prefab/이미지 변경 및 신규 플레이스홀더 없음.

남은 확인: Unity Game 창에서 재료 선택/확정, 탭 전환, 저장·불러오기·닫기 실제 클릭 및 글자 잘림 확인. 특히 이전 형상 추론 데이터가 없는 가공 중 저장은 현재 공작물 기반 빈 추론 탭으로 복원되는 정책을 적용했다.

# Phase 05 REV3 / Phase 06 REV1 결과

작성일: 2026-09-28

적용 명세:
- `Last_Machinist_VERTICAL_SLICE_PHASE_05_REPEATABLE_MACHINING_QUESTS_REV3.txt`
- `Last_Machinist_VERTICAL_SLICE_PHASE_06_TAILSTOCK_DRILL_INTERNAL_UI_REV1.txt`

## 적용 순서와 AUDIT

짧은 의뢰를 긴 보유 소재에서 제작하려면 실제 절단이 필요하다. 따라서 Phase 06 REV1 절단/분할을 먼저 구현해 155개 회귀 테스트를 통과한 뒤, Phase 05 REV3의 짧은 규격과 절단→재장착→납품 흐름을 연결했다. 다음 Phase는 시작하지 않았다.

기존 IMGUI, LatheInputRouter/LatheCommand, InventoryMachiningSession, WorkpieceState, PartEvaluator와 저장 DTO를 확장했다. Canvas/Scene/Build Settings를 새로 만들거나 변경하지 않았다. 기존 외경/내경 계산과 측정 기록의 분리 원칙을 유지한다.

## Phase 05 REV3 — 소형 의뢰

| 템플릿 | 새 A3 전체 길이 | 새 B3 전체 길이 |
|---|---:|---:|
| 마모된 펌프 보조축 | 100 mm | 120 mm |
| 교체용 연결핀 | 40 mm | 60 mm |
| 가이드 롤러축 | 80 mm | 100 mm |
| 베어링 지지축 | 90 mm | 120 mm |
| 밸브 조작축 | 70 mm | 100 mm |

모든 새 의뢰는 40~120 mm이며, 원소재가 300 mm여도 목표 길이에 반영하지 않는다. 기존 외경 규격과 외측 모따기 요구를 유지하고 구간 길이를 소형 규격에 맞췄다. 모든 10개 프리셋을 실제 공통 선반 명령으로 절단하고 가공하여 납품하는 테스트를 통과했다.

PartRequirement에 선택적 RequiredTotalLength를 추가했다. 기존 요구사항은 기본값 0으로 기존 동작을 유지한다. 새 의뢰는 일부 구간만 가공하고 긴 잔재를 붙여 둔 상태로 합격할 수 없다. 과절삭/길이 부족은 Rejected, 긴 잔재가 붙은 상태는 Reworkable로 판정한다. 기존 PartEvaluator를 사용하며 별도 의뢰 평가기를 만들지 않았다.

소재 적합성은 명목 규격 외에 실제 현재 길이를 확인한다. 절단된 58 mm 잔재를 원소재 명목 길이 300 mm로 오인해 100 mm 의뢰에 연결하는 일을 막는다. 절단 조각의 인벤토리 표시는 원소재 명목 규격임을 명시하며, 실제 치수는 측정으로 확인하도록 한다.

저장된 기존 A/B 의뢰는 수락 목표/규격을 재생성하지 않는 저장 원칙에 따라 그대로 유지한다. 새 생성은 A3/B3 소형 규격만 사용한다. 기존 300 mm 의뢰 저장을 로드해도 데이터가 변경되지 않는 호환 테스트를 추가했다.

## Phase 06 REV1 — 미니 탭 / 고정 핵심 조작

- 공구 / 심압대 / 측정 / 카메라 미니 탭을 왼쪽 하단에 배치했다.
- 한 번에 하나만 펼치며, 같은 탭을 다시 누르면 닫는다.
- 주축 회전/정지 버튼은 하단 오른쪽 고정 위치에 상시 표시한다.
- 주축 버튼과 배율은 탭 밖에 있으며 기존 명령을 호출한다.
- 현재 실제 공구/장비와 상태는 상단에서 확인한다.
- 공구대는 X/Z 다이얼, 심압대는 같은 Z 기준 위치의 퀼 Z 다이얼만 표시한다.
- 기존 심압대 퀼 전진/후퇴 버튼을 제거했다. 휠/드래그/키보드 이송은 컨텍스트에 맞는 공통 명령으로 보낸다.

기준 1280×800 좌표에서 미니 패널은 (30,550,320,232), 주축 버튼은 (1018,652,214,50)이다. 기존 확대/여백 처리를 사용하고 16:9, 16:10, 21:9에서 고정 조작 영역/카메라와의 겹침 및 화면 경계를 검사했다.

## 심압대 Visual Toggle / Quill Dial

표시 OFF는 심압대 Renderer만 숨긴다. 본체/퀼 좌표, 공구, 가공 상태와 형상은 유지하며 다시 ON하면 동일 위치로 표시한다. 실제 Play Mode에서 토글 전후 상태와 형상이 동일함을 검사했다.

퀼의 HandwheelDegrees/ZeroDegrees/ZeroExtension은 공구대 Z와 별도다. RotateQuill 명령이 각도를 실제 이송으로 변환하고, 스트로크 제한 후 실제 이동량만 표시 회전에 반영한다. 제한에 걸린 뒤 다이얼만 계속 회전하지 않는다. 기존 FeedQuill API는 호환성을 위해 유지하지만 플레이어 UI의 순간 이동 버튼으로 노출하지 않는다.

## 절단 바이트 / Kerf / Split

절단 폭은 초기 게임 프리셋 2 mm다. 부분 X 절입은 실제 홈을 만들고 원본 인스턴스를 유지한다. 중심까지 도달하면 주축을 멈추고 두 조각의 데이터/형상을 검증한 후 인벤토리에서 원본을 교체한다. 파지 영역 및 5 mm 미만 조각을 만드는 위치는 차단한다.

길이 보존 기준은 **절단 직전 형상의 실제 재료 범위**다. 원소재 정의의 Nominal Length를 사용하지 않는다. 예: 현재 100 mm를 자르면 40+2+58 mm이며, 58 mm 잔재를 다시 자르면 결과 길이와 절단 폭의 합은 58 mm다.

두 조각은 새 고유 ID와 ParentInstanceId를 갖는다. 재료 ID/식별 상태/네 상태 점수/회수 출처를 상속하고 각자 차지한 외경·선형 절삭·내경 구간만 잘라 좌표를 재설정한다. 원본 전체 형상을 양쪽에 복사하지 않는다. 조건과 형상은 독립 복사한다. Kerf는 어느 조각에도 들어가지 않는다.

두 결과를 만들고 검증하기 전 원본을 제거하지 않는다. 실패 시 원본은 인벤토리에 남는다. 성공한 조각은 Available이며 재장착·재절단·저장/불러오기가 가능하다. 짧은 조각은 길이에 맞춰 파지 길이를 제한한다.

분할 시 기존 측정 기록은 새 조각으로 자동 이식하지 않는다. 측정 대상의 고유 ID와 좌표 구간이 바뀌므로 각 조각을 다시 측정한다. 의뢰가 연결된 원본을 절단한 경우 목표 길이를 충족하는 작은 조각에 연결하고, 둘 다 부족하면 소재 선택 상태로 되돌린다. 목표 형상과 공차 자체는 바꾸지 않는다.

절단은 Phase 04의 실제 인벤토리 공작물에서 사용한다. 기존 QUEST 01 튜토리얼 전용 공작물을 임의로 분할하여 메인 진행을 깨뜨리지 않도록 해당 경로에서는 안내하고 차단한다.

## 황삭/정삭 바이트

황삭 바이트 측면을 평행사변형 메시로 바꿨다. 하단 선단의 시작/끝은 RoughingToolState.EdgeStart/EdgeEnd에서 얻고 실제 절삭 범위도 동일 값을 사용한다. ToolX의 반경 높이에 메시 선단이 놓이며 기존 사각 메시 중심 오프셋을 적용하지 않는다.

정삭 바이트 선택을 추가했으며 기존 외경 절삭 계산과 이송 배율을 재사용한다. 별도 절삭 물리나 공구 마모를 만들지 않았다. 심압대·드릴·바이트 외관은 여전히 기능 검증용 단순 메시다.

## 검증 결과

최종: **156개 실행, 156개 통과, 실패 0개**.

- 결과: `Build/phase05rev3-phase06rev1-results.xml`
- 로그: `Build/phase05rev3-phase06rev1-unity.log`
- Unity 6000.6.0f1의 별도 복제 프로젝트에서 전체 EditMode 및 포함된 Play Mode 통합 테스트 실행.

| REV1 테스트 | 결과 |
|---|---|
| 21 미니 탭 단일 열림/닫힘 및 핵심 영역 비중첩 | PASS — Play Mode 상태 및 세 비율 경계 검사 |
| 22 Visual OFF→ON 상태 보존 | PASS — Renderer/퀼/형상 비교 |
| 23 퀼 다이얼 실제 이동·회전 일치 | PASS — 각도, clamp, 영점, 마우스 휠 |
| 24 공구대 X/Z ↔ 심압대 퀼 Z | PASS — 실제 컨텍스트 전환 |
| 25 고정 주축 위치 | PASS — 공통 고정 버튼, 비중첩 검사 |
| 26 부분 절입은 홈만 생성 | PASS |
| 27 완전 절단 새 ID 두 개 | PASS |
| 28 절단 길이 보존 | PASS — 현재 길이 및 재절단 |
| 29 잔재 재사용 및 저장 | PASS |
| 30 평행사변형 선단/실제 절삭 범위 일치 | PASS — 메시 좌표 및 접촉 경계 |

Phase 05의 전체 프리셋 절단→재장착→가공→납품, 기존 목표 저장 호환, 긴 잔재 부착 납품 거부를 추가 검증했다. 기존 QUEST 01과 Phase 01~06 기본 기능 회귀 테스트도 통과했다. 컴파일 오류 및 예상 밖 Unity 오류는 테스트에서 발생하지 않았다.

## 파일 목록

신규 C# (+ .meta):
- `Assets/_Project/Scripts/Inventory/WorkpieceSplitter.cs`
- `Assets/_Project/Scripts/Machining/Turning/LatheParting.cs`
- `Assets/_Project/Scripts/Machining/Turning/LatheMiniTabs.cs`
- `Assets/_Project/Scripts/Machining/Turning/RoughingToolGeometry.cs`
- `Assets/_Project/Tests/Editor/Phase06RevisionTests.cs`

수정 C#:
- `Assets/_Project/Scripts/Inventory/WorkpieceInstance.cs`
- `Assets/_Project/Scripts/Inventory/WorkpieceInventoryPanel.cs`
- `Assets/_Project/Scripts/Machining/Turning/LatheCommands.cs`
- `Assets/_Project/Scripts/Machining/Turning/LatheController.cs`
- `Assets/_Project/Scripts/Machining/Turning/LatheInternalMachining.cs`
- `Assets/_Project/Scripts/Machining/Turning/LatheInventoryMachining.cs`
- `Assets/_Project/Scripts/Machining/Turning/LathePrototype.cs`
- `Assets/_Project/Scripts/Machining/Turning/LatheRepeatableQuests.cs`
- `Assets/_Project/Scripts/Machining/Turning/LatheTailstockPresentation.cs`
- `Assets/_Project/Scripts/Machining/Turning/RoughingToolDefinition.cs`
- `Assets/_Project/Scripts/Machining/Turning/TailstockState.cs`
- `Assets/_Project/Scripts/Quest/RepeatableQuests.cs`
- `Assets/_Project/Scripts/Evaluation/PartEvaluation.cs`
- `Assets/_Project/Tests/Editor/RepeatableQuestTests.cs`
- `Assets/_Project/Tests/Editor/ExplorationInspectionTests.cs`

문서: 본 보고서 신규 작성, 기존 Phase 05/06 결과 보고서에 개정 안내 추가.

## 남은 수동 확인

실제 사용자 화면에서 작은 탭의 글자 가독성과 다이얼 조작감은 수동 검수가 남아 있다. 자동 테스트는 레이아웃 경계와 실제 게임 상태 연결을 확인하지만 눈으로 보는 품질까지 보증하지 않는다. 새 아트 자산은 만들지 않았으며 공구 외관은 단순 메시다. 내경 측정은 이전 Phase 06과 동일하게 실제 값 조회 기반까지만 지원한다.

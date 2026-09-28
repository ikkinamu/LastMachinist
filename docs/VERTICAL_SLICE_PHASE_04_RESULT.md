# VERTICAL SLICE PHASE 04 RESULT

2026-09-26 · Inventory Workpiece → Machining Integration v0.1

## 1. 기존 구조 AUDIT

기존 선반은 LathePrototype이 LatheController/LatheInputRouter/TurningWorkpieceView/MeasurementSession을 연결하는 구조이며 별도 가공 Scene 로드가 아니다. QUEST 01은 자체 CurrentWorkpiece, Measurement와 튜토리얼 소재 소비에 의존한다. 이를 회수 공작물로 덮어쓰면 퀘스트가 손상되므로, 기존 튜토리얼 세션을 보존하고 보유 공작물 세션을 같은 선반 화면에 연결했다.

기존 인벤토리 WorkpieceInstance와 저장 경로를 확장했다. 재료 상태 점수는 전달만 하며 절삭력/마모/품질 계산에 반영하지 않았다.

## 2. 선택 흐름과 호환 필터

작업장 [가공] → [선반 · 회전체 가공] → [공작물 선택] → 개체 선택 → [가공 시작]. 일반 경로의 기본값은 보유 중인 실제 공작물이다. 빈 목록에는 사용 가능한 공작물이 없다는 안내와 비활성 시작 버튼이 나오며 테스트 소재를 생성하지 않는다.

InventoryMachiningSession은 Available 상태이고 WorkpieceDefinition.ShapeType이 RoundBar인 개체를 후보로 제공한다. 명목 직경/길이의 유효성도 확인한다. 목록과 상세는 InstanceId로 선택하며 같은 종류의 A/B도 구분한다. 명목 규격·상태 점수·식별 상태를 표시하고, 실제 가공 후 치수는 자동 정답으로 표시하지 않는다.

## 3. Active 공작물과 형상

ActiveMachiningWorkpiece는 Player Inventory의 실제 WorkpieceInstance다. 내부 MachiningWorkpiece는 기존 WorkpieceState/TurningProfile을 재사용하며 동일 InstanceId/MaterialId/DefinitionId를 유지한다.

첫 장착은 Definition의 Diameter/Length로 초기 형상을 만든다. 이후 장착은 저장된 가공 형상의 복사본을 준비해 초기화 성공 시 같은 Instance에 연결한다. 컷/모따기/방향을 보존하며 제거된 재료를 다시 생성하지 않는다. 상태 점수와 식별 상태는 인벤토리의 원본 값을 그대로 참조한다.

공구 Z 위치, 척/베드/주축대 위치·크기, 카메라 배율은 현재 프로파일 길이를 따른다. 100mm 튜토리얼 설정값 대신 300mm 재고 소재가 실제 가공 화면과 이동 범위에 반영된다. 입력은 기존 Router/Commands 경로를 유지한다.

## 4. 사용 상태 및 실패/종료

초기화가 성공한 뒤에만 Available → InMachining과 ActiveMachiningInstanceId를 확정한다. 가공 시작 시 인벤토리에서 삭제하지 않는다. 사용 중인 개체의 중복 장착/삭제를 차단한다. 취소는 선택만 지운다.

초기화 실패/예외는 준비 복사본을 폐기하고 기존 보유 상태/가공 형상을 유지한다. 선반 화면도 이전 Controller/Router/Measurement로 복원한다. 실패 테스트에서는 준비 중 형상을 변경한 뒤 예외를 발생시켜 원본이 손상되지 않음을 검증했다.

[가공 종료 · 인벤토리로] 또는 작업장/다른 페이지 이동은 주축을 정지하고 측정 기록을 보존한 뒤 Available로 돌린다. 실제 가공 형상은 Instance에 남는다. 측정/기록 페이지 이동은 같은 가공 세션을 유지한다. 완성품 생성/판매 등은 구현하지 않는다.

## 5. 측정 및 식별

재고 가공은 별도 MeasurementSession에서 현재 프로파일을 측정한다. TrueDimension/InstrumentReading/RecordedMeasurement는 기존대로 분리한다. 플레이어의 잘못된 999 입력도 수정하지 않고 개체에 보관한다. 튜토리얼의 측정 메모에는 섞지 않는다.

Unknown 재료는 가공 화면에도 미식별로 표시한다. 인벤토리/선택 화면은 명목 규격을 표시하지만 현재 형상의 정답 수치를 자동 표시하지 않는다. 현재 일반 소재 측정 선택은 중앙 직경과 전체 길이를 제공한다.

## 6. QUEST 01 호환

일반 보유 소재 경로와 별도로, 해당 퀘스트 단계에서 [QUEST 01 · 튜토리얼 소재로 작업]을 제공한다. 기존 가공 방식 → 튜토리얼 소재 선택 → 가공 흐름을 유지한다. 일반 가공이 종료되면 이전 퀘스트 Controller와 측정 세션을 복원하며 Quest 상태를 진행시키거나 원래 소재를 소비하지 않는다.

## 7. Save/Load

같은 기존 저장 슬롯에 Availability, ActiveMachiningInstanceId, MachiningWorkpiece(현재 형상/방향/이력), 개체별 수기 측정 기록을 저장한다. 저장 버튼은 활성 재고 측정 기록도 동기화한다.

불러올 때 현재 가공 형상은 보존하되 가공 예약은 해제하여 Available로 복원한다. 사용자가 다시 명시적으로 장착한다. 주축 자동 재시작이나 오래된 InMachining 예약으로 인한 소재 잠김을 방지한다.

Unity JsonUtility가 null 객체를 빈 객체로 복원하는 동작 때문에 HasMachiningGeometry 플래그를 추가했다. 미가공 재고와 실제 저장된 가공 형상을 명확히 구분한다. 기존 Version 1/Phase 03 저장은 미가공 Available로 호환된다.

## 8. 테스트

Unity 6000.6.0f1 전체 **121/121 PASS, 실패/건너뜀 0**.

- 결과: `Build/phase04-results.xml`
- 엔진 로그: `Build/phase04-unity.log`
- 기존 117개 테스트 + 새 도메인 테스트 4개, 기존 탐색 통합 테스트에 실제 가공 루프 추가
- 별도 검증 프로젝트 사용, 원본의 열린 Scene/사용자 저장 슬롯 보존

| 명세 항목 | 결과 |
| --- | --- |
| TEST 01/02 A/B 개별 선택 | PASS: 같은 Definition의 ID/상태 구분, 선택 전환 |
| TEST 03 비호환 형상 | PASS: Plate 및 빈 목록 시작 거부 |
| TEST 04 Active=A | PASS: 인벤토리 원본과 동일 개체, Controller 소재 ID 동일 |
| TEST 05/06 실제 규격 | PASS: 회수한 Ø50×300 프로파일 사용, 기존 튜토리얼 소재와 별도 |
| TEST 07 상태 전달 | PASS: A/B 상태 점수 유지 |
| TEST 08 미식별 | PASS: 가공 표시 미식별, 기존 정보 숨김 테스트 통과 |
| TEST 09 동시 사용 | PASS: 중복 장착 및 사용 중 삭제 거부 |
| TEST 10 취소 | PASS: 시작 전 취소 후 Available 유지 |
| TEST 11 실패 | PASS: false/예외 초기화 실패 후 보유 수량·ID·기존 절삭 형상 유지 |
| TEST 12 빈 상태 | PASS: 빈 후보/시작 실패, UI 버튼 비활성 분기 확인 |
| TEST 13 탐색 루프 | PASS: 조사/회수/복귀/가공/재진입 연결 |
| TEST 14 QUEST 01 | PASS: 기존 설치/펌프 복구 통합 테스트 및 퀘스트 상태 보존 |
| TEST 15 선반 기능 | PASS: 실제 절삭·측정·4뷰, 기존 Router/모따기/다이얼 등 회귀 통과 |
| TEST 16 오류 | PASS: 테스트 중 예상하지 않은 런타임 오류 없음, 컴파일 성공 |

자동 테스트는 실제 엔진 물리/상태/API를 사용하지만 사람의 포인터 클릭이나 화면 픽셀 검증을 대신하지 않는다. 실제 버튼 배치·조작감은 수동 확인이 남아 있다.

## 9. 파일

신규(Assets/_Project 기준):
- Scripts/Inventory/InventoryMachiningSession.cs 및 meta
- Scripts/Machining/Turning/LatheInventoryMachining.cs 및 meta
- Tests/Editor/InventoryMachiningTests.cs 및 meta
- 본 보고서

수정:
- Scripts/Inventory/InventoryState.cs, WorkpieceInstance.cs, WorkpieceInventoryPanel.cs
- Scripts/Machining/Core/WorkpieceState.cs
- Scripts/Machining/Turning/LathePrototype.cs
- Scripts/Core/GameBootstrap.cs
- Scripts/Save/SaveGameComposer.cs
- Tests/Editor/ExplorationInspectionTests.cs

## 10. 남은 범위

알려진 자동 테스트 실패는 없다. 상태값의 물성/절삭 결과 반영, 재료 식별 시험, 고품질 녹/결함 아트, 완성품 제작 시스템은 이번 범위 밖이다. 기존 Greybox와 기존 공구/소재 표현을 재사용하며 새 아트 플레이스홀더는 추가하지 않았다.

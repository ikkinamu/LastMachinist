# Phase 04 REV1 — QUEST 01 공작물 통합

작성일: 2026-09-28

적용 명세: `Last_Machinist_VERTICAL_SLICE_PHASE_04_INVENTORY_MACHINING_INTEGRATION_REV1_QUEST01_WORKPIECE_UNIFICATION.txt`

## 감사 결과

기존 QUEST 01은 `GameBootstrap`에서 수량형 탄소강 항목 3개를 넣고, `LathePrototype.CreateStockWorkpiece()`로 별도 `WorkpieceState`를 만들어 선반에 공급했다. 탐색 회수 소재는 `WorkpieceDefinition.CreateInstance()`를 통해 `InventoryState.Workpieces`에 들어갔으므로 두 경로가 분리되어 있었다.

## 적용 내용

- `InitialWorkshopWorkpieces.EnsureCreated()`가 새 게임 인벤토리에 초기 작업장 소재 3개를 한 번만 생성한다.
- 세 항목은 모두 일반 `WorkpieceInstance`이며 고유 `InstanceId`, `WorkpieceDefinitionId`, `ConditionProperties`, `IdentificationState`, 실제 초기 `TurningProfile`을 갖는다.
- `AcquisitionSource`는 기록용 메타데이터로만 사용한다. 초기 소재는 `InitialWorkshopStock`, 탐색 회수 소재는 `ExplorationRecovery`다.
- 초기 소재는 `RoundBar50` 정의와 현재 인벤토리 Geometry 규칙을 공유한다. 각 소재는 300 mm Geometry를 개별 보유한다.
- QUEST 01 `SelectMaterial()`은 초기 작업장 소재 중 Available 항목을 골라 `SelectInventoryStock()`으로 `MaterialSelected` 상태를 만들고, 같은 `WorkpieceState`를 `CurrentWorkpiece`와 선반에 전달한다.
- 선택된 인스턴스의 `Availability`와 `Inventory.ActiveMachiningInstanceId`를 갱신한다. 별도 Tutorial Stock을 가공 대상으로 복제하지 않는다.
- QUEST 01 완료 후 선택 인스턴스는 Available로 되돌아가며 미사용 소재와 절단 자투리는 인벤토리에 남는다.
- 초기 미리보기 역시 첫 번째 보유 인스턴스의 Geometry를 사용한다. 신규 소재를 추가 생성하지 않는다.
- 저장 복원 시 Inventory의 동일 InstanceId Geometry를 우선 연결해 `CurrentWorkpiece`와 인벤토리 형상이 분리되지 않게 했다.
- 기존 테스트/도메인용 `CreateStockWorkpiece()` factory는 구형 저장 복원 및 순수 테스트 호환을 위해 남겨 두었으나 새 게임의 초기 지급·QUEST 01 일반 플레이 경로에서는 사용하지 않는다.

## 검증

추가 테스트:

- 초기 3개 생성, 고유 ID, Geometry, Condition, 1회 생성
- QUEST 01이 실제 Inventory Instance를 선택하고 동일 InstanceId/Geometry를 선반에 전달하는지 확인
- Save/Load 후 초기 3개가 중복 생성되지 않고 가공 Geometry를 유지하는지 확인

Unity EditMode 전체 테스트 실행을 시도했으나 현재 Unity Licensing Client가 초기화되지 않아 배치 실행이 완료되지 않았다. 실행 로그에는 스크립트 컴파일 오류가 나타나지 않았고 라이선스 연결 대기에서 중단되었다. 따라서 TEST 01~20의 최종 통과 수와 Console Error 0은 Unity 라이선스가 정상화된 뒤 재실행해야 한다.

`git diff --check`는 통과했다.

## 생성 파일

- `Assets/_Project/Scripts/Inventory/InitialWorkshopWorkpieces.cs` 및 `.meta`
- `Assets/_Project/Tests/Editor/Phase04Rev1QuestWorkpieceTests.cs` 및 `.meta`
- 이 보고서

## 수정 파일

- `WorkpieceInstance.cs`: AcquisitionSource 메타데이터
- `WorkpieceDefinition.cs`: 탐색 회수 출처 기록
- `QuestAndDialogue.cs`: Inventory 공작물 전용 소재 선택 전이
- `Quest01Integration.cs`: 실제 WorkpieceInstance 선택·예약·완료 해제
- `GameBootstrap.cs`: 새 게임 초기 3개 생성과 기존 수량형 지급 제거
- `LathePrototype.cs`: 초기 보유 Geometry 미리보기와 인벤토리 기반 선택 수 표시
- `SaveGameComposer.cs`: 저장된 Inventory Geometry 우선 복원

## 남은 범위

초기 소재의 서사 문구, QUEST 02, 탁상드릴, 새로운 재료 종류는 명세대로 구현하지 않았다. Unity Licensing Client가 정상화되면 REV1 테스트 01~20 및 기존 Phase 05~07 회귀 테스트를 실행해야 한다.

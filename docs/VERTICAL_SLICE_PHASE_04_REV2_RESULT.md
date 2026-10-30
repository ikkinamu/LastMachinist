# Phase 04 REV2 — 공작물 개별 표시·선택

작성일: 2026-09-29

명세: `Last_Machinist_VERTICAL_SLICE_PHASE_04_INVENTORY_MACHINING_INTEGRATION_REV2_WORKPIECE_NON_STACK.txt`

후속 CROSS-PHASE REV 01에서 **초기 3개 공작물의 실제 형상 길이가 각각 100 mm로 변경**되었다. 아래의 초기 300 mm 절단 예와 당시 테스트 설명은 REV2 작업 시점의 기록이며, 현재 동작은 [후속 결과](VERTICAL_SLICE_CROSS_PHASE_REV_01_RESULT.md)를 따른다.

## 1. 원인과 기존 데이터 구조

인벤토리에는 일반 수량형 `Entries`와 개별 공작물 `Workpieces`가 함께 있었다. 개별 공작물 목록 자체에 GroupBy는 없었다. 실제 문제는 다음 경로였다.

- `LathePrototype.BuildPageText()`가 기존 탄소강 수량을 별도로 읽어 인벤토리 상단에 표시했다.
- QUEST 01 준비 화면이 ‘보유 N개’ 버튼을 사용했고, 소재 확정 시 첫 번째 초기 지급 소재를 자동 선택했다.
- 구형 저장과 실패 후 재시도에는 수량형 소재를 소비하고 별도 형상을 만드는 경로가 남아 있었다.
- REV1의 장착 이벤트에서 이미 예약한 인스턴스를 종료해 버려 `InMachining` 연결이 풀릴 수 있었다.

## 2. 개별 인스턴스 및 일반 아이템

새 게임은 기존 RoundBar50 프리셋의 초기 공작물 3개를 유지한다. 치수 프리셋을 재설계하지 않았다. 각 항목은 고유 ID와 독립 Geometry/Condition/IdentificationState를 보유한다.

공작물의 보관 단위는 `InventoryState.Workpieces`의 한 인스턴스다. 초기 지급·탐색 회수·절단 결과·가공 후 소재에 같은 목록을 사용한다. 일반 소비품의 `Entries + Quantity` 시스템은 유지한다.

초기 지급 여부를 저장하는 `InitialWorkshopStockCreated`를 추가했다. 모든 초기 소재가 사용되어 없어졌더라도 초기화 함수를 다시 호출해 무료 소재가 재지급되지 않는다. 소재가 없을 때는 탐색 회수로 확보하며 기존 무료 연습 소재 재지급 경로는 비활성화했다.

## 3. UI Binding과 현재 치수

`WorkpieceInventoryPanel`은 `ListWorkpieces`를 순회하며 행 하나에 InstanceId 하나를 연결한다. 행 선택 후 상세 화면도 그 ID로 인벤토리를 조회한다. 같은 이름과 치수가 표시돼도 행은 합쳐지지 않는다.

목록과 가공 선택 화면은 같은 `WorkpieceInventoryPresentation.ListEntry()`를 사용한다. 현재 최대 외경과 현재 실제 길이를 Geometry에서 구해 표시한다. 상세 화면은 현재 치수와 원소재 기준 치수를 구분한다. 상단의 수량형 탄소강 표시를 제거하고 목록 제목을 ‘보유 공작물’로 통일했다.

길이는 기존 `GetAxialLength()`를 사용하고 대표 직경은 현재 표면 구간들의 최대 외경이다. 소수점 첫째 자리 표시는 표시용이며 실제 형상에 반올림 값을 쓰지 않는다. 판정과 측정 기록의 분리는 유지한다.

## 4. QUEST 01 / 가공 선택

QUEST 01도 일반 공작물 선택 화면을 사용한다. UI가 선택한 정확한 ID를 `SelectMaterial(instanceId)`에 전달하며 `InventoryMachiningSession`으로 예약한다. 획득 경로는 가공 가능 여부를 결정하지 않는다.

선택 조건은 Available, 선반 지원 형상, 현재 길이/직경, 다른 의뢰 연결 여부다. 조건 미달 시 다른 소재를 몰래 선택하지 않고 이유를 표시한다. 기존 펌프 목표와 공차는 유지하며 공통 Requirement로 조회한다.

장착 이벤트는 같은 인스턴스의 예약을 유지한다. 선반 형상과 인벤토리 형상이 동일 가공 상태를 참조하고, 다른 인스턴스의 Geometry와 Condition은 변경하지 않는다. QUEST 01의 최종 측정·평가 흐름도 해당 인스턴스와 연결했다.

## 5. Parting과 납품

초기 소재도 기존 PartingTool/WorkpieceSplitter를 사용한다. 완전 절단 시 원본 행이 제거되고 서로 다른 ID의 두 결과 행이 생긴다. 300 mm를 100 mm 위치에서 자르면 100 + 2(절단 폭) + 198 mm다.

QUEST 01에 연결된 원본을 분할하면 적합한 조각을 후속 선택 대상으로 연결하고 다시 장착한다. 자투리는 일반 소재로 남는다. 실제 설치된 부품만 인벤토리에서 제거하며 미사용 초기 소재와 잔재는 유지한다. 다른 소재가 가공 중이더라도 그 예약은 바꾸지 않는다.

## 6. Save/Load와 이전 저장

각 공작물의 ID, Geometry, Condition, 식별·출처·예약 상태를 인벤토리에 저장한다. 현재 퀘스트 공작물은 ID로 연결해 동일 형상을 중복 직렬화하지 않는다. 불러올 때 예약은 해제하고, 재장착 시 같은 ID의 현재 형상을 사용한다.

이전 저장의 탄소강 Quantity는 남은 수량만큼 실제 공작물로 변환한다. 원래 저장에 없는 초기 3개를 덧붙이지 않는다. 변환 후 수량형 항목을 제거하며 다음 저장부터 개별 ID가 유지된다. 구형 저장에서 이미 가공 중이던 별도 형상도 ID와 절삭 결과를 유지해 인벤토리에 편입한다. 형상 정보가 없는 구형 잔여 소재에는 기존 초기 형상 생성 경로를 사용한다.

일반 소비품 수량은 변환 대상이 아니다. 이전 메서드 일부는 API 호환용으로 남겼지만 플레이어 선택 화면은 정확한 인스턴스 ID만 전달한다.

## 7. 검증

최종 검증 결과는 **177개 실행 / 177개 통과 / 실패 0개**다. 설치 시 다른 소재의 가공 예약 보존도 별도 테스트로 확인했다.

- 결과: `Build/phase04rev2-verified-results.xml`
- 로그: `Build/phase04rev2-verified-unity.log`
- Unity 6000.6.0f1, 별도 복제 프로젝트의 EditMode 및 포함된 Play Mode 통합 테스트
- `git diff --check` 및 최종 소스와 검증 복제본 해시 대조

| 명세 항목 | 검증 경로 |
|---|---|
| TEST 01~06 초기 3개·개별 행·고유 ID·동일 정의/치수 | Phase04Rev1QuestWorkpieceTests / WorkpieceNonStackTests |
| TEST 07 두 번째 행 선택 | WorkpieceInventoryPanel.Select + BootPlayModeTests의 두 번째 ID 장착 |
| TEST 08~10 선택 소재만 가공·다른 소재 보존·실제 치수 | WorkpieceNonStackTests의 A/C 전체 직렬화 비교 및 B 현재 외경/길이 |
| TEST 11 탐색 회수 | ExplorationInspectionTests: 초기 3개 + 회수 2개 = 개별 5개 |
| TEST 12 절단 결과 | 공통 LatheInputRouter 절단 및 두 개의 고유 ID/길이 보존 |
| TEST 13~15 저장·복원·합쳐짐 없음 | 서로 다른 길이 저장 반복 및 구형 Quantity 변환 후 재저장 |
| TEST 16 QUEST 01 | 새 게임 → 두 번째 소재 선택 → 절단 → 재장착 → 측정/판정 → 설치 |
| TEST 17~19 인벤토리·탐색·가공 회귀 | 전체 기존 테스트 및 일반 소비품 Quantity 보존 |
| TEST 20 Console | 최종 Unity 테스트 실행 및 스크립트 컴파일 로그 |

TEST 01~20에 대응하는 자동 검증은 모두 통과했다. 최종 실행에서 스크립트 컴파일 오류와 테스트 실패는 없었다. 실제 사용자 해상도의 화면 배치는 아래의 수동 확인 항목으로 남긴다.

회귀 검증 중 기존 Phase 07 테스트에서 소문 상태만으로 주요 발견을 등록할 수 있는 오류가 확인되어 `Discovered` 상태에서만 등록하도록 보완했다. 새 지역이나 다음 Phase 기능은 추가하지 않았다.

## 8. 파일

새 파일:

- `Assets/_Project/Tests/Editor/WorkpieceNonStackTests.cs` 및 `.meta`
- 이 보고서

기존 파일 수정:

- `Scripts/Inventory/InitialWorkshopWorkpieces.cs`, `InventoryState.cs`: 초기 지급 이력·구형 수량 전환
- `Scripts/Inventory/InventoryWorkpieceGeometry.cs`, `WorkpieceInventoryPanel.cs`: 현재 치수·개별 행 표시
- `Scripts/Integration/Quest01Integration.cs`, `Scripts/Quest/QuestAndDialogue.cs`: ID 선택·예약·분할 연결·설치
- `Scripts/Machining/Turning/LathePrototype.cs`, `LatheInventoryMachining.cs`: 공통 선택 화면·장착·측정 진행
- `Scripts/Save/SaveGameData.cs`, `SaveGameComposer.cs`: ID 연결 저장·구형 저장 호환
- `Scripts/Core/GameBootstrap.cs`: 개발용 소재 추가도 개별 인스턴스로 처리
- `Scripts/Core/RegionProgression.cs`: 기존 주요 발견 상태 검사 보완
- 기존 `BootPlayModeTests`, `ExplorationInspectionTests`, `Phase7Tests`, `V03SaveComposerTests`, `WorkpieceInventoryTests`, `Phase04Rev1QuestWorkpieceTests`: 인스턴스 기반 요구와 실제 플레이 흐름 검증

위 코드·테스트 경로는 모두 `Assets/_Project/` 아래다. 변경 전부터 존재하던 REV1 작업 파일과 사용자 변경을 보존했다.

## 9. 남은 확인

새 플레이스홀더 없음. 실제 사용자 해상도에서 긴 이름의 목록 줄바꿈·가독성은 수동 확인이 남아 있다. 이 보고서의 UI 검증은 공통 표시 데이터와 자동 Play Mode 실행 기준이며 픽셀 단위 수동 검수를 의미하지 않는다.

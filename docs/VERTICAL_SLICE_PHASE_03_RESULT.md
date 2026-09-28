# VERTICAL SLICE PHASE 03 RESULT

2026-09-25 · Workpiece Instance & Inventory v0.1

## 기존 구조 AUDIT

MaterialDefinition이 이미 있어 게임 점수만 확장했다. 기존 InventoryState는 수량형/고유 ID 엔트리를 지원하지만 고유 공작물 상태 payload가 없었으므로 동일 인벤토리에 Workpieces 목록과 API를 추가했다. 독립 Inventory Manager나 Save System은 만들지 않았다.

SaveGameComposer가 InventoryState 전체를 SaveGameData.Inventory에 포함하고 SaveGameStore가 JsonUtility로 파일을 쓰는 구조라, 새 목록은 같은 경로로 저장된다. Main/Workshop의 인벤토리 탭도 이미 있어 목록/상세만 확장했다. 탐색은 기존 Phase 02 회수 버튼에 연결했다.

## 데이터 계층

- MaterialDefinition: 기존 Id/DisplayName 유지, MaterialId 별칭과 0~100 경도·강도·절삭성·내식성 게임 점수 추가. 실제 공학 물성값이 아니다. 테스트 재료 점수는 임시 게임 밸런스 값이다.
- WorkpieceDefinition: 종류 ID, 이름, 형상, 명목 치수, 재료 에셋, 기본 관찰 정보, 선택적 아이콘. RoundBar50은 명목 Ø50 × 길이 300mm다.
- WorkpieceInstance: 고유 InstanceId, DefinitionId, MaterialId, 개별 ConditionProperties, Unknown/Identified, 획득 지역/세션 ID. 저장 데이터에는 Scene/ScriptableObject 참조가 없다.
- 기존 WorkpieceState는 실제 선반에 장착된 기하/가공 이력이다. 이번 재고 메타데이터를 장착하거나 절삭하는 연결은 범위 밖이므로 기존 가공 경로를 변경하지 않았다.

부식/표면/형상/결함 상태는 모두 0~100이며 높을수록 양호하다. 범위를 벗어난 데이터는 저장하지 않는다. 종합 점수는 원본 4개 값의 평균이며 개별 값도 그대로 보존한다. 점수로 가공 적합 여부를 자동 판정하지 않는다.

동일 Definition을 가진 테스트 봉재 두 개에 각각 82/64/95/91(평균 83), 43/88/71/84(평균 71.5)를 지정했다. 상태는 스폰/인벤토리 추가 시 복사해 공유하지 않는다. 이름이 같아도 ID가 다르므로 합쳐지지 않는다. 기존 WornPlate.asset의 파일/GUID는 참조 보존을 위해 유지하고 표시 내용/형상만 두 번째 봉재로 바꿨다. 관찰 문구 수는 기본 문구를 포함해 4개/2개다.

## 회수 및 인벤토리

좌클릭은 조사만 한다. [회수]에서 대상과 중복을 확인한 뒤 InventoryState.AddWorkpiece가 성공해야 세션 기록 및 월드 비활성화를 수행한다. 저장 실패 시 대상과 조사 창을 유지한다. 개별 공작물의 세션 회수 키는 InstanceId이며, 공작물이 없는 기존 조사 대상은 ItemId를 사용한다. 같은 조사 Definition을 재사용해도 개별 공작물 키는 겹치지 않는다.

기존 InventoryState에 AddWorkpiece/RemoveWorkpiece/FindWorkpiece/ListWorkpieces가 있다. 기존 수량형 퀘스트 소재와 별도로 보관하되 같은 저장 슬롯을 사용한다. 탐색 재진입은 새 테스트 개체를 생성하고 기존 보유 공작물은 유지한다. 영구 월드 채집 상태/리스폰 제한은 이번 명세에 없다.

작업장 인벤토리는 왼쪽에 기존 용품 요약과 스크롤 목록, 오른쪽에 개별 상세를 표시한다. 번호와 선택 표시로 같은 이름의 개체도 구별한다. 목록/상세는 InstanceId로 선택하며 스크롤을 지원한다.

Unknown이면 재료명은 미식별, 네 가지 점수는 ???로 표시한다. Identified에서만 재료 ID가 연결된 정의와 일치하는 경우 재료 정보/점수를 보여준다. 이번 게임 흐름에는 식별 기능이 없고 회수 시 모두 Unknown이다. 탐색 관찰 창에는 기존 관찰 문구만 표시하고 내부 재료/상태 점수를 자동 공개하지 않는다.

## Save / Load

기존 작업장 저장/불러오기 버튼을 사용한다. InventoryState.Workpieces가 기존 DTO에 포함되므로 ID·재료·4개 점수·식별 상태·획득 정보를 같은 슬롯에 저장한다. Version 1의 기존 파일에서 Workpieces가 없으면 빈 목록으로 복원한다. SaveGameStore에는 테스트용 경로를 선택할 수 있는 생성자만 추가했고 기본 저장 위치/파일 쓰기 방식은 그대로다. 자동 테스트는 임시 슬롯을 사용한다.

## 파일

Assets/_Project 기준 신규:
- Scripts/Inventory/WorkpieceInstance.cs
- Scripts/Inventory/WorkpieceDefinition.cs
- Scripts/Inventory/WorkpieceInventoryPanel.cs
- Resources/WorkpieceDefinitions/RoundBar50.asset, 폴더 및 meta
- Tests/Editor/WorkpieceInventoryTests.cs 및 meta

수정:
- Scripts/Machining/Core/MaterialDefinition.cs, Data/Materials/StructuralCarbonSteel.asset
- Scripts/Inventory/InventoryState.cs
- Scripts/Core/InspectableItemDefinition.cs, InspectableItem.cs, ExplorationInspection.cs, ExplorationSession.cs
- Scripts/Machining/Turning/LathePrototype.cs
- Scripts/Save/SaveGameStore.cs
- Resources/ExplorationRegions/TestYard.asset, RustyRod.asset, WornPlate.asset
- Tests/Editor/ExplorationInspectionTests.cs
- 본 보고서 신규

## 검증

Unity 6000.6.0f1 전체 테스트 **117/117 PASS, 실패/건너뜀 0**. 별도 검증 프로젝트에서 실행했으며 원본의 열려 있는 Scene/저장 슬롯은 건드리지 않았다. 결과: `Build/phase03-results.xml`, 로그: `Build/phase03-unity.log`. 기존 테스트 110개와 새 공작물 테스트 7개가 통과했다. 기존 조사 통합 테스트에도 실제 인벤토리 연결 검증을 추가했다.

| 명세 테스트 | 결과와 범위 |
| --- | --- |
| 01 동일 종류 독립 상태 | PASS: 동일 Definition, 다른 ID/점수, 원본 변경에도 복사된 상태 보존 |
| 02 A 회수 | PASS: 보유 목록/세션 기록/월드 비활성, 중복 저장 실패 시 대상 유지 |
| 03 B 별도 저장 | PASS: 스택으로 합쳐지지 않고 두 개체 유지 |
| 04 작업장 복귀 | PASS: 기존 인벤토리/공작물 참조 유지 |
| 05 개별 선택 | PASS: 같은 이름의 A/B를 ID로 선택하는 UI API 및 Inventory 페이지 진입 |
| 06 A 상세 | PASS: 실제 UI가 사용하는 표시 함수에서 부식 82/종합 83 |
| 07 B 상세 | PASS: 동일 표시 함수에서 부식 43/종합 71.5 |
| 08 미식별 정보 | PASS: 재료명/ID 노출 없음, 4개 재료 특성 ???, Identified에서만 공개 |
| 09 종합/원본 점수 | PASS: 평균 83/71.5 및 각 상태값 보존, 범위 밖 점수 거부 |
| 10 중복 ID | PASS: 중복 Add 및 이중 회수 방지 |
| 11 Save/Load | PASS: 기존 SaveGameStore 임시 파일에 실제 쓰기/읽기, 모든 필드 동일, 이전 Version 1 호환 |
| 12 재진입 | PASS: 세션 회수 로그 초기화, 보유 공작물 두 개 유지 |
| 13 닫기 | PASS: 월드 유지, 보유 공작물 추가 없음 |
| 14 회귀 | PASS: Phase 01~03/Boot/가공/측정/Quest 등 전체 테스트 통과, 테스트 중 예상하지 않은 런타임 오류 없음 |

UI 선택/상세 검증은 실제 버튼이 호출하는 API/표시 함수를 사용한다. 물리 Ray와 모달/페이지 흐름은 엔진에서 검증했지만 사람이 실제 포인터로 모든 버튼을 클릭하거나 픽셀 배치를 검토한 것은 아니다. UI 시각/실제 장치 확인은 남아 있다.

## 범위와 수동 확인

작업장 → 탐색 → 봉재 두 개 조사/회수 → Esc 작업장 복귀 → 인벤토리에서 각각 선택 → 저장 → 불러오기 순서로 확인할 수 있다. 실제 포인터 클릭·글자 배치의 시각 검증은 자동 데이터/엔진 테스트와 별개다.

가공 재료 선택, 실제 재료 식별, 상태에 따른 절삭 결과, 가격/상점/슬롯 제한은 구현하지 않았다. 공작물 외관은 기존 Greybox이고 아이콘은 생략했다.

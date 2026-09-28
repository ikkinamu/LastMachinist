# Phase 07 — 탐색 지도·지역 진행 기반

작성일: 2026-09-28

적용 명세: `Last_Machinist_VERTICAL_SLICE_PHASE_07_EXPLORATION_MAP_REGION_PROGRESSION_v0.1.txt`

## 구현 내용

- 기존 `ExplorationRegionDefinition`을 확장해 발견 상태, 접근 상태, 알려진 정보, 관련 의뢰, 주요 발견 정의, QUEST 공개 조건을 보유한다.
- `RegionProgression`이 지역 런타임 상태를 소유한다. Hidden / Rumored / Discovered와 Accessible을 분리하고, 지역 완료를 상태로 사용하지 않는다.
- QUEST 01이 Completed가 되면 `RevealAfterQuestId`가 `quest_pump_01`인 지역을 Rumored로 공개한다.
- 폐작업장은 Rumored 상태에서 `?`와 제한된 정보만 표시한다. 탁상드릴을 지도나 주요 발견 목록에 미리 공개하지 않는다.
- 기존 `ExplorationSession.Enter`와 작업장 복귀 흐름을 재사용한다. 별도의 Scene Loader를 만들지 않았다.
- 지도는 `ExplorationMapDefinition.BackgroundImage` 슬롯을 통해 교체할 수 있다. 실제 지도 아트가 없을 때는 임시 배경을 사용하며, 지역 이름과 진행 상태는 이미지에 굽지 않는다.
- 마커는 `MapPosition` 정규화 좌표를 동일한 fitted image rect에 투영한다. 절대 픽셀 좌표를 저장하지 않는다.
- 지도 화면은 왼쪽 지도/마커, 오른쪽 정보 패널, 고정된 탐색 시작 버튼으로 구성한다. 접근 불가 지역도 정보는 확인할 수 있고 시작만 비활성화한다.
- 지역 상태를 기존 `SaveGameData`에 추가했다. 저장 시 런타임 상태를 보존하고 불러올 때 정의와 재결합한다.

## 생성 파일

- `Assets/_Project/Scripts/Core/RegionProgression.cs` 및 `.meta`
- `Assets/_Project/Scripts/Core/ExplorationMapDefinition.cs` 및 `.meta`
- `Assets/_Project/Resources/ExplorationMap.asset` 및 `.meta`
- `Assets/_Project/Resources/ExplorationRegions/AbandonedWorkshop.asset` 및 `.meta`
- `Assets/_Project/Tests/Editor/ExplorationFoundationTests.cs`의 Phase 07 검증 항목

## 수정 파일

- `Assets/_Project/Scripts/Core/ExplorationRegionDefinition.cs`
- `Assets/_Project/Scripts/Core/ExplorationSession.cs`
- `Assets/_Project/Scripts/Integration/Quest01Integration.cs`
- `Assets/_Project/Scripts/Machining/Turning/LathePrototype.cs`
- `Assets/_Project/Scripts/Save/SaveGameData.cs`
- `Assets/_Project/Scripts/Save/SaveGameComposer.cs`

## 테스트 항목

추가한 자동 검증은 지역 상태 분리, Hidden → Rumored → Discovered, 접근 차단, 주요 발견 비공개, QUEST 01 완료 후 폐작업장 공개, Rumored 정보의 탁상드릴 비공개, Region Save/Load 보존을 확인한다. 기존 탐색 진입·복귀 테스트가 지역 ID `test_yard`를 명시하도록 보강해 새 Hidden 지역의 리소스 정렬에 의존하지 않게 했다.

Unity 복제 프로젝트에서 테스트 실행을 시도했으나 현재 Unity Licensing Client 연결이 지연되어 배치 실행이 완료되지 않았다. 로그에는 스크립트 컴파일 오류가 기록되지 않았고, 라이선스 초기화 대기에서 중단했다. 따라서 Phase 07의 최종 테스트 수와 Console Error 0은 라이선스가 정상화된 뒤 재실행해야 한다.

## 외부 아트 / 남은 작업

`ExplorationMap.asset`은 `BackgroundImage` 교체 슬롯과 임시 배경으로 구성되어 있다. 명세의 실제 낡은 현장 지도 이미지는 외부 아트가 제공되면 슬롯에 연결해야 한다. QUEST 02, 탁상드릴 발견·회수·설치, 차량 이동, 지역 간 직접 이동은 구현하지 않았다.

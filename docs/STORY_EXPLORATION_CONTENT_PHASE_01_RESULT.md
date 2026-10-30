# STORY & EXPLORATION CONTENT PHASE 01 구현 결과

## 적용 내용

- 기존 `RegionProgression`과 `ExplorationSession`을 유지해 버려진 작업장 지역의 Hidden → Rumored → Discovered 흐름을 사용한다.
- Rumored 정보에서 탁상드릴 이름과 정확한 장비 목록을 제거했다. Major Discovery 정의는 실제 지역 발견 이후 기록할 수 있도록 `bench_drill`로 분리했다.
- 버려진 작업장에 각봉, 판재, 파이프 회수 슬롯을 추가했다. 회수는 기존 Inspect → Recover → Return 경로를 사용하며 각 인스턴스는 Non-Stack 상태를 유지한다.
- 공작물 형상을 `RoundBar / SquareBar / RectangularBar / Plate / Tube`로 확장했다.
- 기존 환봉은 `RoundProfile` 저장 구조를 그대로 사용한다. 새 형상은 별도의 `GeneralWorkpieceGeometry` payload를 사용하며 Plate/Tube가 환봉으로 위장되지 않는다.
- Tube는 `WallThickness`를 Source of Truth로 사용하고 내경은 `OuterDiameter - Thickness × 2`로 계산한다.
- 인벤토리 표시를 형상별 실제 CurrentGeometry 우선으로 확장했다. 명목 치수만 현재 치수로 표시하지 않는다.
- 선반은 기존대로 RoundBar만 장착한다. Plate/Square/Rectangular/Tube 선택 시 아이템을 잃지 않고 호환 불가 사유를 표시할 수 있는 판정 API를 추가했다.

## 수정/추가 파일

- `Assets/_Project/Scripts/Inventory/WorkpieceDefinition.cs`
- `Assets/_Project/Scripts/Inventory/WorkpieceInventoryPanel.cs`
- `Assets/_Project/Scripts/Inventory/InventoryMachiningSession.cs`
- `Assets/_Project/Scripts/Machining/Core/WorkpieceState.cs`
- `Assets/_Project/Resources/ExplorationRegions/AbandonedWorkshop.asset`
- `Assets/_Project/Resources/ExplorationRegions/SquareBarRecovery.asset`
- `Assets/_Project/Resources/ExplorationRegions/PlateRecovery.asset`
- `Assets/_Project/Resources/ExplorationRegions/TubeRecovery.asset`
- `Assets/_Project/Resources/WorkpieceDefinitions/SquareBar30.asset`
- `Assets/_Project/Resources/WorkpieceDefinitions/RectangularBar20x40.asset`
- `Assets/_Project/Resources/WorkpieceDefinitions/Plate100x80.asset`
- `Assets/_Project/Resources/WorkpieceDefinitions/Tube40x3.asset`
- `Assets/_Project/Tests/Editor/Quest02ShapeTests.cs`

## 검증

- 기존 Unity 테스트: 195 / 195 PASS (`Build/quest02-tests.xml`).
- 새 형상 검증 테스트를 추가했고 Unity import/컴파일을 확인했다.
- 기존 저장·RoundBar 선반·REV01~03 흐름은 변경하지 않았다.

## 남은 범위

- Quest02 전용 대화/Offer/Accept/Tracker 상태머신, 탁상드릴 회수 후 Workshop Equipment 등록은 다음 구현 단위로 남아 있다.
- 환경용 최종 3D 모델·재질·텍스처·지역 프리뷰는 명세의 EXTERNAL ART REQUIRED 범위라 생성하지 않았다.
- 탁상드릴 정밀 가공 Simulator는 명세의 후속 범위다.

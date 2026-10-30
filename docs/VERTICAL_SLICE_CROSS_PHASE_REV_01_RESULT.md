# CROSS-PHASE REV 01 구현 결과

## 1. 원인 및 적용 범위

기존 초기 소재 생성 경로는 `RoundBar50` 정의의 명목 길이 300 mm를 그대로 Current Geometry에 사용했다. 인벤토리 표시와 가공 선택은 이미 개별 형상의 실제 길이를 읽고 있었으므로 초기 형상만 100 mm로 바꾸면 된다. 정의의 명목 길이와 일반 회수 소재 생성 경로는 유지했다.

기존 `RepeatableQuestService`는 생성 즉시 의뢰 3개를 만들었고, 지도 탭과 `ExplorationSession.Enter`에는 메인 의뢰 완료 조건이 없었다. `Quest01Integration`이 이미 보유한 `QuestState.Completed`를 두 시스템의 공통 해금 근거로 사용했다. 별도 해금 저장 플래그는 만들지 않았다.

## 2. 초기 공작물 / 현재 길이

`InitialWorkshopWorkpieces.EnsureCreated`가 새 게임에 서로 다른 ID를 가진 공작물 3개를 생성한다. 초기 소재로 표시된 인스턴스의 `InventoryWorkpieceGeometry.CreateInitialState`는 실제 `TurningProfile` 길이를 100.0 mm로 만든다. 기존 정의의 명목 Ø50 × 300 mm, 재료와 상태 속성은 변경하지 않았다. 인벤토리와 가공 선택은 개별 `TurningProfile.GetAxialLength()`를 사용하므로 한 개를 98 mm로 줄여도 다른 두 개는 100 mm로 남는다. 이전 저장에 기록된 형상은 변경하지 않는다.

## 3. 완료 상태 및 해금 경로

QUEST 01의 완료 값은 `QuestState.Completed`다. `Quest01Integration.EarlyGameUnlocked`가 이 상태에서만 참이다. 상태 변경 시 반복 의뢰 서비스가 기존 REV3의 제안 생성 규칙을 실행한다. 완료 이전에는 새 의뢰가 생성되지 않고, 구형 저장에 제안이 남아 있어도 수락·배정·납품이 거부된다. 퀘스트 창의 반복 의뢰 탭도 비활성화된다.

탐색 탭, 지도 진입 버튼, 실제 `ExplorationSession.Enter` 모두 같은 완료 상태를 확인한다. 완료 후에는 지역의 기존 발견·접근 상태를 그대로 사용한다. 따라서 탐색과 반복 의뢰는 서로의 진행을 요구하지 않는다.

## 4. 폐작업장 소문

QUEST 01 완료 자체는 폐작업장을 발견 상태로 올리지 않는다. 완료 후 작업장에 표시되는 `박성철에게 위치 정보 듣기` 행동이 후속 정보 이벤트다. 이 행동을 선택하면 기존 `RegionProgression.ApplyQuestCompletion` 경로로 폐작업장이 Hidden → Rumored가 되며, 지도에 물음표가 나타난다. 실제 지역 진입 가능 여부는 기존 `IsAccessible` 상태가 따로 결정한다.

## 5. 저장 호환

해금 플래그를 저장하지 않고 QUEST 01 상태에서 재계산한다. 미완료 저장을 복원하면 탐색과 반복 의뢰가 잠기고, 완료 저장을 복원하면 둘 다 열린다. 이미 완료된 이전 저장에 반복 의뢰 데이터가 없으면 REV3 규칙으로 제안 3개를 만든다. 기존 저장의 지역 상태와 개별 공작물 형상은 보존한다. 이전 버전에서 이미 저장된 폐작업장 Rumored 상태도 유지한다.

## 6. 테스트 결과

Unity 6000.6.0f1의 별도 복제 프로젝트에서 전체 EditMode 및 포함된 Play Mode 테스트 **181개 실행 / 181개 통과 / 실패 0개**. 스크립트 컴파일 오류와 테스트가 수집한 게임 Console Error는 0개다. Unity Editor 라이선스 모듈의 비차단 액세스 토큰 로그 1건은 별도로 기록되었으며 테스트 실행에는 영향이 없었다. `git diff --check` 통과, 원본 C# 87개와 검증 복제본 해시 일치.

| 명세 테스트 | 결과와 검증 위치 |
|---|---|
| 01~05 초기 3개·100 mm 형상·개별 가공 | PASS · CrossPhaseProgressionGateTests, WorkpieceNonStackTests, BootPlayModeTests |
| 06~08 QUEST 01 전·진행 중 탐색/반복 잠금 | PASS · CrossPhaseProgressionGateTests, BootPlayModeTests |
| 09~13 완료 후 두 시스템 해금 및 독립 진행 | PASS · BootPlayModeTests, CrossPhaseProgressionGateTests, ExplorationInspectionTests, RepeatableQuestTests |
| 14~15 발견 분리·후속 정보 이벤트 | PASS · CrossPhaseProgressionGateTests, ExplorationFoundationTests |
| 16~18 잠긴/열린/기존 완료 저장 호환 | PASS · CrossPhaseProgressionGateTests, RepeatableQuestTests, 기존 저장 테스트 |
| 19~20 회귀·컴파일·Console | PASS · 전체 181개 테스트, 컴파일 로그 |

결과 파일: `Build/crossphase-rev01-final-results.xml`; 로그: `Build/crossphase-rev01-final-unity.log`.

## 7. 파일 / 남은 확인

새 파일: `Assets/_Project/Tests/Editor/CrossPhaseProgressionGateTests.cs` 및 `.meta`, 이 보고서.

수정 파일: `Assets/_Project/Scripts/Inventory/InitialWorkshopWorkpieces.cs`, `InventoryWorkpieceGeometry.cs`; `Scripts/Integration/Quest01Integration.cs`; `Scripts/Quest/RepeatableQuests.cs`; `Scripts/Machining/Turning/LathePrototype.cs`, `LatheInventoryMachining.cs`, `LatheRepeatableQuests.cs`; `Scripts/Core/ExplorationSession.cs`, `MvpWorldPanel.cs`; 기존 테스트 `BootPlayModeTests.cs`, `ExplorationFoundationTests.cs`, `ExplorationInspectionTests.cs`, `Phase04Rev1QuestWorkpieceTests.cs`, `RepeatableQuestTests.cs`, `WorkpieceNonStackTests.cs`. 위 `Scripts/`와 `Tests/` 경로는 모두 `Assets/_Project/` 아래다. 이전 작업과 사용자 변경은 보존했다.

새 플레이스홀더는 없다. 시작 소재 3개를 모두 사용할 수 없게 만든 뒤 미완료 상태를 수동 저장하면, 탐색이 잠겨 있어 추가 소재를 얻을 수 없다. 이때는 이전 정상 체크포인트를 불러와야 한다. 실제 화면의 잠금 버튼 가독성과 후속 정보 버튼 배치는 수동 화면 확인이 남아 있다.

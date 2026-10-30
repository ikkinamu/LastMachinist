# CROSS-PHASE REV 02 구현 결과

## AUDIT

Boot 씬의 `GameBootstrap`이 `MvpStartupOverlay` 타이틀과 Workshop UI를 런타임에 생성한다. 기존 `SaveGameStore`는 `last_machinist_slot.json` 단일 파일을 임시 파일 작성 후 교체했고, 화면에는 `[저장] [불러오기] [닫기]`가 있었다. 탐색의 ESC는 `FirstPersonExplorer` 전용 메뉴가 처리했으며, 정상 종료 버튼과 공통 시스템 메뉴는 없었다. `SaveGameData`에는 Quest, 개별 공작물 형상·상태, 인벤토리, 반복 의뢰, 지역 진행이 이미 포함되어 있어 구조를 재작성하지 않았다. 메뉴는 기존 1280×800 기준 `MvpUiScale`과 `MvpPanelLayout`을 따른다.

## 구현

- **저장소:** 기존 수동 파일을 그대로 유지하고 `.auto0` 최신 자동 저장과 `.auto1` 직전 자동 저장을 추가했다. 임시 파일을 직렬화·역직렬화·검증한 다음 교체한다. 현재 자동 저장이 손상되어 있으면 백업을 덮어쓰지 않는다. 수동·자동 파일을 수정 시간 내림차순으로 검사하며, 손상 파일은 건너뛰고 첫 정상 파일을 계속하기에 사용한다. 유효성은 `SaveGameData.Validate()`와 복제 데이터의 `SaveGameComposer.Restore()` 성공으로 판단한다. 기존 수동 저장도 후보에 포함된다.
- **자동 저장:** 메인 의뢰의 안전한 체크포인트, 반복 의뢰 수락·납품, 폐작업장 위치 정보 획득, 탐색 출발·공작물 회수·작업장 복귀에 연결했다. 진행 중인 별도 가공 세션에는 자동 저장을 건너뛴다. 탐색 중 저장은 복원 가능한 Workshop 상태로 기록한다. 손휠·형상 변경 매 순간에는 저장하지 않는다.
- **타이틀:** 정상 저장이 없으면 `[시작] [설정] [게임 종료]`, 있으면 `[계속하기] [새 게임] [설정] [게임 종료]`를 표시한다. 계속하기는 선택창 없이 최신 정상 파일을 복원한다. 기존 파일이 있을 때 새 게임은 확인을 거친 뒤에만 수동·자동 저장 파일을 지우고 기존 DAY 1 진입으로 이어진다. 모든 파일이 손상되어도 새 게임 경로를 남긴다.
- **플레이 중 UI:** 상단은 `[저장] [닫기]`로 정리했다. F12 개발 패널의 불러오기는 일반 UI와 분리해 유지했다. ESC 메뉴는 계속하기·저장·설정·게임 종료를 제공한다. 종료 확인에는 취소·저장 후 종료·저장하지 않고 종료가 있다. 저장 후 종료는 수동 저장 성공 후에만 종료 함수를 호출한다.
- **입력:** 시스템 메뉴 동안 선반 입력과 탐색 이동·시점·상호작용을 막고 커서를 표시한다. 닫을 때 이전 가공 입력과 탐색 커서 상태를 복원한다. 조사 창이 열려 있으면 ESC가 조사 창을 먼저 닫으며 같은 프레임에 시스템 메뉴로 넘어가지 않는다.
- **종료:** Player 빌드는 `Application.Quit()`, Editor는 조건부 컴파일된 `EditorApplication.isPlaying = false`를 사용한다. 설정 세부 항목은 이번 명세의 범위 밖이므로 두 설정 화면에 `설정 항목 준비 중`으로 표시한다.

## 검증

Unity 6000.6.0f1 복제 프로젝트에서 **187개 자동 테스트 통과 / 실패 0개**. Windows Player 최종 빌드 성공, `Build/rev02-player/LastMachinist.exe` 생성. 원본 C# 89개와 검증 복제본 해시 일치, `git diff --check` 통과. 테스트가 수집한 게임 Console Error 및 C# 컴파일 오류는 0개다. Unity 라이선스 모듈의 비차단 액세스 토큰 로그 1건은 별개다.

| 명세 테스트 | 결과 |
|---|---|
| 01~08 타이틀 후보·최신 정상 파일·손상 폴백·자동 저장 2세대 | PASS: 타이틀 상태 경로와 `TitleSaveExitFlowTests` 저장소 테스트 |
| 09~10 수동 저장·실패 시 기존 파일 보존 | PASS: 저장소와 메뉴 콜백 테스트 |
| 11~15 ESC 메뉴·탐색/가공 입력 차단과 복구 | PASS: `BootPlayModeTests`, `ExplorationInspectionTests`, 메뉴 테스트 |
| 16~19 종료 취소·저장 성공/실패·저장 없이 종료의 파일 보존 | PASS: 종료 콜백/저장소 자동 테스트 |
| 20 실제 Player 종료 클릭 | **미확인**: 빌드는 성공했으나 앱 제어 승인 시간 초과로 창을 열지 못함 |
| 21 Editor 종료 클릭 | **미확인**: Editor 전용 분기는 컴파일되었으나 Play Mode 종료 클릭은 실행하지 않음 |
| 22~23 새 게임 취소/확인 | PASS: 확인 전 초기화 없음, 확인 후 저장 후보 삭제 경로 테스트 |
| 24~26 QUEST 01 해금·개별 형상·지역 진행 저장 복원 | PASS: `TitleSaveExitFlowTests`와 기존 교차 페이즈 테스트 |
| 27 일반 UI 불러오기 선택 제거 | PASS: UI 경로 점검 및 기존 개발 패널 분리 |
| 28~29 16:9/16:10/21:9 배치 | 배치 계산 테스트 PASS; **실제 화면 육안 검수 미확인** |
| 30 Console Error | 게임 테스트 수집 0건; Unity 라이선스 모듈 비차단 로그 1건 |

테스트: `Build/crossphase-rev02-verified-results.xml`. Player 빌드: `Build/crossphase-rev02-verified-build.log`. 실제 Player 실행을 시도했으나 컴퓨터 사용 도구의 앱 제어 승인이 시간 초과되었고 실행 중인 Player 프로세스는 없다. 따라서 이 REV의 실제 Player 종료·화면 기준 완료 판정은 보류한다.

## 파일과 남은 확인

새 파일: `Assets/_Project/Scripts/Core/GameplaySystemMenu.cs` 및 `.meta`, `Assets/_Project/Tests/Editor/TitleSaveExitFlowTests.cs` 및 `.meta`, 이 보고서.

수정 파일: `Assets/_Project/Scripts/Save/SaveGameStore.cs`, `SaveGameComposer.cs`; `Scripts/Core/GameBootstrap.cs`, `MvpStartupOverlay.cs`, `FirstPersonExplorer.cs`, `ExplorationSession.cs`, `ExplorationInspection.cs`, `MvpWorldPanel.cs`; `Scripts/Machining/Turning/LathePrototype.cs`; `Scripts/Quest/RepeatableQuests.cs`; `Scripts/Integration/Quest01Integration.cs`; `Tests/Editor/BootPlayModeTests.cs`, `ExplorationInspectionTests.cs`. 표시한 `Scripts/`와 `Tests/`는 `Assets/_Project/` 아래다. 이전 변경과 사용자 저장 파일은 보존했다.

남은 확인은 실제 Player에서 타이틀 종료, ESC 저장 후 종료·저장 없이 종료, Editor Play Mode 종료, 세 해상도 화면 검수다. 새 플레이스홀더는 설정 화면 문구뿐이다.

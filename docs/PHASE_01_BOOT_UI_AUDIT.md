# Phase 01 시작 UI 감사 및 최소 복구

2026-09-25

## 원인과 순서별 조사

1. 실제 에디터는 이 프로젝트를 실행하고 있었다. `Library/LastSceneManagerSetup.txt`의 활성 Scene 경로는 빈 값이었다. `Logs/Editor.log`에는 Play 진입이 반복되지만 `Last Machinist: QUEST 01 systems loaded`가 없고, `Temp/__Backupscenes/0.backup` 복원 기록이 있었다. 그 백업에는 Main Camera/Directional Light가 있고 GameBootstrap은 없었다. 이 증거는 Boot가 아닌 미저장 기본 Scene에서 Play한 상황을 가리킨다. 실행 중 Hierarchy를 실시간 API로 조회한 것은 아니며, 에디터 기록과 Play 백업을 대조했다.
2. `Boot.unity`에는 활성 GameBootstrap이 배치되어 있다. `Start()`가 LathePrototype을 만들고 Quest01을 구성한 다음 MvpWorldPanel/MvpStartupOverlay를 붙여 기존 타이틀 UI를 활성화한다. 시작 스토리를 마치면 Workshop UI를 켠다. 별도 Main/Workshop Scene 파일은 없으며 Boot 안의 페이지/상태로 구현되어 있다.
3. 기존 UI는 `OnGUI`/IMGUI다. Canvas 배치도, Canvas Prefab Instantiate도 없다. Canvas 부재 자체는 정상이며 UI 생성 실패의 판정 기준으로 사용할 수 없다.
4. UI 초기화 진입점은 Scene에 배치된 GameBootstrap.Start다. 현재 실행 기록에는 완료 로그가 없었다. Bootstrap이 없는 Scene은 아무 초기화도 하지 않으므로 Console Error 0과 UI 부재가 동시에 발생할 수 있다.
5. Build Scene List의 첫 항목은 활성 Boot, 두 번째는 SampleScene이다. 기존 Phase 01 구현은 탐색 진입 버튼을 눌렀을 때만 별도 Scene을 생성하며 Boot 시작을 가로채는 초기화 코드는 없었다. 에디터 Play 시작 Scene 지정도 없었다. Build 첫 항목과 에디터에서 현재 열린 Scene의 Play는 구별해야 한다. 저장소의 기존 미추적 파일이 많아 과거 전체 버전 간 변경 이력은 확정할 수 없다. 빈 Scene이 처음 열리게 된 계기까지 확정한 것은 아니다.
6. Boot의 GameBootstrap 스크립트, 선반 설정, 대화 데이터, NPC/배경/펌프 이미지 GUID는 현재 파일과 연결된다. 기존 Canvas/UI Prefab을 삭제한 흔적이 아니라, 기존 Bootstrap이 없는 Scene을 실행한 문제다.

## 수정 파일과 내용

- 신규 `Assets/_Project/Editor/BootPlayModeEntry.cs` 및 폴더/스크립트 meta: 에디터 로드/편집 모드 복귀 시 Play 시작 Scene을 기존 Boot로 지정. 작업 중인 Scene을 덮어쓰거나 저장하지 않는다. 실행 중에는 바꾸지 않으며 batch 테스트는 자체 Scene 제어를 유지한다.
- 수정 `Assets/_Project/Tests/Editor/BootPlayModeTests.cs`: Play 이후 Boot를 강제로 로드하던 방식을 제거하고 시작 Scene 지정으로 진입하도록 변경. 활성 Scene 경로와 기존 타이틀 UI 컴포넌트 활성화를 검증하고 종료 시 테스트 전 시작 Scene 설정을 복구한다.
- 신규 본 보고서.

새 Canvas, UI Prefab, 임시 타이틀을 만들지 않았다. Boot/Build List/런타임 UI 및 Quest/가공/측정 코드는 변경하지 않았다.

## 재발 방지와 확인 방법

Unity를 다시 열고 스크립트 컴파일 후 Play한다. 기존 LAST MACHINIST 타이틀에서 시작/스토리를 거쳐 작업장으로 진입한다. Hierarchy 확인 대상은 Canvas가 아니라 GameBootstrap과 그 하위 Lathe Prototype, GameBootstrap에 붙은 MvpStartupOverlay/MvpWorldPanel이다. 탐색 탭과 기존 메뉴를 확인한다.

에디터 Play 시작 Scene을 명시해 빈 Scene/환경 Scene을 열어 둔 경우에도 기존 초기화 경로를 사용한다. 기존 Canvas 존재 조건은 현 구조와 일치하지 않으므로 **Canvas 0개가 정상**이다. Canvas 전환은 이번 수정에 포함하지 않는다.

## 테스트 결과

- Unity 6000.6.0f1 컴파일 성공. 신규 컴파일 오류 없음. 기존 obsolete API 경고 4건.
- `Build/boot-entry-results.xml`: 3개 중 2개 PASS, 1개 FAIL. 탐색 지역 유효성 및 작업장→탐색→복귀→재진입 통과.
- Boot 통합 테스트에서 시작 Scene이 Boot인지, Bootstrap/Quest 생성, 기존 MvpStartupOverlay 활성화, 퀘스트 수락, 가공/측정 페이지 전환과 기록 보존까지 통과했다. 그러나 최종 품질 판정에서 `Perfect` 예상에 `Reworkable`이 나와 테스트 전체는 FAIL이다. 이를 회귀 전체 통과로 보고하지 않는다.
- 이전 방식인 Play 후 `LoadSceneInPlayMode(Boot)`를 임시 복원해 비교했다. `Build/boot-legacy-results.xml`에서도 같은 `Perfect / Reworkable` 실패가 재현됐다. 비교용 코드는 제거했고 최종 파일은 시작 Scene 검증 방식이다. 시작 Scene 변경과 별개로 남아 있는 가공 테스트 문제이며 이번 UI 초기화 수정에서 평가 로직을 변경하지 않았다.
- 런타임 예외/컴파일 오류는 관찰되지 않았지만 테스트 assertion 실패가 있으므로 전체 Console Error 0 또는 전 기능 회귀 없음으로 단정하지 않는다.
- 실제 Game 화면에서 타이틀/작업장 표시, 버튼 클릭과 탐색 탭 배치 검증은 미실시다. Unity 재실행 후 수동 확인이 필요하다.
- Canvas 존재: 해당 없음. 기존 UI는 Canvas를 사용하지 않는다.

# CROSS-PHASE REV 03 구현 보고서

작성일: 2026-10-04

## AUDIT / 적용 범위

- AGENTS.md, MVP_SPEC, ARCHITECTURE, IMPLEMENTATION_PLAN 및 REV03 명세를 확인했다. 기존 MVP 밖 탐색 범위는 후속 명시 요청으로 이미 확장된 범위이며, 이번 작업은 REV03에 한정했다.
- Boot의 GameBootstrap이 기존 MvpStartupOverlay / MvpWorldPanel / LathePrototype을 조합한다. 기존 UI는 IMGUI이며 새 Canvas나 중복 Manager를 추가하지 않았다.
- 새 게임은 기존에 DAY1로 바로 이동했다. 프롤로그/Timeline 시스템은 없었다. 기존 DialoguePage를 프롤로그에서도 공유한다.
- REV02 저장은 수동 슬롯, 자동 저장 2세대, 최신 정상 저장 선택, 손상 저장 대체를 이미 제공한다. 이를 유지한다.
- 탐색은 작업장 Scene을 유지하며 별도 Scene을 만들고 작업장 루트를 일시 비활성화한다. Recover()는 Scene 파괴 전에 Inventory.AddWorkpiece를 완료하고 중복 회수를 차단한다.
- Phase07 지도는 이미 ExplorationMap.asset의 BackgroundImage와 실제 이미지 표시 Rect를 기준으로 정규화 마커를 그린다. 이 구현을 그대로 사용한다.

## 새 게임 / 저장 / 프롤로그

- Title → 시작 또는 확인된 새 게임 → 프롤로그 → 빈 작업장(Cut08) → 암전 / LAST MACHINIST → DAY1 → 박성철 → 이서연 → 기존 Quest01.
- QuestRuntimeState.Prologue: NotStarted / InProgress / Completed. 기존 SaveGameComposer의 Quest 데이터에 포함된다. 알 수 없는 enum 값은 저장 검증에서 거부한다.
- Completed 직후 자동 저장을 예약한다. Completed이면서 Quest가 아직 NotStarted인 저장은 Continue 시 DAY1부터 재개한다. 기존 진행 저장도 프롤로그를 강제 재생하지 않는다.
- 새 게임은 재생 커서를 새로 만들고 InProgress로 초기화한다. 프롤로그 중 수동 저장 메뉴는 제공하지 않는다. 외부 디버그로 InProgress를 저장했다면 Continue는 처음부터 재생한다.
- PrologueSequence.json의 11개 대사 Beat가 Cut01~08을 사용한다. 같은 Cut에서는 이미지를 유지하며, 장면 전환은 0.8초 CrossFade, 이미지에는 느린 Pan/Zoom을 적용한다.
- [제가 할게요.] / […]는 동일 후속 대사와 Main Progression으로 연결한다. 일반 Next나 잘못된 선택 ID로 선택을 건너뛸 수 없다.
- 기존 Canon 범위만 사용했다. 부모 사망 방식, 새 분기나 새 퀘스트는 설정하지 않았다.

## 공통 Dialogue / Choice / 입력

- DialoguePage의 대화창은 화면 하단에 유지하고 선택지 그룹은 바로 위에 배치한다.
- 다음 버튼, 대화 영역 클릭, Space/Enter로 일반 대사를 진행한다. Choice 표시 중 일반 Next 입력은 차단한다.
- 선택 후 별도 확인/닫기 없이 후속 대사를 같은 창에 표시한다. 프롤로그와 기존 NPC 대화가 같은 렌더러를 사용한다.
- DAY1 자산의 NextNodeId는 서로 다른 퀘스트 단계 연결이다. 이를 연속 대사로 자동 실행하면 안 되므로 기존 Quest 이벤트 전환을 유지한다. 첫 테스트에서 이 회귀를 발견해 수정했다.

## 아트 슬롯 / 지도

- `Assets/_Project/Resources/PrologueArt.asset` Inspector: TitleBackgroundImage, PrologueCut01~08, Sequence.
- `Assets/_Project/Resources/ExplorationMap.asset` Inspector: BackgroundImage (Exploration Map Background Image).
- 모든 이미지 슬롯은 교체 시 코드 수정이 필요 없다. 최종 아트는 전부 미할당 Placeholder 상태다. 이미지 생성은 수행하지 않았다.
- Title은 ScaleAndCrop, 지도는 비율 유지 Fit을 사용한다. 기존 1280×800 비례 UI/Letterbox를 유지한다.
- 지도 정보, `?`, 지역 아이콘, 퀘스트 마커, 선택 강조는 배경과 분리된 UI다. Hidden/Rumored/Discovered와 접근 권한은 기존 RegionProgression을 사용한다.
- 16:9 / 16:10 / 21:9 화면에서 이미지 비율과 마커 투영 일치는 수학적 테스트로 검증했다. 실제 교체 아트의 구도/가독성 검수는 별도다.

## 탐색 ESC / 복귀 / 회수 확정

- 기존 GameplaySystemMenu가 탐색 Context에서만 [작업장으로 복귀]를 추가한다.
- 복귀 확인: “탐색을 종료하고 작업장으로 돌아가시겠습니까?” / 취소 / 복귀.
- 취소는 열린 시스템 메뉴로 돌아간다. 계속하기는 탐색 입력과 Cursor를 복구한다. 포커스 손실 후에도 기존 메뉴를 사용한다.
- 확인 → 이미 즉시 확정된 회수 데이터 유지 → 조사창 정리 / Player 입력 차단 → 탐색 Scene unload → 작업장 루트 복원 → Workshop 페이지 → 세션 종료 → Auto Save.
- returning 가드가 재진입을 막는다. 복귀 중 인벤토리에 다시 Add하지 않는다. 동일 Definition 공작물도 고유 InstanceId를 유지한다.
- 게임 종료는 기존 REV02 저장 후 종료 / 저장하지 않고 종료 흐름이며 복귀 명령과 분리된다.

## 수정 / 추가 파일

추가:
- Scripts/Core/PrologueDefinition.cs 및 meta
- Resources/PrologueArt.asset, PrologueSequence.json 및 meta
- Tests/Editor/PrologueRevisionTests.cs 및 meta
- 본 보고서

수정(모두 Assets/_Project 아래):
- Scripts/Core/DialoguePage.cs
- Scripts/Core/MvpStartupOverlay.cs
- Scripts/Core/GameBootstrap.cs
- Scripts/Core/GameplaySystemMenu.cs (REV02에서 추가된 파일)
- Scripts/Core/ExplorationSession.cs
- Scripts/Core/FirstPersonExplorer.cs
- Scripts/Core/MvpWorldPanel.cs
- Scripts/Quest/QuestAndDialogue.cs
- Scripts/Save/SaveGameData.cs
- Tests/Editor/ExplorationInspectionTests.cs

기존 미커밋 REV01/REV02 및 다른 패치 변경은 보존했다.

## 검증

- 최종 Unity EditMode suite: **195 / 195 PASS**, Play Mode 진입 테스트 포함.
- 근거: `Build/rev03-final-tests.xml`, `Build/rev03-final-tests.log`.
- 전체 Quest01 가공/측정/재클램핑/설치, 초기 진행 잠금, 비중첩 공작물, REV02 저장 대체/종료 분기 회귀 테스트 포함.
- 새 게임 프롤로그 실제 UI 렌더링, 두 선택 결과, 선택 건너뛰기 차단, 완료 저장/복원, DAY1 재개 시 선반 활성화, 탐색 메뉴 취소/복귀, 회수품 2개 유지, 복귀 후 저장 이벤트 순서를 검증했다.
- 검증 프로젝트와 원본의 C#/JSON/asset 내용 일치 확인. 사용자 저장 파일을 테스트에 사용하지 않았다.
- `git diff --check`: 통과.
- Windows x64 빌드: **Success**. 로그: `Build/rev03-build.log`의 `Build Finished, Result: Success.`
- 실행 파일: `Build/rev03-player/LastMachinist.exe`.
- 실제 Player 실행 후 제목, 기존 저장이 있을 때 Continue/New Game 구성, 새 게임 확인창 및 취소→제목 복귀를 화면으로 확인했다. Player는 제목 화면에 열어 두었다.
- 기존 사용자 저장이 존재하므로 새 게임 초기화는 실행하지 않았다. 실제 Player에서 프롤로그 전체 재생/탐색 복귀/재실행 Continue까지는 미검증이다. 이 경로의 자동 Play Mode 검증과 구분한다.
- Player 로그에서 시작 차단 오류나 예외는 확인되지 않았다. URP의 Gaussian/Bokeh DepthOfField, PaniniProjection 셰이더 미지원/stripping 경고가 존재한다. 이번 범위에서 렌더 파이프라인 설정은 변경하지 않았다.

### TEST01~40 판정 범위

| 번호 | 결과 / 근거 |
|---|---|
| 01~04 | PASS: 시작/완료 저장/재생 커서 초기화/Cut01~08 순서 자동 테스트 |
| 05 | 구현됨; Fade/Pan/Zoom의 실제 Player 시각 검수 필요 |
| 06~10 | 공통 창 렌더링/선택 가드 자동 검증; 실제 클릭/키보드 조작과 선택지 구도 수동 확인 필요 |
| 11~13 | PASS: 두 선택 모두 같은 완료 경로 |
| 14~15 | PASS: 완료→DAY1→기존 Quest01 진입 및 Quest01 회귀 suite |
| 16~18 | 슬롯/Placeholder/Crop 구현; 실제 Title 아트 교체 및 세 화면비 시각 검수 필요 |
| 19~21 | 기존 지도 슬롯/Placeholder/분리 Overlay 유지; 실제 아트 교체 검수 필요 |
| 22~25 | PASS: RegionProgression 및 기존 탐색 테스트 |
| 26 | PASS(좌표/비율 계산); 실제 Player 화면비별 시각 검수 필요 |
| 27~32 | PASS: Play Mode에서 Context 메뉴, 취소/확인, 회수품 유지, 중복 방지 |
| 33~34 | PASS(저장 이벤트 순서와 Save DTO roundtrip); 실제 프로그램 재실행 검수 필요 |
| 35~37 | PASS: 복귀/종료 별도 경로, REV02 종료 분기, 취소/Resume 입력 상태 |
| 38~39 | PASS: 완료 상태 저장/복원, REV01 잠금 회귀 |
| 40 | 자동 테스트 중 예상하지 않은 Unity Error 없음; 실제 Player 최종 확인은 아래 결과 참조 |

## EXTERNAL ART REQUIRED / 남은 확인

Title Background, Exploration Map Background, Prologue Cut01~08 총 10장 필요.
최종 아트가 없는 상태이며 새 설정 화면의 기존 REV02 Placeholder도 유지한다.
자동 검증 통과를 실제 Player 전 구간 수동 검증 완료로 간주하지 않는다.

# VERTICAL SLICE PHASE 01 RESULT

2026-09-25 · Exploration Foundation v0.1

## 범위와 구조

사용자가 지정한 새 수직 슬라이스 명세가 이번 탐색 구현을 명시적으로 허용한다. AGENTS.md의 '후속 명시 요청이 있을 때만 탐색 구현' 조건에 해당한다. Phase 01만 구현한다.

기존 Boot/Workshop은 IMGUI(1280×800 비례 배치), Input System, 장면 내 페이지 전환 구조다. 기존 1인칭 플레이어나 별도 탐색 Scene 전환 서비스는 없었다. 탐색은 독립적인 InputActionMap과 Perspective Camera를 사용하며 선반 Orthographic 카메라를 재사용하지 않는다.

## 실행 경로

작업장 왼쪽 탐색 → 지도에서 작업장 외곽 시험 구역 선택 → 탐색 시작 → WASD 이동/마우스 시점 → Esc → 작업장 복귀. 복귀 후 같은 경로로 재진입한다. 지역 미선택 또는 잠긴 지역은 진입하지 못한다.

ExplorationRegionDefinition ScriptableObject가 RegionId, DisplayName, SceneName, IsUnlocked, Description, 지도 위치, Spawn 위치, 이동 속도와 감도를 보유한다. Resources/ExplorationRegions의 지역 목록을 지도에서 읽는다. 테스트 지역 1개만 등록했다.

ExplorationSession은 SceneName으로 별도 Unity Scene을 런타임 생성하고 바닥 1개, 경계벽 4개, 장애물 2개, Spawn Point, 광원, 플레이어를 만든다. 별도 .unity 파일이나 빌드 인덱스 의존성은 없다. 이 Scene은 Phase 01용 Greybox이며 최종 환경 아트가 아니다. 향후 정식 환경 Scene 로딩은 별도 단계에서 확장할 수 있다.

작업장 Scene을 메모리에 유지하고 활성 루트만 비활성화한다. 선반 입력과 주축을 중지하고 탐색 입력만 활성화한다. 복귀 시 탐색 Scene을 비동기 해제한 후 원래 루트 활성 상태와 작업장 UI·커서를 복원한다. Quest/Inventory/Workpiece 객체를 재생성하거나 저장 데이터로 덮어쓰지 않는다. 중복 진입/중복 복귀를 차단한다.

FirstPersonExplorer: WASD 정규화 이동, yaw 기준 방향, CharacterController 충돌, 중력/바닥 접촉, pitch ±85도 제한. 마우스 감도는 지역 데이터 한 곳에서 설정한다. 좌클릭은 InteractRequested 이벤트만 발생시키며 콘텐츠·아이템·퀘스트 효과는 없다. Esc 메뉴에서는 시점/수평 이동/상호작용을 중지한다. 포커스를 잃으면 메뉴를 열어 커서를 해제한다.

점프/달리기/앉기, 자원 회수, NPC, 적, 전투, 체력, 생존 및 QUEST02는 구현하지 않았다.

## 파일

신규:
- Assets/_Project/Scripts/Core/ExplorationRegionDefinition.cs 및 meta
- Assets/_Project/Scripts/Core/FirstPersonExplorer.cs 및 meta
- Assets/_Project/Scripts/Core/ExplorationSession.cs 및 meta
- Assets/_Project/Resources/ExplorationRegions/TestYard.asset 및 meta, 폴더 meta
- Assets/_Project/Tests/Editor/ExplorationFoundationTests.cs 및 meta
- 본 보고서

수정: Assets/_Project/Scripts/Machining/Turning/LathePrototype.cs (탐색 탭/지도/선택/진입 연결).

## 검증

런타임 및 Editor 테스트 어셈블리 컴파일 통과(기존 API 경고 4건). PATCH001 접촉 모따기, PATCH002 핸드휠/영점, PATCH003 수락/재료/추론 저장 및 펌프 복구 E2E 회귀 통과.

ExplorationFoundationTests는 지역 해금 guard, Boot에서 탐색 진입, 스폰/카메라, WASD 네 방향 이동, 바닥/경계 충돌, pitch/yaw, Interact 이벤트, 작업장 상태 보존 및 2회 진입·복귀를 검증한다. UI 실제 마우스 클릭 및 키보드 장치 이벤트 대신 진입 API/이동·시점 입력 처리 메서드를 호출한다.

Unity 6000.6.0f1에서 2026-09-25 실행: **2/2 PASS, 실패 0**. 결과: `Build/exploration-test-results.xml`, 로그: `Build/exploration-unity.log`.

| 명세 항목 | 결과와 검증 범위 |
| --- | --- |
| TEST 01 탐색 탭/기존 UI | 코드 연결 확인. 실제 화면 배치 수동 확인 필요 |
| TEST 02 지도 표시 | 지도 그리기 분기 확인. 실제 클릭/표시 수동 확인 필요 |
| TEST 03 미선택 진입 차단 | PASS: null 지역 진입 거부, 버튼 비활성 조건 확인 |
| TEST 04 지역 선택 | 선택/정보/버튼 연결 코드 확인. Marker 클릭 수동 확인 필요 |
| TEST 05 Scene/Spawn/Camera | PASS: 별도 Scene, 지정 스폰, Perspective Camera |
| TEST 06 WASD | PASS: 네 방향 이동 처리. 실제 키보드 입력 수동 확인 필요 |
| TEST 07 Mouse Look | PASS: yaw 회전과 pitch 제한. 실제 마우스 감도 수동 확인 필요 |
| TEST 08 벽 충돌 | PASS: 반복 전진에도 경계벽 통과 없음 |
| TEST 09 중력/바닥 | PASS: 낙하 후 바닥 높이 유지 |
| TEST 10 Interact | PASS: 이벤트 1회 발생. 실제 좌클릭 입력 수동 확인 필요 |
| TEST 11 작업장 복귀 | PASS: Workshop 페이지/활성 상태, 퀘스트 및 공작물 참조 보존 |
| TEST 12 재진입 | PASS: 진입/이동/복귀 2회, 이전 탐색 Scene 해제 |

## 남은 확인과 제한

알려진 테스트 실패는 없다. 위 자동 검증은 화면의 시각적 품질이나 실제 장치 입력을 대신하지 않는다. Unity에서 실행 경로를 따라 탐색 탭/지도 선택과 WASD·마우스·Esc 메뉴를 직접 확인해야 한다. 지도와 탐색 환경은 명세 범위의 임시 Greybox다. 좌클릭은 이벤트 연결 지점만 제공하며 수집할 대상은 없다. 탐색 위치 저장과 정식 환경 아트는 이번 범위에 포함하지 않는다.

# VERTICAL SLICE PHASE 06 RESULT

후속 변경: [Phase 05 REV3 / Phase 06 REV1 결과](VERTICAL_SLICE_PHASE_05_REV3_PHASE_06_REV1_RESULT.md). 아래는 v0.1 당시 기록이며 UI·절단·퀼 조작은 후속 보고서를 따른다.

기준: `Last_Machinist_VERTICAL_SLICE_PHASE_06_TAILSTOCK_DRILL_INTERNAL_UI_v0.1.txt`

작성일: 2026-09-26

## 1. Lathe / Geometry / Tool / UI AUDIT

기존 외경 데이터는 TurningProfile, 렌더링은 TurningMeshBuilder/TurningWorkpieceView, 입력은 LatheInputRouter → LatheCommand → LatheController 구조였다. UI는 런타임 IMGUI이며 별도 Canvas를 생성하지 않았다. InventoryMachiningSession의 실제 인스턴스 연결과 기존 저장 DTO를 유지했다.

이번 명세는 드릴/내경 가공을 명시적으로 추가하는 후속 범위다. Phase 05의 의뢰 및 공작물 길이/규격은 변경하지 않았다. 다음 Phase, 리머, 나사, 밀링, 자동 이송은 구현하지 않았다.

## 2. Inner Geometry

TurningProfile에 독립된 HoleProfile을 추가했다. 구멍은 공작물 로컬 좌표의 원통 구간 목록으로 표현한다. 각 구간에는 시작/끝/반경이 있으며, 작업 이력에는 공구 ID, 입구 위치, 직경, 깊이가 기록된다. 센터 가공 여부는 양쪽 끝면별로 구분한다.

내경은 확대만 가능하며 기존 외경을 초기화하지 않는다. 외경 절삭도 내경 데이터를 삭제하지 않는다. 외경과 내경의 교차를 막는 0.2 mm 최소 벽 두께는 **게임 검증용 제한값**이다. 유한 값, 구간 순서, 범위와 교차를 검사하고 허용 범위 밖 작업은 거부하거나 제한한다. 방향 전환은 기존 Orientation을 사용한다.

## 3. Tailstock / Quill

TailstockState는 활성 상태, 장착 공구, 본체 기준 위치, 퀼 이송량을 관리한다. Body/Quill/ToolHolder/MountedTool 표시가 이 상태를 따라간다. 본체 이동·퀼 이송·공구 선택 모두 기존 Router와 명령을 통해 처리한다. UI가 컨트롤러 값을 직접 바꾸지 않는다.

주축 회전 중 공구 변경을 막고, 공구가 공작물 내부에 있으면 후퇴 후 변경/재고정하도록 안내한다. 깊은 이송에서도 홀더가 공작물 안으로 들어가지 않도록 표시 공구 길이를 프리셋의 최대 깊이에 맞췄다.

## 4. CenterDrill

센터드릴 Ø4는 접촉한 끝면에 최대 3 mm의 단순 원통 센터 구멍을 만든다. 이는 **간략화된 게임 형상**이며 실제 센터드릴 원추 형상의 재현은 아니다. 주축 정지·비접촉에서는 형상이 바뀌지 않는다. 실제 이송 시 HoleProfile과 메시가 함께 바뀌며 센터 가공 이력을 따로 기록한다.

## 5. Drill

드릴 Ø10/Ø16 두 프리셋을 제공한다. DrillDefinition에 ID, 한국어 이름, 직경, 공구 종류, 표시 종류, 최대 깊이가 있다. 해당 끝면의 센터 가공을 확인한 후 드릴의 실제 이송으로 구멍을 확대/연장한다. 센터드릴 버튼으로 드릴 구멍을 자동 생성하지 않는다.

드릴 최대 깊이 80 mm, 퀼 최대 이송 90 mm, 척 파지 구간 제한을 적용한다. 모두 초기 게임 프리셋이며 실제 장비 규격을 뜻하지 않는다. 주축 정지 상태의 이동은 위치 조정만 한다.

## 6. BoringTool / Internal Cutting

기존 ToolKind에 내경 바이트를 추가했다. 구멍 없는 소재, 기존 구멍보다 깊은 진입, 들어갈 공간이 없는 위치에서는 절삭하지 않으며 한국어 안내를 표시한다. 바이트는 기존 구멍의 입구로 진입해야 한다.

입구부터 원통 내경을 확대하거나 단순 내부 단차를 만들 수 있다. 바이트 뒤쪽의 진입 통로가 좁은 상태에서 구멍 중간만 넓히는 내부 홈 가공은 차단한다. 진입이 차단되면 실제 위치와 핸드휠 표시 회전도 함께 유지한다. 내경 절삭은 기존 X/Z 핸드휠 명령을 사용한다.

## 7. Mesh / Visual

기존 외경 메시 생성기에 안쪽 벽, 구멍 입구 링, 막힌 구멍 바닥과 내부 단차 면을 추가했다. 구멍 입구를 솔리드 뚜껑으로 막지 않는다. 내부 벽은 안쪽을 향하는 면 방향을 사용한다. 변경 이벤트가 실제 Workpiece View를 갱신한다.

센터드릴은 단차가 있는 형상, 드릴은 프리셋 직경에 맞는 긴 원통, 내경 바이트는 가는 봉 형태로 구분한다. **공구와 심압대 외관은 기능 확인용 단순 메시**이며 완성된 아트나 실제 드릴 날 형상은 아니다. 기존 네 가지 Orthographic View를 유지한다.

## 8. Measurement

`TurningProfile.InnerTrueDiameter(z)`와 `HoleProfile.DepthFromFace(length, left)`로 실제 내경과 깊이를 조회할 수 있다. 내경 실제 값을 UI에 자동 표시하지 않는다. 기존 측정기 UI는 내경 측정을 지원하지 않으므로 명세가 허용한 조회/연결 기반까지만 구현했다. 외경/길이 기기 눈금과 수동 입력 기록은 유지한다.

## 9. UI 접근성

기존 선반 화면에 외경 공구 / 내경 공구 / 심압대 분류를 추가했다. 공구 선택은 팝업 없이 같은 영역에서 한다. 상단은 실제 활성 공구와 한국어 작업 상태를 표시한다. 불가능한 공구 변경, 센터 가공 누락, 진입 공간 부족, 벽 두께 한계를 안내한다.

X/Z 핸드휠과 Zero Set 위치는 유지했다. 심압대 활성 시 하단 오른쪽에 퀼 전진/후퇴 및 본체 이동 버튼을 표시한다. 1280×800 기준 레이아웃과 기존 비율 유지 확대/여백 처리를 재사용했다. 16:9, 16:10, 21:9에서 심압대 조작 영역과 핸드휠 영역의 겹침 및 화면 이탈을 자동 검사했다.

## 10. Save / Load

HoleProfile이 기존 Workpiece Geometry 저장 구조에 포함된다. WorkpieceState.Copy도 내경과 이력을 깊은 복사한다. 저장 후 구멍, 작업 이력, 외경이 유지된다. 구멍 필드가 없는 이전 저장은 빈 내경으로 처리한다.

심압대 조작 위치와 주축 회전은 저장하지 않는다. 로드 시 기존 Phase 04 정책대로 장착 예약을 해제하고 형상을 유지한 채 명시적으로 다시 장착한다. 별도 저장 시스템은 만들지 않았다.

## 11. TEST 01~20

Unity 6000.6.0f1, 별도 복제 프로젝트에서 전체 EditMode 테스트 및 포함된 Play Mode 통합 시나리오를 실행했다.

최종 결과: **147개 실행, 147개 통과, 실패 0개**. 변경한 C# 파일 12개는 테스트 복사본과 해시가 일치하며 `git diff --check`도 통과했다.

| 항목 | 결과 / 검증 범위 |
|---|---|
| 01 | PASS — 공통 명령으로 심압대 공구 선택 |
| 02 | PASS — 실제 Play Mode에서 선택 종류와 표시 종류 일치 |
| 03 | PASS — 센터 접촉 시 데이터 변경, 실제 View revision 증가 |
| 04–05 | PASS — Ø10/Ø16 생성, 깊이 제한, 입구/바닥 메시 좌표 |
| 06–07 | PASS — 솔리드 진입 거부, 구멍 진입 후 실제 내경 확대 |
| 08–09 | PASS — 내경 확대 시 외경 보존, 외경 절삭 시 내경 보존 |
| 10 | PASS — 교차 경계 제한, 비유한 입력 거부, 내부 면 방향 검사 |
| 11–12 | PASS — 현재 공구 및 불가능 조작의 한국어 피드백 |
| 13–15 | 배치 계산 PASS — 16:9/16:10/21:9 조작 영역 경계/겹침 검사. 실제 화면 가독성은 수동 검수 필요 |
| 16 | PASS — 탐색에서 회수한 실제 인벤토리 공작물을 선반에 장착해 드릴/내경 가공 |
| 17 | PASS — Phase 05의 10개 규격 가공·납품·반복·저장 회귀 |
| 18 | PASS — 기존 QUEST 01 전체 진행 회귀 |
| 19 | PASS — 기존 핸드휠/영점/모따기/측정 및 네 Orthographic View |
| 20 | PASS — 테스트 중 컴파일 오류 및 예상 밖 Unity 오류 0 |

결과 파일: `Build/phase06-release-results.xml`

로그: `Build/phase06-release-unity.log`

## 12. 수정 파일

신규 (+ 각 C# .meta):
- `Assets/_Project/Scripts/Machining/Turning/HoleProfile.cs`
- `Assets/_Project/Scripts/Machining/Turning/TailstockState.cs`
- `Assets/_Project/Scripts/Machining/Turning/LatheInternalMachining.cs`
- `Assets/_Project/Scripts/Machining/Turning/LatheTailstockPresentation.cs`
- `Assets/_Project/Tests/Editor/InternalMachiningTests.cs`
- `docs/VERTICAL_SLICE_PHASE_06_RESULT.md`

수정:
- `Assets/_Project/Scripts/Machining/Core/WorkpieceState.cs`
- `Assets/_Project/Scripts/Machining/Turning/TurningProfile.cs`
- `Assets/_Project/Scripts/Machining/Turning/TurningMeshBuilder.cs`
- `Assets/_Project/Scripts/Machining/Turning/LatheController.cs`
- `Assets/_Project/Scripts/Machining/Turning/LatheCommands.cs`
- `Assets/_Project/Scripts/Machining/Turning/LathePrototype.cs`
- `Assets/_Project/Tests/Editor/ExplorationInspectionTests.cs`

## 13. 회귀 및 남은 확인

QUEST 01, Phase 01~05, 저장 호환, 별도 측정 기록과 기존 공구 가공을 함께 검증했다. 임의 무료 소재, 의뢰 규격 변경, 새로운 Canvas/Scene은 추가하지 않았다.

자동 배치 검사는 실제 사용자의 해상도별 글자 가독성, 클릭 감각, 시각적 가림까지 보증하지 않는다. 이 부분은 수동 Play 검수가 남아 있다. 배치 실행에서 게임 화면 캡처가 생성되지 않아 시각 검수 완료로 보고하지 않는다. 기존 구식 Unity API 경고는 남아 있다.

게임 확인 경로: 보유 공작물 가공 시작 → 심압대 분류에서 센터드릴/드릴 선택 → 기존 주축 버튼과 하단 심압대 이송 → 공구 후퇴·주축 정지 후 내경 바이트 선택 → 기존 X/Z 핸드휠로 가공. 실제 내경 숫자는 자동 표시되지 않는다.

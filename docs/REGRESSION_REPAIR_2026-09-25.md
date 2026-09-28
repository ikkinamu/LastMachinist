# 회귀 테스트 14개 실패 원인과 수정

2026-09-25 — 최종 Unity 6000.6.0f1 결과: **110/110 PASS, 실패 0**.

## 원인

### 1. 다이얼 입력 단위 변경이 테스트에 반영되지 않음 — 12개

PATCH_002 REV4/REV5는 `이송 mm = 회전 각도 / 90 × 배율`을 요구한다. LatheInputRouter의 ApplyHandwheelDelta도 호환용 이름만 유지한 각도 입력 API다. 이전 테스트는 예전 `0.05 mm × 배율 × 입력값` 기준으로 -2 같은 값을 전달했다. 예를 들어 ×100에서 -2를 전달하면 과거에는 -10mm였지만 현재는 약 -2.222mm다. 그래서 공구가 소재까지 도달하지 않거나 모따기 깊이가 부족했고, 최종 품질도 Reworkable로 나왔다.

해당 그룹: LatheTests 4개, Phase3Tests 6개, Phase7Tests 1개, BootPlayModeTests 1개.

수정: 필요한 실제 이송 거리는 유지하고 입력을 명시적인 회전 각도로 변경했다. 예를 들어 ×100에서 -10mm 이송은 -9°, ×10에서 -30mm 이송은 -270°다. Boot의 현재 위치 기준 이동은 MillimetresToDegrees로 환산한다. 기존 반경/메시/절삭/품질/펌프 설치 기대값은 유지했다. 이미 통과하던 같은 파일의 절삭 방지 테스트도 각도 입력으로 바꿔 실제 접촉 조건에서 검증하도록 했다.

### 2. 반지름 오차와 직경 공차 혼동 — 1개

Phase5Tests.FunctionalButRoughPartIsAcceptable은 반지름을 +0.4mm 늘려 놓고 기본 직경 공차 ±0.5mm 안이라고 예상했다. 실제 직경 차이는 +0.8mm이므로 Reworkable이 맞다.

수정: '허용 범위 내 부품' 테스트의 반지름 차이를 +0.2mm(직경 +0.4mm)로 교정했다. 기존 +0.4mm 반지름 사례는 별도 경계 테스트에서 Reworkable이어야 함을 명시했다. 전역 공차를 넓히거나 Quest 01 전용 공차를 전역에 적용하지 않았다.

### 3. 동시에 표시되지 않는 화면에 공간 비겹침을 요구 — 1개

V04IntegrationTests는 대화창과 확대 측정 눈금의 사각형이 겹치지 않아야 한다고 가정했다. 현재 LathePrototype.OnGUI는 대화 중 전체 HUD를 숨기므로 두 화면이 동시에 표시되지 않는다. 확대 눈금의 하단 영역 사용은 가림 오류가 아니다.

수정: 각 화면이 화면 경계 안에 배치되는지 검사하도록 바꾸고, 기존 표시 조건을 HudVisible 속성으로 추출해 OnGUI에서도 그대로 사용했다. Boot 통합 테스트에서 측정 페이지 표시 → 대화 차단 시 숨김 → 대화 해제 시 표시를 검증한다. 글자나 눈금을 축소하지 않았다.

## 수정 파일

Assets/_Project 기준:

- Tests/Editor/LatheTests.cs: 각도 입력 전환, ×1/×10/×100 환산 및 호환 API 검증 3개 추가
- Tests/Editor/Phase3Tests.cs: 모따기/재클램핑 시 실제 이송 거리를 회전 각도로 표현
- Tests/Editor/Phase5Tests.cs: 반지름/직경 구분, 허용 경계 안팎 검증 5개 추가
- Tests/Editor/Phase7Tests.cs: 펌프 설치 통합 가공 입력 교정
- Tests/Editor/BootPlayModeTests.cs: 실제 Boot 가공 입력 교정, 대화 중 측정 HUD 숨김/복원 검증
- Tests/Editor/V04IntegrationTests.cs: 별도 페이지의 화면 경계 검사
- Scripts/Machining/Turning/LathePrototype.cs: 기존 HUD 표시 조건을 읽기 전용 HudVisible로 추출

문서: 본 보고서 신규, VERTICAL_SLICE_PHASE_02_RESULT.md에 후속 해결 결과 추가.

## 검증과 한계

원본 에디터를 방해하지 않도록 Build/phase02-validation-project 복사본에서 전체 EditMode 테스트를 실행했다. Play Mode로 진입하는 Boot 통합 및 탐색 테스트도 포함한다. 최종 변경 소스와 검증 복사본의 해시를 비교했다.

- 총 110개 PASS, 실패/건너뜀 0
- Boot 타이틀 초기화, 가공 → 측정 → 판정 → 설치/펌프 복구 통합 테스트 통과
- Phase 01 탐색 왕복 및 Phase 02 조사/회수 테스트 통과
- 새 입력 환산 3개 + 직경 공차 경계 5개 통과
- git diff --check 통과
- 결과: Build/regression-repair-results.xml
- 엔진 로그: Build/regression-repair-unity.log

이번 실패는 낡은 테스트 입력과 검증 전제에서 발생했으며, 가공 로직/품질 기준을 완화하지 않았다. 테스트 성공이 실제 화면의 시각적 품질이나 모든 장치 조작을 보장하지는 않는다. 새로운 플레이스홀더는 없다.

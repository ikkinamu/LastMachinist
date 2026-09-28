# v0.10 반영 보고 — 2026-09-20

기준: 사용자가 요청한 v0.10 Section 112~118. AGENTS.md의 입력 경로, 측정값 분리, 실제 형상 소유권 규칙을 유지한다. 이번 작업은 기존 측정/선반 기능 수정이며 다음 페이즈를 시작하지 않는다.

## 반영 내용

- 측정 목록을 왼쪽 세로 목록으로 배치했다. L1/L2/L3 직경·길이와 전체 길이를 제공한다.
- 목록 선택과 공작물 클릭이 동일 SelectMeasurementPoint 경로 및 pendingMeasurementPoint를 사용한다. 최초 클릭은 직경, 이후 클릭은 선택한 직경/길이 종류를 유지한다.
- 공작물을 630×252 영역에 확대하고 선택 구간을 강조했다. 원통이 하나의 세그먼트여도 선택한 구간만 강조된다. 클릭 높이는 공작물보다 약간 넓다.
- 공작물 아래에 확대 버니어, 입력·기록·재측정, 메시지, 복귀 버튼 순으로 배치했다. 기존 공작물 배경이 측정 목록을 덮던 겹침도 제거했다.
- X/Z 휠 지름을 62에서 94 기준 픽셀로 약 1.52배 확대했다. 휠 간 간격을 유지하고 클릭 반경을 7픽셀 확장하되 셀 내부로 제한했다. 패널은 카메라 아래에 유지한다.
- 공구 선택 버튼에 `모따기 바이트 (45°)`를 표시했다.
- 모따기는 기존 명령 및 실제 프로파일 변경·Changed 이벤트·메시 갱신 경로를 유지한다. 상단 실행 버튼과 끝단 실제 형상 확대도도 유지한다.
- 플레이어 기록 자동 보정이나 수치 정답 표시는 추가하지 않았다.

## 수정 파일

- Assets/_Project/Scripts/Machining/Turning/LathePrototype.cs
- Assets/_Project/Scripts/Measurement/VernierView.cs
- Assets/_Project/Tests/Editor/Phase3Tests.cs
- Build/SaveRestoreRegression.cs (로컬 검증 실행기)

신규 파일: 이 보고서. 신규 플레이스홀더 없음.

## 검증 결과 및 남은 확인

- 런타임 및 Editor 테스트 어셈블리 컴파일 통과. 기존 FindFirstObjectByType 경고 4개.
- 도메인 실행기: 실제 가공·모따기·최종 검사·펌프 복구 통과.
- 저장 DTO 6개 상태 왕복 실행: Chamfer 존재와 끝단 반경 유지 확인. FinalInspection 포함. System.Text.Json 기반이며 Unity 디스크 저장 UI 실행은 아님.
- v0.9 회귀 실행: 현재 어깨 위치에서 Z 길이 27.6 판독 갱신, 수기 기록 유지, 첫 퀘스트 공차 격리 통과.
- Unity Editor용 테스트 추가: 모따기 후 뷰 Revision 및 메시 끝단 반경 변화, JsonUtility 왕복 형상 유지. 컴파일만 확인했으며 Unity Test Runner 실행은 남아 있다.
- git diff --check 통과.
- Unity Game 화면에서 목록/공작물 클릭 동기화, 해상도별 배치, 휠 드래그를 직접 확인하지 못했다. 따라서 v0.10의 화면 관련 FINAL PASS를 모두 완료했다고 주장하지 않는다.

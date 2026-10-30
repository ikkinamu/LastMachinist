# UI 점검 — 2026-10-04

## 수정한 내용

- `Assets/_Project/Scripts/Machining/Turning/LathePrototype.cs`: 측정의 기록 목록 버튼을 (1040,35,170,42)에서 (780,35,180,42)로 이동했다. 기존 버튼은 공통 Save/Close 영역 (990,15,274,30)과 세로 10픽셀 겹쳤다. 새 영역은 공통 버튼에서 30픽셀 떨어져 있다.
- `Assets/_Project/Scripts/Core/MvpStartupOverlay.cs`: 게임 내 NPC 대화가 열려 있을 때 상단 저장/닫기 및 상태 알림 Overlay를 숨긴다. 기존 DialoguePage가 대화 표시/입력을 맡는다.

## 검증과 제한

- Unity 테스트 195 / 195 PASS: `Build/playtest-ui-tests.xml`.
- `git diff --check` 통과.
- 사용자 저장과 분리하려고 검증용 복사본만 company/product를 LastMachinistPlaytest로 설정했다. 원본 ProjectSettings 및 기존 사용자 저장은 변경하지 않았다.
- 테스트 Player는 실행됐으나 computer-use의 창 활성화가 `GetCursorPos failed: 액세스가 거부되었습니다. (0x80070005)`로 실패했다. 창을 다시 조회·연결한 뒤에도 재현됐다.
- 따라서 위 수정은 코드의 실제 Rect 교차 및 Overlay 조건을 근거로 한다. 직접 클릭하며 전체 플레이를 완료한 결과가 아니다. 화면 조작이 가능해지면 프롤로그→작업장→퀘스트→측정/가공→탐색을 실제 플레이하며 재검수해야 한다.
- 실제 눈으로 확인하지 못한 화면을 정상이라고 판정하지 않았다.

## 생성 파일

- 본 보고서. 런타임 새 클래스나 임시 UI는 추가하지 않았다.

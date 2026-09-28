# v0.7 반영 기록

작성: 2026-09-15. 범위: 기존 MVP의 v0.7 Section 89–95 수정. 다음 개발 페이즈는 시작하지 않았다.

## 변경 사항

1. **핸드휠**: 사각 텍스트 상자를 원형 림·스포크·손잡이로 교체했다. X/Z 각각 드래그로 조작하며 선택 축과 배율을 표시한다. 실제 공구 위치로 회전 표시를 계산하므로 키보드·휠·드래그 입력 결과가 함께 반영된다. 이동은 기존 LatheInputRouter를 거친다.
2. **퀘스트 UI**: 대화, 가공, 측정, 기록, 인벤토리 및 상세 페이지에서 Tracker와 Toast를 숨긴다. 작업장/농장 일반 화면에서만 표시한다.
3. **반복 측정**: Machining 상태에서 정지·측정기 보유·공작물·측정점 조건을 확인한다. 가공/최종 검사 중 유효한 기록도 수락한다. 새 소재의 측정 기록은 고장 축 추론용 WorkMemo를 덮어쓰지 않는다. 페이지 전환은 공작물/세션/공구 위치/재고정/가공 이력을 재생성하지 않는다.
4. **형상 식별성**: 기존 황삭/모따기 Mesh가 서로 다르고, 고장 축은 세 구간 및 L3 직경·길이 마모를 갖는다. 펌프는 본체·모터·커플링·흡입/배출관·베이스와 복구 시 물/음향 상태를 유지한다. 이름표 없는 스크린샷의 식별성 PASS는 아직 판정하지 않았다.
5. **NPC 프레이밍**: 전신을 작게 맞추던 배치를 확대하고 캐릭터 뒤에 그리던 대화창을 캐릭터 앞 레이어로 옮겼다. 하반신은 불투명 대화창이 가리며 얼굴과 상반신은 크게 보이도록 배치했다. 기존 임의 도형 NPC는 복구하지 않았다.
6. **외부 배경**: GameBootstrap Inspector에 Workshop Background, Farm Background, Pump Broken, Pump Repaired 슬롯을 추가했다. 배경과 작업대·선반·소재·기록·수도·펌프의 표시 계층을 분리했다. PNG 교체 시 코드 수정 없이 연결할 수 있다. 이미지가 없는 펌프 상태는 기존 절차적 표현을 사용한다.

## 변경 파일

- `Assets/_Project/Scripts/Machining/Turning/LathePrototype.cs`
- `Assets/_Project/Scripts/Integration/Quest01Integration.cs`
- `Assets/_Project/Scripts/Core/DialoguePage.cs`
- `Assets/_Project/Scripts/Core/MvpStartupOverlay.cs`
- `Assets/_Project/Scripts/Core/MvpWorldPanel.cs`
- `Assets/_Project/Scripts/Core/GameBootstrap.cs`
- `Assets/_Project/Tests/Editor/BootPlayModeTests.cs`
- `Build/MeasurementBindingRegression.cs` (로컬 독립 검증 도구)
- 신규 문서: 이 파일

## 자산 및 미완료 항목

현재 제공된 성철/서연 Dialogue Default PNG 참조를 유지한다. 추가 이미지/폴더는 만들지 않았다.

누락 자산:
- `BG_Workshop_Default.png`
- `BG_Farm_Default.png`
- `PROP_Pump_Broken.png`
- `PROP_Pump_Repaired.png`
- 두 NPC의 별도 World 이미지 및 표정 변형

배경·펌프 슬롯은 현재 비어 있으므로 기존 절차적 환경/펌프가 대체 표현으로 남는다. World NPC는 표시하지 않는다. 외부 배경 품질 및 실루엣/프레이밍/실제 드래그 시각 판정은 Unity 화면 확인이 필요하다.

상속된 1920×1080 Canvas Scaler/Anchor 체계는 아직 전환하지 않았다. 현재 UI는 기존 1280×800 IMGUI 레이아웃에 균일 배율을 적용한다. 따라서 v0.7 전체 Presentation 완료로 판정하지 않는다.

## 검증

### 후속: 진행 버튼 가림 복구

- 스크린샷과 코드 비교로, 월드 UI(depth 20)의 왼쪽 하단 진행 버튼이 선반 UI(depth 10)의 새 불투명 사이드바에 가려진 것을 확인했다.
- 모든 월드 진행 컨트롤을 x=400 이후의 장면 하단으로 이동했다. 사이드바는 x=384에서 끝나므로 겹치지 않는다. 대화가 열리면 대화창이 해당 영역을 덮고 행동 입력은 잠긴다.
- 농장 출발·버니어 획득·측정 시작 단계의 목표 문구를 구체화했다. 저장 데이터/퀘스트 상태는 변경하지 않았다.
- 런타임 컴파일 성공. 실제 Unity 화면에서 버튼 노출과 클릭은 별도 확인이 필요하다.

### 후속: 뒤 장면 비침 수정

- `MvpUiScale`에 알파 블렌딩을 사용하지 않는 불투명 페이지 배경을 추가했다.
- 작업장/농장은 전체 화면 배경을 먼저 채우고, 측정/상세 페이지도 전체를 덮는다. 가공 페이지는 카메라 영역(384,120,896,520)만 남겨 기계 시야를 유지한다.
- 퀘스트 페이지는 스크롤 영역을 닫은 뒤 본문을 그려 잘못된 좌표계로 패널이 잘리는 문제를 수정했다.
- 런타임 컴파일 성공. 실제 Unity 화면에서의 비침 제거는 아직 확인하지 않았다.

- 런타임 및 Editor 테스트 어셈블리 컴파일 성공. 기존 obsolete API 경고 4개.
- 독립 도메인 검증: 추가 가공/반복 측정/같은 공작물과 기록 유지/정답 자동 보정 없음/추론 메모 분리/주축 게이트 PASS.
- 기존 소재 소진·재시도·다른 재고 보존·고장 축 관찰/추론 검증 PASS.
- Boot 실행 테스트에 세 번의 측정 왕복과 공작물 직렬화·좌표·재고정·입력 상태 비교를 추가했다.
- Unity 전체 테스트: 별도 복사 프로젝트에서 실행을 시도했다. 샌드박스 라이선스 연결 거부 후 권한을 확보해 재실행했고 라이선스는 정상 연결됐다. 이후 Package Manager의 resolve-packages/AssetDatabase Initial Refresh 단계에서 진행이 멈춰 결과 XML이 생성되지 않았다. 검증 전용 Unity 프로세스는 종료했다. 이번 버전의 전체 Unity 테스트 PASS를 주장하지 않는다. 로그: `Build/v07-validation/unity-retry.log`, `Build/v07-validation/Project/Logs/upm.log`.

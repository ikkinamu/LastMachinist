# 모따기 조작 및 형상 확인 수정

- 기존 모따기는 공구 이동만으로 실행되지 않고 별도 명령을 요구했다. 실행 버튼이 사이드바 아래에 있었고 접촉 조건 불충족 시 안내 없이 반환했다.
- 선반 상단에 `끝단 모따기 실행` 버튼을 배치했다. 공구 선택, 끝단 Z 위치, 표면 X 접촉, 주축, 완료 여부를 도메인의 동일 조건 검사로 안내한다.
- 하단 다이얼 패널의 남는 영역에 실제 노출 끝단 단면 확대도를 배치했다. 반전 상태를 반영하며 실제 RadiusAt 값으로 경사를 그린다. 숫자 정답이나 장식용 가짜 모따기를 표시하지 않는다.
- 모따기 공구가 장착되어도 Setup으로 표시되던 도메인 모드 판정을 수정했다.
- 실제 형상 변경은 기존 LatheInputRouter → ApplyChamfer → Profile.AddChamfer → Changed → 메시 재생성 경로를 유지한다. 자동 가공이나 저장 파일 변경은 없다.

## 변경 파일

- Assets/_Project/Scripts/Machining/Turning/LatheController.cs
- Assets/_Project/Scripts/Machining/Turning/LathePrototype.cs
- Assets/_Project/Tests/Editor/Phase3Tests.cs
- Build/ChamferGeometryRegression.cs (로컬 검증 실행기)
- 신규 문서: 이 파일

## 검증

- 런타임 C# 컴파일 통과.
- Editor 테스트 어셈블리 컴파일 통과. 기존 FindFirstObjectByType 사용 경고 4개.
- 독립 실행 회귀 검증 통과: 준비 상태별 안내, 재실행 시 중복 절삭 방지, 양쪽 방향 모따기, 접촉/주축 조건, 실제 경사, 후속 절삭 시 재료 복원 금지, 지워진 모따기 재작업.
- 실제 도메인 명령을 사용한 DAY 1 → 양쪽 가공 → 모따기 → 검사 → 펌프 복구 실행기 통과.
- git diff --check 통과.
- Unity Game 화면에서 버튼 배치와 확대도 렌더링은 이번 검증에서 직접 실행하지 못했다. 새 플레이스홀더는 없다. 실제 메시의 작은 모따기는 전체 화면에서 작게 보일 수 있어 확대도로 보완한다.

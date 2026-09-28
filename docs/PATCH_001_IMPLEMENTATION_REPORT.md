# PATCH 001 — 45° 접촉 모따기

날짜: 2026-09-21

## 기준과 범위

사용자가 명시한 `PATCH_001_CHAMFER_TOOL_45DEG_CONTACT_CUTTING.txt`를 이번 변경의 기준으로 적용했다. 기준 문서는 v0.10_REVIEWED다. 이전의 고정 크기 생성 버튼은 Patch 001의 명시적 폐기 규칙으로 대체했다. 기존 AGENTS.md의 공통 명령 경로, 재료 제거, 상태/표현 분리 규칙은 유지한다. 향후 변경 요청도 사용자가 제공하는 Patch 단위로 범위를 확인한다.

## AUDIT

- 공구 정의: ChamferToolDefinition / ChamferToolState.
- 기존 실행: 화면 버튼 → ApplyChamfer(width) → Profile.AddChamfer → Changed → TurningWorkpieceView → TurningMeshBuilder.
- 기존 Move/Cut은 황삭만 처리했다. 화면 공구에는 황삭용 비균등 스케일이 적용되어 실제 45°와도 일치하지 않았다.
- 선행 조건: 실제 프로파일 기반 메시, 공통 입력 라우터, 반전 좌표, 직렬화가 이미 존재한다. 다음 페이즈나 관련 없는 시스템 구현은 추가하지 않는다.

## 변경

- 정식 플레이의 모따기 생성 버튼 및 해당 안내 제거.
- X/Z 이송 → 기존 라우터 → 컨트롤러의 공통 Cut 경로에서 접촉 절삭.
- 유효 날: 기계 좌표의 끝점 `(Z, X)`에서 `(Z - 10, X + 10)`까지의 유한한 45° 절삭면. 10 mm는 날의 길이 방향 도달 범위이며 목표 모따기 크기가 아니다.
- X 이동은 가장 깊은 위치의 날, Z 이동은 이동 경로 전체에서 날과 끝점이 지나간 영역을 계산한다. 주축 정지·비접촉·공구 미장착·비활성·완제품에서는 재료를 제거하지 않는다.
- TurningProfile의 직렬화 가능한 LinearCuts에 실제 제거 경계를 저장한다. 기존 원통 및 ChamferFeature와의 최소 반경을 취해 재료 복원을 방지한다. 반복 절삭, 과가공, 반전 및 기존 저장 형상을 보존한다.
- RadiusAt과 SurfaceSpans가 같은 절삭 경계를 반영한다. 기존 Changed 이벤트 및 메시 생성기가 즉시 갱신하는 구조를 유지한다.
- 공구 표시 원점을 날 끝으로 바꾸고 양 축에 동일 단위를 적용하여 실제 절삭 방향과 시각적 45°를 일치시켰다.
- 구형 ApplyChamfer API/enum은 소스 호환용으로만 남겼고, 호출해도 절삭하지 않는다. AddChamfer는 기존 데이터/테스트용으로 유지하며 플레이 UI는 호출하지 않는다.

## 파일

수정:
- Assets/_Project/Scripts/Machining/Turning/ChamferToolDefinition.cs
- Assets/_Project/Scripts/Machining/Turning/LatheController.cs
- Assets/_Project/Scripts/Machining/Turning/TurningProfile.cs
- Assets/_Project/Scripts/Machining/Turning/LathePrototype.cs
- Assets/_Project/Tests/Editor/Phase3Tests.cs
- Assets/_Project/Tests/Editor/Phase7Tests.cs
- Assets/_Project/Tests/Editor/BootPlayModeTests.cs
- Build/E2eMachiningRegression.cs, Build/SaveRestoreRegression.cs — 로컬 실행기

신규:
- Build/Patch001Regression.cs / .rsp / .runtimeconfig.json — 로컬 실행기
- 이 보고서

## 검증

- 런타임 및 Editor 테스트 어셈블리 컴파일 통과. 기존 API 사용 중단 경고 4개 유지.
- Patch 001 독립 실행: A~H 관련 도메인 검증 통과. 양쪽 방향, 비접촉, 주축 정지, 공구 미장착, 비활성, 작은 절삭·추가 절삭·과가공·후퇴 시 유지 확인.
- 1회의 큰 Z 이동과 100회의 작은 Z 이동 결과를 1,000개 위치에서 비교해 허용 오차 내 일치 확인.
- 저장된 프로파일의 반경과 메시 입력 SurfaceSpans 중간점 값 일치 확인.
- 6개 상태에서 저장 DTO 왕복 시 기존 ChamferFeature와 새 LinearCuts 유지. 최종 검사 상태 포함. System.Text.Json 기반 검증.
- 실제 라우터 이송 명령으로 양쪽 가공 → 모따기 → 검사 → 펌프 복구 회귀 검증 통과.
- git diff --check 통과.
- Unity용 실제 메시 갱신/JsonUtility/과가공 테스트를 추가·갱신하고 컴파일 확인. 배치 실행은 Unity Licensing Client 연결 실패로 테스트 시작 전 중단됐으며, 실행한 배치 Unity 프로세스는 정리했다. 테스트 통과로 집계하지 않는다.

## 남은 확인 / 아트

- 실제 Unity 렌더링 및 손으로 휠을 조작하는 화면 검증은 미확인. 따라서 Patch의 모든 시각 PASS를 완료했다고 보고하지 않는다.
- 공구는 기능 검증용 최소 삼각 프리즘이다. 완성형 외형은 **EXTERNAL ART REQUIRED**.
- 검사 공차 정책은 기존대로 유지하며 목표 모따기 폭 자동 보정은 없다.

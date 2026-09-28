# Phase 06 REV2 — 인벤토리 현재 길이 표시

작성일: 2026-09-28

적용 명세: `Last_Machinist_VERTICAL_SLICE_PHASE_06_TAILSTOCK_DRILL_INTERNAL_UI_REV2.txt`

## 범위와 원인

REV1의 심압대·공구·분할 구현 위에 REV2 L~O 항목을 적용했다. 기존 분할 형상은 정상 저장되지만 인벤토리와 가공 선택 목록은 WorkpieceDefinition의 원소재 길이를 표시했다. 이전 보고서의 ‘실제 길이는 측정으로 확인’ 안내는 이번 REV2의 현재 길이 표시 요구로 대체된다. 측정기 눈금·사용자 입력 기록·정답 판정의 데이터 분리는 유지한다.

## 1. Actual Length Source of Truth

`TurningProfile.GetAxialLength()`가 현재 표면 형상에서 재료가 남아 있는 최초/최후 축 좌표의 차이를 반환한다. 기존 분할의 MaterialBounds 계산을 이 메서드와 공통화했다. `TotalLength`는 좌표 범위이므로 끝부분이 제거되어도 실제 길이로 그대로 사용하지 않는다. 뒤집기 방향에도 길이는 동일하다.

`InventoryWorkpieceGeometry.TryGetActualLength()`를 인벤토리와 의뢰가 함께 사용한다. 기존 저장 구조에서 아직 장착한 적 없는 소재는 형상이 지연 생성된다. 이런 소재만 장착과 동일한 `CreateInitialState()`로 초기 형상을 구성해 길이를 구한다. 표시 과정에서 인벤토리나 저장 상태를 변경하지 않는다. 가공 형상이 있는 인스턴스는 현재 형상을 우선하며, 가공 이력 표시가 있는데 형상이 누락된 경우 기준 길이로 대체하지 않는다.

## 2. Inventory List

각 인스턴스의 이름과 `현재 길이: XX.X mm`를 표시한다. 동일 정의의 서로 다른 길이도 각 고유 ID별로 유지한다. 절단 조각 표시와 상태 점수도 유지한다. 여러 줄 표시가 잘리지 않도록 목록 버튼의 높이를 내용에 맞춘다.

## 3. Inventory Detail

현재 길이를 이름 바로 아래 표시한다. 원소재 치수는 `기준 길이`, `기준 직경`으로 구분한다. 분할 후 원소재 기준 정보라는 안내는 유지하며 현재 길이를 측정해야만 알 수 있다는 이전 문구를 교체했다.

## 4. Machining Selection UI

인벤토리와 동일한 `ListEntry()` 및 `Details()`를 사용한다. 따라서 가공 선택 화면도 같은 인스턴스의 현재 길이를 표시한다. 장착 전 최소 길이 검사와 파지 길이 제한도 현재 실제 길이를 사용한다. 위치 좌표 범위 자체는 기존 형상 좌표를 유지한다.

## 5. Split 후 길이

100 mm를 절단 폭 2 mm로 분리하면 결과 목록·상세에 각각 40.0 mm와 58.0 mm가 표시된다. 각 조각의 잘린 형상에서 직접 계산하며 원소재 길이를 재사용하지 않는다. 재장착·잔재 재사용도 같은 형상을 사용한다.

## 6. Quest Length Compatibility

반복 의뢰의 소재 선택 판정을 공통 실제 길이 조회에 연결했다. 기준 길이가 충분해도 현재 길이가 목표보다 짧으면 차단한다. UI만 소수점 첫째 자리로 반올림하며 판정과 형상에는 원래 정밀도의 값을 사용한다.

## 7. Save/Load 동기화

새 ActualLength 저장 필드나 UI 길이 캐시를 추가하지 않았다. 저장된 Geometry를 복구하고 표시 시 길이를 다시 계산한다. 기존 미가공 소재 저장도 그대로 읽힌다. 표시용 반올림 값은 Geometry에 기록하지 않는다.

## 8. 테스트 결과

Unity 6000.6.0f1의 별도 복제 프로젝트에서 전체 EditMode 테스트와 그 안의 Play Mode 통합 검증을 실행했다.

**164개 실행 / 164개 통과 / 실패 0개.** 프로젝트 컴파일 오류 없음. 기존 BootPlayModeTests의 구형 객체 검색 API 경고는 남아 있다.

| REV2 항목 | 결과 | 검증 내용 |
|---|---|---|
| TEST 31 | PASS | 미가공/장착 소재의 목록·상세 및 초기 형상 길이 일치 |
| TEST 32 | PASS | 같은 정의의 82.437 / 46 / 31.5 mm 개별 표시, 원본 정밀도 유지 |
| TEST 33 | PASS | 형상 양 끝 재료 제거 후 100 → 82.4 mm 표시, 좌표 범위와 뒤집기 분리 |
| TEST 34 | PASS | 공통 절단 명령으로 분리한 A/B의 40.0 / 58.0 mm 표시 |
| TEST 35 | PASS | 위 예시 표시값으로도 40 + 2 + 58 = 100 보존 |
| TEST 36 | PASS | 가공 선택 후보와 인벤토리의 동일 인스턴스/공통 표시 및 잔재 재장착 |
| TEST 37 | PASS | 기준 길이가 충분해도 실제 길이 부족 시 의뢰 선택 거부, 반올림 전 판정 |
| TEST 38 | PASS | 분할·끝면 제거·미가공 소재 저장 복원 후 실제 길이와 표시 일치 |

기존 QUEST 01 진행, 반복 의뢰 10종 가공·납품, 측정 기록 분리, 분할·내경·심압대·다이얼 회귀 테스트도 통과했다. TEST 33은 현재 형상 도메인의 끝부분 제거를 검증하며 새로운 단면 가공 모드를 추가한 것은 아니다. TEST 36은 공통 표시 함수와 후보/재장착 연결 검증이며, 화면의 실제 픽셀 가독성을 수동 확인한 결과는 아니다.

검증 산출물:

- `Build/phase06rev2-results.xml`
- `Build/phase06rev2-unity.log`
- 변경 소스와 검증 복제본의 파일 해시 대조
- `git diff --check`

## 생성 파일

- `Assets/_Project/Scripts/Inventory/InventoryWorkpieceGeometry.cs` 및 `.meta`
- `Assets/_Project/Tests/Editor/InventoryActualLengthTests.cs` 및 `.meta`
- `docs/VERTICAL_SLICE_PHASE_06_REV2_RESULT.md`

## 수정 파일

- `Assets/_Project/Scripts/Machining/Turning/TurningProfile.cs`: 실제 재료 범위와 축 길이 API
- `Assets/_Project/Scripts/Inventory/WorkpieceSplitter.cs`: 기존 공개 API를 유지하며 공통 형상 계산 호출
- `Assets/_Project/Scripts/Inventory/InventoryMachiningSession.cs`: 초기 형상 생성 경로 공유
- `Assets/_Project/Scripts/Inventory/WorkpieceInventoryPanel.cs`: 실제 길이 목록·상세 및 기준 치수 구분
- `Assets/_Project/Scripts/Machining/Turning/LatheInventoryMachining.cs`: 공통 표시와 실제 길이 장착 검사
- `Assets/_Project/Scripts/Quest/RepeatableQuests.cs`: 공통 실제 길이 조회로 적합성 판정

## 한계 / 플레이스홀더

추가 플레이스홀더 없음. 자동 검증에서 발견된 실패 없음. 실제 사용자 해상도에서 목록 줄바꿈과 읽기 편의성은 수동 확인이 남아 있다. 현재 지원하지 않는 형상 또는 형상 정보를 확인할 수 없는 데이터는 기준 길이를 실제 값처럼 표시하지 않고 ‘형상 정보 없음’으로 표시한다.

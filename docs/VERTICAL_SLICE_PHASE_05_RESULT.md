# VERTICAL SLICE PHASE 05 RESULT — REV2

후속 변경: [Phase 05 REV3 / Phase 06 REV1 결과](VERTICAL_SLICE_PHASE_05_REV3_PHASE_06_REV1_RESULT.md). 아래는 REV2 당시 기록이며 새 의뢰 길이는 후속 보고서를 따른다.

작성일: 2026-09-26

## 1. 기준 및 AUDIT

REV2는 REV1의 미구현 중공 슬리브 요구를 가이드 롤러축으로 교체한다. 이번 작업은 이 수정과 Phase 05 범위를 따른다. 다음 Phase는 시작하지 않았다.

기존 Quest01Integration은 메인 퀘스트 상태와 튜토리얼 공작물을 소유한다. Phase 04의 InventoryMachiningSession은 인벤토리의 실제 인스턴스와 가공 형상을 연결한다. 기존 UI는 Canvas가 아닌 LathePrototype의 IMGUI다. 평가에는 PartEvaluator, 저장에는 SaveGameData/SaveGameComposer를 그대로 사용했다. Scene, Boot, Build Settings, 입력 명령 계층, 기존 평가 엔진은 변경하지 않았다.

## 2. Template / Instance

RepeatableQuestCatalog에 정적 템플릿과 프리셋을 정의하고, RepeatableQuestInstance는 별도 ID와 복사된 목표 형상·공차를 갖는다. 정적 데이터는 순수 C# 카탈로그이며 런타임 저장 데이터에 Unity 객체 참조가 없다. 진행 로직은 UI 밖 RepeatableQuestService에 있다.

## 3. 다섯 Template / Variant

각 의뢰는 A/B 두 규격이다. 검증용 플레이테스트 치수이며 최종 밸런스 수치는 아니다. 현재 탐색 소재 Ø50 × 300 mm로 제작할 수 있도록 전체 길이를 300 mm로 유지했다. 모든 구간 직경은 서로 다른 인접 단차이며 길이도 A/B에서 달라진다. 드릴, 내경, 나사 또는 새로운 절단 공정은 요구하지 않는다.

| 의뢰 | A 외경 (mm) | B 외경 (mm) | 추가 요구 |
|---|---|---|---|
| 마모된 펌프 보조축 | 40 / 46 / 38 | 38 / 44 / 36 | 3구간 단차 |
| 교체용 연결핀 | 38 / 42 | 36 / 40 | 오른쪽 끝단 45° 모따기 |
| 가이드 롤러축 | 38 / 46 / 38 | 36 / 44 / 36 | 3구간 실축 |
| 베어링 지지축 | 46 / 40 / 34 | 44 / 38 / 32 | 3구간 단차 |
| 밸브 조작축 | 44 / 38 / 32 | 42 / 36 | 오른쪽 끝단 45° 모따기 |

3구간 A 길이 100/100/100, B 길이 90/90/120 mm. 2구간 A 길이 100/200, B 길이 90/210 mm. 기능 허용 편차 ±0.5 mm, 우수 판정 ±0.1 mm. 별도 구간 가중치나 신규 평가 엔진을 만들지 않았다.

## 4. Quest Window / Accept / Tracker

기존 퀘스트 창에 메인/스토리와 반복 의뢰 탭을 추가했다. 수락 전에는 진행하거나 공작물을 연결할 수 없다. 수락 후 공작물 선택/가공 계속 버튼으로 기존 준비 화면에 진입한다. 행동 중심 안내와 의뢰 도면을 표시한다. 도면의 목표 값과 실측값은 구분하며 실제 측정값을 자동 공개하지 않는다. 긴 목록과 상세 내용은 스크롤된다.

## 5. Workpiece Requirement

실제 소유 인스턴스, Available, 선반 호환 원형봉, 충분한 길이, 목표 최대 외경보다 1 mm 이상 큰 명목 직경을 검사한다. 이미 가공된 소재는 기존 평가기로 회복 불가능한 과가공도 확인한다. 다른 활성 의뢰와 같은 소재를 공유할 수 없다. 실패한 소재는 남겨 두고 다른 소재를 연결해 재시도할 수 있다. 무료 소재는 생성하지 않는다.

## 6. Phase 04 가공 / 측정 연결

기존 InventoryMachiningSession으로 장착하며 의뢰와 동일 WorkpieceInstanceId를 연결한다. 시작 실패 시 이전 연결·검사 메시지를 복구한다. 의뢰별 구간 외경/길이 측정 지점을 기존 MeasurementSession에 제공한다. 재장착 시 가공 형상과 입력 기록을 유지한다. 실제 값과 기기 눈금, 수동 입력값의 구분은 유지한다.

## 7. 납품 / Evaluation

장착 중에는 납품할 수 없다. 가공 종료 후 연결된 공작물의 실제 형상을 기존 PartEvaluator로 검사한다. Perfect/Acceptable이면 완료하고 소유 인벤토리에서 납품품을 제거한다. 완료 기록에는 형상, 재료 식별 상태, 네 상태 점수, 회수 출처와 측정 기록을 보존한다. 실패는 Active를 유지하며 피드백을 표시한다. 잘못 입력한 측정값은 수정하지 않고 실제 형상으로 판정한다.

## 8. 반복 생성 / 중복 방지

Offered와 Active 합계 최대 3개의 슬롯을 사용한다. 완료 시 다음 가능한 템플릿으로 보충한다. 동일 템플릿의 활성/제안 중복을 막는다. 같은 템플릿 재등장은 새 GUID와 교대 선택한 A/B 규격으로 생성한다. 완료 인스턴스는 재활성화하지 않는다. 경제/평판 보상은 추가하지 않았다.

## 9. Save / Load

기존 저장 파일에 RepeatableQuestState를 추가했다. ID, 템플릿, 규격, 목표 구간, 공차, 상태, 소재 연결, 판정, 납품 결과를 저장한다. 로드 시 저장된 목표를 다시 생성하지 않는다. 기존 Phase 04 정책대로 장착 예약은 해제하고 가공 형상은 유지한다. 이전 저장에는 초기 의뢰만 추가하며 소재를 지급하지 않는다. 중복 ID·잘못된 목표·깨진 소재 연결은 저장 검증에서 거부한다.

## 10. 테스트 결과

Unity 6000.6.0f1의 별도 복제 프로젝트에서 EditMode 전체 테스트와 포함된 Play Mode 통합 시나리오를 실행했다. 최종 결과 파일: `Build/phase05-verified-results.xml`, 로그: `Build/phase05-verified-unity.log`.

최종 결과: **137개 실행, 137개 통과, 실패 0개**. 변경한 소스 9개 모두 테스트 복사본과 해시가 일치한다. `git diff --check`도 통과했다.

| 명세 테스트 | 검증 내용 | 결과 |
|---|---|---|
| 01 | 기존 퀘스트 페이지에서 반복 의뢰 목록/상세 IMGUI 실행 | 자동 확인, 실제 클릭/시각 검수 별도 |
| 02–03 | 미수락 연결 차단, 수락→Active, 준비 흐름 | PASS |
| 04 | 동일 템플릿 활성 중복 방지 | PASS |
| 05–07 | 작은 소재 거부, 고유 소재 연결, 실제 Phase 04 장착 | PASS |
| 08–09 | 실제 형상 합격 납품, 실패 시 Active 유지 | PASS |
| 10–12 | 12회 반복 완료, 슬롯 보충, 새 ID, A/B 순환 | PASS |
| 13 | 가공 중 저장, 로드 후 목표/연결 유지, 완료 결과 복원 | PASS |
| 14 | 다섯 템플릿의 모든 10개 규격을 공통 명령으로 가공·납품 | PASS |
| 15 | 기존 QUEST 01 시작~펌프 복구 회귀 테스트 | PASS |
| 16 | 탐색→회수→가공→구간 측정→납품 통합 시나리오 | PASS |
| 17 | 테스트 중 컴파일 오류 및 예상 밖 Unity 오류 | 0 |

추가 확인: 과가공 손실 보존 및 대체 소재 연결, 기존 저장 호환, 손상된 목표 거부, 수동 기록 999를 보존하면서 실제 형상으로 납품 판정. 처음 추가한 테스트의 공구 생성자 인자 오류와 정상 안내 로그를 오류 취급하던 검증 조건을 수정했다.

## 11. 파일 목록

신규:
- `Assets/_Project/Scripts/Quest/RepeatableQuests.cs` (+ .meta)
- `Assets/_Project/Scripts/Machining/Turning/LatheRepeatableQuests.cs` (+ .meta)
- `Assets/_Project/Tests/Editor/RepeatableQuestTests.cs` (+ .meta)
- `docs/VERTICAL_SLICE_PHASE_05_RESULT.md`

수정:
- `Assets/_Project/Scripts/Integration/Quest01Integration.cs`
- `Assets/_Project/Scripts/Save/SaveGameData.cs`
- `Assets/_Project/Scripts/Save/SaveGameComposer.cs`
- `Assets/_Project/Scripts/Machining/Turning/LathePrototype.cs`
- `Assets/_Project/Scripts/Machining/Turning/LatheInventoryMachining.cs`
- `Assets/_Project/Tests/Editor/ExplorationInspectionTests.cs`

## 12. 확인할 사항 / 제한

실제 사용자의 해상도에서 버튼 클릭, 스크롤, 도면·버니어 가독성은 수동 검수가 남아 있다. 자동 Play Mode 테스트는 페이지와 가공/측정 연결을 실행하지만 마우스 클릭의 시각 품질을 보증하지 않는다. 새 임시 이미지나 가짜 가공 기능은 없다. 기존 구식 Unity API 경고는 남아 있으며 이번 기능과 별개다.

실행 경로: 시작 → 퀘스트 → 반복 의뢰 → 수락 → 공작물 선택/가공 계속 → 탐색에서 회수한 소재 선택 → 가공/측정 → 가공 종료 → 퀘스트의 실제 형상 검사·합격 시 납품.

# 배경 이미지 반영

- `workplace.png` → `Assets/_Project/Art/External/Backgrounds/Workshop/BG_Workshop_Default.png`
- `farm.png` → `Assets/_Project/Art/External/Backgrounds/Farm/BG_Farm_Default.png`

원본은 보존했고 Boot 씬의 GameBootstrap에 두 Texture2D 참조를 연결했다. 비율을 유지하며 장면 영역을 채우고, 영역과 비율이 다른 부분은 중앙 기준으로 잘린다. 최대 2048, 밉맵 없음, 무압축, Bilinear 설정이다.

배경이 있으면 작업장 가구/농장 배경의 기존 임시 도형을 중복해서 그리지 않는다. 선반·측정·소재·기록은 별도 UI로 접근하며, 농장 펌프 및 수리 후 물/음향은 기존 상태 기반 표현으로 남는다. 별도 펌프 이미지는 아직 없다.

검증: 두 복사본과 원본 SHA256 일치. 런타임 컴파일 성공. 실제 Unity 재임포트 및 플레이 화면은 아직 확인하지 않았다.

## 배경 미표시 후속 대응

씬의 슬롯만으로 배경을 전달하던 방식에 기본 자산 바인딩을 추가했다. 작은 `Resources/EnvironmentArt.asset`이 External 폴더의 두 PNG를 참조한다. 월드 화면 활성화, 초기 바인딩, 시작 대화 표시 시 누락된 배경 참조를 복구한다. 원본 PNG 경로나 파일명으로 런타임 파일 읽기를 수행하지 않는다. 실제 플레이에서 미표시 원인이 이 참조 누락이었는지는 아직 확인하지 못했다.

# 펌프 이미지 연결

- `broken_pump.png`를 `Assets/_Project/Art/External/Props/Pump/PROP_Pump_Broken.png`로 등록했다.
- `repaired_pump.png`를 동일 폴더의 `PROP_Pump_Repaired.png`로 등록했다.
- 원본 보존, 투명 PNG/무압축/밉맵 없음/Bilinear 임포트 설정.
- Boot 씬과 EnvironmentArt 기본 바인딩 모두 연결했다. PumpPresentation이 누락 참조를 복구하며 Quest의 PumpRepaired 상태로 이미지를 선택한다.
- 두 상태 모두 동일한 380×380 영역에 비율을 유지해 표시한다. 이미지가 있으면 기존 임시 도형 펌프는 그리지 않는다.
- 수리 후 물 흐름 시작점을 새 이미지의 위쪽 배출구로 옮겼다. 펌프 소리는 기존 수리 상태 로직을 유지한다.

검증: 런타임 컴파일 성공, 원본/복사본 SHA256 일치. 실제 Unity 이미지 재임포트 및 수리 전후 플레이 화면은 아직 확인하지 않았다. 이전 v0.9 보고서의 두 펌프 이미지 누락 항목은 이번 연결로 해소했다.

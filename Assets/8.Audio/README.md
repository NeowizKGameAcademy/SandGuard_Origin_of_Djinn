# 오디오

효과음 제안 목록은 `Docs/plan/SandGuard_SFX_제안목록_v0.1.md`, 제작 방식 결정은 그 문서의 대화 기록을 따른다.
마력·크리스탈·UI처럼 세계관 안에서 인공적인 소리는 **코드로 합성**하고, 재질이 실재하는 소리(발소리·갑옷·돌·불)는 외부 생성기(ElevenLabs)로 뽑는다. 사람 목소리는 음성 모델을 따로 쓴다.

## 합성 SFX 베이커 (`Editor/Synth/`)

VFX 빌더와 같은 규칙이다. **WAV를 손으로 고치지 말고 레시피를 고쳐 다시 굽는다.** 같은 시드는 같은 파형을 낸다.

| 실행 | 명령 |
|---|---|
| 메뉴 | `SandGuard > Audio > Bake Synth SFX` |
| 배치 | `Unity.exe -batchmode -nographics -projectPath . -executeMethod SandGuard.Audio.Editor.Synth.SynthSfxBaker.BakeAll -quit` |

| 출력 | 내용 |
|---|---|
| `Assets/8.Audio/Synth/<이름>.wav` | 16비트 48kHz. 원샷은 PCM + DecompressOnLoad, 루프는 Vorbis 0.7 + CompressedInMemory로 임포트 설정까지 적용 |
| `Docs/audio-preview/synth-v1/<이름>.png` | 위 파형 개요, 아래 40Hz~20kHz 로그 스펙트로그램(-90~0dB). 루프는 오른쪽에 처음 0.5초를 이어 붙이고 빨간 선으로 이음매를 표시 |
| `Docs/audio-preview/synth-v1/report.md` | 피크·RMS·DC·NaN·스펙트럼 중심·이음매 지표와 판정 |

### 파일 구성

| 파일 | 역할 |
|---|---|
| `Dsp.cs` | 잡음(백색·핑크·갈색·알갱이), 발진기·스윕·부분음, 포락선, RBJ 바이쿼드(스윕 가능), 포화, Schroeder 잔향, 에코, 루프 크로스페이드, 주기 LFO |
| `SynthRecipes.cs` | 소리 하나 = 함수 하나. `All()`이 굽는 목록. 값을 바꾸고 다시 굽는다 |
| `AudioAnalysis.cs` | 지표 측정, FFT, 스펙트로그램 PNG |
| `WavWriter.cs` | 16비트 PCM WAV |
| `SynthSfxBaker.cs` | 메뉴·배치 진입점, 임포트 설정, 보고서 |

### 루프를 이음매 없이 만드는 두 가지 방법

- **주기 층**(드론·부분음·LFO): 모든 주파수와 LFO 속도를 `1/T`의 정수배로 잡는다 (`Dsp.LoopFreq`, `Dsp.PeriodicLfo`). 수학적으로 정확히 이어진다.
- **잡음 층**: `T + X`초를 렌더한 뒤 `Dsp.LoopCrossfade`로 꼬리 X초를 머리에 등파워 크로스페이드한다.

보고서의 "이음매 비율"은 마지막→첫 샘플의 점프를 평균 샘플 변화량으로 나눈 값이다. 1 근처면 들리지 않고, 3을 넘으면 확인한다.

### 1단계(2026-09-16) 레시피

| 파일 | 설계 |
|---|---|
| `SFX_Player_ManaBolt_Fire_01~04` | 1ms 클릭 + 900→260Hz 포화 바디 + 크리스탈 비화성 부분음(1046Hz × {1, 1.5, 2.52, 3.01, 4.63}) + 3.5k→1.2k 대역통과 whoosh + 짧은 잔향. 변형은 피치 ±4%, 감쇠 ±15% |
| `SFX_UI_Click_01~03`, `SFX_UI_Hover_01` | 두 부분음 핑 + 접촉 틱 + 저역 바디. 호버는 얇고 높게 |
| `SFX_Core_Hum_Loop` (6초, 스테레오) | 55Hz 드론(비트 2회/T) + 크리스탈 부분음 트윙클(좌우 교차) + 핑크 숨결 + 6.2kHz 마력 지잉 |
| `SFX_Env_DesertWind_Loop` (12초, 스테레오) | 갈색 잡음 저역 몸통 + 핑크 중역 모래 질감 + 고Q 휘파람 + 모래알, 돌풍 2회가 레벨과 컷오프를 함께 연다 |
| `SFX_Player_LevelUp_01` (2.4초, 스테레오) | A4 C#5 E5 A5 종 아르페지오 → 화음 스웰 + 스파클 상승 + 링 whoosh + 110→220Hz 서브 + 1.3초 잔향 |

크리스탈 부분음 비율(`CrystalRatios`)은 마력 계열 전체가 공유한다. 톤을 바꾸려면 이 표 하나만 고친다.

## 아직 없는 것

- 외부 생성(ElevenLabs) 항목의 프롬프트 표와 배치 스크립트, ffmpeg 레이어 결합·라우드니스 정규화
- 게임 코드 연결(AudioSource·믹서 그룹·이벤트 리스너). 지금은 파일만 있다
- 음성(기합·비명·보스 대사)

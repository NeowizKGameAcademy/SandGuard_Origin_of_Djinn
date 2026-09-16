# 오디오

효과음 제안 목록은 `Docs/plan/SandGuard_SFX_제안목록_v0.1.md`.
마력·크리스탈·UI처럼 세계관 안에서 인공적인 소리는 **코드로 합성**하고, 재질이 실재하는 소리(발소리·갑옷·돌·불)는 외부 생성기(ElevenLabs)로 뽑는다. 사람 목소리는 음성 모델을 따로 쓴다.

## 담당자 작업 흐름 (인스펙터만으로)

1. WAV를 `Assets/8.Audio/Generated/`에 넣는다. 이름 규칙 `SFX_<분류>_<이름>_<번호>.wav`를 지키면 `SandGuard > Audio > Build Cues From WAVs`가 **빈 큐만** 채워 준다. 안 지켜도 큐에 직접 드래그하면 된다.
2. `Assets/8.Audio/Cues/<분류>/<큐>.asset`을 열어 클립을 바꾸거나 변형을 추가하고, 볼륨·피치 지터·거리·동시 재생 한도를 조정한다. **큐가 편집의 유일한 지점**이다. 이 큐를 참조하는 모든 자리가 같이 바뀐다.
3. 분류 간 균형은 `Assets/8.Audio/SandGuard.mixer`(Master > SFX / Environment / Music / UI / Voice)에서 맞춘다. 큐 볼륨은 "그 소리의 기본 크기", 믹서는 "분류 간 균형"이다.

빌더는 이미 값이 있는 큐 필드를 절대 덮어쓰지 않는다. 프리팹·씬을 다시 만들었으면 `SandGuard > Audio > Wire Audio Into Prefabs And Scenes`만 다시 실행한다 (멱등).

## 런타임 구조 (`Runtime/`, asmdef `SandGuard.Audio.Runtime`)

| 층 | 파일 | 역할 |
|---|---|---|
| 자산 | `SfxCue` | 클립 변형, 볼륨·피치 지터, 2D/3D·거리·감쇠, 믹서 그룹, 루프, 동시 보이스 한도, 최소 간격 |
| 재생 | `SfxPlayer` | 씬마다 하나(자동 생성). `Play(cue, pos)` 3D 원샷 / `PlayAttached(cue, t)` 대상 부착 / `PlayLoop(cue, t)`+`Stop(voice)` 루프 / `Play2D(cue)` UI·화면. 같은 프레임 중복 합치기, 최소 간격, 큐별 한도(초과 시 가장 오래된 보이스 재사용), 직전 변형 회피 |
| 보이스 | `SfxVoice` | `PrefabPool`에서 빌리는 AudioSource. 원샷은 클립 길이 뒤 스스로 끝나고, 따라가던 대상이 꺼지면 함께 정리. 반납은 `SfxPlayer.LateUpdate`가 모아서 한다(부모의 활성/비활성 콜백 안에서 부모를 바꾸면 Unity 오류) |
| 연결 | `SfxOneShot` | UnityEvent `Fire()`. `PlayerVisuals.onFired`, `PlayerProgression.onLevelUp` 같은 기존 이벤트에 꽂는다. `VfxOneShot`의 소리판 |
| 연결 | `SfxEmitter` | 켜지면 나고 꺼지면 멈춘다. VFX 프리팹 자식(횃불·코어·폭풍·화염), 씬 배치 3D 루프, 2D 베드(바람) 모두 이것 하나 |
| 연결 | `SfxHitReaction` | `IDamageEvents.Damaged` → HitCue, `ILifeState.Died` → DeathCue. `VfxHitReaction`의 소리판 |
| 연결 | `SfxUiButton` | uGUI Button의 호버·클릭. 다른 스크립트에 의존하지 않는다 |

풀링은 3D 원샷·루프 보이스에만 쓴다. 2D는 `SfxPlayer`의 고정 소스 4개에 `PlayOneShot`. 준비 단계에 `SfxPlayer.Prewarm(n)`으로 보이스를 미리 만들 수 있다.

의존 방향: 오디오 런타임은 `DesertTower.VFX.Runtime`(풀), `Gameplay.Abstractions`, `Combat.Runtime`, `UnityEngine.UI`만 참조한다. Player·Enemy는 오디오를 모르고 UnityEvent와 인터페이스로만 만난다.

## 큐 목록과 자리표시 클립

제안 목록의 모든 소리(89개)가 `Editor/SfxCatalog.cs`에 이름·기본값·자리표시 종류로 정의되어 있고, `Assets/8.Audio/Cues/<분류>/`에 큐 에셋으로 존재한다.
클립이 없는 큐에는 `Assets/8.Audio/Placeholder/`의 **자리표시 클립**(코드 합성, 종류별로 성격이 다르고 이름 해시로 피치가 달라 어느 큐가 울렸는지 구분됨)이 들어 있다. 담당자가 할 일은 큐의 클립을 진짜 소리로 바꾸는 것뿐이다.

| 자리표시 종류 | 성격 | 예 |
|---|---|---|
| Impact | 클릭 + 잡음 붐 | 피격·착탄·폭발·붕괴 |
| Whoosh | 대역통과 잡음 스윕 | 휘두름·대시·건설 연막 |
| Zap | 톱니파 하강 + 부분음 | 마나탄·빔 발사 |
| Ping | 두 부분음 | UI·차임 |
| Tick | 짧은 잡음 | 발소리·카운트다운·거절 |
| Loop | 4초 잡음 루프 | 앰비언스·화염·필드 |
| Drone | 9.6초 100BPM 드론 | 음악 |
| Sting | 상승 4음 + 잔향 | 레벨업·클리어·승리 |
| Voice | 톱니파 + 포먼트 | 기합·비명 (음성 모델로 교체) |

폴더 우선순위는 `Generated` > `Synth` > `Placeholder`다. 담당자가 `Generated/SFX_Enemy_Hit_01.wav`를 넣고 큐 `Enemy_Hit`의 클립 목록을 비운 뒤 `Build Cues From WAVs`를 실행하면 자리표시 대신 그 파일이 들어간다. 큐에 직접 드래그해도 된다.

## 현재 배선 (2026-09-16, `Editor/AudioWiring.cs`)

| 자리 | 컴포넌트 | 큐 |
|---|---|---|
| `Player.prefab` 자식 `Sfx` | `SfxOneShot` ← `onFired` / `onLevelUp` / `onDashStarted` / `onLanded` / `onHardLanded` / `onRespawned` | ManaBolt_Fire, LevelUp, Dash, Land, HardLand, Revive |
| `Player.prefab` | `SfxHitReaction`, `SfxFootsteps`(CharacterController 접지, 보폭 0.75) | Player_Hit / Player_Death, Player_Footstep_Sand |
| `Player.prefab` 자식 `Sfx` | `SfxLoopToggle` ← `PlayerUpdraft.onChargeStarted/onCharging/onChargeCancelled/onLaunched` | Player_Updraft_Charge_Loop (세기에 따라 피치 상승) |
| `Enemy.prefab` + 변형 5종 | `SfxOneShot` ← `EnemyVisuals.onAttack` / `onDied`, `SfxHitReaction`, `SfxFootsteps` | 변형별 휘두름·사망 음성·발소리(경/중)·보폭 오버라이드 |
| `Chief_Bomb.prefab`, `ExperienceOrb.prefab`, `Tower_Obelisk.prefab` | `SfxEmitter` | 도화선 루프, 경험치 드롭, 오벨리스크 필드 |
| VFX 프리팹 32종 (`VfxTable`) | 자식 `Sfx`의 `SfxEmitter` (항상 켜진 VFX는 `WhileParticlesEmit`) | 착탄·폭발·건설·화염·방패·소환·벽·코어 파괴·웨이브 클리어·횃불 등 |
| `Level.unity` 코어 | `SfxEmitter` + `SfxOneShot` ← `CoreReceiver.onChanged` | Core_Hum_Loop, Core_Hit |
| `Level.unity` 폭풍(`StormLightning`) | `SfxEmitter` + `SfxOneShot` ← `onStrike` | Env_StormWall_Loop, Env_Lightning |
| `Level.unity` `Sfx Ambience` / `Sfx Music` | `SfxEmitter` / `MusicDirector` | Env_DesertWind_Loop / 준비·전투·승리·패배·위험 레이어·웨이브 클리어 스팅어 |
| `MainScene.unity` | 버튼 `SfxUiButton`, `Sfx Ambience`, `Sfx Music`(MenuMode) | UI_Hover/Click, Env_Menu_Ambience_Loop, Music_Menu_Loop |

`DesertTower > VFX > Build All`이 끝날 때 `AudioWiring.WireVfxPrefabs()`를 불러 VFX 프리팹을 다시 만들어도 이미터가 유지된다.

이벤트로 내는 소리는 VFX에 다시 붙이지 않는다(대시·착지·발사·피격·레벨업). 그래서 한 동작에 소리가 두 번 나지 않는다.

배치 설정·검증: `Unity.exe -batchmode -nographics -projectPath . -executeMethod SandGuard.Audio.Editor.AudioWiring.All -quit` → 로그의 `AUDIO_WIRED` 줄.
테스트: `-runTests -testPlatform PlayMode -assemblyNames SandGuard.Audio.Tests` (오디오 장치 없이 돈다. 14개).

### 아직 훅이 없어 큐만 있는 소리

`Player_Voice_*`(시전·점프 음성), `Player_ManaBolt_Flight_Loop`(투사체 프리팹에는 붙어 있음), `Player_SandShackle` 풀림, `Player_Xp_Pickup`(획득 이벤트 없음), 흔적 귀환, 적 낙하(`EnemyFall` C# 이벤트), 코브라 회전, 시설 켜기/끄기, 해골 발소리, `Core_Warning`, `Wave_Start`, `Wave_Countdown_Tick`, `UI_Menu_Open/Close`·`UI_Fail`·`UI_Skill_*`·`UI_Cooldown_Ready`·`UI_Health_Low_Loop`(HUD·건설 메뉴·스킬트리는 Assembly-CSharp라 인스펙터에서 `SfxOneShot.Fire`를 직접 연결), `Env_Torch_Loop`(Level에 횃불 미배치), `Env_Altar_Loop`, `Music_Boss_*`(`MusicDirector.BossAppeared()`를 부르는 쪽이 필요).

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

| 파일 | 역할 |
|---|---|
| `Dsp.cs` | 잡음(백색·핑크·갈색·알갱이), 발진기·스윕·부분음, 포락선, RBJ 바이쿼드(스윕 가능), 포화, Schroeder 잔향, 에코, 루프 크로스페이드, 주기 LFO |
| `SynthRecipes.cs` | 소리 하나 = 함수 하나. `All()`이 굽는 목록 |
| `AudioAnalysis.cs` | 지표 측정, FFT, 스펙트로그램 PNG |
| `WavWriter.cs` | 16비트 PCM WAV |
| `SynthSfxBaker.cs` | 메뉴·배치 진입점, 임포트 설정, 보고서 |

루프를 이음매 없이 만드는 두 가지 방법: 주기 층(드론·부분음·LFO)은 모든 주파수와 LFO 속도를 `1/T`의 정수배로 잡는다(`Dsp.LoopFreq`, `Dsp.PeriodicLfo`). 잡음 층은 `T + X`초를 렌더한 뒤 `Dsp.LoopCrossfade`로 꼬리를 머리에 크로스페이드한다.

1단계 레시피 5종(마나탄 발사 ×4, UI 클릭 ×3·호버, 코어 험 6초, 사막 바람 12초, 레벨업)의 설계는 `SynthRecipes.cs` 주석에 있다. 청취 피드백(2026-09-16): 마나탄이 "알루미늄 캔"처럼 들림 → 다음 베이크에서 기본음을 낮추고 감쇠를 늘리고 바디 비중을 올릴 것.

## 아직 없는 것

- 적·시설·벽·코어의 `SfxHitReaction` 배선(큐가 아직 없어 넣지 않음), 애니메이션 이벤트 발소리, 환경 이미터(횃불·폭풍 벽·번개), `MusicDirector`
- 외부 생성(ElevenLabs) 항목의 프롬프트 표와 배치 스크립트, ffmpeg 레이어 결합
- 음성(기합·비명·보스 대사)

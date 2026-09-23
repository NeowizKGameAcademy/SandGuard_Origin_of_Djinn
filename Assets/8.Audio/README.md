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

## 애니메이션·연출 동기 (2026-09-17)

소리를 "시작 시점에 한 번"이 아니라 애니메이션의 실제 프레임·주기·단계에 맞추는 장치. 전부 `AudioWiring`이 꽂는다.

| 장치 | 어디에 | 무엇을 |
|---|---|---|
| `SfxAnimatorLoop` | 코어 프리팹의 Circle Effect(Animator, Ground 2초 루프). Level은 `New Core.prefab`(레이어 7 "Circle"), 구형 `Core.prefab`은 레이어 0 | 루프가 감길 때마다 `Core_Ping`. `normalizedTime` 정수부로 판정하므로 속도·일시정지와 어긋나지 않는다. 배선은 "Ground 상태를 가진 애니메이터"를 찾아 걸므로 코어 프리팹을 바꿔도 다시 실행만 하면 된다 |
| `SfxAnimatorState` | `Player.prefab`의 Character(Animator) | `Falling` 상태 동안 `Player_Fall_Loop` |
| 음성(기합·신음) | `Player.prefab` `Sfx`의 추가 `SfxOneShot`, `SfxHitReaction`의 `HitVoiceCue/DeathVoiceCue` | 동작음과 **같은 이벤트**에 한 번 더: 점프·공중 점프 → `Player_Voice_Jump`, 대시 → `Player_Voice_Dash`, 기본 공격·Q/E/R(`PlayerSkillCaster.onCast`) → `Player_Voice_Cast`, 하드랜딩 → `Player_Voice_HardLand`, 피격·사망 → `Player_Voice_Hit/Death`. 큐의 **Chance**(재생 확률: 시전 0.2, 점프 0.3, 대시 0.4, 나머지 1)로 빈도를 조절한다 |
| `SfxAnimationEvents` | Animator와 같은 오브젝트: Player Character, 적 5종 `<이름>_CombatVisual.prefab` | 클립 이벤트 `Swing` / `Impact` / `BodyFall` / `Footstep` / `ReleaseChiefBomb`(족장 던지기)를 받아 큐를 낸다 |
| `ClipEventBuilder` (에디터) | `<이름>_Attack.fbx`, `<이름>_Death.fbx`, 플레이어 `Death.fbx` | Swing = windup − 0.15초, Impact = `EnemyMeleeAttack.windup`(변형별), BodyFall = 클립을 샘플링해 엉덩이 뼈가 바닥에 닿는 시각. 임포터 이벤트는 0~1 정규화 |
| `SfxFootsteps` FootBones 모드 | Player·Enemy 루트 | LeftFoot/RightFoot 뼈 높이로 접지 순간 감지(들림 8cm → 접지 3cm, 외형 배율에 비례). 블렌드 트리와 무관하게 애니메이션과 맞는다. 이동 속도 조건으로 제자리 발 구르기는 무시 |
| `SfxEmitter` BeginCue/EndCue | 코브라 화염(점화·소화), 모래 족쇄(풀림), 소용돌이·폭풍(잦아듦) | 파티클 방출 시작·정지 순간에 한 번씩 |
| `SfxTimeline` | `VFX_Core_Destruction`(0초 충전 → 0.2초 폭발 → 1.1초 파편), `VFX_Cobra_Destruction`(0.08초 붕괴 → 0.9초 파편) | 코드 상수 단계에 맞춘 지연 목록 |

주의: `EnemyCombatArtBuilder`(Connect Combat Art)나 플레이어 팩 빌더가 클립을 다시 임포트하면 이벤트가 사라진다. 그 뒤 `SandGuard > Audio > Wire Enemy Clip Events Only`(적) 또는 `Wire Audio Into Prefabs And Scenes`(전체)를 다시 실행한다.

측정된 시각(초): Swordsman Swing 0.35 / Impact 0.50 / BodyFall 1.57 · Assassin 0.15 / 0.30 / 1.57 · ShieldGuard 0.35 / 0.50 / 3.13 · HammerBrute 0.78 / 0.93 / 1.57 · Chief 0.45 / 0.60 / 2.10 · Player Death BodyFall 1.63.

## 담당자 클립 (`Assets/8.Audio/AudioResource`)

큐의 클립은 담당자가 인스펙터에서 넣는다. `Editor/AudioResourceMap.cs`의 대응표는 **비어 있는 큐만** 채우고(같은 소리의 원본과 `_auda`가 있으면 `_auda`), 코드 합성·자리표시 클립(`SFX_*`)은 큐에서 제거한다. 결과와 남은 빈 큐 목록은 `Docs/Audio/audio-resource-map.md`.
합성·자리표시 WAV(`Synth/`, `Placeholder/`)는 더 이상 큐에 들어가지 않는다. 필요하면 인스펙터에서 직접 드래그한다.

2026-09-18 웨이브 시작음: `Wave_Start` 큐는 클립만 있고 재생하는 곳이 없었다. `MusicDirector.WaveStart`가 WaveDirector 상태 Preparing → Running(준비 시간이 끝나 스폰 시작)마다 한 번 낸다(2D). 확인: `LevelAudioSmokeTests.WaveStartCuePlaysWhenWaveBegins`(Level 재생, 그래픽 장치 필요).

2026-09-18 클립 점검·수정: 점검 스크립트(길이·클리핑·직류·앞뒤 무음·끝 끊김·루프 이음매·변형 크기 차·3D 스테레오·임포트 설정) 결과는 `Docs/Audio/cue-clip-audit.md`. 수정본은 원본을 보존한 채 `AudioResource/Repaired/`에 두고 `Docs/Audio/repair-2026-09-18/manifest.json`에 기록한다(1차는 다른 작업, 2차는 `second_pass`). 연결은 `AudioResourceMap.Repair2`(메뉴 `Apply Repair 2026-09-18 (second pass)`). 3D 큐에서만 쓰는 클립은 Force To Mono(`ForceMonoFor3D`). 교체 표에서 빈 배열로 비운 큐는 자동 채우기가 다시 채우지 않는다(마지막 표 기준). 음악 루프는 `MusicDirector.SongEndCrossfade`(기본 3초)로 곡 끝 전에 처음부터 겹쳐 넘긴다.

2026-09-18 오후(`Overrides0918b`, 배치 `AudioWiring.Update0918b`): 흔적 귀환은 출발(`VFX_Recall_Depart`)=워프, 도착(`VFX_Recall_Arrive`)=워프 완료로 나눔. 관통탄 충전은 루프 대신 충전 시작 원샷(짧은 충전음 1.2s, 충전 0.9s). 레벨업은 차임 + 팡파레(`Player_LevelUp_Fanfare`)를 겹침. 코어 흡수(`VFX_Core_Damage_Enemy`)에 호로록 6조각(`AudioResource/호로록_01~06`, ffmpeg로 분할). 음악: 전투=웨이브테마곡, 보스=보스테마곡(`MusicDirector`가 `EnemyBossInfo.Active`로 전환), 승리=승리음악, 패배=게임오버. 30초 넘는 음악·앰비언스는 Streaming 임포트. 분석은 Python/librosa·ffmpeg(경로는 메모리 `audio-analysis-tools`).

2026-09-18: `Docs/Audio`의 추가 파일(정리본 5개, 원본 8개, kenney 임팩트·발소리 130개 → `AudioResource/kenney/`)을 복사하고, `AudioResourceMap.Overrides` 표로 이미 채워진 큐도 더 맞는 클립으로 한 번 교체했다(메뉴 `Apply Overrides 2026-09-18`, 자동 빌드에는 포함되지 않음). 원칙: 같은 소리는 `_auda` 정리본, 변형은 모두 넣기, 자리와 다른 소리는 바로잡거나 비우기, 한 동작에 같은 클립이 두 번 나는 구성은 한쪽만 남기기.

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
| `Player.prefab` 자식 `Sfx` | `SfxOneShot` ← `onFired` / `onJumped` / `onAirJumped` / `onLevelUp` / `onDashStarted` / `onLanded` / `onHardLanded` / `onRespawned` | ManaBolt_Fire, Jump, AirJump, LevelUp, Dash, Land, HardLand, Revive |
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

이벤트로 내는 소리는 VFX에 다시 붙이지 않는다(점프·대시·착지·발사·피격·레벨업). 그래서 한 동작에 소리가 두 번 나지 않는다. 점프 소리는 `PlayerMotor.Jumped` → `PlayerVisuals.onJumped/onAirJumped`에서 내며, 이동 VFX의 속도 감지에 의존하지 않는다.

배치 설정·검증: `Unity.exe -batchmode -nographics -projectPath . -executeMethod SandGuard.Audio.Editor.AudioWiring.All -quit` → 로그의 `AUDIO_WIRED` 줄.
테스트: `-runTests -testPlatform PlayMode -assemblyNames SandGuard.Audio.Tests` (23개. `LevelAudioSmokeTests`는 Level.unity를 실제로 재생해 코어 핑 발화를 확인하며 약 4분 걸린다). **`-nographics`를 빼고** 돌린다. Unity는 그래픽 장치가 없으면 애니메이션 클립 이벤트를 보내지 않아 `AnimationEventsComponentReceivesClipEvent`가 실패한다. 테스트용 애니메이터는 `Tests/Resources/SfxTestAssets.asset`(Setup Everything이 생성).

### 남은 훅 연결 (2026-09-18, `Editor/AudioHookWiring.cs`)

| 소리 | 훅 | 클립 |
|---|---|---|
| 스킬트리 열림·닫힘 | `SkillTreeWindow.onOpened/onClosed` | 케니 generic light (열림 ×1.15, 닫힘 ×0.9) |
| 스킬 구매 / 장착·해제 / 거부 | `SkillTreeWindow.onLearned/onEquipped/onFailed` (새 이벤트) | 종 3종 ×1.2 / 금속 딸깍 2종 / 마나부족 거절음 |
| 건설 메뉴 열림·닫힘 / 건설 거부 | Level 루트 `Sfx Hooks`의 `SfxBuildMenuWatcher`가 `FacilityBuildMenu.IsOpen`·`LastResult`를 지켜본다. 건설 쪽은 담당자가 통합 중이라 그 스크립트·프리팹은 고치지 않는다 | 위와 같음 |
| 타워 수리 성공 | 같은 `SfxBuildMenuWatcher`가 수리 모드에서 1을 누른 프레임에 `LastRepairResult`가 성공이면 받침대 위치에서 3D로 `Facility_Repair`를 낸다. 연결: 메뉴 `SandGuard > Audio > Wire Tower Repair Sound` | `타워수리_쇠깡깡.wav` (Docs/Audio/쇠깡깡소리 1.wav, 원본이 클리핑돼 볼륨 0.6) |
| 스킬 시전 거부(쿨다운·마나) | `PlayerSkillCaster.onCastFailed` (새 이벤트) | 거절음, 0.3초 최소 간격 |
| 쿨다운 완료 | `SfxCooldownReady` (Q/E/R·관통탄·흔적 귀환, 1.5초 미만 쿨다운과 대시는 제외) | 유리 울림 2종 ×1.5 |
| 체력 낮음 | `SfxLowHealthLoop` (20% 아래에서 켜고 25% 위에서 끔, 사망 시 끔) | `Repaired/Heartbeat_Loop.wav` (케니 soft impact로 만든 80BPM 박동) |
| 코어 경고 | `CoreReceiver.onChanged` (코어가 공격당할 때) | 코어핑 원본 ×0.75, 4초 최소 간격 |
| 제단 루프 | `New Core.prefab`의 `SkillAltarAnchor` 자식 이미터 | 비움 — 제단이 코어 자체라 코어 험과 겹침. 맞는 루프 파일이 생기면 큐에만 넣으면 된다 |

카운트다운 틱(`Wave_Countdown_Tick`)은 게임에 카운트다운이 없어 큐와 카탈로그에서 뺐다.
`FacilityPlayModeTests` 5개는 이 작업 전에도 실패한다. 건설 쪽 통합이 아직 끝나지 않았기 때문이다(2026-09-18 사용자 확인).

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

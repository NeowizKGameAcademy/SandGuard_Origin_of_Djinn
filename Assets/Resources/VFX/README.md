# VFX

코브라 타워 파괴는 [CobraDestruction.md](CobraDestruction.md)를 참고한다. Showcase NEW 10에 배치되어 있다.

코어 파괴는 [CoreDestruction.md](CoreDestruction.md)를 참고한다. Core Base 원본 형상을 사용하며 Showcase NEW 09에서 확인할 수 있다.

UI 없이 제작한 방패 방어·시설/플레이어 피격·철거 폭발·시설 정지·코어 피격·경험치·레벨 업 8종은 [RequestedSet.md](RequestedSet.md)에 프리팹 경로와 연결 방법을 정리했다.

코드로 생성하는 파티클 프리팹 모음. 근거 문서: `Docs/plan/SandGuard_VFX_제작계획_v0.2.md`.
폴더는 `Assets/Resources/VFX`이고 빌더가 빌려 쓰는 팩도 `Assets/Resources/` 아래에 있다 (`VfxBuildKit`의 경로 상수).
프리팹은 손으로 고치지 말고 `Editor/` 빌더를 수정한 뒤 다시 빌드한다(GUID 유지, 덮어쓰기 안전).

## 빌드

Unity 메뉴 `DesertTower > VFX > Build All`, 또는 배치:

```
Unity.exe -batchmode -projectPath . -executeMethod DesertTower.VFX.Editor.VfxBatch.BuildAll -quit
```

`BuildAllAndRender`로 바꾸면 `Temp/VfxPreview/`(또는 env `VFX_PREVIEW_DIR`)에 프레임 PNG를 함께 뽑는다. 렌더는 `-nographics` 없이 실행해야 한다.

## 눈으로 확인하기

- **에디터에서 바로**: 프리팹을 씬에 놓고 루트를 선택하면 Particle Effect 오버레이의 Play가 자식 레이어까지 재생한다(루트마다 빈 허브 ParticleSystem이 있다). 단, 스크립트가 움직이는 부분(경험치 흡수, 코어 안정도, 큐브 회전, 라이트 깜빡임, 시설 솟아오름)은 에디트 모드에서 안 돈다.
- **전부 한 번에**: `DesertTower > VFX > Showcase > Open Showcase Scene + Play`. `Scenes/VFX_Showcase.unity`에 8개 효과가 스테이션별로 놓여 있고 `VfxShowcaseStation`이 반복 재생한다 — 투사체는 날아가서 착탄하고, 적 사망은 매번 다른 색으로, 코어는 안정도 100→0→100을 오간다. 씬은 `Build All`이 다시 만든다.

## 프리팹

| 프리팹 | 계획서 | 원점 | 런타임 연결 |
|---|---|---|---|
| `VFX_ManaBolt_Projectile` | #1 | 투사체 위치, 로컬 +Z가 진행 방향 | 투사체에 자식으로 붙인다. 총구 플래시는 스폰 시 1회 자동 재생 |
| `VFX_ManaBolt_Impact` | #2 | 착탄점 | 없음 |
| `VFX_Enemy_Death` | #23 | 적 발밑 | `VfxTint.Apply(적 색)` · `VfxParticleAttractor.Target = 플레이어` |
| `VFX_Experience_Mote` | 경험치 드롭 외형 | 바닥 기준, 큐브 중심 y=0.35 | 유지형 금빛 큐브. 생성·획득·경험치 지급은 게임플레이에서 연결 |
| `VFX_Build_Complete` | #14 | 슬롯 중심(바닥) | 시설 오브젝트에 `VfxScaleIn`을 붙이면 0→1 솟아오름 |
| `VFX_Core_Ambient` | #9 | 코어 바닥 (크리스탈 y=1.2) | `CoreAmbientVfx.SetStability(0~100)` — 60/30 경계에서 틸→노랑→빨강+연기 |
| `VFX_FlameCobra_Breath` | #17 | 코브라 입, 로컬 +Z가 분사 방향 | 루프. 공격 시작/끝에 Play/Stop |
| `VFX_Fire_Impact` | #17 | 화염 착탄점 | 없음 |
| `VFX_Burning_Loop` | #17 | 불붙은 적 발밑 | 2초 원샷. 라이트는 남으니 3초 뒤 Destroy |
| `VFX_Pierce_Beam` | #3 | 시전자, 로컬 +Z가 발사 방향 | `PlayerBasicAttack.beamPrefab` (관통탄). 총구에 놓고 자식 `VfxBeam.SetLength`를 실제 도달 거리로. 꿰뚫은 적마다 `beamHitPrefab` = `VFX_ManaBolt_Impact` |
| `VFX_Sand_Burst` | #4 | 착탄점(바닥) | `PlayerBasicAttack.burstPrefab` (모래 폭발·폭발 관통탄). 카메라 킥은 `PlayerCameraRig.Kick`. 히트스톱 없음 |
| `VFX_Sand_Root` | 모래 족쇄 | 적 발밑, 적의 자식으로 | `EnemyRestraint.vfxPrefab`. 묶일 때 생성, 풀릴 때 `VfxSandRoot.Release` |
| `VFX_Sand_Vortex` | ⑮ 모래 소용돌이 | 지면 중심, 반경 1m 기준 루프 | `PlayerSkillCaster.vortexPrefab`. 지역 오브젝트의 자식으로 두고 실제 반경으로 스케일(Hierarchy). 끝나면 방출만 멈춘다 |
| `VFX_Sand_Storm` | ⑯ 사막 폭풍 | 지면 중심, 반경 1m 기준 루프 | `PlayerSkillCaster.stormPrefab`. 위와 같다 |
| `VFX_LevelUp` | #7 | 플레이어 발밑 | 없음 |
| `VFX_Mana_Charge` | #10 | 코어 바닥 | 루프. `VfxParticleAttractor.Target = 램프`. 충전 완료 시 Stop 후 램프에 `VFX_Mana_Charge_Complete` |
| `VFX_Mana_Charge_Complete` | #10 | 램프 | 없음 |
| `VFX_Core_Damage_Enemy` | #11 | 도착한 적 발밑 | `VfxParticleAttractor.Target = 코어`. 코어에는 `VFX_Core_Damage_Flash` |
| `VFX_Core_Damage_Flash` | #11 | 코어 바닥 (플래시는 y=1.2) | 없음 |
| `VFX_Summon_Circle` | #18 | 마법진 바닥 | 루프 |
| `VFX_Summon_Pillar` | #19 | 소환 지점 바닥 | 0.6초 원샷. 소환수 디졸브 인은 캐릭터 메시 이후 |
| `VFX_Enemy_Spawn` | #22 | 입구 바닥 | 없음 |
| `VFX_Wave_Clear` | #27 | 코어 바닥 | 없음 |
| `VFX_Torch` | #36 | 횃불 머리 | 루프. 프리팹 1개 반복 배치 |
| `VFX_Floating_Dust` | #38 | 레벨 중심 | 루프. 20×6×20 상자 안에서 부유. 씬에 1개 |
| `VFX_Player_Hit` | #8 | 적중점, +Z가 표면 바깥 | `VfxHitReaction.HitPrefab` |
| `VFX_Player_Hit_Screen` | #8 | 위치 무관 (오버레이 캔버스) | `VfxHitReaction.ScreenPrefab`. 가장자리 빨강 비네트 0.35s |
| `VFX_Enemy_Hit` | #8 | 적중점 | `VfxHitReaction.HitPrefab`. 조각 색은 `VfxTint` |
| `VFX_Staff_Cast` | #1 | 지팡이 끝, 로컬 +Z가 발사 방향 | `VfxOneShot.Fire()` ← `PlayerVisuals.onFired` |
| `VFX_Jump_Dust` · `VFX_Land_Dust` | — | 발밑 | `VfxCharacterMovement` (CharacterController 접지 변화로 자동) |
| `VFX_AirJump_Ring` | #6 | 공중의 발 위치 | `VfxCharacterMovement.AirJumpPrefab` |
| `VFX_Footstep_Sand` | #24 | 발 위치에 자식으로 | 루프. Rate over Distance. 붙어 있는 `VfxFootstepDust`가 속도·접지로 켜고 끈다 |
| `VFX_Wall_Hit` | — | 적중점, +Z가 표면 법선 | `VfxHitReaction.HitPrefab` (벽·타워·코어) |
| `VFX_Updraft_Launch` | ③ 상승 기류 | 플레이어 발밑 | `VfxOneShot.Fire()` ← `PlayerUpdraft.onLaunched` (Player.prefab의 `UpdraftVfx` 자식). 링 3.4m 두 겹 + 파편 + 큰 먼지 + 위로 솟는 모래 기둥. `VfxCharacterMovement.MaxJumpVelocity`=12로 발사 때 점프 먼지는 안 낸다 |
| `VFX_Updraft_Charge` | ③ 상승 기류 | 몸 중심(허리 높이)에 자식으로 | 루프. `VfxChargeLoop`: `Begin()` ← `PlayerUpdraft.onChargeStarted`, `SetIntensity(충전량)` ← `onCharging`, `End()` ← `onChargeCancelled`/`onLaunched`. 몸을 도는 모래 줄기 + 빨려드는 알갱이 + 발밑 잔먼지, 세기에 따라 방출·속도·크기 증가 |
| `VFX_Updraft_Lens` | ③ 상승 기류 | 몸 중심(허리 높이)에 자식으로 | `VfxRefractionBubble`: `Begin` ← `onChargeStarted`, `SetIntensity` ← `onCharging`, `End` ← `onChargeCancelled`, `Pulse` ← `onLaunched`. `Shaders/VFX_ScreenRefraction`(URP Opaque Texture 굴절) 구체가 충전으로 부풀고 발사 때 2.8m로 터지며 사라진다 |
| `VFX_Wall_Destroy` | — | 경계 상자 바닥 중심 | `VfxHitReaction.DeathPrefab` + `FitDeathToBounds` → `VfxVolume.Fit(bounds)`가 벽 크기로 늘린다 |

## 전투 연결 컴포넌트 (`Runtime/`)

| 컴포넌트 | 역할 |
|---|---|
| `VfxHitReaction` | `IDamageEvents.Damaged` → 피격 프리팹·흰색 플래시(`_BaseColor` MPB)·움찔·화면 프리팹·카메라 셰이크. `ILifeState.Died` → 사망 프리팹(크기 맞춤·틴트·흡수 대상). 적·플레이어·벽·코어 공용 |
| `VfxCameraShake` | `VfxCameraShake.Shake(강도, 시간)`. Camera.main에 자동 부착. 카메라 리그가 매 프레임 위치를 다시 써도 동작 |
| `VfxCharacterMovement` | CharacterController의 접지·속도 변화로 점프·공중 점프·착지 프리팹 스폰 |
| `VfxFootstepDust` | 발자국 루프의 방출을 수평 속도·접지 레이캐스트로 켜고 끔 |
| `VfxOneShot` | UnityEvent용 1회 스폰기 (`Fire()`), 기준 Transform의 위치·방향 사용 |
| `VfxVolume` | 부피형 이펙트를 경계 상자 크기에 맞추고 파편 수를 부피 비율로 늘림 |
| `VfxAfterimage` | 대시 잔상. `Play(대시 시간)`마다 메시 스냅샷을 틸 실루엣으로 남김. 플레이어 대시에서는 뺐다(눈이 아픔). 필요하면 UnityEvent에 다시 꽂는다 |
| `VfxChargeLoop` | 충전형 루프 제어기. `Begin`/`SetIntensity(0~1)`/`End`로 방출량·속도·크기 배수를 올리고 내린다 (미리보기용 Play On Awake는 시작 시 멈춤) |
| `VfxRefractionBubble` | 볼록 렌즈 구체 제어기. `Begin`/`SetIntensity`/`End`/`Pulse`. 대기 중엔 렌더러를 꺼 둔다. 셰이더 `_Strength`는 MaterialPropertyBlock |
| `VfxFadeOut` | CanvasGroup 페이드 후 제거 (비네트) |

`DesertTower > VFX > Wire Combat VFX Into Demo Assets`가 위 컴포넌트를 `Assets/Enemy/Generated/Enemy.prefab`, `Assets/Player/Generated/Player.prefab`, `EnemyTest.unity`의 표적에 꽂는다. 여러 번 실행해도 안전하다. 데모 프리팹을 다시 생성했으면 다시 실행한다.

임팩트형(플래시·코어·링·파편·불씨·먼지 + 라이트)은 전부 `ImpactVfxBuilder`의 `ImpactSpec` 하나로 정의된다.
새 임팩트가 필요하면 스펙을 하나 더 만들고 `ImpactVfxBuilder.All()`에 추가하면 `Build All`·프리뷰가 같이 돈다.
빔 계열은 Hovl 원기둥 + `VfxBeam`(머티리얼 프로퍼티 블록으로 텍스처 오프셋을 밀어 스크롤)이라 커스텀 셰이더가 없다.

## 빌려 쓰는 팩 에셋

빌더가 아래 에셋을 참조하므로 두 팩을 지우면 빌드가 실패한다(어떤 파일이 없는지 예외 메시지에 나온다).

| 에셋 | 용도 |
|---|---|
| Hovl `Models/Torus1.fbx` | 충격파 링(메시 파티클). bounds로 크기·축을 맞추므로 팩 단위와 무관 |
| Hovl `Models/Crystal1.fbx` | 코어 크리스탈. 높이 1유닛으로 정규화 |
| Hovl `Models/Cylinder.fbx` | 관통 빔·소환 빛기둥. 지름 1, 길이 1(z 0→1)로 정규화한 뒤 `VfxBeam`이 스케일 |
| Hovl `Textures/MagicCircle.png` | 소환진 룬 링 → `Textures/T_VFX_MagicCircle.png`로 변환 |
| CFXR `cfxr fire small anim.png` | 3×3 8프레임 화염 플립북 → `Textures/T_VFX_FireFlipbook_3x3.png`로 변환(밝기→알파, 포스터라이즈) |
| CFXR `cfxr flamme stepped.png` | 불꽃 스프라이트(1:2) → `Textures/T_VFX_Flame_Stepped.png` |
| CFXR `cfxr smoke cloud x4.png` | 코어 위험 단계 연기, 2×2 랜덤 타일. 원본을 그대로 참조 |

## 머티리얼 규칙

머티리얼은 흰색 HDR(`M_VFX_Cube_White` 2.5, `M_VFX_Glow_White` 4.0, `M_VFX_Fire_Flipbook` 3.0 …)이고 색은 파티클 Start Color × Color over Lifetime으로 정한다.
그래서 같은 머티리얼로 런타임 틴트(적 색, 코어 안정도)가 된다. `M_VFX_Cube_Teal`만 MeshRenderer용(투사체 코어) 고정 색.

## 아직 없는 것 (계획서 §5)

히트스톱, Shader Graph 4종(링·디졸브·빔·히트플래시 — 히트플래시는 현재 MPB `_BaseColor`로 대체), 오브젝트 풀 `VfxService`.
셰이크·비네트는 `VfxCameraShake`·`VFX_Player_Hit_Screen`으로 들어왔다. Feel을 도입하면 `VfxHitReaction`의 플래시·움찔·셰이크 부분만 MMF_Player 호출로 바꾸면 된다.

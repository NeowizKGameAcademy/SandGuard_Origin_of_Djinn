# Protagonist and lamp

`Generated/Player.prefab`의 PlayerVisuals에 `Art/Protagonist/ProtagonistVisual.prefab`을 연결합니다. 기존 이동/점프/대시/카메라/공격 설정을 변경하지 않습니다. 기본 Player 프리팹을 참조하는 테스트 씬에도 적용됩니다.

## 마나탄 VFX

`PlayerBolt.prefab`의 `VisualRoot`에 `VFX_ManaBolt_Projectile`을 연결합니다. 임시 구체 대신 청록색 코어·빛무리·큐브 잔상·조명·발사 플래시가 재생되고, 충돌 시 `VFX_ManaBolt_Impact`가 생성됩니다. 속도 45m/s와 판정 반경 0.08m는 유지합니다. 종료 시 코어와 조명은 끄고 월드 공간 입자는 새 방출을 멈춘 채 0.6초 동안 마저 재생한 뒤 정리합니다. 재연결 메뉴는 `SandGuard > Player > Connect Mana Bolt VFX`입니다.

## 실행

강한 착지(`Hard Landing`)는 모션 전체와 복귀 블렌드가 끝날 때까지 수평 이동을 잠급니다. 착지 순간의 관성도 제거하고 점프·대시·상승 기류를 막으며, 중력·접지·카메라·공격은 계속 처리합니다. 이동 입력으로 모션을 일찍 취소하지 않습니다. 사망·텔레포트·외형 컴포넌트 비활성화 시에는 착지 잠금을 해제하며, 일시정지 중에는 회복 시간이 진행되지 않습니다. 다른 시스템의 `Anchored`와 `MovementEnabled` 값은 덮어쓰지 않습니다.

대시는 실행 중 수직 속도를 0으로 유지하고 중력·하향 바닥 보정·CharacterController의 자동 단차 이동을 일시 중단합니다. 지상에서 절벽을 넘어가도 대시 종료 프레임까지 높이를 유지하고 다음 프레임부터 수직 속도 0에서 낙하를 시작합니다. 공중에서 대시를 시작할 수 있는지는 기존 `AirDashEffect` 해금으로 별도 제어합니다.

`SandGuard > Player > Open Character Preview` 또는 `Assets/Player/Generated/PlayerArtPreview.unity`를 열고 Play.

- WASD / 마우스: 기존 이동/시점
- 마우스 왼쪽 클릭/유지: 손바닥 마나 볼트 단발/연사
- V: 미리보기에서만 걷기(2m/s)/달리기(기존 속도) 전환
- B: 미리보기에서만 시전 3종 순환 (Magic → GreatSwordSpell → SwordShieldSpell)
- K: 미리보기에서만 10 피해 적용 (체력 0이면 사망 동작, Play 재시작으로 복구)
- G: 미리보기에서만 램프 문양 발광과 주변 조명 전환
- H: 에디터·개발 빌드의 플레이어가 있는 씬에서 마나 흡수 반짝임 재생 (마나 수치는 유지)
- Esc: 기존 커서 잠금 해제

기본 게임 씬의 입력 맵에는 새 단축키를 추가하지 않았습니다. 장비/스킬 상태에서 `PlayerLampEquipment.SetHeld(bool)`과 `SetGlowing(bool)`을 호출할 수 있습니다.

## 캐릭터

모델 및 기본 Idle 원본은 `Docs/model-art/protagonist/character-protagonist-mixamo@Idle.fbx`입니다. 각 파일의 자체 Humanoid Avatar를 만들고 Unity가 동작을 재적용합니다. Idle/Walk/Run은 Speed 0/2/5에 연결되어 있으며 루트 모션은 꺼져 있습니다. Walk/Run에는 Pro Magic Pack의 전후좌우 8개 동작을 적용했습니다. 단, 팩의 Standing 계열은 오른손을 가슴 높이로 든 시전 대기 자세라(Blender 측정: 오른손이 골반 위 0.33~0.35m, 원본 Walk/Running은 0~0.1m) 이동 중 팔을 내리도록 전진 걷기·달리기는 원본 Mixamo Walk/Running, 달리기 후진은 원본 Run Backward로 바꿨습니다. 옆걸음과 걷기 후진은 조준 중에만 나오고 오른팔은 시전 레이어가 덮으므로 팩 클립 그대로입니다. 이동 클립은 모두 Root Rotation을 Body Orientation 기준으로 가져옵니다. 원본은 정면을 보지 않기 때문입니다(Blender 어깨선 측정: Run Backward는 다른 클립과 180° 반대, Standing Run/Walk Right는 약 50~60° 우측으로 몸 전체가 돌아 있음). 그대로 두면 뒤로 쏘며 달릴 때 캐릭터가 돌아서고, 우측으로 이동하며 쏠 때 상체가 조준에서 벗어납니다. 캐릭터 기준 실제 이동 방향을 `MoveX`/`MoveZ`로 전달하므로 조준하며 옆이나 뒤로 이동할 때 해당 동작을 혼합합니다.

`Protagonist_BaseColor.jpg`는 구매한 2K GLB 내부의 JPEG를 손실 없이 추출한 것입니다. Mixamo FBX의 UV가 이 2K GLB와 일치하는 것을 Blender로 확인했습니다. 4K GLB의 UV는 달라서 해당 이미지를 무작정 대체하지 않습니다. 재질은 URP/Lit, Base Map에 2K 이미지, 금속도 0입니다.

점프 계열은 상태를 나눠 씁니다. 지상 점프는 `Jumping Up`(`MobilityAnimations/JumpUp.fbx`, 웅크림 구간을 잘라 도약 직전 프레임 14부터), 공중 추가 점프는 `Running Forward Flip`(`Flip.fbx`, 도약~착지 프레임 8~27만 약 0.6초에 재생), 하강은 `Falling Idle`(`Falling.fbx`, 반복), 착지는 `Falling To Landing`(`Landing.fbx`, 접지 직전 프레임 7부터), 낙하 속도 20m/s 이상(약 4m 이상 낙하)의 착지는 `Hard Landing`(`HardLanding.fbx`)입니다. 점프·플립 클립은 몸의 상하 이동을 루트 모션으로 빼내 캐릭터 컨트롤러의 실제 점프 높이와 겹치지 않게 하고, 낙하·착지 클립은 웅크림이 보이도록 높이를 자세에 굽습니다. 프레임 번호는 Blender로 원본의 발·골반 높이를 분석해 정했습니다.

전환 규칙: `PlayerMotor`가 `Jumped`(지상/공중은 `LastJumpWasAirJump`)와 `Landed(충돌 속도)`를 알리고 `PlayerVisuals`가 `Jump`/`DoubleJump` 트리거, `HardLand` Bool(다시 공중에 뜨면 해제), `VerticalSpeed`를 넣습니다. 점프는 정점을 지나거나 클립이 끝나면 Falling, 플립은 끝나면 Falling, 절벽에서 걸어 떨어질 때는 4m/s보다 빠르게 내려갈 때만 Falling(생성 직후 내려앉기·계단·턱에서는 나오지 않음), 접지하면 Landing 또는 Hard Landing, 회복이 끝나면(Landing 70%, Hard Landing 85%) 이동으로 돌아가며 이동 입력이 있으면 충격 구간(30%/45%) 뒤에 일찍 끊습니다. 시전 중 공중은 기존 `Air Cast`를 유지하고 착지하면 같은 Landing을 씁니다. `PlayerVisuals.onLanded`/`onHardLanded`에 착지 효과를 연결할 수 있습니다.

대시는 Blender에서 제작한 `MobilityAnimations/SandDash.fbx`를 0.3초에 맞춰 재생합니다. 상체를 앞으로 기울이고 뒷다리를 뻗는 전신 자세입니다. 원본은 `Docs/model-art/protagonist/SandDash.blend`이며 `Standing Run Forward`를 바탕으로 수정했습니다. 기존 `Dash Legs` 레이어 이름은 유지하지만 마스크는 전신을 포함하며, 시전·피격 레이어보다 아래에 배치해 공격이 상체를 덮어쓸 수 있습니다. `PlayerVisuals`가 대시 중 가중치를 올리고 끝나면 0.15초에 내립니다. 이동 거리·속도 곡선·마나·쿨다운은 기존 `PlayerMotor`가 그대로 담당합니다. Unity 메뉴 `SandGuard > Player > Connect Authored Sand Dash`로 대시만 다시 연결할 수 있습니다.

피격에는 Pro Magic Pack의 `Standing React Small From Front`를 상체에 짧게 재생해 이동을 유지합니다. 체력 0이면 `Standing React Death Backward`를 전신에 재생하고 마지막 자세를 유지합니다. 사망 상태에서는 시전·피격 레이어를 끄며 점프·대시가 사망 동작을 덮어쓰지 않습니다. 피격 레이어(Damage Reactions)의 가중치는 Hit 동작이 재생되는 동안만 1이고 끝나면 0.1초에 걸쳐 0으로 내립니다. 모션이 없는 Empty 상태라도 가중치 1이면 마스크 부위(척추·머리·팔)의 근육 값이 고정되어 이동 중 상체가 골반과 함께 막대처럼 흔들리기 때문입니다(2026-09-10 측정: 가중치 1일 때 가슴↔골반 회전 0도, 0일 때 20도).

`SandGuard > Player > Connect Jump and Dash Animations`는 `Docs/model-art/protagonist`의 다섯 원본을 `MobilityAnimations/`로 가져오고 Jump/Double Jump/Falling/Landing/Hard Landing/Dash 상태와 `Player Mobility:` 전환을 다시 만듭니다(예전 `Jump.fbx`는 삭제). 기존 Locomotion Blend Tree와 시전·피격 레이어, 모델/램프 프리팹은 보존합니다. 클립 시작 프레임과 속도는 `PlayerMobilityAnimationBuilder` 상수에서 조절합니다.

## 조준점과 카메라

`PlayerCrosshair`(Player.prefab 루트)가 실행 시 Screen Space Overlay Canvas에 십자선과 중앙 점을 만듭니다. `PlayerAimer`가 화면 중앙 광선으로 조준하므로 십자선 위치가 실제 탄착 방향입니다. 이동 속도와 공중 여부에 따라 살짝 벌어지고, 커서가 풀리거나 일시정지·사망이면 숨습니다. 색·길이·간격·퍼짐은 인스펙터에서 조절하며 씬에 이 컴포넌트가 있으면 테스트 HUD의 임시 + 표시는 그리지 않습니다.

카메라는 포트나이트식 오른쪽 어깨 너머 시점입니다(`PlayerCameraRig` distance 3.5, shoulderOffset 0.9). 몸이 화면 왼쪽 1/3에 오고 중앙 조준선을 가리지 않습니다. `SandGuard > Player > Apply Feel Tuning`이 이 값과 조준점 연결을 프리팹에 다시 적용합니다.

대시 카메라 연출은 `PlayerDashCameraFeel`(Player.prefab 루트)이 담당합니다. 시야각이 모터의 대시 속도 곡선(`dashProfile`, `PlayerMotor.DashSpeedFactor`)을 그대로 따라 출발 때 최대 4도(`widenDegrees`) 넓어졌다가 감속과 함께 돌아오며, 종료 뒤 잔여분은 0.08초 SmoothDamp로 반동 없이 붙습니다. 곡선을 바꾸면 카메라도 같은 박자로 따라옵니다. 시야각만 바꾸므로 화면 중앙 광선(조준)은 움직이지 않습니다. 몸이 먼저 튀어나가는 뒤처짐은 별도 효과가 아니라 리그의 `followSmoothTime`(0.08초)·`maxFollowLag`(0.5m)가 만듭니다. 대시 속도(초당 13m)면 피벗이 늘 `maxFollowLag`에 걸리므로 이 값이 곧 뒤처짐의 크기입니다. 좌우 대시 롤(기울기)은 3인칭에서 수평선이 기울어 이질적이라 넣지 않았습니다. 리그의 시야·거리 연출 입력은 `AddFov`·`AddDistance`로 매 프레임 넣는 합산 방식이라 상승 기류 연출과 대시 연출이 같은 프레임에 겹쳐도 서로 지우지 않습니다. 검증: `PlayerCameraFeelTests`.

## 스탯 수정자

`PlayerStats`(Player.prefab 루트)가 스킬트리 패시브와 버프가 얹을 수정자를 관리합니다. 최종값 = (기본값 + 가산 합) × (1 + 백분율 합) × (배수 곱), 0 미만은 0. 스탯은 MoveSpeed, JumpHeight, ExtraAirJumps, DashDistance, DashCooldown, DashManaCost, AttackDamage, AttackInterval, ProjectileSpeed, DamageTaken이며 기본값은 각 컴포넌트의 기존 인스펙터 필드 그대로입니다. `PlayerMotor`(MoveSpeed·JumpHeight·DashDistance·DashCooldown·DashManaCost·ExtraAirJumps 속성), `PlayerBasicAttack`(Damage·AttackInterval, 발사 시 볼트 속도), `PlayerHealth`(DamageTakenMultiplier)가 매번 `Evaluate`로 읽으므로 수정자를 넣고 빼면 즉시 반영됩니다.

사용: `stats.Add(PlayerStat.DashDistance, StatModifierKind.PercentAdd, 0.25f, source: 노드객체)`가 핸들을 돌려주고, `Remove(핸들)` 또는 `RemoveAll(출처)`로 지웁니다. `duration`을 주면 그 시간 뒤 스스로 만료됩니다(⑧ 사막의 주인 같은 시간제 버프). `Changed` 이벤트와 `Version`, `CopyModifiers`는 UI 툴팁용입니다. 개수형 스탯(공중 점프, 마나 비용)은 `EvaluateCount`로 반올림합니다. 예: ② 긴 보폭 = DashDistance PercentAdd +0.25, ⑤ 빠른 재정비 = DashCooldown PercentAdd -0.2, ⑬ 응축 마나탄 = AttackDamage PercentAdd +0.3, ⑧ = DamageTaken PercentAdd -0.3 + MoveSpeed PercentAdd +0.03 (duration). `Apply Feel Tuning`이 컴포넌트와 참조를 프리팹에 다시 연결합니다. 검증: `PlayerStatsTests`.

## 붙였다 떼는 효과와 타격 시 마나

스킬 노드와 버프는 모두 `IPlayerEffect`(`Apply`/`Remove`)를 따르고, 플레이어 루트의 `PlayerEffects`가 붙은 효과 목록을 관리합니다(`Apply`·`Remove`·`RemoveAll`·`Changed`, 같은 효과는 한 번만). `PlayerEffect` 베이스는 `AddStat`으로 넣은 수정자와 `Subscribe`로 건 이벤트 구독을 기록했다가 Remove 때 역순으로 되돌리므로, 파생 효과는 OnApply만 쓰면 떼는 코드가 자동으로 대칭이 됩니다. 리스펙은 `RemoveAll` 뒤 다시 `Apply`입니다. 프리팹 기본값은 스킬트리의 기본 상태(공중 점프 0, 공중 대시 0, 지상 대시만)이며 ① 더블 점프는 `Effects/DoubleJumpEffect`(ExtraAirJumps +1), ④ 공중 대시는 `Effects/AirDashEffect`(AirDashes +1, 접지하면 다시 참)로 붙입니다. 스킬트리가 붙기 전까지 테스트 씬(PlayerTest·PlayerArtPreview·PlayerAndEnemyTest)의 Test HUD에 있는 `PlayerStarterEffects`가 시작 시 두 효과를 붙여 예전처럼 2단 점프·공중 대시를 씁니다.

명중 사건: `PlayerProjectile.Hit`은 실제로 피해가 적용된 경우에만(벽·아군·보호 대상 제외) `PlayerHitInfo`(대상, 적용 피해, 처치 여부, 위치, 방향, 원인)를 알리고, `PlayerBasicAttack.Hit`이 이를 플레이어 단위 이벤트로 모읍니다. ⑩ 마나 순환은 `Effects/ManaOnHitEffect`가 `ManaPerHit` 스탯을 +3 붙이는 것이고, 회복은 공격 컴포넌트가 명중마다 `ManaPerHit`만큼 `IManaWallet.Gain`을 불러 수행합니다(최대치에서 잘림). 꿰뚫은 적마다 한 번씩 세며, 광역 스킬이 명중으로 칠지는 스킬 정의에서 정합니다. 검증: `PlayerEffectsTests`.

## 공격 마법 스킬 (Q / E / R)

트리의 공격 계열 8노드 중 ⑨ 관통탄·⑬ 응축 마나탄·⑭ 폭발 관통탄은 기본 공격(마우스 왼쪽)을 바꾸는 패시브, ⑪ 모래 폭발·⑮ 모래 소용돌이·⑯ 사막 폭풍은 Q·E·R 액티브, ⑫ 모래 족쇄는 폭발 지점에 붙는 패시브입니다(⑩ 마나 순환은 위 절 참고). 모든 노드는 `Effects/`의 `IPlayerEffect`이고 켜짐은 개수형 스탯 플래그(0보다 크면 켜짐, 여러 노드가 같은 플래그를 켜도 세기는 그대로)입니다. 수치는 `PlayerBasicAttack`(빔·폭발·족쇄)과 `PlayerSkillCaster`(마나·쿨다운·소용돌이·폭풍) 인스펙터 기본값이며 `BeamRange`·`BurstRadius`·`BurstDamageRatio`·`ShackleRadius`·`ShackleDuration`·`BoltScale`·`VortexRadius`·`VortexDuration`·`StormRadius`·`StormDuration`·`StormDamageRatio`·`StormSlow` 스탯 수정자로 올릴 수 있습니다. 테스트 씬에서는 Test HUD의 `PlayerStarterEffects` 체크박스(기본 꺼짐)로 켜고, HUD 세 번째 줄이 Q/E/R의 잠김·쿨다운을 보여 줍니다.

| 노드 | 효과 클래스 | 입력 | 동작 | 기본 수치 |
|---|---|---|---|---|
| ⑬ 응축 마나탄 | `CondensedBoltEffect` | 패시브 | 볼트 피해 +30%, 볼트 외형 +35% | 생성자 인자 |
| ⑨ 관통탄 | `PierceBeamEffect` | 패시브 (LMB) | 볼트 대신 즉발 빔. 몸통→총구→사거리 순으로 훑어 적대 대상은 전부 꿰뚫고(각각 피해, 원인 `player.pierce`) 벽·아군 시설에서 멈춘다. 죽은 개체는 통과 | beamRange 14m, beamRadius 0.2m |
| ⑭ 폭발 관통탄 | `ExplosivePierceEffect` | 패시브 (LMB) | 관통탄 + 빔이 꿰뚫은 적마다 모래 폭발(Q와 같은 반경·피해). Q 해금과는 별개 | 위 값 그대로 |
| ⑪ 모래 폭발 | `SandBurstEffect` | Q | 볼트를 쏴 닿은 자리(적·벽)에서 폭발. 반경 안 적대 대상에 볼트 피해 × 비율(원인 `player.burst`). 직접 맞은 적은 둘 다. 카메라 킥. 연출은 실제 반경 ÷ burstVfxRadius로 스케일 | 마나 15, 쿨 4초, burstRadius 2.5m, burstDamageRatio 0.6 |
| ⑫ 모래 족쇄 | `SandShackleEffect` | 패시브 | 모래 폭발이 터진 자리마다(Q, 폭발 관통탄) 주변 `IRestrainable` 적을 묶는다(이동만 멈춤) | shackleRadius 2m, shackleDuration 1.5초 |
| ⑮ 모래 소용돌이 | `SandVortexEffect` | E | 조준 지점에 소용돌이. 반경 안 `IDisplaceable` 적을 매 프레임 중심으로 끌어당기고(높이 유지, 중심 0.35m 안은 정지) 끝날 때 모인 적을 잠시 묶는다 | 마나 25, 쿨 8초, 반경 4m, 1.5초, 초당 3m, 마무리 속박 0.5초 |
| ⑯ 사막 폭풍 | `SandStormEffect` | R | 조준 지점에 폭풍. 틱마다 반경 안 적대 대상에 피해(원인 `player.storm`)와 둔화(`ISlowable`). 첫 틱은 즉시, 둔화는 다음 틱까지만이라 벗어나면 곧 풀린다 | 마나 40, 쿨 15초, 반경 5m, 5초, 0.5초 틱, 볼트 피해 × 0.25, 둔화 60% |

지정 지점(E·R)은 십자선이 가리키는 곳을 최대 `maxCastDistance`(20m)로 자르고 바닥에 붙인 자리입니다. 벽을 조준하면 벽 위가 아니라 벽 앞 바닥에 놓입니다. 시전 결과는 `ActionResult`(잠김 Locked, 쿨다운 Cooldown, 마나 InsufficientMana, 시전 불가 InvalidRequest/NotAlive)이고 이벤트 `Cast(슬롯, 지점)`, `LastVortex`·`LastStorm`·`LastCastPoint`, `CooldownRemaining(슬롯)`, `ResetCooldowns`가 있습니다. 지역은 `PlayerSandZone`(빈 오브젝트에 붙는 `PlayerSandVortex`·`PlayerSandStorm`)이 관리하며 끝나면 파티클 방출을 멈추고 꼬리가 사라진 뒤 지웁니다. 볼트는 수명이 다해 사라질 때 터지지 않습니다. 폭발·폭풍 명중도 `Hit` 이벤트로 오므로 ⑩ 마나 순환이 적마다 회복합니다. 기타 이벤트: `BeamFired(PlayerBeamShot)`·`Burst(위치)`·`Shackled(위치, 묶은 수)`, `LastBeam`.

입력: `PlayerInput.asset`의 `Skill1`(Q)·`Skill2`(E)·`Skill3`(R), `PlayerInputReader.SkillPressed(0~2)`. 기획에 없던 옛 `Rotate`(R) 액션은 지웠습니다(`Create Missing Demo Assets`가 기존 에셋에서도 제거).

적 쪽: `IRestrainable`·`ISlowable`·`IDisplaceable`(`Assets/Resources/Interfaces`)을 `EnemyRestraint`(Enemy.prefab 루트, 변형 프리팹은 상속)가 구현합니다. 속박은 `EnemyMotor.Restrained`가 목적지·경로 판단은 그대로 두고 에이전트만 세우므로 두뇌 상태는 바뀌지 않고 공격·회전은 가능합니다. 둔화는 `EnemyMotor.SpeedMultiplier`(가장 강한 것 하나만, 같은 세기는 시간 연장), 밀림은 `EnemyMotor.Displace`(NavMeshAgent.Move, 그 프레임 자기 걸음 정지 후 0.1초 뒤 재개)입니다. 죽으면 전부 즉시 풀립니다. 속박 연출은 `VFX_Sand_Root`(`DesertTower > VFX > Wire Combat VFX Into Demo Assets`가 `vfxPrefab`에 연결)이고 풀릴 때 `VfxSandRoot.Release`로 주저앉습니다. 검증: `PlayerSkillShotTests`, `PlayerSkillCastTests`, `EnemyRestraintTests`.

VFX: 관통 빔 `VFX_Pierce_Beam`(자식 `VfxBeam` 길이를 실제 도달 거리로), 꿰뚫은 적마다 `VFX_ManaBolt_Impact`, 폭발 `VFX_Sand_Burst`, 소용돌이 `VFX_Sand_Vortex`, 폭풍 `VFX_Sand_Storm`(둘 다 반경 1m 기준 루프, `SandZoneVfxBuilder`, 인스턴스를 실제 반경으로 스케일). 같은 메뉴가 `PlayerBasicAttack`의 `beamPrefab`·`beamHitPrefab`·`burstPrefab`과 `PlayerSkillCaster`의 `vortexPrefab`·`stormPrefab`에 연결합니다. 응축 마나탄은 별도 프리팹 없이 볼트 외형 스케일만 키웁니다. 히트스톱은 없습니다.

## 상승 기류 (③)

`PlayerUpdraft`(Player.prefab 루트, `UpdraftEffect`로 해금)가 담당합니다. 해금되면 지상에서 Space는 탭과 홀드로 갈립니다: 누른 채 `holdToCharge`(0.15초)가 지나면 웅크림 충전이 시작되고, 그 전에 놓으면 놓는 순간 최대 높이로 점프합니다(탭 점프는 버튼을 떼도 낮아지지 않으며, 그 대신 누르는 길이로 점프 높이를 조절하는 기능은 해금 뒤에는 없습니다). 공중 점프는 평소처럼 누르는 즉시 나가고, 착지까지 계속 누르고 있으면 그대로 충전으로 이어집니다. 모터 쪽은 `PlayerMotor.DeferGroundJumps`(지상 점프를 버튼 누름에 하지 않음)와 `TryJump(fullHeight)`로 지원합니다. 충전 중에는 `PlayerMotor.Anchored`로 이동·점프·대시를 막고(중력·접지는 유지), 놓으면 충전량(`Charge` 0~1, `chargeTime` 0.8초에 가득)에 따라 `minHeight`(4m)~`maxHeight`(10m)로 `PlayerMotor.LaunchVertical`이 발사하듯 솟아오릅니다. 발사 상승 중에는 버튼을 떼도 낮은 점프 중력을 쓰지 않습니다. 공중에 뜨거나 대시·사망·해금 해제면 취소되고, `manaCost`가 부족하면 발사 대신 취소됩니다. 실사 테스트 결과 서 있는 상태에서 바로 모을 수 있어야 해서 탭/홀드 방식으로 정했습니다. 탭 점프는 놓는 순간 나가므로 최대 0.15초 늦습니다.

애니메이터: `Charging`(Bool)·`Charge`(Float)·`Fly`(Trigger). `Charge` 상태는 선 자세(Idle)와 `Male Crouch Pose`를 Charge로 섞는 블렌드 트리라 충전할수록 낮게 웅크리고, 발사하면 `Fly` 상태(임시 동작: Mixamo `Flying`은 수평 비행이라 어색해서 `Jumping Up`의 웅크림 최저점 프레임 10부터 공중 자세 25까지를 한 번 재생하고 마지막 자세를 유지, 충전 웅크림에서 0.12초로 이어짐, `PlayerMobilityAnimationBuilder`의 FlyPose 상수)로 갔다가 정점을 지나면 Falling → (Hard) Landing으로 이어집니다. 연출은 `Charge` 값과 `ChargeStarted`/`ChargeCancelled`/`Launched(충전량, 높이)` 이벤트만 구독하면 됩니다. 카메라 연출은 `PlayerUpdraftCameraFeel`(Player.prefab 루트)이 담당합니다: 충전량이 오를수록 흔들림 진폭(최대 0.09m)과 빈도(9→26Hz)가 커지고 시야가 7도 조여들며 카메라가 0.45m 당겨 와서 힘이 응축되는 느낌을 내고, 발사 순간 감쇠 흔들림(0.22m, 0.28초)과 시야 펀치(+9도가 0.35초에 걸쳐 돌아옴)로 터뜨리며, 취소되면 0.12초에 풀립니다. 흔들림 곡선(`shakeByCharge`)은 초반 완만·후반 급격입니다. 리그 쪽은 `PlayerCameraRig`의 연출 입력(`SetShake`·`Kick`·`AddFov`·`AddDistance`, 흔들림에 약간의 롤 포함)으로만 받으므로 다른 연출도 같은 입력을 쓰면 됩니다. 시야·거리는 매 프레임 더하는 합산 방식입니다. 발사 충격파·모래먼지는 `Assets/Resources/VFX/Prefabs/VFX_Updraft_Launch`(빌더 `UpdraftVfxBuilder`, `DesertTower > VFX > Build All`에 포함)이고 `DesertTower > VFX > Wire Combat VFX Into Demo Assets`가 `PlayerUpdraft.onLaunched` → `VfxOneShot.Fire`로 꽂습니다. 충전 기류는 `VFX_Updraft_Charge`(몸 중심에 자식 `UpdraftCharge`, `VfxChargeLoop`가 `onChargeStarted`/`onCharging`/`onChargeCancelled`/`onLaunched`로 켜고 세기를 올리고 끕니다). 렌즈 왜곡은 `VFX_Updraft_Lens`(자식 `UpdraftLens`, `VfxRefractionBubble` + `Shaders/VFX_ScreenRefraction`)로, 충전할수록 몸 주변이 볼록하게 부풀고 발사 순간 크게 터지며 사라집니다. URP 에셋의 Opaque Texture가 꺼져 있으면 보이지 않습니다. 검증: `PlayerUpdraftTests`.

## 사망과 부활

`PlayerHealth`가 0이 되면 무력화되어 이동·공격·조준점이 꺼지고 캐릭터 컨트롤러가 비활성화됩니다. 같은 프리팹의 `PlayerRespawner`가 사망을 받아 다음 프레임(또는 `respawnDelay` 뒤)에 스폰 지점으로 옮긴 뒤 `PlayerHealth.TryRevive`로 최대 체력으로 되살립니다. 부활 연출은 아직 정해지지 않았으므로 `PlayerRespawner.onRespawned`(UnityEvent) 또는 `Respawned(사망 위치, 부활 위치)` 이벤트에 연결합니다. `PlayerHealth.reviveProtection`(기본 0초)을 주면 그 시간 동안 피해를 거부하고 적의 대상에서 빠집니다. 부활하면 자원도 전부 회복합니다: 마나 최대, 대시·공격 쿨다운 0, 공중 점프 횟수 복구(`restoreResources`, 기본 켜짐).

스폰 지점은 레벨 에디터 마커에서 읽습니다. 시작할 때는 `PlayerStart` 마커로 이동하므로 기획자가 마커를 옮기면 씬을 다시 만들지 않아도 그 자리에서 시작합니다. 부활은 사망 위치에서 가장 가까운 `Respawn` 마커를 쓰고, 없으면 `PlayerStart`, 마커가 전혀 없는 씬(PlayerTest 등)에서는 씬에 놓인 시작 위치를 씁니다. 마커가 회전되어 있으면 그 Y 회전이 시작 방향이 되고 카메라도 그쪽을 봅니다. 회전이 없으면 기존 방향을 유지합니다. `preferRespawnMarkers`를 끄면 항상 `PlayerStart`에서 부활합니다. Animator에는 `Dead`가 꺼지면 Death에서 Locomotion으로 돌아오는 `Player Pack: Revive` 전환이 있습니다.

## 손바닥 시전

기본 시전은 Pro Magic Pack의 `Standing 1H Magic Attack 01`입니다. 비교용으로 `Great Sword Pack/spell cast`와 `Pro Sword and Shield Pack/sword and shield casting (2)`도 연결했으며, 두 동작은 오른손 시전으로 미러링했습니다. 외형 프리팹 Character의 `PlayerSpellcasting.castStyle`에서 기본 종류를 바꾸거나 미리보기에서 B로 비교할 수 있습니다. B 변경은 실행 중에만 유지됩니다.

`Upper Body Casting` Override 레이어와 `CastingUpperBody.mask`가 오른팔/오른손가락/오른손 IK에만 적용하므로 걷기·달리기·점프는 척추와 하체 모두 계속 재생됩니다. 몸통(Body)은 2026-09-10에 마스크에서 뺐습니다. 시전 프레임이 척추를 고정하면 공격하며 이동할 때 상체가 골반과 함께 막대처럼 흔들리기 때문이며, 손 위치는 IK가 어깨 기준으로 매 프레임 잡고 손바닥 방향도 LateUpdate에서 맞추므로 조준은 유지됩니다(어깨가 달리기 리듬으로 출렁이는 만큼 손 위치만 몇 cm 따라 움직입니다). 대신 시전 클립의 상체 기울임은 사라졌습니다. 원본 전체를 매 발마다 재생하지 않고 `CastPhase`로 준비·발사 구간을 사용합니다. `PlayerSpellcasting`이 레이어를 부드럽게 올리고 내리며, 팔 IK는 준비/반동 때 줄이고 발사 때 높여 원본의 움직임과 정확한 손바닥 조준을 함께 사용합니다. 최종 손목 회전으로 손바닥을 조준 방향에 맞추고, 팔 조준 각도는 몸 정면에서 80도로 제한합니다. 직접 만든 `ManaBoltPose.anim`은 팩 적용 전 기본 자세로 남겨 두었습니다.

첫 발은 약 0.12초의 준비 동작과 해당 프레임의 손 자세 적용을 기다립니다. 짧은 클릭도 한 발을 예약하며, 버튼을 유지하면 기존 Attack Interval에 따라 연사합니다. 연사 중에는 자세를 유지하고 발사마다 짧은 팔 반동/손바닥 발광을 재생합니다. 조작 중단·일시정지·공격 불가·사망 시 준비 중인 발사는 취소합니다. `TryFire()`는 실제 볼트가 나간 경우에만 true이며 준비 중에는 false를 반환하고 발사를 예약합니다. 시전 컴포넌트가 없는 교체 외형에서는 기존 즉시 발사를 사용합니다.

`PlayerVisualBindings.firePoint`는 오른손뼈의 `RightPalmMuzzle`입니다. 볼트 생성은 손목 보정 뒤 LateUpdate에서 실행하며, 조준점까지의 방향과 기존 몸통→총구 벽 검사를 사용합니다. 기존 시전 이펙트는 루트의 안정적인 `CastingEffectAnchor`를 발사 직전에 손 위치로 맞춰 0.35배 크기로 재생합니다. 외형 교체 후에도 효과 이벤트가 파괴된 손뼈를 참조하지 않습니다.

램프는 시전 전후에도 허리에 남습니다. `RequestedHeld`는 보관된 요청 상태이며 `allowHandAttachment`가 꺼져 있는 동안 `IsHeld`는 항상 false입니다. 공중 시전은 새 `@Jumping.fbx` 원본의 `CastingJump.fbx`를 사용하는 `Air Cast` 상태로 전환해 몸이 뒤집히지 않게 합니다. 시전을 마쳐도 착지까지 이 상태를 유지합니다.

`SandGuard > Player > Apply Animation Packs`으로 위 시전 3개·이동 8개·피격/사망 2개를 `Art/Protagonist/CombatAnimations`에 가져오고 연결합니다. 원본은 `Docs/model-art`에 보존합니다. `Connect Hand Casting`은 자세/마스크/프리팹 연결을 재생성하며, 기존 외형/점프 연결 메뉴도 팩 자산이 있으면 팩 설정을 다시 적용합니다. 세부 조절은 외형 프리팹 Character의 `PlayerSpellcasting`에서 Raise/Lower Duration, Pose Hold Duration, Recoil Duration/Distance, Arm Extension으로 합니다.

## 램프

마나 순환으로 실제 회복한 양이 0보다 크면 `PlayerBasicAttack.ManaAbsorbed`가 램프 반짝임을 시작합니다. 문양과 램프 본체 발광은 0.1초 동안 올라갔다가 0.65초 동안 원래 마나 밝기로 돌아오고 주변 조명은 최대 0.22를 더합니다. 연속 명중은 현재 밝기에서 반짝임을 갱신하며 중첩하지 않습니다. 마나가 가득 찼거나 스킬이 없으면 자동 반짝임이 발생하지 않습니다. 에디터·개발 빌드에서 H 또는 `PlayerLampEquipment > Preview Mana Absorption`으로 연출만 시험할 수 있습니다. G로 강제로 꺼 놓은 상태에서는 반짝임도 꺼집니다.

램프는 뒤쪽 왼쪽 허리, 가방 옆에 고정합니다. 모델의 `BeltAttach`(손잡이 윗부분)를 골반의 소켓에 맞추고, 몸체가 아래로 늘어지도록 기본 각도를 잡습니다. `SandGuard > Player > Connect Belt Lamp`는 기존 외형을 유지하며 램프 배치만 다시 적용합니다. 외형 전체 생성 시에도 같은 설정을 사용합니다.

소켓의 실제 가속도를 감쇠 스프링에 전달해 출발·정지·보행 시 흔들리고 정지 후 가라앉습니다. 몸 쪽 각도는 더 좁게 제한하며 회전 중에도 손잡이 접점을 유지합니다. 일시정지에서는 멈추고, 사망·부활·비활성화·`PlayerMotor.Teleport`에서는 흔들림을 초기화합니다. `allowHandAttachment`는 기본 꺼짐이며 손 장착 요청과 시전 상태 변경으로 소켓을 바꾸거나 팔 IK를 적용하지 않습니다. 꺼내는 애니메이션이 준비되면 손 소켓 코드를 다시 사용할 수 있습니다.

마나 비율의 제곱을 밝기로 사용합니다(0% 꺼짐, 50% 최대 밝기의 25%, 100% 최대). `IManaReader.Changed`를 구독하고 약 0.2초의 감쇠 시간으로 밝기를 부드럽게 바꿉니다. 문양은 MaterialPropertyBlock, 주변은 Point Light의 intensity만 변경합니다. `SetGlowing(false)`는 즉시 강제 소등하고 `SetGlowing(true)`는 마나 연동을 복구합니다. 미리보기 G로 전환하며 L 손 장착 단축키는 제거했습니다. `PlayerLampTests`에서 부착점, 마나 변화, 일시정지, 흔들림 감쇠, 순간이동과 사망 초기화를 확인합니다.

`SandGuard > Player > Connect Protagonist Art` 메뉴는 이 외형 연결을 재생성합니다. 모델/재질/외형 프리팹은 갱신하고, 기존 Animator Controller와 미리보기 씬은 보존합니다.

## 검증

2026-09-10 허리 램프 적용: Unity PlayMode에서 램프 통합 검사 1개 통과(`Logs/lamp-focused-tests.xml`), 시전 8개·외형 1개 회귀 검사 통과. 마나 0/50/100%, 강제 소등, 일시정지, 부착점 유지, 가속도 흔들림과 감쇠, 순간이동·사망·부활, 최대 마나 0을 확인했습니다. 뒤쪽 배치와 밝기 렌더링은 `Logs/lamp-captures/`의 `full.png`, `full-detail.png`, `half.png`, `empty.png`입니다.

2026-09-09 애니메이션 팩 적용 후 Unity 6000.2.8f1에서 Player/PlayerAndEnemy PlayMode 테스트 **33개 통과**. 기존 시전·이동·점프·대시 검사에 시전 3종의 클립/손바닥 조준, 전후좌우/대각선 이동 혼합, 이동 중 피격과 사망 우선순위를 추가했습니다. 결과: `Logs/player-animation-packs-tests.xml`. 주인공 적용 렌더링: `Logs/player-casting-captures/`의 `cast-idle.png`, `cast-run.png`, `cast-air.png`, `cast-up.png`, `cast-great-sword.png`, `cast-sword-shield.png`, `death.png`.

2026-09-09 손바닥 시전 연결 후 Unity 6000.2.8f1에서 Player/PlayerAndEnemy PlayMode 테스트 **30개 통과**. 단발 준비·연사 간격·달리는 하체 유지·손바닥/이펙트 생성 위치·손 방향·램프 복귀·중단된 발사 취소·공중 시전·외형 교체 재연결을 포함합니다. 결과: `Logs/player-casting-tests.xml`. 렌더링: `Logs/player-casting-captures/cast-idle.png`, `cast-run.png`, `cast-air.png`, `cast-up.png`.

2026-09-09 현재 프로젝트의 Unity 6000.2.8f1에서 Player PlayMode 테스트 **24개 통과**. 지상 점프, 추가 공중 점프 재시작, 세 번째 점프 거부, 지상/공중 대시, 착지/대시 종료 후 상태 복귀, 루트 모션 비활성화를 확인했습니다. 결과는 `Logs/player-animation-tests.xml`, 최신 렌더링은 `Logs/player-animation-captures/`에 저장됩니다(로컬 검증 출력).

Unity 6000.2.8f1 PlayMode에서 Player 및 PlayerAndEnemy 검사 25개 통과. 새 검사는 2K 텍스처/URP 재질/UV 존재, 실제 이동 속도에 따른 Idle/Walk/Run 전환, 다리뼈 움직임, 루트 모션 이동 방지, 동일 램프의 허리/손 전환 및 크기 유지, 발광/Light 켜고 끄기를 확인합니다.

결과: `Docs/model-art/verification/player-tests.xml`.
Unity 렌더링: 같은 폴더의 `idle.png`, `walk.png`, `run.png`, `lamp-hand-lit.png`.

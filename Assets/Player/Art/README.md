# Protagonist and lamp

`Generated/Player.prefab`의 PlayerVisuals에 `Art/Protagonist/ProtagonistVisual.prefab`을 연결합니다. 기존 이동/점프/대시/카메라/공격 설정을 변경하지 않습니다. 기본 Player 프리팹을 참조하는 테스트 씬에도 적용됩니다.

## 실행

`SandGuard > Player > Open Character Preview` 또는 `Assets/Player/Generated/PlayerArtPreview.unity`를 열고 Play.

- WASD / 마우스: 기존 이동/시점
- 마우스 왼쪽 클릭/유지: 손바닥 마나 볼트 단발/연사
- V: 미리보기에서만 걷기(2m/s)/달리기(기존 속도) 전환
- B: 미리보기에서만 시전 3종 순환 (Magic → GreatSwordSpell → SwordShieldSpell)
- K: 미리보기에서만 10 피해 적용 (체력 0이면 사망 동작, Play 재시작으로 복구)
- L: 미리보기에서만 램프 허리/오른손 전환
- G: 미리보기에서만 램프 문양 발광과 주변 조명 전환
- Esc: 기존 커서 잠금 해제

기본 게임 씬의 입력 맵에는 새 단축키를 추가하지 않았습니다. 장비/스킬 상태에서 `PlayerLampEquipment.SetHeld(bool)`과 `SetGlowing(bool)`을 호출할 수 있습니다.

## 캐릭터

모델 및 기본 Idle 원본은 `Docs/model-art/protagonist/character-protagonist-mixamo@Idle.fbx`입니다. 각 파일의 자체 Humanoid Avatar를 만들고 Unity가 동작을 재적용합니다. Idle/Walk/Run은 Speed 0/2/5에 연결되어 있으며 루트 모션은 꺼져 있습니다. Walk/Run에는 Pro Magic Pack의 전후좌우 8개 동작을 적용했습니다. 캐릭터 기준 실제 이동 방향을 `MoveX`/`MoveZ`로 전달하므로 조준하며 옆이나 뒤로 이동할 때 해당 동작을 혼합합니다.

`Protagonist_BaseColor.jpg`는 구매한 2K GLB 내부의 JPEG를 손실 없이 추출한 것입니다. Mixamo FBX의 UV가 이 2K GLB와 일치하는 것을 Blender로 확인했습니다. 4K GLB의 UV는 달라서 해당 이미지를 무작정 대체하지 않습니다. 재질은 URP/Lit, Base Map에 2K 이미지, 금속도 0입니다.

점프에는 `Running Forward Flip` 원본을 `Jump.fbx`로 가져와 연결했습니다. 한 번의 공중 회전을 약 0.65초에 재생하며, 공중 추가 점프가 성공하면 다시 시작합니다. 착지하면 재생 완료를 기다리지 않고 Idle/Walk/Run으로 돌아갑니다. 낙하와 공중 대시 종료에도 Jump 상태를 사용합니다.

대시는 별도 전용 클립이 없어 기존 Run을 1.8배속으로 사용합니다. `Dashing`이 켜진 동안 우선 재생하며 종료 시 접지 상태에 따라 이동/공중 동작으로 돌아갑니다. 실제 이동 거리·점프 높이·마나·쿨다운은 기존 PlayerMotor 설정을 따릅니다.

피격에는 Pro Magic Pack의 `Standing React Small From Front`를 상체에 짧게 재생해 이동을 유지합니다. 체력 0이면 `Standing React Death Backward`를 전신에 재생하고 마지막 자세를 유지합니다. 사망 상태에서는 시전·피격 레이어를 끄며 점프·대시가 사망 동작을 덮어쓰지 않습니다.

`SandGuard > Player > Connect Jump and Dash Animations`는 점프 임포트 설정과 Jump/Dash 상태 및 해당 전환을 갱신합니다. 기존 Locomotion Blend Tree와 모델/램프 프리팹은 보존합니다. 전용 점프/대시 클립을 구하면 Animator의 Jump/Dash Motion과 Speed를 교체하면 됩니다.

## 손바닥 시전

기본 시전은 Pro Magic Pack의 `Standing 1H Magic Attack 01`입니다. 비교용으로 `Great Sword Pack/spell cast`와 `Pro Sword and Shield Pack/sword and shield casting (2)`도 연결했으며, 두 동작은 오른손 시전으로 미러링했습니다. 외형 프리팹 Character의 `PlayerSpellcasting.castStyle`에서 기본 종류를 바꾸거나 미리보기에서 B로 비교할 수 있습니다. B 변경은 실행 중에만 유지됩니다.

`Upper Body Casting` Override 레이어와 `CastingUpperBody.mask`가 몸통/오른팔/오른손가락에만 적용하므로 하체의 걷기·달리기·점프는 계속 재생됩니다. 원본 전체를 매 발마다 재생하지 않고 `CastPhase`로 준비·발사 구간을 사용합니다. `PlayerSpellcasting`이 레이어를 부드럽게 올리고 내리며, 팔 IK는 준비/반동 때 줄이고 발사 때 높여 원본의 움직임과 정확한 손바닥 조준을 함께 사용합니다. 최종 손목 회전으로 손바닥을 조준 방향에 맞추고, 팔 조준 각도는 몸 정면에서 80도로 제한합니다. 직접 만든 `ManaBoltPose.anim`은 팩 적용 전 기본 자세로 남겨 두었습니다.

첫 발은 약 0.12초의 준비 동작과 해당 프레임의 손 자세 적용을 기다립니다. 짧은 클릭도 한 발을 예약하며, 버튼을 유지하면 기존 Attack Interval에 따라 연사합니다. 연사 중에는 자세를 유지하고 발사마다 짧은 팔 반동/손바닥 발광을 재생합니다. 조작 중단·일시정지·공격 불가·사망 시 준비 중인 발사는 취소합니다. `TryFire()`는 실제 볼트가 나간 경우에만 true이며 준비 중에는 false를 반환하고 발사를 예약합니다. 시전 컴포넌트가 없는 교체 외형에서는 기존 즉시 발사를 사용합니다.

`PlayerVisualBindings.firePoint`는 오른손뼈의 `RightPalmMuzzle`입니다. 볼트 생성은 손목 보정 뒤 LateUpdate에서 실행하며, 조준점까지의 방향과 기존 몸통→총구 벽 검사를 사용합니다. 기존 시전 이펙트는 루트의 안정적인 `CastingEffectAnchor`를 발사 직전에 손 위치로 맞춰 0.35배 크기로 재생합니다. 외형 교체 후에도 효과 이벤트가 파괴된 손뼈를 참조하지 않습니다.

시전 중에는 램프를 허리로 옮기고 종료 후 요청된 장착 상태로 복귀합니다. `RequestedHeld`는 요청 상태, `IsHeld`는 실제 손 장착 상태입니다. 공중 시전은 새 `@Jumping.fbx` 원본의 `CastingJump.fbx`를 사용하는 `Air Cast` 상태로 전환해 몸이 뒤집히지 않게 합니다. 시전을 마쳐도 착지까지 이 상태를 유지합니다.

`SandGuard > Player > Apply Animation Packs`으로 위 시전 3개·이동 8개·피격/사망 2개를 `Art/Protagonist/CombatAnimations`에 가져오고 연결합니다. 원본은 `Docs/model-art`에 보존합니다. `Connect Hand Casting`은 자세/마스크/프리팹 연결을 재생성하며, 기존 외형/점프 연결 메뉴도 팩 자산이 있으면 팩 설정을 다시 적용합니다. 세부 조절은 외형 프리팹 Character의 `PlayerSpellcasting`에서 Raise/Lower Duration, Pose Hold Duration, Recoil Duration/Distance, Arm Extension으로 합니다.

## 램프

Blender 제작 Lamp.fbx를 별도 프리팹으로 가져왔습니다. 뼈대 없이 소켓을 따라 움직이는 장비입니다. 허리에서는 제한된 각도로 흔들리며, 손에 들면 Humanoid IK로 오른팔을 들어 올립니다. 손가락 전용 쥐기 동작은 아직 없으며 최종 그립은 후속 애니메이션 작업 대상입니다.

문양만 MaterialPropertyBlock으로 발광을 제어하므로 다른 램프의 재질을 변경하지 않습니다. 주변 조명은 기본 꺼진 Point Light입니다.

`SandGuard > Player > Connect Protagonist Art` 메뉴는 이 외형 연결을 재생성합니다. 모델/재질/외형 프리팹은 갱신하고, 기존 Animator Controller와 미리보기 씬은 보존합니다.

## 검증

2026-09-09 애니메이션 팩 적용 후 Unity 6000.2.8f1에서 Player/PlayerAndEnemy PlayMode 테스트 **33개 통과**. 기존 시전·이동·점프·대시 검사에 시전 3종의 클립/손바닥 조준, 전후좌우/대각선 이동 혼합, 이동 중 피격과 사망 우선순위를 추가했습니다. 결과: `Logs/player-animation-packs-tests.xml`. 주인공 적용 렌더링: `Logs/player-casting-captures/`의 `cast-idle.png`, `cast-run.png`, `cast-air.png`, `cast-up.png`, `cast-great-sword.png`, `cast-sword-shield.png`, `death.png`.

2026-09-09 손바닥 시전 연결 후 Unity 6000.2.8f1에서 Player/PlayerAndEnemy PlayMode 테스트 **30개 통과**. 단발 준비·연사 간격·달리는 하체 유지·손바닥/이펙트 생성 위치·손 방향·램프 복귀·중단된 발사 취소·공중 시전·외형 교체 재연결을 포함합니다. 결과: `Logs/player-casting-tests.xml`. 렌더링: `Logs/player-casting-captures/cast-idle.png`, `cast-run.png`, `cast-air.png`, `cast-up.png`.

2026-09-09 현재 프로젝트의 Unity 6000.2.8f1에서 Player PlayMode 테스트 **24개 통과**. 지상 점프, 추가 공중 점프 재시작, 세 번째 점프 거부, 지상/공중 대시, 착지/대시 종료 후 상태 복귀, 루트 모션 비활성화를 확인했습니다. 결과는 `Logs/player-animation-tests.xml`, 최신 렌더링은 `Logs/player-animation-captures/`에 저장됩니다(로컬 검증 출력).

Unity 6000.2.8f1 PlayMode에서 Player 및 PlayerAndEnemy 검사 25개 통과. 새 검사는 2K 텍스처/URP 재질/UV 존재, 실제 이동 속도에 따른 Idle/Walk/Run 전환, 다리뼈 움직임, 루트 모션 이동 방지, 동일 램프의 허리/손 전환 및 크기 유지, 발광/Light 켜고 끄기를 확인합니다.

결과: `Docs/model-art/verification/player-tests.xml`.
Unity 렌더링: 같은 폴더의 `idle.png`, `walk.png`, `run.png`, `lamp-hand-lit.png`.

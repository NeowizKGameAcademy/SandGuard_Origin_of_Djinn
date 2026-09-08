# Protagonist and lamp

`Generated/Player.prefab`의 PlayerVisuals에 `Art/Protagonist/ProtagonistVisual.prefab`을 연결합니다. 기존 이동/점프/대시/카메라/공격 설정을 변경하지 않습니다. 기본 Player 프리팹을 참조하는 테스트 씬에도 적용됩니다.

## 실행

`SandGuard > Player > Open Character Preview` 또는 `Assets/Player/Generated/PlayerArtPreview.unity`를 열고 Play.

- WASD / 마우스: 기존 이동/시점
- V: 미리보기에서만 걷기(2m/s)/달리기(기존 속도) 전환
- L: 미리보기에서만 램프 허리/오른손 전환
- G: 미리보기에서만 램프 문양 발광과 주변 조명 전환
- Esc: 기존 커서 잠금 해제

기본 게임 씬의 입력 맵에는 새 단축키를 추가하지 않았습니다. 장비/스킬 상태에서 `PlayerLampEquipment.SetHeld(bool)`과 `SetGlowing(bool)`을 호출할 수 있습니다.

## 캐릭터

모델 및 동작 원본은 `Docs/model-art/character-protagonist-mixamo@Idle.fbx`, `@Walk.fbx`, `@Running.fbx`입니다. 각 With Skin 파일의 자체 Humanoid Avatar를 만들고 Unity가 동작을 재적용합니다. Idle/Walk/Run은 Speed 0/2/5에 연결되어 있으며 루트 모션은 꺼져 있습니다.

`Protagonist_BaseColor.jpg`는 구매한 2K GLB 내부의 JPEG를 손실 없이 추출한 것입니다. Mixamo FBX의 UV가 이 2K GLB와 일치하는 것을 Blender로 확인했습니다. 4K GLB의 UV는 달라서 해당 이미지를 무작정 대체하지 않습니다. 재질은 URP/Lit, Base Map에 2K 이미지, 금속도 0입니다.

현재 새 점프/공격/피격 애니메이션은 추가하지 않았습니다. 기존 게임 기능은 유지되며 전용 동작은 추후 연결할 수 있습니다. 추가 다운로드된 Running Forward Flip은 기본 이동 동작과 용도가 달라 이번 연결에서 사용하지 않습니다.

## 램프

Blender 제작 Lamp.fbx를 별도 프리팹으로 가져왔습니다. 뼈대 없이 소켓을 따라 움직이는 장비입니다. 허리에서는 제한된 각도로 흔들리며, 손에 들면 Humanoid IK로 오른팔을 들어 올립니다. 손가락 전용 쥐기 동작은 아직 없으며 최종 그립은 후속 애니메이션 작업 대상입니다.

문양만 MaterialPropertyBlock으로 발광을 제어하므로 다른 램프의 재질을 변경하지 않습니다. 주변 조명은 기본 꺼진 Point Light입니다.

`SandGuard > Player > Connect Protagonist Art` 메뉴는 이 외형 연결을 재생성합니다. 모델/재질/외형 프리팹은 갱신하고, 기존 Animator Controller와 미리보기 씬은 보존합니다.

## 검증 (2026-09-08)

Unity 6000.2.8f1 PlayMode에서 Player 및 PlayerAndEnemy 검사 25개 통과. 새 검사는 2K 텍스처/URP 재질/UV 존재, 실제 이동 속도에 따른 Idle/Walk/Run 전환, 다리뼈 움직임, 루트 모션 이동 방지, 동일 램프의 허리/손 전환 및 크기 유지, 발광/Light 켜고 끄기를 확인합니다.

결과: `Docs/model-art/verification/player-tests.xml`.
Unity 렌더링: 같은 폴더의 `idle.png`, `walk.png`, `run.png`, `lamp-hand-lit.png`.

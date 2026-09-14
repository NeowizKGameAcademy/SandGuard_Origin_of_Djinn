# Chief 폭탄 프리팹

- 프리팹: `Assets/Enemy/Generated/Chief_Bomb.prefab`
- 프리뷰 씬: `Assets/Enemy/Art/ChiefBomb/Chief_Bomb_Preview.unity`
- Blender 편집 원본: `ChiefBomb.blend`
- 모델: `Assets/Enemy/Art/ChiefBomb/ChiefBomb.fbx`

Chief의 붉은 천과 황동 장식에 맞춘 검은 철제 폭탄이다. 몸통 지름은 약 0.39m, 심지 포함 높이는 약 0.45m이며, 모델은 1,380삼각형이다. 별도 이미지 텍스처 없이 URP 재질과 메시로 구성했다.

`VisualRoot`, `GripPoint`, `FuseTip`, `FuseEffects`를 분리했다. 루트에 Rigidbody와 SphereCollider 하나가 있고, 최초 상태는 손에 들기 위한 Kinematic / Collider 비활성 / 심지 꺼짐이다.

## 공격 코드에서 사용

`SandGuard.Enemy.ChiefBombProp`은 외형 및 투척 준비를 돕는 컴포넌트다.

```csharp
// bomb은 Instantiate한 프리팹의 ChiefBombProp.
bomb.Hold(handSocket);            // 손 소켓 아래 배치, 물리와 심지 끄기
bomb.SetFuseLit(true);            // 필요하면 던지기 전에 심지 켜기
bomb.Release(launchVelocity);     // 손에서 분리, 중력/충돌 켜기, 속도 설정
```

기존 `VFX_Demolition_Bomb_Explosion`을 `explosionPrefab` 필드에 연결했다. 실제 폭발 시 공격 컨트롤러가 이 이펙트를 생성하고, 2.5초 이상 재생시킨 후 회수하면 된다. 폭탄은 자체 타이머, 광역 피해, 소유자 충돌 무시, 자동 삭제를 수행하지 않는다. 타이밍·피해·수명 처리는 공격 컨트롤러에서 결정한다. Chief의 근접 공격이나 던지기 애니메이션을 이 작업에서 교체하지 않았다.

재생성: `build_chief_bomb.py` 실행 후 Unity 메뉴 `SandGuard > Enemy > Build Chief Bomb`. 모델 생성기는 Blender 원본과 FBX를 재생성하므로 수동 수정본은 다른 이름으로 저장한다.

`model-validation.json`과 `unity-validation.txt`에 모델 통계·프리팹 참조 검증 결과를 기록한다. `Chief_Bomb_Unlit.png`와 `Chief_Bomb_Lit.png`는 Unity 렌더다.

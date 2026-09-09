# 플레이어 — 이동·전투·대시·체력·마나

## 시작

`Assets/Player/Generated/PlayerTest.unity`를 열고 Play를 누른다.
일반 씬에서는 `Assets/Player/Generated/Player.prefab`을 배치한다.
플레이어에 카메라와 AudioListener가 포함되어 있으므로 씬의 기존 게임 카메라/리스너와 중복되지 않게 한다.

- WASD / 게임패드 왼쪽 스틱: 카메라 기준 이동
- 마우스 / 게임패드 오른쪽 스틱: 시점
- Space / 입력 자산의 Jump 바인딩: 점프, 공중 추가 점프 1회
- 마우스 왼쪽 버튼 / Attack 바인딩: 누르는 동안 기본 투사체 발사
- Shift / 게임패드 왼쪽 스틱 누르기: 대시 (기본 10마나, 4초 쿨다운)
- Esc: 커서 해제 및 플레이어 조작 중단. 안내 패널 밖을 클릭하면 조작 재개
- 테스트 씬 과녁: 10 피해씩 5회 맞으면 비활성화. Play를 다시 시작하면 복구

현재 Esc는 실제 게임 전체 일시정지 메뉴가 아니다. 전체 일시정지는 이후 게임 상태 시스템에서 연결한다.
대시·마나·체력이 연결되어 있다. 부활·마법·설치 모드의 실제 동작은 후속 범위다.

## 캐릭터 외형 교체

1. `Player.prefab`을 Prefab Mode에서 연다.
2. 루트의 `PlayerVisuals`에서 **Visual Prefab**에 원하는 모델 프리팹을 넣는다.
3. **외형 적용 / Rebuild Visual** 버튼을 누른다.
4. `Local Position / Local Euler Angles / Local Scale`로 발 위치와 정면(+Z), 크기를 맞춘 뒤 다시 적용한다.
5. 프리팹을 저장한다. 게임 실행 시에도 선택된 모델로 자동 구성된다.

교체되는 것은 `VisualRoot` 아래 생성된 외형뿐이다. 이동·체력 등의 동작 컴포넌트를 모델에 넣지 않는다.
외형 프리팹은 렌더러와 Animator 중심으로 구성하고 Rigidbody, CharacterController, 게임 판정용 Collider는 루트 쪽에서 관리한다.
캐릭터 이동은 CharacterController가 담당하므로 외형 Animator의 Apply Root Motion은 꺼진다.
모델 크기를 변경해도 캐릭터 충돌체 크기는 자동으로 바뀌지 않는다. 큰 체형은 루트 CharacterController의 Height/Radius/Center도 맞춘다.

### 손/지팡이에서 발사하기

모델 프리팹에 `PlayerVisualBindings`를 추가하고 **Fire Point**에 지팡이 끝 Transform을 연결한다.
설정하지 않으면 플레이어 루트의 `DefaultFirePoint`를 사용하므로 모델만 바꿔도 기본 공격이 유지된다.
Animator도 Bindings에서 지정할 수 있으며, 비워 두면 모델 자식에서 찾는다.

현재 Protagonist는 `RightPalmMuzzle`에 연결되어 손바닥에서 발사한다. 기본 동작은 Pro Magic Pack의 `Standing 1H Magic Attack 01`이다. 약 0.12초의 준비 뒤 발사하고, 연사 중에는 상체 시전 자세를 유지한다. 오른손 램프는 시전 동안 허리로 이동했다가 복귀한다. `SandGuard > Player > Apply Animation Packs`으로 팩 동작을 재연결할 수 있다. 상세 설정은 `Assets/Player/Art/README.md`의 손바닥 시전 항목을 참고한다.

`Assets/Player/Generated/PlayerArtPreview.unity`에서 B는 시전 3종 비교, V는 걷기/달리기, K는 10 피해 적용이다. 시전 대안은 Great Sword의 `spell cast`, Sword and Shield의 `casting (2)`이며 오른손용으로 미러링했다. 체력 0이면 사망 동작을 확인할 수 있고 Play를 재시작하면 복구된다. 이 키들은 미리보기 전용이다.

### 애니메이션 / 발사 효과

`PlayerVisuals`의 파라미터 이름을 Animator Controller와 맞춘다.

| 필드 | 종류 | 전달 값 |
|---|---|---|
| Speed Parameter | Float | 수평 이동 속도 |
| Move X Parameter | Float | 캐릭터 기준 수평 이동 방향 X (-1~1) |
| Move Z Parameter | Float | 캐릭터 기준 수평 이동 방향 Z (-1~1) |
| Grounded Parameter | Bool | 지면 접촉 상태 |
| Attack Trigger | Trigger | 기본 공격 발사 |
| Hit Trigger | Trigger | 피해 적용 |
| Death Trigger | Trigger | 체력 0, 무력화 |
| Dash Parameter | Bool | 대시 중 여부 |
| Jump Trigger | Trigger | 성공한 지상/추가 공중 점프 |

해당 파라미터나 Animator가 없어도 게임 동작은 유지된다.
현재 Protagonist Controller는 Idle, Pro Magic Pack의 전후좌우 Walk/Run, Jump(앞 공중회전), Dash(기존 Run 1.8배속)를 연결한다. 상체 시전과 하체 이동은 별도로 혼합하며, 공중 시전은 몸이 뒤집히지 않는 Air Cast 동작을 사용한다. Space로 점프/추가 점프, Shift로 대시를 확인한다. 착지와 대시 종료는 Exit Time을 기다리지 않고 전환한다. 점프 높이와 대시 거리는 루트 모션이 아닌 PlayerMotor가 제어한다.

피격은 상체에 짧게 재생해 이동을 유지한다. 현재 Controller의 전신 사망은 `Dead` Bool로 유지하며 체력 상태에서 자동으로 전달한다. 사망 중에는 시전·피격 레이어를 끄고 점프·대시 전환을 막는다. `Death Trigger` 필드는 다른 외형 Controller와 연결할 때 사용할 수 있다. `Casting`/`CastPhase`/`CastStyle`은 `PlayerSpellcasting`이 전달한다.
**On Fired** 이벤트에는 효과음/파티클 재생을 인스펙터에서 연결할 수 있다.
교체되어 파괴되는 모델 인스턴스를 이벤트의 고정 참조로 사용하지 않는다. 루트의 안정적인 효과 컴포넌트를 연결한다.

## 투사체 외형 교체

`PlayerBolt.prefab`의 `VisualRoot` 자식 메시를 원하는 모델/파티클로 바꾼다.
루트의 `PlayerProjectile`은 유지한다. 이동과 명중은 코드의 구체형 검사로 처리하므로 투사체 외형에는 Collider/Rigidbody가 필요 없다.
다른 투사체 프리팹을 쓰려면 동일 컴포넌트를 붙이고 `PlayerBasicAttack.Projectile Prefab`에 연결한다.
충돌 효과는 `PlayerProjectile.Impact Prefab`과 `Impact Lifetime`으로 지정한다.

## 조절 가능한 값

### 대시·체력·마나

- `PlayerMotor`: Dash Mana Cost 10, Dash Cooldown 4초, Dash Duration 0.2초, Dash Distance 4m, Allow Air Dash 켜짐이 기본이다. 이동 입력 방향으로 수평 대시하며 입력이 없으면 카메라 전방으로 나간다. 공중 추가 점프를 소모하지 않고, 중력과 벽 충돌은 유지한다. 대시에 무적 효과는 없다.
- `PlayerHealth`: Max Health 기본 100, Faction Id 기본 Ally. 피해는 `IDamageable.TakeDamage`로 전달한다. 같은 진영, 잘못된 요청, 일시정지 중 피해는 거부한다. 초과 피해는 남은 체력까지만 적용한다.
- 체력 0이면 `ILifeState.State`가 Incapacitated로 바뀌고 한 번만 Died가 발생한다. 이동·점프·대시·공격이 차단되고 설정된 충돌체가 꺼진다. 부활/코어 연동은 하지 않는다.
- `PlayerManaWallet`: Max Mana / Starting Mana 기본 100. `IManaWallet`의 소비·예약·예약 확정·취소·충전을 구현한다. 자동 마나 재생은 없으며 실제 충전/보상 시스템은 `Gain`을 호출하면 된다. 예약 중인 마나는 표시 잔액에 포함되지만 다른 행동에서 쓸 수 없다.
- `PlayerMotor`의 Mana Source에는 `IManaWallet`, Life Source에는 `ILifeState` 구현 컴포넌트를 연결한다. 공격의 Life Source와 외형의 Health Source도 같은 체력 컴포넌트에 연결한다. 인스펙터에서는 컴포넌트를 지정하고 내부에서는 인터페이스로 사용한다.
- `PlayerVisuals`의 On Damaged / On Incapacitated / On Dash Started에 소리나 이펙트를 연결할 수 있다. 외형을 교체해도 체력·마나·대시 수치는 유지된다.
- 테스트 씬에서 Esc를 누른 뒤 **Test: Take 25 damage**, **Test: Refill mana** 버튼으로 확인할 수 있다. 무력화 상태는 Play를 다시 시작해야 초기화된다.

이미 만들어진 기본 플레이어 프리팹에는 `SandGuard > Player > Connect Health Mana and Dash`로 컴포넌트와 비어 있는 연결을 추가할 수 있다. 기존 모델과 조절한 수치는 덮어쓰지 않는다.

- PlayerMotor: 이동 속도, 가속도, 점프 높이, 중력, 공중 추가 점프 횟수, 회전 속도, 추락 복구 높이
- CharacterController: 높이/반경, 계단 높이, 경사 제한
- PlayerCameraRig: 추적 기준, 어깨 오프셋, 거리, 감도, 상하 시점 제한, 장애물 레이어
- PlayerBasicAttack: 발사 간격, 피해량, 진영, 몸통 발사 검사 기준
- PlayerProjectile: 속도, 반경, 수명, 충돌 레이어, 충돌 효과
- PlayerAimer: 카메라, 조준 거리, 조준 레이어

레이어 마스크는 테스트에서 전체 레이어를 사용한다. 실제 씬에서는 지형/시설/적 등 필요한 레이어로 제한한다.
현재 명중 판정은 Trigger를 제외한다. 적의 명중용 Collider는 일반 Collider로 설정한다.
공격받을 대상의 루트 또는 Collider 부모에 `IDamageable` 구현이 있어야 한다.
`ICombatTarget`이 있으면 해당 DamageReceiver를 사용하고 같은 진영/공격 불가 대상을 배제한다.
복잡한 진영 관계 판정은 이후 공통 전투 서비스에 연결한다.

## 구조와 후속 연결

입력은 원본 Input Actions의 런타임 복제본을 사용해 다른 시스템의 입력 활성 상태에 영향을 주지 않는다.
플레이어용 입력 자산은 기존 `Assets/10.Input/InputSystem_Actions.inputactions`에서 복제해 생성했다.
프로젝트 원본 입력 자산을 나중에 수정해도 이 복제본은 자동 갱신되지 않으므로 `PlayerInput.asset`을 함께 수정하거나 교체한다.

`PlayerInputReader.GameplayEnabled`, `PlayerMotor.MovementEnabled`, `PlayerBasicAttack.CombatEnabled`로 외부 상태 시스템이 동작을 제한할 수 있다.
이동 중 떨어지면 처음 생성된 위치로 돌아오는 기본 복구가 있다. 실제 레벨의 안전 위치/부활 마커 연동은 후속 작업이다.
타워나 소환물 프리팹에는 의존하지 않는다. 투사체는 공통 피해 인터페이스로 연결한다.

`SandGuard > Player > Create Missing Demo Assets`는 없는 데모 자산만 생성한다. 기존 프리팹과 테스트 씬의 수동 편집을 덮어쓰지 않는다.
테스트 전용 `PlayerDemoOverlay`, `PlayerTestTarget`은 실제 HUD/적 구현이 연결되면 제거할 수 있다.

## 검증 기록

2026-09-09 현재 프로젝트의 Unity 6000.2.8f1에서 애니메이션 팩 적용 후 Player/PlayerAndEnemy PlayMode 테스트 **33개 통과**. 시전 3종, 손바닥 발사 위치와 방향, 이동 중 시전, 전후좌우/대각선 이동 혼합, 점프·대시, 피격·사망 우선순위를 확인했다. 결과는 `Logs/player-animation-packs-tests.xml`, 적용 렌더링은 `Logs/player-casting-captures/`에 있다.

2026-09-08 후속 구현: Unity 6000.2.8f1 별도 검증 프로젝트에서 **PlayMode 테스트 15개 통과**. 기존 7개에 마나 예약/취소/초기화, 대시 비용/쿨다운/벽 충돌/일시정지/공중 방향, 예약 마나 부족 시 무변경, 치명타 처리와 행동 차단, 잘못된 피해 거부 검증을 추가했다. 테스트용 바닥 생성 직후 물리 월드 동기화도 명시했다.

2026-09-08, Unity 6000.2.8f1 별도 검증 프로젝트에서 컴파일과 PlayMode 테스트 7개 통과.

- 이동 중 벽 통과 방지 및 일시정지 중 이동 차단
- 공중 추가 점프 1회 제한 및 착지 시 횟수 복구
- 카메라 뒤 벽에 따른 거리 축소
- 빠른 투사체의 가장 가까운 충돌체 명중 및 중복 피해 방지
- 총구가 벽을 넘어간 경우 발사 경로 차단
- 외형 교체 후 이동 컴포넌트 유지, 모델 전용 발사 위치와 기본 위치 전환
- 기본 공격의 발사 간격 및 일시정지 중 발사 차단

생성 자산의 GUID 참조 21개도 누락 없이 확인했다.
화면 렌더링 검증과 사람이 직접 조작하는 감각 검증은 완료하지 않았다. 실제 모델과 레벨을 연결한 뒤 카메라 오프셋, 발사 위치, 계단/경사 이동을 조절한다.

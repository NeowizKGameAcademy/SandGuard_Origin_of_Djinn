# SandGuard — 적 Mixamo 애니메이션 목록 v0.1

작성일: 2026-09-09
용도: 적 5종의 Mixamo 클립 선정과 다운로드·임포트 절차
콘셉트 원본: `Docs/concept-art/character2.png`
프롬프트 문서: `SandGuard_Tripo_A포즈_캐릭터시트_프롬프트_v0.1.md` §3.2~3.6
최종 선정본: `Docs/model-art/animation-selection-v1/` (README + `selection.json`) — 클립 파일 선택은 그쪽이 기준입니다
확인 방법: mixamo.com 카탈로그 직접 검색 + 팩 FBX 154개와 선정본 39개를 Blender 5.2로 측정 (2026-09-09). 이 문서의 길이·이동량·본 수는 실측값입니다.

---

## 1. 게임 쪽 요구사항

`EnemyVisuals`가 애니메이터에 넘기는 파라미터는 셋뿐입니다.

| 파라미터 | 타입 | 값 |
|---|---|---|
| `Speed` | Float | `EnemyMotor.Velocity`의 수평 크기 |
| `Attack` | Trigger | `EnemyMeleeAttack.Swing()` 시작 시 |
| `Die` | Trigger | `EnemyHealth.Died` |

`EnemyMotor.moveSpeed = 3.5f` **단일 속도**입니다. 플레이어처럼 Idle/Walk/Run 3단 블렌드 트리가 필요 없고, **Idle ↔ Move 2단**이면 충분합니다.

따라서 적 1종당 필요한 클립은 **Idle / Move / Attack / Death 네 개**입니다. 피격 리액션은 선택 사항입니다(→ 상체 레이어는 `Docs/plan` 논의대로 지금은 만들지 않습니다).

`EnemyBrain.Engage()`가 사거리 안에서 `motor.Stop()`을 부르고 휘두르는 동안 이동하지 않으므로, Attack은 **전신 클립**으로 충분합니다.

---

## 2. 장비 프리팹 매핑

`Assets/Enemy/Art/Equipment/`에 이미 만들어진 프리팹과 적의 대응입니다. 무기는 손 본 아래 소켓으로 붙이므로, **Mixamo 클립의 그립이 소켓 오프셋을 결정합니다.**

| 적 | 선정 폴더 | 무기 프리팹 | 방패/기타 | 클립 그립 |
|---|---|---|---|---|
| 검병 (일반 도적) | `Swordsman` | `ShortSword` | `RoundShield` | 오른손 + 왼팔 방패 |
| 암살자 (빠른 도적) | `Assassin` | `AssassinDagger` ×2 | — | 양손 단검 |
| 방패병 (방패 도적) | `ShieldGuard` | `ShortSword` | `TowerShield` | 오른손 + 왼팔 방패 |
| 망치병 (철거꾼) | `HammerBrute` | `Warhammer` | — | 양손 |
| 우두머리 (자히르) | `Chief` | `ChiefScimitar` | `ChiefCape`, 방패 없음 | 오른손 (왼팔 빈손 보정) |

> 2026-09-09 선정본 기준. 곡괭이 프리팹은 필요 없어졌습니다.

---

## 3. 적별 클립 — 최종 선정은 `animation-selection-v1`

최종 선정본은 **`Docs/model-art/animation-selection-v1/`** 입니다. 캐릭터별 폴더(`Swordsman` / `Assassin` / `ShieldGuard` / `HammerBrute` / `Chief`)에 용도 이름(`Idle.fbx`, `Move.fbx`, `Attack.fbx`, `Death.fbx` …)으로 복사돼 있고, 원본 경로·SHA-256·길이·이동량은 각 폴더의 `selection.json`, 선정 이유는 그 README에 있습니다. 아래는 그 선정본을 **독립적으로 재측정**한 결과입니다.

### 3.1 core 4종 — 39개 전부 재측정 (2026-09-09)

| 적 | Idle | Move | Attack | Death |
|---|---|---|---|---|
| 검병 Swordsman | `idle (4)` 2.53초 | `run` 0.70초 전진 ✔ | `attack (4)` 1.00초 제자리 ✔ | `death` 2.30초 뒤로 ✔ |
| 암살자 Assassin | `Knife Idle` 4.47초 | `Ninja Run` 0.63초 제자리(In Place) ✔ | `Double Dagger Stab` **3.07초** 제자리 ✔ | Magic `Standing React Death Left` 3.60초 옆으로 ✔ |
| 방패병 ShieldGuard | `block idle` 1.40초 | `walk` 1.07초 전진 ✔ | `attack (4)` 1.00초 제자리 ✔ | `death` 2.30초 뒤로 ✔ |
| 망치병 HammerBrute | `great sword idle` 2.00초 | `great sword run (2)` 0.60초 전진 ✔ | `great sword slash (3)` **1.83초** 제자리 ✔ | `two handed sword death` 2.40초 뒤로 ✔ |
| 우두머리 Chief | `idle (4)` 2.53초 | `walk` 1.07초 전진 ✔ | `slash` **1.50초** 제자리 ✔ | Magic `Standing React Death Backward` 3.63초 뒤로 ✔ |

판정 기준은 §4와 같습니다. core 20개 중 이동 방향이 틀리거나 공격이 전진하는 클립은 **없습니다.** 지원(support) 클립 19개도 길이가 선정 README 기재와 일치하고, 39개 복사본의 SHA-256이 원본과 일치합니다.

### 3.2 선정본이 이 문서의 초기 추천과 다른 점

| 항목 | 초기 추천 | 최종 선정 | 비고 |
|---|---|---|---|
| 암살자 무기 | 한손 단검 | **쌍단검** | `Double Dagger Stab`이 Attack. 프롬프트 §3.3 수정 불필요 |
| 방패병 무기 | `Warhammer` 또는 곡괭이 | **`ShortSword` + `TowerShield`** | 곡괭이 프리팹 불필요 |
| 우두머리 방패 | 검+방패 세트 그대로 | **방패 없음**, 곡도 + 망토 | 검+방패 클립의 왼팔 자세를 빈손으로 보정하는 조건 |
| 망치병 Attack | `great sword attack` 1.2초 | `great sword slash (3)` 1.83초 강타 | 1.2초 클립은 `AttackQuick`으로 보조 |
| 우두머리 Idle | `idle (3)` 8.67초 연출 | `idle (4)` 2.53초 | 안정성 우선 |
| 방패 없는 피격/사망 | 검+방패 `death` 재사용 | **`Pro Magic Pack`의 React Death / React Small** | 암살자·우두머리에 적용 |

### 3.3 공격 길이와 `EnemyMeleeAttack` 기본값 (§5-3)

`interval = 1.2f` 기준으로 넘치는 core Attack이 셋입니다. `interval`은 public 필드라 프리팹별로 다르게 둘 수 있습니다.

| 적 | Attack 길이 | 그대로 쓰려면 | 또는 |
|---|---|---|---|
| 망치병 | 1.83초 | `interval ≥ 1.9` | — |
| 우두머리 | 1.50초 | `interval ≥ 1.6` | 보스는 별도 공격 컨트롤러 예정 |
| 암살자 | 3.07초 | `interval ≥ 3.2`는 "빠른 적"과 모순 | `AttackQuick`(2.13초)를 1.8배속 → 1.2초 |

타격 시점(`windup`)은 무기 장착 후 클립을 보고 잡습니다. 선정 README도 같은 입장입니다.

### 3.4 리깅 몸체

| 적 | 리깅된 FBX (메시 + 스킨 + 본) | 본 수 |
|---|---|---|
| 검병 | `Pro Sword and Shield Pack/bandit1.fbx` (팩 폴더 안) | 41 |
| 암살자 | `bandit-ninja/…/tripo_convert_…@Zombie Stand Up.fbx` | 41 |
| 방패병 | `bandit-shielder/…/tripo_convert_…@Dying.fbx` | 41 |
| 망치병 | `bandit-hammerer/tripo_convert_…@Capoeira.fbx` | 41 |
| 우두머리 | `bandit-leader/…/tripo_convert_…@Capoeira.fbx` | **33** |

선정된 39개 클립의 본 수도 41입니다. 우두머리만 33본(선정 README: "엄지 본이 없는 리그")이라 Humanoid 리타게팅은 되지만 손가락 동작은 적용되지 않고, 곡도 그립 소켓은 따로 잡아야 합니다. 검병의 리깅 몸체는 다른 넷과 달리 `bandit-minion/`이 아닌 팩 폴더 안에만 있습니다(`bandit-minion/bandit1.fbx`는 리깅 전 원본, 0본).

### 3.5 `Pro Magic Pack` (56 애니메이션)

원래 플레이어 캐스팅용(`PlayerSpellcasting.cs`, `CastingUpperBody.mask`, `ManaBoltPose.anim`)으로 받은 팩이지만, **방패 없는 피격·사망 공용 클립**(`Standing React Death Left/Backward`, `Standing React Small From Front`)을 암살자·우두머리에 재사용합니다.

### 3.6 미다운로드 — `Pro Melee Axe Pack`

방패병이 `ShortSword`로 확정되어 곡괭이용 팩은 필요 없어졌습니다.

---

## 4. 클립을 어떻게 식별했나

Mixamo가 내려주는 FBX는 **어떤 클립인지 파일에서 알 수 없습니다.**

- 파일명은 사이트의 **제목**만 씁니다. 같은 제목이 여러 개면 `(2)`, `(3)`이 붙습니다. `Sword And Shield Idle`은 4개라 `idle` / `idle (2)` / `idle (3)` / `idle (4)`가 되는데 어느 것이 "Stretch Idle"인지 알 수 없습니다.
- FBX 내부 AnimStack 이름은 전부 `mixamo.com`이고, 사이트에 보이는 **설명은 파일에 저장되지 않습니다.**

그래서 Blender로 전부 열어 힙 본의 **이동량·높이·길이**를 측정해 식별했습니다.

| 판별 대상 | 근거 |
|---|---|
| 전진 / 후진 | 힙의 Y 이동 부호. 음수 = 전진 |
| 앞으로 쓰러짐 / 뒤로 쓰러짐 | Y 이동 부호 + 힙 최종 높이가 0.05~0.08로 낮아짐 |
| 웅크린 자세 | 힙 시작 높이 0.20~0.24 (선 자세는 0.40~0.46) |
| 제자리 여부 | 이동량 절댓값 0.02 미만 |

위 표의 ✔는 이 측정으로 확정된 것, "추정"은 길이로만 골라 Unity 프리뷰 확인이 필요한 것입니다.

---

## 5. 반드시 피해야 할 함정 세 개

**1. run / walk의 번호가 팩마다 반대입니다.**

| 팩 | 전진 | 후진 |
|---|---|---|
| Sword and Shield | `run.fbx` (-1.40) | `run (2).fbx` (+1.13) |
| Great Sword | `run (2).fbx` (-1.27) | `run.fbx` (+0.93) |

번호만 보고 복사하면 적이 뒤로 걸어옵니다.

**2. 공격 클립 대부분이 앞으로 이동합니다.**

`sword and shield attack.fbx`는 1.76, `slash (2).fbx`는 1.18만큼 전진합니다. 그런데 `EnemyBrain.Engage()`가 공격 전에 `motor.Stop()`을 부르므로 캐릭터는 제자리에 고정된 채 애니메이션만 앞으로 갑니다 = 발 미끄러짐.

**이동량 0인 클립만 고르세요.** §3의 표는 전부 그 기준으로 뽑았습니다.

**3. 클립 길이가 `EnemyMeleeAttack` 기본값과 안 맞습니다.**

현재 `windup = 0.35f`, `interval = 1.2f`입니다.

| 클립 | 길이 | interval 1.2초 |
|---|---|---|
| `sword and shield attack (4).fbx` | 1.0초 | 여유 있음 ✔ |
| `great sword attack.fbx` | 1.2초 | 경계 |
| `sword and shield slash.fbx` | 1.5초 | 초과 — 다음 공격에 잘림 |
| `Stabbing (1).fbx` | 2.13초 | 크게 초과 |

빠른 도적은 클립을 배속하거나 `interval`을 올려야 합니다. 그리고 실제 타격 순간은 1초짜리 클립에서 대략 0.5~0.6초 지점이라 `windup = 0.35f`도 클립에 맞춰 조정해야 합니다.

---

## 6. 팩 단위 다운로드

클립을 하나씩 받을 필요 없습니다. Mixamo는 팩을 **한 덩어리로** 취급합니다.

1. 검색 URL에 `type=MotionPack`을 붙이면 팩만 걸러집니다.
   `https://www.mixamo.com/#/?query=sword+and+shield&type=MotionPack`
2. 팩 카드를 클릭합니다. 뷰어 제목이 `SWORD AND SHIELD PACK ON DEFAULT CHARACTER`로 바뀌면 팩 전체가 선택된 상태입니다.
3. 오른쪽 **DOWNLOAD** 버튼 하나로 팩 전체를 받습니다. (Adobe 로그인 필요)

검색 시 팩 카드 왼쪽 아래 배지가 클립 개수입니다 — Sword And Shield 49 / Pro 51 / Lite 17.

### 다운로드 설정

팩 다운로드 대화상자에는 **Skin 옵션이 없습니다.** 항목은 넷뿐입니다.

| 항목 | 값 | 이유 |
|---|---|---|
| Format | `FBX for Unity(.fbx)` | 기본값 유지. 몸체와 클립을 **같은 포맷으로 통일**하는 것만 지키면 됨 |
| Pose | `T-pose` | Humanoid 아바타가 기준으로 삼는 자세 |
| Frames per Second | `30` | 기본값 유지. 60은 용량만 2배 |
| Keyframe Reduction | `none` | 기본값 유지. 압축은 Unity 임포터에서 |

### Skin 옵션이 없어도 메시는 중복되지 않습니다 (실측 확인)

Mixamo가 팩에서 알아서 처리합니다. Blender로 154개를 열어본 결과, **메시를 가진 파일은 각 팩의 `bandit1.fbx` 하나뿐**이고 나머지는 전부 모션 전용입니다(FBX 내부에 `MotionOnlyScene; Retargeted Clip` 표기). 클립 하나가 약 250KB, `Sword and Shield Pack` 50개 합계가 13MB입니다.

주인공은 클립마다 With Skin으로 받아 `character-protagonist-mixamo@Idle.fbx`에 12759버텍스 메시가 통째로 들어 있는데, 팩 다운로드는 그렇지 않습니다.

용량은 문제없지만, 쓰지도 않을 49개를 Unity가 임포트할 이유는 없습니다. 주인공에서 이미 쓰고 있는 구조를 그대로 따릅니다.

| 위치 | 내용 |
|---|---|
| `Docs/model-art/` | 팩 원본 전부 (Unity 밖) |
| `Assets/Enemy/Art/<적이름>/` | 실제 쓰는 4~5개만 이름 바꿔서 복사 |

주인공도 `Docs/model-art/`에 Mixamo 원본 7개가 있고 `Assets/Player/Art/Protagonist/`에는 `Protagonist / Run / Walk / Jump / CastingJump` 5개만 들어가 있습니다.

Unity에서는 몸체 FBX와 클립 FBX **모두** `Animation Type: Humanoid`, `Avatar Definition: Create From This Model`로 둡니다 — 주인공 README의 "각 With Skin 파일의 자체 Humanoid Avatar" 방식과 같습니다. 모션 전용 FBX에도 전체 골격이 들어 있어 아바타 생성이 됩니다. `Copy From Other Avatar`는 골격이 동일한 원본(같은 팩의 `bandit1.fbx`)에서만 허용되고, **골격이 다른 몸체(암살자·방패병·망치병·우두머리)의 아바타를 클립에 지정하면 안 됩니다.**

---

## 7. 루트 모션 — In Place 안 걸어도 됩니다

팩 다운로드는 클립별 **In Place** 옵션을 조절할 수 없습니다. 그래도 문제 없습니다.

`EnemyVisuals.RebuildVisual()`이 `animator.applyRootMotion = false`를 강제하고, Humanoid 리타게팅이 루트 모션을 루트로 뽑아낸 뒤 적용하지 않으므로 캐릭터는 제자리에서 재생됩니다. 이동은 `EnemyMotor`의 NavMeshAgent가 전담합니다.

주인공에서 이미 증명된 동작입니다. `Assets/Player/Tests/ProtagonistArtTests.cs:163`:

```csharp
Assert.False(animator.applyRootMotion);
Assert.Less(Vector3.Distance(modelOrigin, animator.transform.localPosition), .01f);
```

주인공 점프는 이동량이 큰 `Running Forward Flip` 원본을 쓰는데도 모델 로컬 좌표가 원점에서 0.01 이상 벗어나지 않습니다.

---

## 8. 임포트 체크리스트

- [ ] Assets에는 `animation-selection-v1/<적>/`의 core 4개(+필요한 support)만 복사. 팩 원본은 `Docs/model-art/`에 유지
- [ ] 몸체 `Animation Type: Humanoid`, `Avatar Definition: Create From This Model`
- [ ] 클립 FBX도 `Create From This Model` — 골격이 다른 몸체의 아바타를 복사 지정하지 않기
- [ ] 애니메이터 파라미터 이름이 `Speed` / `Attack` / `Die` (기본값, `EnemyVisuals`에서 변경 가능)
- [ ] Idle ↔ Move 전환 임계값을 `moveSpeed = 3.5f` 기준으로 설정
- [ ] Move 클립은 Walk보다 Run 권장 — 3.5m/s에서 Walk는 발이 미끄러짐. 속도 배율로 맞춤
- [ ] 무기 소켓 오프셋은 적마다 따로 — Sword And Shield(오른손+왼팔), Great Sword(양손), Melee Axe(한손)로 그립이 다름
- [ ] `EnemyVisualBindings`에 `animator`, `attackOrigin` 연결. 애니메이터가 있으면 `weaponPivot` 코드 스윙은 안 쓰임

---

## 9. 미결 사항

### 해결됨 (2026-09-09 재확인)

| 항목 | 결과 |
|---|---|
| 쌍단검 여부 | **쌍단검 확정.** `Double Dagger Stab`을 Attack으로 선정. 프롬프트 §3.3 수정 불필요 |
| 곡괭이 프리팹 | 불필요. 방패병은 `ShortSword` + `TowerShield` |
| `bandit-leader` fbx | 해결. `@Capoeira.fbx` 리깅 몸체(33본) + 리깅 전 원본 보유 |
| 빠른 도적 Death | 해결. `Pro Magic Pack/Standing React Death Left` |
| 파일명 불일치 | 원본은 그대로 두고 `animation-selection-v1/<적>/<용도>.fbx`로 복사. SHA-256 39/39 일치 |
| Pro / 일반 팩 중복 | 일반 팩 삭제, Pro로 통일 |
| Idle 최종 선택 | 선정 README에서 확정 (검병·우두머리 `idle (4)`, 방패병 `block idle`, 망치병 `great sword idle`, 암살자 `Knife Idle`) |

### 남은 것

| 항목 | 내용 |
|---|---|
| 공격 간격 | **적용됨** — `Enemy_HammerBrute` interval 1.9, `Enemy_Chief` 1.6, `Enemy_Assassin`은 `AttackQuick` 2배속(1.07초). §3.3 참조 |
| 리타게팅 검증 | **완료** — `SandGuard > Enemy > Connect Combat Art` + `EnemyArtTests` 12개 통과. 결과 `Docs/model-art/enemy-combat-art-v1/` |
| 우두머리 리그 | 33본(엄지 없음). 곡도는 엄지 방향을 +Z로 가정해 부착됨. 왼팔은 방패 자세 그대로(빈손 보정 미적용) |
| 검병 리깅 몸체 위치 | 팩 폴더의 `bandit1.fbx`뿐. 빌더가 `Assets/Enemy/Art/Characters/Swordsman/Swordsman_Rig.fbx`로 복사하므로 동작에는 지장 없음 |
| 루트의 낱개 클립 6개 | `Knife Idle` 등은 원본으로 유지 중. `bandit-ninja/`로 옮기려면 `selection.json`의 `source` 경로도 같이 수정 |
| 검병 원형 방패 | 대기 자세에서 손바닥이 아래를 향해 방패가 눕는다. 막기 자세 기준으로 규칙을 정했으므로 검병만 `RoundShield_Placement` 회전을 손보면 됨 |
| 피격 리액션 | Hit 클립은 골랐지만 `EnemyVisuals`에 파라미터 없음. 상체 레이어 논의는 그대로 보류 |
| 보스 다단 패턴 | `EnemyMeleeAttack`은 단일 공격. `AttackAlt` / `Kick`은 별도 컨트롤러 필요 |

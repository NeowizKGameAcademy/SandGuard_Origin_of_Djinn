# 적 5종 외형 준비

`Assets/Enemy/Generated/EnemyArtPreview.unity`를 열고 Play를 누르면 외형 확인 화면을 사용할 수 있습니다. 메뉴는 `SandGuard > Enemy > Open Art Preview`입니다.

| 종류 | 프리팹 | 몸체 높이 |
|---|---|---:|
| 검병 | Swordsman/Swordsman_PreviewVisual.prefab | 1.75m |
| 암살자 | Assassin/Assassin_PreviewVisual.prefab | 1.68m |
| 방패병 | ShieldGuard/ShieldGuard_PreviewVisual.prefab | 1.84m |
| 망치병 | HammerBrute/HammerBrute_PreviewVisual.prefab | 1.94m |
| 우두머리 | Chief/Chief_PreviewVisual.prefab | 2.04m |

원본 GLB 비율을 유지하며 균일 스케일로 높이를 맞췄습니다. 루트 스케일은 1이며 몸체만 크기를 보정했습니다. 바닥은 Y=0, 정면은 +Z입니다. 다운로드한 `Docs/model-art/bandit-*` 원본은 그대로 보존했습니다.

각 프리팹은 `Body`와 `PreviewAttachments`로 분리되어 있습니다. `RightHand_PreviewSocket`, `LeftHand_PreviewSocket`, 우두머리의 `Back_PreviewSocket` 아래에 장비를 임시 장착했습니다. 손 위치는 원본 메시의 손 영역을 측정해 보정했고, 장비의 HandGrip/ShoulderAttach 기준점을 해당 소켓에 일치시켰습니다.

## 확인 화면

- 전체 5종 또는 한 캐릭터 선택
- 정면·측면·후면: 개별 회전으로 나란히 비교
- 게임 거리: 저장된 Player 프리팹의 카메라 거리·피치·FOV를 사용
- 빈 화면 드래그: 회전, 마우스 휠: 확대/축소
- 장비 표시, 망토 흔들림 토글
- 이름과 키 표시, 바닥 1m 격자

게임 거리의 초기값은 이번 생성 시 실제 플레이어 프리팹에 저장된 2m / 15도 / FOV 60입니다. 씬별 카메라 오버라이드는 반영하지 않습니다. 이는 카메라 설정 비교용이며 플레이어와 적 사이의 실제 전투 간격을 재현한 씬은 아닙니다. 휠로 더 먼 거리에서도 확인할 수 있습니다.

## 전투용 외형 (2026-09-09, Mixamo 연결 완료)

메뉴 `SandGuard > Enemy > Connect Combat Art` (`Assets/Enemy/Editor/EnemyCombatArtBuilder.cs`)가 각 폴더에 전투용 세트를 만들고 Enemy 프리팹에 연결합니다. 위의 `_PreviewVisual`은 정적 확인용으로 그대로 두었습니다.

| 파일 | 내용 |
|---|---|
| `<이름>_Rig.fbx` | Mixamo 리깅 몸체(Humanoid, `Docs/model-art/bandit-*`의 `@…fbx` 또는 팩의 `bandit1.fbx`). 키는 위 표와 같게 자동 보정 |
| `<이름>_Idle/Move/Attack/Death.fbx` | `Docs/model-art/animation-selection-v1/<이름>/`에서 복사한 모션 전용 클립. 각각 자체 Humanoid 아바타 |
| `<이름>_BaseColor.jpg`, `<이름>_Combat.mat` | Tripo 원본 2K JPEG(`.fbm`)를 그대로 쓰는 URP 재질 |
| `<이름>.controller` | `Speed` / `Attack` / `Die`. Locomotion(Idle↔Move 블렌드) → Attack → Locomotion, AnyState → Dead |
| `<이름>_CombatVisual.prefab` | 몸체 + `EnemyVisualBindings` + 손 본 아래 장비. `EnemyVisuals.visualPrefab`에 넣는 프리팹 |

연결된 프리팹: `Assets/Enemy/Generated/Enemy.prefab`(검병 외형) 과 그 변형 `Enemy_Swordsman / Enemy_Assassin / Enemy_ShieldGuard / Enemy_HammerBrute / Enemy_Chief.prefab`. 변형별로 `EnemyMeleeAttack.windup/interval`(클립 타격 시점·길이), `EnemyMotor.moveSpeed`(방패병 2.0, 우두머리 2.2 — 걷기 클립 기준), `EnemyHealth.removeDelay`(사망 클립 길이 + 0.3초)를 덮어씁니다. `EnemyTest.unity` 뒤쪽 `Variant Showcase`에 변형 4종이 서 있습니다.

### 장비 부착 규칙

장비 프리팹의 `HandGrip`을 손목 원점 대신 손 안쪽의 `EquipmentPalm`에 맞춥니다. `<장비>_Placement` 래퍼는 손 본 아래에 두고, 손가락·엄지·손바닥 기하로 방향을 계산합니다. 모델과 원본 클립은 수정하지 않습니다.

- 검·단검·곡도·망치: 손잡이의 긴 축을 엄지 방향으로, 얇은 면을 손바닥 법선으로 맞춥니다. 휘어진 칼끝이나 옆으로 돌출된 망치 타격점을 방향 축으로 사용하지 않아 손잡이가 기울어지지 않습니다.
- 방패: 손잡이의 가로축과 손의 쥐는 축을 맞추며, `EnemyEquipmentGrip`이 왼손과 팔을 보정해 원형 방패·타워실드의 앞면과 세로축을 유지합니다. 팔꿈치는 뒤쪽으로 접어 방패 앞면을 뚫지 않게 합니다. 대기·이동·공격 모두 적용합니다.
- 망치: 오른손은 `HandGrip`, 왼손은 위쪽 `OffHandGrip`을 잡습니다. 애니메이션 적용 후 왼팔을 맞추고, 체형 차이로 손잡이가 왼팔의 도달 범위를 벗어나면 오른팔도 함께 안쪽으로 보정합니다. 팔 길이는 늘리지 않습니다.
- 장비를 든 손가락: Humanoid 쥐기 자세에서 리그에 존재하는 손가락 회전만 저장해 대기·이동·공격 중 유지합니다. 우두머리의 빈 왼손은 원본 동작을 사용합니다.
- 망토: `ShoulderAttach`를 `(0, 1.45, -0.13) × 키/1.8` 위치에 두고 가슴 본(`Spine1`) 아래에 붙입니다. 미리보기와 같은 좌표입니다.
- 장비 배율은 미리보기 값 그대로(검병 검 0.65, 방패병 검 0.72, 단검 0.63, 원형 방패 0.9, 나머지 1).

우두머리 리그는 엄지 본이 없어(33본) 엄지 방향을 정면(+Z)으로 가정합니다. 없는 손가락 본을 새로 만들지는 않습니다. 사망이 시작되면 팔·손가락 보정을 해제해 사망 클립을 그대로 재생합니다.

손·장비만 다시 맞추는 메뉴는 `SandGuard > Enemy > Fit Hands and Equipment`입니다. 외형 프리팹과 각 Enemy 프리팹의 저장된 외형을 갱신하며 이동 속도·공격 간격·피해량·씬 배치는 바꾸지 않습니다. `Connect Combat Art` 전체 재생성 메뉴에도 같은 보정이 연결되어 있습니다. 정적 `_PreviewVisual`은 기존 원본 미리보기이며, 실제 보정 동작은 `EnemyTest.unity`에서 확인합니다.

### 루트 모션과 이동 속도

클립은 루트 XZ 이동을 추출만 하고(`lockRootPositionXZ = false`) `applyRootMotion = false`로 버리므로 제자리에서 재생되고 이동은 `EnemyMotor`가 맡습니다. Y·회전은 포즈에 굽습니다(쓰러짐·점프 유지). Move 클립의 `averageSpeed`를 몸체 humanScale로 환산해 블렌드 트리 배율을 잡습니다(`build-report.txt`의 `stride`, `moveTimeScale`).

### 검증

2026-09-09 손·장비 보정 후 `SandGuard.Enemy.Tests` **12개 통과**. `Logs/enemy-equipment-fit-tests-v4.xml`에 결과를 저장했습니다. 각 손잡이와 손바닥의 정렬, 이동·공격·상태 전환 중 모든 관측 프레임의 망치 보조 손 오차(3.5cm 미만), 방패 전방/수직 유지, 사망 시 팔 보정 해제를 검사합니다. `captures/*_idle_hands.png`, `*_attack_hands.png`는 손 확대, `*_shield_back.png`는 방패 뒤쪽 손과 팔꿈치 확인용입니다.

`Assets/Enemy/Tests/EnemyArtTests.cs` — 적 5종마다 Humanoid 아바타, 2K 텍스처, 키, 장비가 지정 본 아래에 있는지, 이동 중 힙이 루트에서 0.45m 이상 벗어나지 않는지(루트 모션 누출), Attack 트리거→복귀, 사망→제거를 확인하고 `Docs/model-art/enemy-combat-art-v1/captures/`에 렌더를 남깁니다. `BindPoseEquipmentLayout`은 애니메이터를 끈 바인드 포즈의 장비 배치를 정면·위에서 찍습니다. 2026-09-09 `SandGuard.Enemy.Tests` 12개 통과.

`EnemyVisuals.RebuildVisual()`은 이제 `VisualRoot` 아래 자식을 전부 지웁니다. 프리팹 변형이 원본의 새 외형 자식을 다시 상속해 겹치는 것을 막기 위해서입니다.

## 재생성·검증

Blender의 `Docs/model-art/enemy-equipment-v1/export_preview_bodies.py`로 원본 GLB에서 몸체 FBX와 텍스처를 내보낼 수 있습니다. Unity 메뉴 `SandGuard > Enemy > Build Art Preview`는 이 외형 프리팹과 확인 씬을 다시 생성합니다. 재실행하면 생성된 장착 위치도 기본값으로 돌아가므로 수동 보정본은 별도 프리팹으로 보관하세요. 에디터 메뉴로 실행할 때는 현재 씬을 먼저 저장하세요.

정적 미리보기 렌더 PNG와 검사 결과는 `Docs/model-art/enemy-preview-v1`에 있습니다. 몸체 높이·바닥 기준, 손잡이와 소켓 정렬, 재질 연결, 선택·시점·장비 표시 기능을 검사했습니다. 전투 애니메이션과 손·장비 보정 렌더는 `Docs/model-art/enemy-combat-art-v1/captures`에 있습니다.

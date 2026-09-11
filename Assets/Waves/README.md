# 웨이브 (WaveSet → WaveDirector → EnemyPool)

레벨 데이터의 `WaveSet`을 그대로 실행합니다. 스포너는 없고, 적은 웨이브 그룹에 **등록만** 하면 카탈로그가 프리팹을 풀고 풀에서 꺼냅니다.

## 흐름

1. `LevelRoot.waves`(WaveSet) → 웨이브마다 `preparationSeconds` 카운트다운
2. 전투: 그룹마다 `delay` 뒤 `count`마리를 `interval` 간격으로 스폰. 위치는 `spawnId` 마커(`spawnRadius` 안 무작위, NavMesh에 스냅), 목표는 `EnemyObjective` → 없으면 `targetId` 코어 마커
3. 등장 예정(`PendingEnemyCount`)과 생존(`AliveEnemyCount`)이 모두 0이면 다음 웨이브 준비. 마지막 웨이브 뒤 `Victory`
4. 제거된 적은 `EnemyPool`에 비활성 보관 → 다음 스폰에서 `ResetForReuse`(새 EntityId·체력·NavMesh 위치·상태·애니메이터)로 되살림

집계는 `IWaveStateReader` 계약대로 `Died` 또는 `Despawned` 중 먼저 온 것에서 한 번만 뺍니다. `WaveDirector`는 `IGameStateReader`이기도 해서 건설 서비스(`FacilityBuildService`)가 자동으로 단계를 따릅니다.

## 적 등록

| 어디 | 무엇 |
|---|---|
| `Assets/Waves/Generated/Elements/Bandit_*.asset` | `LevelElementDefinition` — `gameKey` (`bandit.swordsman` / `bandit.assassin` / `bandit.shieldguard` / `bandit.hammerbrute` / `bandit.chief`) |
| `Assets/Waves/Generated/EnemyCatalog.asset` | gameKey → `Assets/Enemy/Generated/Enemy_*.prefab` |
| `Assets/Waves/Generated/CanyonBanditWaves.asset` | 협곡 5웨이브(검병 → +암살자 → +방패병 → 철거꾼 → 우두머리). 수치는 임시값이라 인스펙터에서 바꾸세요 |

새 적을 넣으려면: 요소 에셋(`Desert Tower > Level Element`)에 gameKey → 카탈로그에 같은 키와 프리팹 → 웨이브 그룹의 `element`에 요소를 지정. 웨이브 에셋은 프리팹을 모릅니다.

## 만들기·연결

- `SandGuard > Waves > Create Missing Assets` — 요소·카탈로그·웨이브 세트 생성(재실행하면 웨이브 구성을 코드 값으로 되돌림)
- `SandGuard > Waves > Wire Into Player And Enemy Scene` — 씬 복사본의 `LevelRoot.waves`를 협곡 웨이브로 바꾸고 `Wave Director` + `Enemy Pool`을 넣습니다. 원본 레벨(`Assets/Resources/DesertTowerLevels/...`)은 건드리지 않습니다. `SandGuard > Player And Enemy > Rebuild Test Scene`도 같은 연결을 합니다
- 다른 레벨이면 그 레벨 기존 WaveSet의 첫 그룹이 가리키는 스폰·코어·경로 ID를 따릅니다

## 풀 재사용에 필요한 적 쪽 훅

`EnemyHealth.ReleaseHandler`(있으면 Destroy 대신 호출) + `ResetForReuse()`, `EnemyMotor.Enable(위치)`, `EnemyBrain / EnemyMeleeAttack / EnemyVisuals.ResetForReuse()`. 핸들러가 없으면 기존처럼 Destroy됩니다. 적 프리팹에 상태를 가진 컴포넌트를 추가하면 `EnemyPool.Revive`에 초기화를 함께 넣으세요.

## 조정 지점

- `WaveDirector.autoStart`(씬 시작 시 첫 준비), `loopWaves`, `objective`(진격 목표 강제), `spawnSampleRadius`
- HUD(`PlayerAndEnemyOverlay`): 웨이브 번호/준비 시간/남은 등장/생존, "준비 건너뛰기"

## 검증

`Assets/Waves/Tests/WavePlayModeTests.cs` — 준비→전투 전환, 2마리 스폰(풀 생성 2), 처치 즉시 생존 0, 다음 웨이브 준비, 시체가 풀로 복귀(IdleCount 2), 2웨이브에서 **재사용 2·신규 0**, 재사용 적의 새 EntityId·만피·NavMesh 복귀·충돌체·사망 자세 해제·재진격, 마지막 웨이브 뒤 Victory. 2026-09-09 `SandGuard.Waves/PlayerAndEnemy/Enemy/Facility.Tests` 16개 통과.

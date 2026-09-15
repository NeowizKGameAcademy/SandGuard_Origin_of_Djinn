# 웨이브 (WaveSet → WaveDirector → EnemyPool)

레벨 데이터의 `WaveSet`을 그대로 실행합니다. 스포너는 없고, 적은 카탈로그에 **등록만** 하면 웨이브가 키로 부르고 풀에서 꺼냅니다.

실행기는 `DesertTower.LevelIntegration.WaveDirector` 하나입니다. 이 폴더(`Assets/Waves`)에는 적 풀과 협곡 웨이브 에셋 생성기만 남아 있습니다.

## 흐름

1. `LevelRoot.waves`(WaveSet) → 웨이브마다 `preparationSeconds` 카운트다운. 이 동안 다음 웨이브가 쓸 적을 **프레임당 하나씩 미리 만들어** 풀에 쌓는다
2. 전투: 그룹마다 `delay` 뒤 `count`마리를 `interval` 간격으로 스폰. 위치는 `spawnId` 마커의 **정확한 좌표**(나선 지형에서 NavMesh 샘플링이 아래층으로 새는 것을 막는다)
3. 이동: `RouteGraph` 노드를 따라 층을 올라가고, 노드와 노드 사이는 NavMesh가 건는다. 분기는 `RouteLink.weight` 가중 무작위. 그룹에 `routeId`가 있으면 그 `LevelRoute`의 웨이포인트를 그대로 따른다
4. 코어 도착 → `ILevelCoreReceiver.TryAbsorb`로 피해를 넘기고 적은 풀로 돌아간다
5. 모든 그룹이 소진되고 생존이 0이면 다음 웨이브. 마지막 웨이브 뒤 `Won`
6. 제거된 적은 `EnemyPool`에 비활성 보관 → 다음 스폰에서 `ResetForReuse`(새 EntityId·체력·NavMesh 위치·상태·애니메이터)로 되살림

`WaveDirector`는 `IWaveStateReader`와 `IGameStateReader`를 구현합니다. HUD(`GameHUDPresenter`)가 웨이브 번호·준비 시간을 읽고, 건설(`FacilityBuildService`, `PlacementPhaseRule`)이 단계를 따릅니다.

## 적 등록

| 어디 | 무엇 |
|---|---|
| `Assets/2.Model/Prefabs/Level/GamePrefabCatalog.asset` | key → displayName → role → 프리팹. **적 카탈로그는 이 하나뿐** |
| 적 프리팹 루트의 `ActorBridge` | 경로 러너와 적 AI의 연결. `coreDamage`(코어에 입히는 피해)를 여기서 적별로 조정 |
| `Assets/Waves/Generated/Elements/Bandit_*.asset` | `LevelElementDefinition` — 편집기 표시용(이름·색·프리뷰). 웨이브가 키를 직접 들면 필수는 아님 |
| `Assets/Waves/Generated/CanyonBanditWaves.asset` | 협곡 5웨이브. 수치는 임시값이라 편집기에서 바꾸세요 |

새 적을 넣으려면: 프리팹을 `Enemy.prefab`의 변형으로 만들고(→ `ActorBridge` 상속) → 카탈로그에 key·이름·프리팹을 `role: Enemy`로 등록 → 웨이브 그룹에서 목록으로 고릅니다. 웨이브 에셋은 프리팹을 모릅니다.

`Tools > Desert Tower > Enemy Bridge — 검사만`이 브리지 누락·중복을 잡아 줍니다.

## 기획자용 편집

`Tools > Desert Tower > Level Editor` → **웨이브 구성** 탭.

웨이브 이름·준비 시간, 그룹마다 스폰/목표 코어/경로(전부 드롭다운), **적 종류(카탈로그 드롭다운)**, 출현 수, 시작 지연, 출현 간격을 편집합니다.

적 종류 드롭다운은 `LevelRoot.elementCatalog`에 게임 카탈로그가 지정돼 있어야 뜹니다. 비어 있으면 예전처럼 요소 에셋을 직접 지정하는 필드가 나옵니다.

## 메뉴

| 메뉴 | 동작 |
|---|---|
| `SandGuard > Waves > Create Missing Assets` | 없는 에셋만 생성. **기존 웨이브 구성·요소·카탈로그 항목은 건드리지 않음** |
| `SandGuard > Waves > Reset Canyon Waves To Code Defaults` | 협곡 웨이브를 코드 기본값으로 되돌림. 확인 대화상자 있음 |
| `Tools > Desert Tower > Wire Level Scene — 검사만 / 적용` | Level 씬의 코어 배선·마커 정렬·카탈로그 연결. 멱등 |
| `Tools > Desert Tower > Enemy Bridge — 검사만 / 적용` | 적 기반 프리팹의 `ActorBridge` 부착·중복 정리 |

## 풀 재사용에 필요한 적 쪽 훅

`EnemyHealth.ReleaseHandler`(있으면 Destroy 대신 호출) + `ResetForReuse()`, `EnemyMotor.Enable(위치)`, `EnemyBrain / EnemyMeleeAttack / EnemyVisuals.ResetForReuse()`. 핸들러가 없으면 기존처럼 Destroy됩니다. 적 프리팹에 상태를 가진 컴포넌트를 추가하면 `EnemyPool.Revive`에 초기화를 함께 넣으세요.

## 조정 지점

- `WaveDirector`: `startAutomatically`(씬 시작 시 첫 준비), `randomSeed`(분기 재현), `maxSpawnsPerFrame`
- `EnemyPoolActorFactory`: `maxActive`(동시 활성 상한 — 넘으면 스폰을 다음 프레임으로 미룸), `prewarmPerPrefab`(0이면 미리 만들지 않음). 이 값은 `Wire Level Scene`이 웨이브 데이터에서 계산해 넣는다 — 한 웨이브가 같은 프리팹을 가장 많이 쓰는 수. 웨이브를 고쳤으면 도구를 다시 돌린다
- 풀 오브젝트는 씬에 둔다. 비워 두면 런타임에 이름 없이 생겨 인스펙터에서 보이지 않는다
- `ActorBridge.coreDamage`: 적 프리팹별 코어 피해
- `SkipPreparation()`: 남은 준비 시간을 건너뛴다

## 검증

**`Assets/DesertTowerLevelIntegration/Tests/LevelWavePlayModeTests.cs`** — Level 씬을 실제로 돌립니다.

- `SceneEntersPreparationAndReportsWaveState` — 준비 단계 진입, 상태 계약 전부(`Phase`·`WaveNumber`·`PendingEnemyCount`·`PreparationSecondsRemaining`·`NextRouteIds`)
- `PreparationPrewarmsThePool` — 풀이 씬에 배치돼 있고, 전투 시작 **전에** 예비 개체가 쌓이는지
- `PreparationEndsAndEnemiesSpawn` — 준비→전투 전환, 실제 스폰, `Changed` 이벤트
- `EnemiesClimbToCoreAndAreAbsorbed` — 0층 스폰에서 5층 코어까지 올라가 흡수

**`Assets/Waves/Tests/EnemyPoolTests.cs`** — 풀 자체. 실행기 구조에 기대지 않습니다.

- `PrewarmCreatesInactiveReserve` — 예비는 비활성으로 쌓이고 활성 수에 잡히지 않는다
- `PrewarmedInstanceIsRentedInsteadOfCreated` — 예비가 있으면 새로 만들지 않는다
- `RentReturnsNullWhenActiveLimitReached` — 상한을 넘으면 `null`을 준다(감독이 다음 프레임에 재시도)
- `ReleasedEnemyReturnsToPoolAndIsReused` — 파괴되지 않고 같은 개체가 새 ID·만피로 돌아온다

두 어셈블리는 **따로** 돌립니다. `-assemblyNames`에 둘을 함께 넘기면 이 환경에서 실패합니다.

```bash
Unity.exe -batchmode -accept-apiupdate -projectPath <프로젝트> -runTests -testPlatform PlayMode -assemblyNames DesertTower.LevelIntegration.Tests -testResults level.xml -logFile level.log
```

```bash
Unity.exe -batchmode -accept-apiupdate -projectPath <프로젝트> -runTests -testPlatform PlayMode -assemblyNames SandGuard.Waves.Tests -testResults pool.xml -logFile pool.log
```

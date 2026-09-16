# SandGuard SFX 제안 목록 v0.1

작성일: 2026-09-16
근거: `Docs/plan/Desert_Tower_상세_게임기획서_v0.2.txt` 10.5 음향, `Assets/Resources/VFX/README.md`, `Assets/Resources/VFX/RequestedSet.md`, `Protagonist.controller`·적 5종 컨트롤러 상태, `Level.unity` 배치, `WaveDirector` 상태.

분류는 요청대로 **VFX 부착 / 애니메이션 부착 / 환경 부착 / 배경음** 네 갈래이며, 마지막에 UI를 덧붙였다.
색 규칙(청록=코어·마력, 주황=화염, 붉은색=적 위험)에 맞춰 소리도 **청록=크리스탈·유리질 마력음, 주황=불, 붉은색=경고·타격**으로 톤을 나눈다.

우선순위: **P1** 없으면 게임이 비어 보이는 것, **P2** 완성도, **P3** 여유 있을 때.

파일명 규칙 제안: `SFX_<분류>_<대상>_<동작>_<번호>.wav` (예: `SFX_Player_ManaBolt_Fire_01`). 랜덤 변형은 `_01~_04`.

---

## 1. VFX에 붙는 SFX

프리팹이 스폰되는 순간(`PrefabPool.Spawn` / `VfxOneShot.Fire`)에 함께 재생한다. 루프형은 프리팹 활성/비활성에 맞춘다.

### 1-1. 플레이어 공격·스킬

| 우선 | VFX 프리팹 | 제안 소리 | 비고 |
|---|---|---|---|
| P1 | `VFX_Staff_Cast` + `VFX_ManaBolt_Projectile` | 마나탄 발사: 짧은 "슉" + 유리질 크리스탈 톤(청록) | `PlayerVisuals.onFired`. 응축 마나탄(⑬)은 피치 낮고 두껍게 별도 변형 |
| P2 | `VFX_ManaBolt_Projectile` | 비행 루프: 아주 작은 마력 윙윙 | 투사체 자식, 3D 감쇠 짧게 |
| P1 | `VFX_ManaBolt_Impact` | 착탄: 크리스탈 파열 + 모래 퍽 | 빔이 꿰뚫은 적마다도 재생되므로 동시 재생 수 제한 필요 |
| P1 | `VFX_Pierce_Beam` | 관통 빔: 짧은 충전(0.1초) → "즈왕" 레이저 방출 | `PlayerBasicAttack.BeamFired` |
| P1 | `VFX_Sand_Burst` | 모래 폭발: 저역 붐 + 모래 흩뿌림 | Q 모래 폭발·폭발 관통탄 공용. 카메라 킥과 동기 |
| P2 | `VFX_Sand_Root` | 모래 족쇄: 모래가 솟아 조이는 "크르륵" / 풀릴 때 부스러짐 | `EnemyRestraint.Restrain` / `VfxSandRoot.Release` |
| P1 | `VFX_Sand_Vortex` | 소용돌이: 시작 스팅어(모래 빨림) + 회전 바람 루프 + 종료 시 족쇄음 | E 스킬. 지역 오브젝트 수명 동안 루프 |
| P1 | `VFX_Sand_Storm` | 사막 폭풍: 강풍 + 모래 채찍 루프 5초, 페이드 아웃 | R 스킬. 틱 피해음은 넣지 않음(과밀) |
| P2 | `VFX_Updraft_Charge` + `VFX_Updraft_Lens` | 상승 기류 충전: 피치가 올라가는 바람 + 굴절 "웅" 루프. 취소 시 바람 빠짐 | `onChargeStarted` / `onCharging(세기)` 로 피치·볼륨 / `onChargeCancelled` |
| P1 | `VFX_Updraft_Launch` | 발사: 모래 기둥 폭발 + 상승 whoosh | `onLaunched` |
| P1 | `VFX_Dash_Airflow` / `VFX_Dash_SonicBoom` | 대시: 짧은 whoosh + 저역 팝 | `PlayerMotor.DashStarted`. 공중 대시는 팝만 |
| P2 | `VFX_AirJump_Ring` | 공중 점프: 마력 링 팝(청록) | `Jumped` + `LastJumpWasAirJump` |
| P2 | `VFX_Jump_Dust` / `VFX_Land_Dust` | 점프 모래 튐 / 착지 모래 | 발소리와 겹치지 않게 얇게 |
| P2 | 흔적 귀환(전용 VFX 없음) | 순간이동: 모래로 흩어짐 → 도착 지점 재응집 | `PlayerMotor.Teleported` |

### 1-2. 플레이어 상태

| 우선 | VFX 프리팹 | 제안 소리 | 비고 |
|---|---|---|---|
| P1 | `VFX_Player_Hit` + `VFX_Player_Hit_Screen` | 피격: 둔탁한 타격 + 저역 심장 펄스 1회 | `VfxHitReaction`. 비네트와 동기 |
| P1 | `VFX_LevelUp` | 레벨업: 금빛 상승 아르페지오 + 링 확산 | `PlayerProgression.LevelUp` |
| P1 | `VFX_Experience_Mote` | 드롭: 작은 짤랑 / 흡수: 크리스탈 틱, 연속 흡수 시 피치 계단 상승 | `ExperienceOrb` 획득 시 |
| P2 | `VFX_Mana_Charge` / `VFX_Mana_Charge_Complete` | 코어 마나 충전 루프: 램프로 빨려드는 마력 "웅" / 완료 차임 | `CoreController.ReGainMana`, 램프 발광과 동기 |
| P2 | 부활(전용 VFX 없음) | 마력 응집 → 숨 들이쉼 | `PlayerRespawner.Respawned` |

### 1-3. 적

| 우선 | VFX 프리팹 | 제안 소리 | 비고 |
|---|---|---|---|
| P1 | `VFX_Enemy_Spawn` | 출현: 모래 뚫고 솟는 "푸슉" + 낮은 북 1타 | 웨이브 시작 시 다수 동시 → 재생 수 제한 |
| P1 | `VFX_Enemy_Hit` | 피격: 가죽·살 타격 + 마력 지짐(청록) | 적 종류별 변형 불필요, 3~4개 랜덤 |
| P1 | `VFX_Enemy_Death` | 사망: 모래로 흩어짐 + 마력이 플레이어로 빨려가는 상승음 | `VfxParticleAttractor` 흡수 시간에 맞춤 |
| P1 | `VFX_Shield_Front_Guard` | 방패 방어: 금속 "캉" + 육각 마력 팝 | `EnemyShield.Guarded` |
| P2 | `VFX_Shield_Gold_Guard` | 황금 방패 방어: 종 울림 섞인 금속음 | 위와 같은 길이, 톤만 황금 |
| P2 | `VFX_Chief_Golden_Shield_Loop` | 소환 0.35초: 웅장한 금속 공명 / 유지 4초: 낮은 험 루프 / 해제 0.3초: 금빛 흩어짐 | `ChiefGoldenShieldSkill`, `VfxGoldenShield.PulseHit` |
| P1 | `VFX_Demolition_Bomb_Explosion` | 폭발: 큰 폭발 + 파편 + 사막 잔향 롤 | `ChiefBombProjectile.Detonate`. 투척·비행은 애니메이션 항목 |
| P2 | `VFX_Burning_Loop` | 불타는 적: 지글거림 2초 | 코브라 화염 피해 |

### 1-4. 시설·코어

| 우선 | VFX 프리팹 | 제안 소리 | 비고 |
|---|---|---|---|
| P1 | `VFX_Build_Poof` → `VFX_Build_Complete` | 건설: 모래 연막 "퍽"(0초) → 돌 "쿵" + 금빛 차임(0.12초) | `VfxPopIn` 단계와 동기, 카메라 셰이크 |
| P1 | `VFX_FlameCobra_Breath` | 화염 분사: 점화 "훅" → 토치 루프 → 꺼짐 "푸스" | `FireOnOff.Flame(bool)` Play/Stop |
| P2 | `VFX_Fire_Impact` | 화염 착탄: 불꽃 튐 | 틱마다 재생하면 과밀, 0.25초 간격 제한 |
| P2 | `Tower_Obelisk` (Obelisk Emmit / Slow Range) | 둔화 필드 루프: 모래시계 흐르는 모래 + 시간 늘어진 저역 펄스 | 전용 VFX 없음. 활성 동안 3D 루프 |
| P2 | `VFX_Summon_Circle` / `VFX_Summon_Pillar` | 소환진 루프: 룬 공명 / 빛기둥: 솟구침 + 해골 등장 "달그락" | 해골 소환진·아누비스 해금 후 |
| P1 | `VFX_Facility_Hit` | 시설 피격: 사암 타격 + 돌가루 | `VfxHitReaction` (Tower) |
| P1 | `VFX_Cobra_Destruction` | 코브라 파괴: 붕괴 + 돌 파편 낙하 | `VfxDestructionOnDeath` |
| P1 | `VFX_Facility_Disabled_Loop` | 정지 시작: 파워다운 "위이잉↓" / 정지 중: 전기 지직 루프 5초 / 복구: 파워업 | `TowerDisableReceiver.Stop/Resume` |
| P1 | `VFX_Wall_Hit` / `VFX_Wall_Destroy` | 벽 피격: 돌 타격 / 벽 파괴: 돌무더기 붕괴 | 크기는 `VfxVolume.Fit` 비율로 볼륨 조절 |
| P1 | `VFX_Core_Ambient` | 코어 루프: 크리스탈 험(청록). 안정도 60 이하 노란 경고 레이어, 30 이하 붉은 경고 펄스 추가 | `CoreAmbientVfx.SetStability` 경계와 동기 |
| P1 | `VFX_Core_Damage_Enemy` + `VFX_Core_Damage_Flash` | 코어 피격: 적 흡수 "슈욱" + 크리스탈 균열 + 경고음 1회 | `CoreReceiver.onChanged` |
| P1 | `VFX_Core_Destruction` | 코어 파괴: 대형 붕괴 → 0.5초 정적 → 긴 잔향 | `CoreReceiver.onDefeated`, 패배 스팅어와 이어짐 |
| P1 | `VFX_Wave_Clear` | 웨이브 클리어 스팅어(짧은 팡파르) | `WaveDirector.onWaveCleared` |

---

## 2. 애니메이션에 붙는 SFX

애니메이션 이벤트 또는 애니메이터 상태 진입에 붙인다. 발소리는 `VfxFootstepDust`와 같은 조건(수평 속도·접지)을 쓰면 클립을 건드리지 않아도 된다.

### 2-1. 플레이어 (`Protagonist.controller`)

| 우선 | 상태·클립 | 제안 소리 | 비고 |
|---|---|---|---|
| P1 | Walk/Run 4방향 | 모래 발소리 걷기·달리기 2세트, 각 6~8개 랜덤 | 표면 분기: 모래 / 돌 포장(신전 바닥) / 사암 |
| P1 | Jump | 짧은 기합 + 옷 스침 | `Jumped` 지상 점프일 때만 음성 |
| P2 | Double Jump | 음성 없이 마력 팝(1-1과 하나만) | 중복 금지 |
| P2 | Flip | 회전 whoosh | 공중 회전 |
| P2 | Falling | 낙하 바람 루프 | 0.5초 이상 체공 시 페이드 인 |
| P1 | Landing | 착지 발 + 옷 | 높이(`Landed(float)`)에 따라 볼륨 |
| P1 | Hard Landing | 무거운 착지 + 신음 | `HardLandingLocked` 동안 회복 숨소리(P3) |
| P1 | Dash / Dash Legs | 발 밀기 + whoosh (1-1 대시와 통합) | |
| P2 | Charge (Player Charge Crouch) | 웅크림 옷 + 숨 들이마심 | 상승 기류 충전과 동기 |
| P2 | Fly / Fly Apex | 상승 바람, 정점에서 잦아듦 | |
| P1 | Cast Magic / Cast Great Sword / Cast Sword Shield (Cast Style 0~2) | 시전 기합(짧게) + 지팡이 휘두름 whoosh | 세 스타일 톤만 다르게, 연사 시 음성 확률 30% |
| P2 | Air Cast | 위와 같음 + 공중 변형 | |
| P1 | Hit | 주인공 피격 음성 3~4종 | 1-2 피격 타격음과 레이어 |
| P1 | Death | 사망 음성 + 쓰러짐 | |
| P1 | Revive | 부활 숨 + 마력 응집(1-2와 하나만) | |
| P3 | Lamp 흔들림 | 램프 쇠사슬 짤랑 | `PlayerLampEquipment` sway 세기에 비례 |

### 2-2. 적 5종 (`Idle Move` / `Attack` / `Dead` / Chief `Throw`)

| 우선 | 종류 | 이동 | 공격 | 사망 |
|---|---|---|---|---|
| P1 | 검병 Swordsman | 가벼운 모래 발소리 + 가죽 | 검 whoosh + 기합 | 남성 사망 음성 + 쓰러짐 |
| P1 | 암살자 Assassin | 거의 무음, 천 스침 | 단검 빠른 whoosh(2연) | 짧은 비명 + 가벼운 쓰러짐 |
| P1 | 방패병 ShieldGuard | 무거운 발 + 금속 갑옷 삐걱 | 검 whoosh + 방패 덜컥 | 갑옷 낙하 금속음 + 음성 |
| P1 | 망치병 HammerBrute | 무거운 발 | 큰 whoosh → 지면 "쿵" + 낮은 포효 | 묵직한 쓰러짐 + 망치 낙하 |
| P1 | 우두머리 Chief (보스) | 무겁고 느린 발 + 망토 펄럭 | 곡도 whoosh + 보스 음성 | 긴 사망 음성 + 대형 쓰러짐 + 흙먼지 |

추가 애니메이션 훅:

| 우선 | 항목 | 제안 소리 | 비고 |
|---|---|---|---|
| P1 | Chief `Throw` (2.2초, 38%에 `ReleaseChiefBomb`) | 들어올림 기합 → 던지기 whoosh → 폭탄 도화선 지글 루프(비행 1초 + 0.35초) → 착지 바운스 | 폭발은 1-3 |
| P2 | Chief 황금 방패 소환 | 전용 클립 없음 → 1-3 VFX에 부착 | |
| P2 | `EnemyFall` (절벽 낙하) | 낙하 비명 + 착지 "쿵" | `Fell` / `Landed` |
| P3 | `EnemyRestraint.Knockback` | 밀림 신음 | |
| P3 | 방패병·족장 Idle | 갑옷 삐걱, 망토 펄럭(아주 작게) | |
| P1 | 적 근접 공격이 플레이어·시설·벽에 명중 | 대상별 피격음은 1절 참조 | `EnemyMeleeAttack` |

### 2-3. 시설 동작

| 우선 | 항목 | 제안 소리 | 비고 |
|---|---|---|---|
| P2 | `RotateTower` (코브라 회전) | 돌 마찰 짧은 grind, 회전 중에만 | 각속도에 비례 |
| P2 | `TowerOnOff` 활성/비활성 | 활성 "클릭+마력 점등" / 비활성 "꺼짐" | 재질 전환과 동기 |
| P2 | 해골 소환수 이동 (`CreatureMove`) | 뼈 덜그럭 발소리 | |
| P3 | 해골 소환수 소멸 (`Despawn`) | 뼈 무너짐 | |

---

## 3. 환경에 붙는 SFX

`Level.unity` 배치 기준. 대부분 3D 루프이며 `AudioMixer` 환경 그룹으로 묶는다.

| 우선 | 위치·오브젝트 | 제안 소리 | 비고 |
|---|---|---|---|
| P1 | 전체 (2D 베드) | 사막 바람 앰비언스 3층: 저역 바람 / 중역 모래 흐름 / 간헐 돌풍 gust(20~40초 랜덤) | 준비·전투 공통, 음악과 주파수 겹치지 않게 |
| P1 | `Storm - opaque buried curtain` / `curtain crown` (신전 외곽 모래폭풍 벽) | 폭풍 벽 루프: 플레이어가 가장자리에 가까울수록 커지는 거리 기반 강풍 | 반경 130m 안쪽은 작게, 벽에 닿으면 최대 |
| P2 | `Storm - player barrier` 접촉 | 강풍 밀침 "후우욱" | 경계 충돌 시 1회 |
| P1 | `Storm - magic lightning` (`StormLightning`, 1.5~4.5초 간격) | 마법 번개: 크랙 + 청록 마력 잔향 → 거리 딜레이 천둥 롤 | `Strike()` 시점, 조명 깜빡임과 동기 |
| P1 | `VFX_Torch` (횃불, 다수 배치) | 타닥거림 루프, 감쇠 반경 짧게(6~8m) | 인스턴스 많으므로 가까운 4개만 재생 |
| P1 | 코어 (`VFX_Core_Ambient`) | 크리스탈 험 3D 루프(1-4와 동일 소스), 코어 범위 진입 시 마나 회복 톤 | `CoreController.Range` 안 |
| P2 | 신전 내부·기둥 사이 | 리버브 존: 돌 실내 잔향 | 광장은 드라이, 내부는 잔향 |
| P1 | 표면별 발소리 | 모래 / 돌 포장(`Exposed processional paving`) / 사암 벽·계단 | 레이캐스트 재질 태그로 분기 |
| P2 | 스킬 제단 (`SkillAltarAnchor`) | 룬 공명 루프 (근접 시 페이드 인) | |
| P2 | 건설 슬롯 (`건설 슬롯` ×8) | 근접 시 빈 받침대의 마력 대기 루프 (아주 작게) | 시설이 지어지면 정지 |
| P2 | 적 출현 입구 (`EnemySpawn` 마커) | 웨이브 시작 직전 지면 진동 + 모래 흐름 | 1-3 출현음의 예고 |
| P3 | 야자수·마른 가지·깃발 | 바람에 흔들림, gust 때만 | 배치 시 |
| P3 | 원경 | 독수리·매 울음 one-shot 랜덤(60~120초) | 준비 단계에서만 |
| P2 | 메인 메뉴 (`MainScene`, God Ray / SkyTint) | 메뉴 앰비언스: 사막 바람 + 신비 드론 | 메뉴 테마 아래 깔림 |

---

## 4. 배경음 (BGM)

`WaveDirector` 상태(`Preparing` / `Running` / `Won` / `Lost`)와 보스·코어 이벤트로 전환한다. 수직 레이어링(같은 템포·키로 레이어 on/off)을 권장한다.

| 우선 | 상황 | 제안 | 전환 훅 |
|---|---|---|---|
| P1 | 메인 메뉴 | 아라비안·이집트 풍 테마: 우드, 네이, 카눈, 프레임 드럼, 느린 템포 | `MainScene` 진입 |
| P1 | 준비 단계 (Preparation) | 잔잔한 루프: 드론 + 타악 약하게. 카운트다운 마지막 10초에 타악 레이어 추가 | `PreparationSecondsRemaining` |
| P1 | 전투 (Combat) | 강한 타악 루프. 웨이브가 올라갈수록 레이어(현·금관·코러스) 추가 | `WaveIndex` |
| P1 | 보스 등장 | 보스 스팅어 → 보스 테마 전환(전투 테마 변주, 저역 강조) | `EnemyBossInfo` 등록 / `BossStatusHUD.ShowBoss` |
| P1 | 코어 위험 (안정도 30 이하) | 긴장 레이어: 저역 펄스 + 불협 스트링 | `CoreAmbientVfx.SetStability` 경계 |
| P1 | 웨이브 클리어 | 짧은 팡파르 스팅어 → 준비 테마로 복귀 | `onWaveCleared` |
| P1 | 승리 (Victory) | 승리 팡파르 → 결과 화면 루프(밝은 변주) | `GamePhase.Victory` |
| P1 | 패배 (Defeat) | 패배 스팅어 → 조용한 드론 | `CoreReceiver.onDefeated` |
| P2 | 스킬트리 창 열림 | 음악 로우패스 + 볼륨 -6dB (덕킹) | `SkillTreeWindow.onOpened/onClosed` |
| P2 | 플레이어 사망 → 부활 5초 | 음악 덕킹 + 로우패스, 부활 시 복귀 | `RespawnStarted` / `Respawned` |
| P3 | 일시정지 | 음악 로우패스, 환경음 정지 | `SetPhase(isPaused)` |

---

## 5. 덧붙임: UI SFX

| 우선 | 항목 | 제안 소리 | 훅 |
|---|---|---|---|
| P1 | 메뉴 버튼 호버 / 클릭 | 가벼운 모래 스침 / 돌 클릭 | `MenuButtonSelected.OnPointerEnter`, 클릭 |
| P1 | 건설 메뉴 열림 / 닫힘 | 마력 팝 / 역재생 | `FacilityBuildMenu.Open/Close` |
| P1 | 건설 성공 / 실패(잠김·마나 부족·점유) | 성공은 1-4 건설음 / 실패는 둔탁한 거절음 | `Select` 결과 |
| P1 | 스킬트리 열림 / 닫힘 / 구매 / 장착 / 실패 / 초기화 | 룬 페이지 넘김 / 금빛 해금 차임 / 슬롯 장착 클릭 / 거절음 | `SkillTreeWindow` |
| P2 | 스킬 쿨다운 완료 | 짧은 준비 완료 틱 | `CombatSkillSlotHUD.SetReady` |
| P2 | 스킬 사용 불가(마나·쿨다운) | 거절음 | `SetUnavailable` |
| P1 | 웨이브 시작 / 카운트다운 3-2-1 | 북 1타 / 틱 | `WaveHUD.SetWave` |
| P2 | 보스 체력바 등장 | 1회 스팅어(4절 보스 스팅어와 통합) | `ShowBoss` |
| P2 | 체력 낮음 | 심장 박동 루프(20% 이하) | `PlayerStatusHUD` |
| P2 | 레벨업 알림 / 스킬 포인트 획득 | 1-2 레벨업음과 통합 | |

---

## 6. 연결 시 지켜야 할 것

- **동시 재생 수 제한**: 기획서 10.5 요구. 피격·착탄·출현음은 같은 클립 최대 3~4 보이스, 같은 프레임 중복은 1회로 합친다.
- **믹서 그룹**: `Master > SFX / Environment / BGM / UI / Voice` 5그룹. 덕킹은 BGM 그룹 스냅샷으로.
- **재생 위치**: VFX 부착음은 VFX 프리팹 자식 `AudioSource`(3D)로 두면 `PrefabPool` 반환과 함께 정리된다. 루프형은 `OnDisable`에서 반드시 Stop.
- **랜덤 변형**: 발소리·피격·검 휘두름은 최소 4개, 피치 ±5% 랜덤.
- **거리 감쇠**: 플레이어 스킬·피격은 2D 또는 최소 거리 넓게, 적·시설·환경은 3D 로그 감쇠.
- **이미 있는 훅**: 본 문서의 "훅" 열은 모두 코드에 존재하는 이벤트·메서드다. 새 이벤트를 만들 필요 없이 리스너만 붙이면 된다.

# 큐 클립 점검

큐 114개, 클립이 있는 큐 68개, 서로 다른 파일 99개.

## 발견 사항

| 등급 | 큐 | 클립 | 내용 |
|---|---|---|---|
| 주의 | Env_DesertWind_Loop | DesertWind_Loop.wav | 58s인데 Streaming 아님 (loadType 0) |
| 주의 | Env_Lightning | 천둥-먼 거리.wav | 앞 무음 0.63s — 이벤트보다 늦게 들림 |
| 주의 | Env_Lightning | - | 변형끼리 크기 차 20.7 dB (Thunder_Near.wav -7.5 ↔ 천둥-먼 거리.wav -28.2) |
| 주의 | Music_Boss_Loop | 보스테마곡2.mp3 | 루프 이음매: 끝↔처음 점프 비율 0, 끝/처음 크기 차 +19.9 dB |
| 주의 | Music_Combat_Loop | 웨이브테마곡2.mp3 | 루프 이음매: 끝↔처음 점프 비율 0, 끝/처음 크기 차 -45.9 dB |
| 주의 | Music_Menu_Loop | 메인화면 배경음.mp3 | 루프 이음매: 끝↔처음 점프 비율 2, 끝/처음 크기 차 -14.2 dB |
| 주의 | Music_Preparation_Loop | 인게임 배경음.mp3 | 루프 이음매: 끝↔처음 점프 비율 0, 끝/처음 크기 차 -25.6 dB |
| 정보 | Core_Ping | CorePing.wav | 뒤 무음 0.90s — 보이스를 불필요하게 오래 잡음 |
| 정보 | Enemy_BodyFall | impactSoft_heavy_000.ogg | 3D 큐인데 스테레오 클립 (좌우 상관 1.00) — 3D에서는 Force To Mono 권장 |
| 정보 | Enemy_BodyFall | impactSoft_heavy_001.ogg | 3D 큐인데 스테레오 클립 (좌우 상관 1.00) — 3D에서는 Force To Mono 권장 |
| 정보 | Enemy_BodyFall, Player_BodyFall | impactSoft_heavy_000.ogg | 여러 큐가 같은 클립을 공유 |
| 정보 | Enemy_BodyFall, Player_BodyFall | impactSoft_heavy_001.ogg | 여러 큐가 같은 클립을 공유 |
| 정보 | Enemy_Chief_ShieldBlock, Enemy_ShieldBlock | ShieldHit.wav | 여러 큐가 같은 클립을 공유 |
| 정보 | Enemy_HammerBrute_Impact, Facility_Cobra_Destroy_Debris | impactMining_000.ogg | 여러 큐가 같은 클립을 공유 |
| 정보 | Enemy_HammerBrute_Impact, Facility_Cobra_Destroy_Debris | impactMining_001.ogg | 여러 큐가 같은 클립을 공유 |
| 정보 | Enemy_HammerBrute_Impact, Facility_Cobra_Destroy_Debris | impactMining_002.ogg | 여러 큐가 같은 클립을 공유 |
| 정보 | Enemy_HammerBrute_Impact, Facility_Hit, Wall_Hit | impactMining_003.ogg | 여러 큐가 같은 클립을 공유 |
| 정보 | Enemy_HammerBrute_Impact, Facility_Hit, Wall_Hit | impactMining_004.ogg | 여러 큐가 같은 클립을 공유 |
| 정보 | Env_DesertWind_Loop | DesertWind_Loop.wav | 2D 음악·앰비언스인데 모노 |
| 정보 | Env_Lightning | 천둥-먼 거리.wav | 뒤 무음 1.38s — 보이스를 불필요하게 오래 잡음 |
| 정보 | Facility_Cobra_Destroy, Wall_Destroy | TowerDestroy_01.wav | 여러 큐가 같은 클립을 공유 |
| 정보 | Music_Defeat | 게임오버_auda.wav | 2D 음악·앰비언스인데 모노 |
| 정보 | Player_SandBurst | SandBurst.wav | 뒤 무음 0.99s — 보이스를 불필요하게 오래 잡음 |

## 클립 측정

| 큐 | 클립 | 길이 | ch | Hz | 피크 dBFS | 최대100ms | 앞 무음 | 뒤 무음 | 끝 dB | 로드 |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| Core_Absorb | 호로록_01.wav | 1.66 | 2 | 96000 | -8.4 | -17.9 | 0.05 | 0.05 | -72 | Decompress |
| Core_Absorb | 호로록_02.wav | 1.50 | 2 | 96000 | -10.8 | -21.0 | 0.05 | 0.04 | -67 | Decompress |
| Core_Absorb | 호로록_03.wav | 1.88 | 2 | 96000 | -11.1 | -21.6 | 0.06 | 0.06 | -75 | Decompress |
| Core_Absorb | 호로록_04.wav | 1.26 | 2 | 96000 | -6.6 | -17.0 | 0.06 | 0.09 | -77 | Decompress |
| Core_Absorb | 호로록_05.wav | 1.81 | 2 | 96000 | -11.6 | -21.3 | 0.06 | 0.06 | -64 | Decompress |
| Core_Absorb | 호로록_06.wav | 1.18 | 2 | 96000 | -8.8 | -18.4 | 0.05 | 0.07 | -73 | Decompress |
| Core_Destroy | 폭발9_auda.wav | 1.94 | 1 | 48000 | -0.2 | -8.2 | 0.04 | 0.60 | -85 | Decompress |
| Core_Destroy_Debris | impactGlass_heavy_000.ogg | 0.24 | 2 | 44100 | -1.1 | -18.7 | 0.00 | 0.00 | -39 | Decompress |
| Core_Destroy_Debris | impactGlass_heavy_001.ogg | 0.43 | 2 | 44100 | -1.0 | -19.0 | 0.00 | 0.22 | -45 | Decompress |
| Core_Destroy_Debris | impactGlass_heavy_002.ogg | 0.25 | 2 | 44100 | -0.9 | -19.3 | 0.00 | 0.00 | -40 | Decompress |
| Core_Hit | impactGlass_medium_000.ogg | 0.54 | 2 | 44100 | -1.2 | -18.2 | 0.00 | 0.26 | -48 | Decompress |
| Core_Hit | impactGlass_medium_001.ogg | 0.54 | 2 | 44100 | -0.9 | -18.0 | 0.00 | 0.25 | -48 | Decompress |
| Core_Hit | impactGlass_medium_002.ogg | 0.54 | 2 | 44100 | -1.0 | -18.4 | 0.00 | 0.28 | -47 | Decompress |
| Core_Hum_Loop | 코어허밍_auda.wav | 9.38 | 1 | 48000 | -0.7 | -6.4 | 0.00 | 0.00 | -20 | Decompress |
| Core_Ping | CorePing.wav | 2.48 | 1 | 44100 | -18.2 | -23.2 | 0.00 | 0.90 | -75 | Decompress |
| Core_Warning | impactBell_heavy_000.ogg | 1.48 | 2 | 44100 | -1.1 | -16.4 | 0.00 | 0.43 | -60 | Decompress |
| Enemy_Assassin_Swing | 휘두름_가벼움_01.wav | 0.57 | 2 | 48000 | -2.6 | -12.8 | 0.00 | 0.05 | -68 | Decompress |
| Enemy_Assassin_Swing | Swing_Light_03.wav | 0.31 | 1 | 44100 | -1.0 | -13.4 | 0.03 | 0.09 | -60 | Decompress |
| Enemy_BodyFall | impactSoft_heavy_000.ogg | 0.51 | 2 | 44100 | -0.9 | -10.4 | 0.00 | 0.10 | -43 | Decompress |
| Enemy_BodyFall | impactSoft_heavy_001.ogg | 0.57 | 2 | 44100 | -1.0 | -10.1 | 0.00 | 0.12 | -43 | Decompress |
| Enemy_BodyFall | impactSoft_heavy_002.ogg | 0.57 | 2 | 44100 | -0.9 | -9.8 | 0.00 | 0.14 | -44 | Decompress |
| Enemy_BodyFall | impactSoft_heavy_003.ogg | 0.54 | 2 | 44100 | -1.0 | -9.4 | 0.00 | 0.07 | -41 | Decompress |
| Enemy_BodyFall | impactSoft_heavy_004.ogg | 0.50 | 2 | 44100 | -1.0 | -10.0 | 0.00 | 0.08 | -42 | Decompress |
| Enemy_Chief_Bomb_Explosion | BombExplosion.wav | 3.36 | 1 | 48000 | -0.5 | -9.8 | 0.01 | 0.65 | -91 | Decompress |
| Enemy_Chief_Bomb_Throw | 죽어!.wav | 0.88 | 2 | 44100 | -2.9 | -11.6 | 0.00 | 0.03 | -60 | Decompress |
| Enemy_Chief_ShieldBlock | ShieldHit.wav | 0.31 | 2 | 44100 | -2.8 | -9.7 | 0.01 | 0.01 | -46 | Decompress |
| Enemy_Chief_Swing | 휘두름_무거움_01.wav | 1.49 | 2 | 48000 | -0.5 | -9.2 | 0.00 | 0.05 | -69 | Decompress |
| Enemy_Chief_Swing | 휘두름_무거움_03.wav | 0.84 | 2 | 48000 | -0.6 | -8.7 | 0.00 | 0.05 | -65 | Decompress |
| Enemy_Death | 적 사망음1_auda.wav | 0.62 | 1 | 44100 | -10.8 | -19.2 | 0.01 | 0.06 | -69 | Decompress |
| Enemy_Death | 적 사망음2_auda.wav | 0.87 | 1 | 44100 | -11.1 | -18.9 | 0.00 | 0.00 | -36 | Decompress |
| Enemy_Death | 적 사망음3_auda.wav | 0.42 | 1 | 44100 | -11.1 | -19.7 | 0.00 | 0.03 | -73 | Decompress |
| Enemy_Footstep_Heavy | HeavyStep_01.wav | 0.52 | 2 | 44100 | -13.0 | -20.8 | 0.01 | 0.03 | -65 | Decompress |
| Enemy_Footstep_Heavy | HeavyStep_02.wav | 0.53 | 2 | 44100 | -13.3 | -20.4 | 0.00 | 0.02 | -70 | Decompress |
| Enemy_Footstep_Heavy | HeavyStep_03.wav | 0.49 | 2 | 44100 | -11.9 | -20.0 | 0.01 | 0.02 | -58 | Decompress |
| Enemy_Footstep_Light | 발걸음2_auda.wav | 0.76 | 2 | 44100 | -6.6 | -20.7 | 0.09 | 0.04 | -51 | Decompress |
| Enemy_HammerBrute_Impact | impactMining_000.ogg | 0.94 | 2 | 44100 | -1.1 | -11.2 | 0.00 | 0.41 | -65 | Decompress |
| Enemy_HammerBrute_Impact | impactMining_001.ogg | 0.87 | 2 | 44100 | -1.0 | -11.9 | 0.00 | 0.37 | -64 | Decompress |
| Enemy_HammerBrute_Impact | impactMining_002.ogg | 0.80 | 2 | 44100 | -1.2 | -14.1 | 0.00 | 0.35 | -60 | Decompress |
| Enemy_HammerBrute_Impact | impactMining_003.ogg | 0.99 | 2 | 44100 | -1.0 | -13.7 | 0.00 | 0.43 | -66 | Decompress |
| Enemy_HammerBrute_Impact | impactMining_004.ogg | 0.83 | 2 | 44100 | -0.9 | -10.9 | 0.00 | 0.35 | -61 | Decompress |
| Enemy_HammerBrute_Swing | 휘두름_무거움_02.wav | 1.56 | 2 | 48000 | -3.5 | -9.3 | 0.00 | 0.07 | -70 | Decompress |
| Enemy_HammerBrute_Swing | 휘두름_무거움_03.wav | 0.84 | 2 | 48000 | -0.6 | -8.7 | 0.00 | 0.05 | -65 | Decompress |
| Enemy_Hit | EnemyHit_01.wav | 0.61 | 2 | 48000 | -4.8 | -14.0 | 0.01 | 0.28 | -103 | Decompress |
| Enemy_Hit | EnemyHit_02.wav | 0.82 | 1 | 44100 | -4.3 | -11.6 | 0.00 | 0.53 | -91 | Decompress |
| Enemy_ShieldBlock | ShieldHit.wav | 0.31 | 2 | 44100 | -2.8 | -9.7 | 0.01 | 0.01 | -46 | Decompress |
| Enemy_ShieldGuard_Impact | impactMetal_light_000.ogg | 0.35 | 2 | 44100 | -1.0 | -15.4 | 0.00 | 0.17 | -57 | Decompress |
| Enemy_ShieldGuard_Impact | impactMetal_light_001.ogg | 0.25 | 2 | 44100 | -0.9 | -16.0 | 0.00 | 0.11 | -51 | Decompress |
| Enemy_ShieldGuard_Impact | impactMetal_light_002.ogg | 0.24 | 2 | 44100 | -1.4 | -16.9 | 0.00 | 0.11 | -53 | Decompress |
| Enemy_ShieldGuard_Impact | impactMetal_light_003.ogg | 0.48 | 2 | 44100 | -1.5 | -14.0 | 0.00 | 0.22 | -59 | Decompress |
| Enemy_ShieldGuard_Impact | impactMetal_light_004.ogg | 0.21 | 2 | 44100 | -1.3 | -17.4 | 0.00 | 0.10 | -50 | Decompress |
| Enemy_ShieldGuard_Swing | 휘두름_가벼움_02.wav | 0.48 | 2 | 48000 | -0.2 | -8.7 | 0.00 | 0.05 | -67 | Decompress |
| Enemy_ShieldGuard_Swing | 휘두름_가벼움_01.wav | 0.57 | 2 | 48000 | -2.6 | -12.8 | 0.00 | 0.05 | -68 | Decompress |
| Enemy_Spawn | 북소리_auda.wav | 5.39 | 1 | 48000 | -0.3 | -4.2 | 0.02 | 0.11 | -51 | Decompress |
| Enemy_Swordsman_Swing | 휘두름_가벼움_01.wav | 0.57 | 2 | 48000 | -2.6 | -12.8 | 0.00 | 0.05 | -68 | Decompress |
| Enemy_Swordsman_Swing | 휘두름_가벼움_02.wav | 0.48 | 2 | 48000 | -0.2 | -8.7 | 0.00 | 0.05 | -67 | Decompress |
| Enemy_Swordsman_Swing | Swing_Light_03.wav | 0.31 | 1 | 44100 | -1.0 | -13.4 | 0.03 | 0.09 | -60 | Decompress |
| Env_DesertWind_Loop | DesertWind_Loop.wav | 58.15 | 1 | 44100 | -1.1 | -10.7 | 0.00 | 0.00 | -25 | Decompress |
| Env_Lightning | Thunder_Near.wav | 22.01 | 2 | 48000 | 0.0 | -7.5 | 0.03 | 0.53 | -66 | Decompress |
| Env_Lightning | 천둥-먼 거리.wav | 18.99 | 2 | 48000 | -18.4 | -28.2 | 0.63 | 1.38 | -162 | Decompress |
| Env_Lightning | Thunder_Mid_Alternative.wav | 7.10 | 2 | 48000 | -10.8 | -20.6 | 0.00 | 0.12 | -77 | Decompress |
| Facility_Build_Complete | 타워 건설음_auda.wav | 0.50 | 1 | 44100 | -0.1 | -9.8 | 0.00 | 0.13 | -83 | Decompress |
| Facility_Cobra_Destroy | TowerDestroy_01.wav | 2.00 | 1 | 44100 | -11.8 | -17.4 | 0.00 | 0.01 | -50 | Decompress |
| Facility_Cobra_Destroy | TowerDestroy_02.wav | 1.07 | 2 | 48000 | -2.9 | -18.8 | 0.01 | 0.21 | -87 | Decompress |
| Facility_Cobra_Destroy_Debris | impactMining_000.ogg | 0.94 | 2 | 44100 | -1.1 | -11.2 | 0.00 | 0.41 | -65 | Decompress |
| Facility_Cobra_Destroy_Debris | impactMining_001.ogg | 0.87 | 2 | 44100 | -1.0 | -11.9 | 0.00 | 0.37 | -64 | Decompress |
| Facility_Cobra_Destroy_Debris | impactMining_002.ogg | 0.80 | 2 | 44100 | -1.2 | -14.1 | 0.00 | 0.35 | -60 | Decompress |
| Facility_Cobra_Flame_Loop | 코브라타워 화염방사_auda.wav | 5.48 | 1 | 44100 | -0.0 | -11.1 | 0.00 | 0.00 | -15 | Decompress |
| Facility_Hit | impactMining_003.ogg | 0.99 | 2 | 44100 | -1.0 | -13.7 | 0.00 | 0.43 | -66 | Decompress |
| Facility_Hit | impactMining_004.ogg | 0.83 | 2 | 44100 | -0.9 | -10.9 | 0.00 | 0.35 | -61 | Decompress |
| Music_Boss_Loop | 보스테마곡2.mp3 | 197.61 | 2 | 48000 | -3.1 | -14.1 | 0.16 | 0.63 | -89 | Streaming |
| Music_Combat_Loop | 웨이브테마곡2.mp3 | 179.85 | 2 | 48000 | -0.6 | -11.6 | 0.00 | 0.86 | -105 | Streaming |
| Music_Defeat | 게임오버_auda.wav | 3.40 | 1 | 48000 | -8.1 | -17.8 | 0.00 | 0.00 | -37 | Decompress |
| Music_Menu_Loop | 메인화면 배경음.mp3 | 219.97 | 2 | 48000 | -1.5 | -10.9 | 0.00 | 0.01 | -75 | Streaming |
| Music_Preparation_Loop | 인게임 배경음.mp3 | 212.77 | 2 | 48000 | -0.9 | -11.6 | 0.00 | 2.40 | -110 | Streaming |
| Music_Victory | 승리음악.wav | 4.53 | 2 | 48000 | -1.8 | -11.9 | 0.00 | 0.00 | -38 | Decompress |
| Player_AirJump | 더블점프_auda.wav | 0.31 | 1 | 44100 | -3.9 | -18.2 | 0.05 | 0.07 | -68 | Decompress |
| Player_BodyFall | impactSoft_heavy_000.ogg | 0.51 | 2 | 44100 | -0.9 | -10.4 | 0.00 | 0.10 | -43 | Decompress |
| Player_BodyFall | impactSoft_heavy_001.ogg | 0.57 | 2 | 44100 | -1.0 | -10.1 | 0.00 | 0.12 | -43 | Decompress |
| Player_Dash | Dash.wav | 0.53 | 2 | 48000 | -3.9 | -15.1 | 0.02 | 0.01 | -41 | Decompress |
| Player_Death | 적 사망음.wav | 0.70 | 2 | 48000 | -10.9 | -19.2 | 0.01 | 0.06 | -71 | Decompress |
| Player_Fall_Loop | Fall_Loop.wav | 3.70 | 1 | 48000 | -5.6 | -14.6 | 0.00 | 0.00 | -20 | Decompress |
| Player_Footstep_Sand | 플레이어 발걸음 _auda.wav | 0.37 | 1 | 44100 | -6.6 | -24.2 | 0.03 | 0.05 | -69 | Decompress |
| Player_Footstep_Sand | 발걸음2_auda.wav | 0.76 | 2 | 44100 | -6.6 | -20.7 | 0.09 | 0.04 | -51 | Decompress |
| Player_Footstep_Sand | SandStep_01.wav | 0.43 | 1 | 48000 | -8.7 | -23.0 | 0.00 | 0.01 | -48 | Decompress |
| Player_HardLand | 강한착지음_auda.wav | 0.77 | 1 | 48000 | -5.0 | -22.5 | 0.05 | 0.02 | -50 | Decompress |
| Player_Hit | 피격음.wav | 1.03 | 1 | 44100 | -1.0 | -11.6 | 0.08 | 0.61 | -85 | Decompress |
| Player_Jump | 점프_auda.wav | 0.29 | 1 | 44100 | -3.8 | -16.6 | 0.02 | 0.09 | -76 | Decompress |
| Player_Land | impactSoft_medium_000.ogg | 0.12 | 2 | 44100 | -1.0 | -13.6 | 0.00 | 0.00 | -33 | Decompress |
| Player_Land | impactSoft_medium_001.ogg | 0.18 | 2 | 44100 | -0.9 | -12.3 | 0.00 | 0.00 | -36 | Decompress |
| Player_Land | impactSoft_medium_002.ogg | 0.14 | 2 | 44100 | -1.0 | -12.9 | 0.00 | 0.00 | -33 | Decompress |
| Player_LevelUp | 레벨업_auda.wav | 1.15 | 1 | 44100 | -16.1 | -20.5 | 0.00 | 0.59 | -69 | Decompress |
| Player_LevelUp_Fanfare | LevelUpFanfare.wav | 1.81 | 2 | 96000 | -1.1 | -12.8 | 0.03 | 0.05 | -72 | Decompress |
| Player_ManaBolt_Fire | 기본공격_auda.wav | 0.42 | 1 | 44100 | -4.8 | -19.2 | 0.00 | 0.18 | -74 | Decompress |
| Player_ManaBolt_Impact | impactGlass_light_000.ogg | 0.21 | 2 | 44100 | -1.6 | -18.1 | 0.00 | 0.10 | -56 | Decompress |
| Player_ManaBolt_Impact | impactGlass_light_001.ogg | 0.21 | 2 | 44100 | -0.9 | -18.1 | 0.00 | 0.10 | -52 | Decompress |
| Player_ManaBolt_Impact | impactGlass_light_002.ogg | 0.21 | 2 | 44100 | -1.2 | -16.8 | 0.00 | 0.09 | -51 | Decompress |
| Player_ManaBolt_Impact | impactGlass_light_003.ogg | 0.21 | 2 | 44100 | -1.1 | -17.9 | 0.00 | 0.10 | -52 | Decompress |
| Player_ManaBolt_Impact | impactGlass_light_004.ogg | 0.21 | 2 | 44100 | -0.8 | -17.3 | 0.00 | 0.10 | -56 | Decompress |
| Player_PierceBeam_Fire | 빔발사_auda.wav | 1.28 | 1 | 48000 | 0.0 | -12.9 | 0.00 | 0.07 | -67 | Decompress |
| Player_Pierce_Charge | 짧은 충전음.wav | 1.20 | 2 | 96000 | -10.9 | -20.5 | 0.00 | 0.76 | -169 | Decompress |
| Player_Recall_Arrive | RecallArrive.wav | 3.01 | 2 | 48000 | -16.3 | -27.1 | 0.01 | 0.36 | -76 | Decompress |
| Player_Recall_Mark | 흔적마크설치_auda.wav | 1.12 | 2 | 48000 | -0.0 | -12.3 | 0.00 | 0.77 | -87 | Decompress |
| Player_Recall_Warp | 흔적귀환워프_auda.wav | 1.56 | 1 | 48000 | -3.9 | -15.4 | 0.02 | 0.18 | -55 | Decompress |
| Player_Revive | Revive.wav | 1.87 | 1 | 48000 | -6.6 | -17.0 | 0.01 | 0.63 | -85 | Decompress |
| Player_SandBurst | SandBurst.wav | 2.48 | 2 | 48000 | -0.1 | -7.2 | 0.00 | 0.99 | -91 | Decompress |
| Player_SandStorm_Cast | SandStorm_Cast.wav | 3.50 | 2 | 48000 | -5.1 | -14.9 | 0.03 | 0.02 | -56 | Decompress |
| Player_SandStorm_Loop | SandStorm_Loop.wav | 9.36 | 1 | 48000 | -2.0 | -13.0 | 0.00 | 0.00 | -15 | Decompress |
| Player_SandVortex_Loop | SandVortex_Loop.wav | 27.66 | 2 | 48000 | -3.8 | -10.9 | 0.00 | 0.00 | -19 | Decompress |
| Player_Updraft_Charge_Loop | Charge_Loop.wav | 1.30 | 2 | 48000 | -9.2 | -17.3 | 0.00 | 0.00 | -11 | Decompress |
| Player_Updraft_Launch | 폭발4_차지점프_auda.wav | 1.95 | 1 | 48000 | -0.0 | -3.9 | 0.02 | 0.34 | -82 | Decompress |
| UI_Click | UI버튼 클릭음_auda.wav | 0.25 | 1 | 44100 | -0.1 | -23.8 | 0.02 | 0.02 | -51 | Decompress |
| UI_Fail | 마나부족스킬잠금건설불가_auda.wav | 0.28 | 1 | 44100 | -10.0 | -12.7 | 0.01 | 0.08 | -74 | Decompress |
| Wall_Destroy | TowerDestroy_01.wav | 2.00 | 1 | 44100 | -11.8 | -17.4 | 0.00 | 0.01 | -50 | Decompress |
| Wall_Hit | impactMining_003.ogg | 0.99 | 2 | 44100 | -1.0 | -13.7 | 0.00 | 0.43 | -66 | Decompress |
| Wall_Hit | impactMining_004.ogg | 0.83 | 2 | 44100 | -0.9 | -10.9 | 0.00 | 0.35 | -61 | Decompress |
| Wave_Start | WaveStart.wav | 3.29 | 1 | 48000 | -1.1 | -8.1 | 0.01 | 0.10 | -86 | Decompress |

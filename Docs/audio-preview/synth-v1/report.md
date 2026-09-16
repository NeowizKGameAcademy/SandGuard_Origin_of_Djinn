# 합성 SFX 베이크 보고

생성: 2026-09-16 13:25. 레시피 `Assets/8.Audio/Editor/Synth/SynthRecipes.cs`.

| 파일 | 길이 | ch | 피크 dBFS | RMS dBFS | DC | NaN | 중심 Hz | 이음매 비율 | 머리/꼬리 dB | 판정 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| `SFX_Player_ManaBolt_Fire_01` | 0.45s | 1 | -1.1 | -20.1 | 0.0000 | 0 | 2435 | - | - | OK |
| `SFX_Player_ManaBolt_Fire_02` | 0.45s | 1 | -1.1 | -19.3 | 0.0000 | 0 | 2344 | - | - | OK |
| `SFX_Player_ManaBolt_Fire_03` | 0.45s | 1 | -1.1 | -18.9 | 0.0000 | 0 | 2300 | - | - | OK |
| `SFX_Player_ManaBolt_Fire_04` | 0.45s | 1 | -1.1 | -19.6 | 0.0000 | 0 | 2430 | - | - | OK |
| `SFX_UI_Click_01` | 0.12s | 1 | -3.0 | -19.0 | 0.0000 | 0 | 1821 | - | - | OK |
| `SFX_UI_Click_02` | 0.12s | 1 | -3.0 | -19.1 | 0.0000 | 0 | 1792 | - | - | OK |
| `SFX_UI_Click_03` | 0.12s | 1 | -3.0 | -18.7 | 0.0000 | 0 | 1811 | - | - | OK |
| `SFX_UI_Hover_01` | 0.09s | 1 | -8.0 | -24.7 | 0.0000 | 0 | 2372 | - | - | OK |
| `SFX_Core_Hum_Loop` | 6.00s | 2 | -8.0 | -15.1 | -0.0014 | 0 | 1232 | 1.26 | 0.2 | OK |
| `SFX_Env_DesertWind_Loop` | 12.00s | 2 | -10.0 | -26.9 | 0.0000 | 0 | 1720 | 0.68 | 0.9 | OK |
| `SFX_Player_LevelUp_01` | 2.40s | 2 | -1.1 | -17.1 | 0.0000 | 0 | 3433 | - | - | OK |

판정 기준: NaN 0, 피크 ≤ 0 dBFS, RMS > -60, |DC| < 0.02, 루프는 이음매 비율 < 3 및 머리/꼬리 RMS 차 < 6 dB.

## 레시피 메모

- `SFX_Player_ManaBolt_Fire_01`: 마나탄 발사: 클릭 + 하강 바디 + 크리스탈 부분음 + 공기 whoosh
- `SFX_Player_ManaBolt_Fire_02`: 마나탄 발사: 클릭 + 하강 바디 + 크리스탈 부분음 + 공기 whoosh
- `SFX_Player_ManaBolt_Fire_03`: 마나탄 발사: 클릭 + 하강 바디 + 크리스탈 부분음 + 공기 whoosh
- `SFX_Player_ManaBolt_Fire_04`: 마나탄 발사: 클릭 + 하강 바디 + 크리스탈 부분음 + 공기 whoosh
- `SFX_UI_Click_01`: UI 클릭: 두 부분음 핑 + 틱
- `SFX_UI_Click_02`: UI 클릭: 두 부분음 핑 + 틱
- `SFX_UI_Click_03`: UI 클릭: 두 부분음 핑 + 틱
- `SFX_UI_Hover_01`: UI 호버: 클릭보다 얇고 높음
- `SFX_Core_Hum_Loop`: 코어 크리스탈 험 6초 루프. 주기 층은 1/T 배수 주파수, 숨결 층만 크로스페이드
- `SFX_Env_DesertWind_Loop`: 사막 바람 베드 12초 루프: 갈색 잡음 2층 + 휘파람 + 돌풍 2회
- `SFX_Player_LevelUp_01`: 레벨업: 상승 아르페지오 종 + 스파클 + 서브 스웰 + 잔향

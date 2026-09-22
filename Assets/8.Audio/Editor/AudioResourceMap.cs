using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SandGuard.Audio.Editor
{
    /// <summary>
    /// 담당자가 준 `Assets/8.Audio/AudioResource`의 파일(한국어 이름)을 큐에 대응시킨다.
    /// 같은 소리의 원본과 `_auda`(정리본)가 둘 다 있으면 `_auda`만 쓴다. 바꾸고 싶으면 표를 고친다.
    /// 규칙: 큐의 클립 목록이 비어 있을 때만 채운다. 담당자가 인스펙터에서 바꾼 것은 건드리지 않는다.
    /// `SFX_`로 시작하는 클립(코드 합성·자리표시)은 큐에서 제거한다.
    /// </summary>
    public static class AudioResourceMap
    {
        public const string Dir = "Assets/8.Audio/AudioResource";

        public static readonly (string cue, string[] files)[] Map =
        {
            ("Enemy_Voice_Taunt", new[] { "도발 3_auda", "도발 5_auda", "도발 6_auda", "도발 10_auda", "도발 10_auda_2", "도발 10_auda_3", "도발 11_auda", "도발 12_auda" }),
            ("UI_Click", new[] { "UI버튼 클릭음_auda" }),
            ("Player_HardLand", new[] { "강한착지음_auda" }),
            ("Env_DesertWind_Loop", new[] { "기본 사막환경 배경음_auda" }),
            ("Player_ManaBolt_Fire", new[] { "기본공격_auda" }),
            ("Player_Dash", new[] { "대쉬 효과음_auda" }),
            ("Player_AirJump", new[] { "더블점프_auda" }),
            ("Player_LevelUp", new[] { "레벨업_auda" }),
            ("Player_ManaCharge_Complete", new[] { "짧은 충전음" }), // 2026-09-21 마나 회복 1틱(코어·오벨리스크 근처). 관통탄 충전과 같은 클립을 쓴다
            ("UI_Fail", new[] { "마나부족스킬잠금건설불가_auda" }),
            ("Music_Menu_Loop", new[] { "메인화면 배경음" }),
            ("Player_Footstep_Sand", new[] { "플레이어 발걸음 _auda" }), // 2026-09-18: 한 클립만 (딛는 소리 + 모래, 가장 빠른 어택). 적은 발걸음2로 구분
            ("Enemy_ShieldBlock", new[] { "방패타격음_auda" }),
            ("Wave_Start", new[] { "웨이브 시작음_auda" }),
            ("Music_Preparation_Loop", new[] { "인게임 배경음" }),
            ("Music_Combat_Loop", new[] { "인게임 배경음" }),
            ("Enemy_Death", new[] { "적 사망음1_auda", "적 사망음2_auda", "적 사망음3_auda" }),
            ("Player_Jump", new[] { "점프_auda" }),
            ("Player_Fall_Loop", new[] { "추락바람소리" }),
            ("Facility_Cobra_Flame_Loop", new[] { "코브라타워 화염방사_auda" }),
            ("Core_Ping", new[] { "코어핑_auda" }),
            ("Facility_Build_Complete", new[] { "타워 건설음_auda" }),
            ("Facility_Cobra_Destroy", new[] { "타워파괴2_auda", "타워파괴음" }),
            ("Enemy_Chief_Bomb_Explosion", new[] { "폭발", "폭발2" }),
            ("Enemy_Hit", new[] { "피격음", "피격음2", "피격음3_auda" }),
            ("Player_SandStorm_Cast", new[] { "폭풍" }),
            ("Player_Recall_Warp", new[] { "whoosh모음" }),

            /* ---- 2026-09-18 Docs/Audio 추가분 (비어 있는 큐만 채움) ---- */
            ("Env_Lightning", new[] { "천둥-가까운 거리", "천둥-중간", "천둥-먼 거리" }),
            ("Core_Hum_Loop", new[] { "코어허밍_auda" }),
            ("Enemy_Spawn", new[] { "북소리_auda" }),                      // 출현: 낮은 북 1타
            ("Enemy_Swordsman_Swing", new[] { "휘두르는소리" }),
            ("Enemy_Assassin_Swing", new[] { "휘두르는소리" }),
            ("Enemy_ShieldGuard_Swing", new[] { "휘두르는소리" }),
            ("Enemy_HammerBrute_Swing", new[] { "휘두르는소리" }),
            ("Enemy_Chief_Swing", new[] { "휘두르는소리" }),
            ("Enemy_Chief_Bomb_Throw", new[] { "죽어!" }),                 // 폭탄 던질 때 외침
            ("Player_Updraft_Charge_Loop", new[] { "기 모으기" }),
            ("Player_Updraft_Launch", new[] { "폭발4_차지점프_auda" }),
            ("Player_SandBurst", new[] { "폭발3" }),
            ("Player_SandStorm_Loop", new[] { "폭풍_auda" }),
            ("Player_SandVortex_Loop", new[] { "바람소리_auda" }),
            ("Player_ManaBolt_Impact", new[] { "impactGlass_light_000", "impactGlass_light_001", "impactGlass_light_002", "impactGlass_light_003", "impactGlass_light_004" }),
            ("Player_Land", new[] { "impactSoft_medium_000", "impactSoft_medium_001", "impactSoft_medium_002" }),
            ("Player_BodyFall", new[] { "impactSoft_heavy_000", "impactSoft_heavy_001" }),
            ("Enemy_BodyFall", new[] { "impactSoft_heavy_000", "impactSoft_heavy_001", "impactSoft_heavy_002", "impactSoft_heavy_003", "impactSoft_heavy_004" }),
            ("Enemy_ShieldGuard_Impact", new[] { "impactMetal_light_000", "impactMetal_light_001", "impactMetal_light_002", "impactMetal_light_003", "impactMetal_light_004" }),
            ("Enemy_HammerBrute_Impact", new[] { "impactMining_000", "impactMining_001", "impactMining_002", "impactMining_003", "impactMining_004" }),
            ("Core_Destroy_Debris", new[] { "impactGlass_heavy_000", "impactGlass_heavy_001", "impactGlass_heavy_002" }),
            ("Facility_Cobra_Destroy_Debris", new[] { "impactMining_000", "impactMining_001", "impactMining_002" }),
            ("Core_Warning", new[] { "impactBell_heavy_000" }),
        };

        /// <summary>
        /// 담당자가 넣어 둔 것 중 명백히 자리가 다른 클립을 바로잡는다. 큐의 클립이 정확히 `wrong` 하나일 때만 `right`로 바꾼다.
        /// (코어 험 루프에 핑이 들어가 있었고, 이제 전용 허밍 파일이 생겼다.)
        /// </summary>
        public static readonly (string cue, string wrong, string right)[] Corrections =
        {
            ("Core_Hum_Loop", "코어핑_auda", "코어허밍_auda"),
        };

        /// <summary>
        /// 2026-09-18 일괄 교체. 이미 채워진 큐도 더 맞는 클립으로 바꾼다 (사용자 지시). 빈 배열은 "비운다".
        /// 자동 빌드에는 포함되지 않고 메뉴로 한 번만 실행한다 — 이후 담당자가 큐에서 바꾼 것은 다시 덮어쓰지 않는다.
        /// 기준: 같은 소리는 `_auda` 정리본, 변형이 있으면 전부 넣기, 자리와 다른 소리(파괴음이 건설 연막에, 핑이 피격에)는 바로잡기,
        ///       한 동작에 같은 클립이 두 번 나는 구성(사망음 + 사망 음성)은 한쪽만 남기기.
        /// </summary>
        public static readonly (string cue, string[] files)[] Overrides =
        {
            ("UI_Click", new[] { "UI버튼 클릭음_auda" }),
            ("Env_DesertWind_Loop", new[] { "기본 사막환경 배경음_auda" }),
            ("Player_ManaBolt_Fire", new[] { "기본공격_auda" }),
            ("Player_Dash", new[] { "대쉬 효과음_auda" }),
            ("Player_AirJump", new[] { "더블점프_auda" }),
            ("Player_Jump", new[] { "점프_auda" }),
            ("Player_LevelUp", new[] { "레벨업_auda" }),
            ("UI_Fail", new[] { "마나부족스킬잠금건설불가_auda" }),
            ("Player_Footstep_Sand", new[] { "플레이어 발걸음 _auda" }),
            ("Enemy_Footstep_Light", new[] { "발걸음2_auda" }),
            ("Enemy_Footstep_Heavy", new[] { "몬스터발소리" }),
            ("Enemy_ShieldBlock", new[] { "방패타격음_auda" }),
            ("Enemy_Chief_ShieldBlock", new[] { "방패타격음_auda" }),
            ("Wave_Start", new[] { "웨이브 시작음_auda" }),
            ("Enemy_Death", new[] { "적 사망음1_auda", "적 사망음2_auda", "적 사망음3_auda" }),
            ("Enemy_Voice_Death_Light", new string[0]),     // Enemy_Death와 같은 클립이 두 번 나던 것을 정리
            ("Enemy_Voice_Death_Heavy", new string[0]),
            ("Player_Fall_Loop", new[] { "추락바람소리_auda" }),
            ("Facility_Cobra_Flame_Loop", new[] { "코브라타워 화염방사_auda" }),
            ("Facility_Build_Complete", new[] { "타워 건설음_auda" }),
            ("Facility_Build_Poof", new string[0]),         // 파괴음이 들어가 있었다. 맞는 연막 소리가 없어 비운다
            ("Facility_Cobra_Destroy", new[] { "타워파괴2_auda", "타워파괴음" }),
            ("Wall_Destroy", new[] { "타워파괴2_auda" }),
            ("Enemy_Chief_Bomb_Explosion", new[] { "폭발", "폭발2" }),
            ("Core_Hit", new[] { "impactGlass_medium_000", "impactGlass_medium_001", "impactGlass_medium_002" }), // 핑 대신 크리스탈 균열
            ("Wall_Hit", new[] { "impactMining_003", "impactMining_004" }),     // 방패 금속음 대신 돌 타격
            ("Facility_Hit", new[] { "impactMining_003", "impactMining_004" }),
            ("Env_Menu_Ambience_Loop", new string[0]),      // 메뉴 곡이 Music_Menu_Loop와 겹쳐 두 번 나던 것을 정리
        };

        /// <summary>
        /// 2026-09-18 폭발음 분할(폭발5~9) 뒤 교체. 측정: 9_auda 어택 0.16s·꼬리 1s·밝음 → 코어 파괴 폭발,
        /// 5 저역 80%·꼬리 2.1s → 철거 폭탄(큰 폭발 + 잔향 롤). 6·7·8은 14~22초 롤이라 원샷으로 쓰지 않는다.
        /// </summary>
        public static readonly (string cue, string[] files)[] ExplosionOverrides =
        {
            ("Core_Destroy", new[] { "폭발9_auda" }),
            ("Enemy_Chief_Bomb_Explosion", new[] { "폭발5" }),
        };

        [MenuItem("SandGuard/Audio/Apply Explosion Overrides 2026-09-18")]
        public static void ApplyExplosionOverrides() => ApplyTable(ExplosionOverrides, "AUDIO_EXPLOSION_OVERRIDES_APPLIED");

        /// <summary>
        /// 2026-09-18 오후 추가분. `_auda`가 있으면 그것만. 측정 근거는 README.
        /// 짧은 충전음(1.2s)은 관통탄 충전(0.9s)에 맞춘 원샷이라 충전 루프(기 모으기) 대신 쓴다 — 루프 큐는 비운다.
        /// 호로록 모음은 ffmpeg로 6조각(호로록_01~06)으로 잘라 코어 흡수음 변형으로 쓴다. 3.3초짜리 한 덩이는 길어서 뺐다.
        /// 보스테마곡2·웨이브테마곡2는 1과 바이트까지 같은 중복이라 쓰지 않는다.
        /// </summary>
        public static readonly (string cue, string[] files)[] Overrides0918b =
        {
            ("Player_Recall_Mark", new[] { "흔적마크설치_auda" }),
            ("Player_Recall_Warp", new[] { "흔적귀환워프_auda" }),
            ("Player_Recall_Arrive", new[] { "흔적귀환워프완료" }),
            ("Player_PierceBeam_Fire", new[] { "빔발사_auda" }),
            ("Player_Pierce_Charge", new[] { "짧은 충전음" }),
            ("Player_Pierce_Charge_Loop", new string[0]),
            ("Player_Revive", new[] { "부활_auda" }),
            ("Player_LevelUp_Fanfare", new[] { "레벨업팡파레" }),
            ("Wave_Start", new[] { "웨이브시작_auda" }),
            ("Music_Combat_Loop", new[] { "웨이브테마곡" }),
            ("Music_Boss_Loop", new[] { "보스테마곡" }),
            ("Music_Victory", new[] { "승리음악" }),
            ("Music_Defeat", new[] { "게임오버_auda" }),
            ("Core_Absorb", new[] { "호로록_01", "호로록_02", "호로록_03", "호로록_04", "호로록_05", "호로록_06" }),
        };

        /// <summary>
        /// 2026-09-18 최신 버전으로 교체. 보스·웨이브 테마 2는 14:31/14:32에 다시 저장된 새 버전(같은 템포·조, 화성 유사도 0.99,
        /// 길이만 다름. 웨이브2는 끝이 -40dB로 닫혀 반복에 자연스럽다). 대쉬효과음2(09-18)는 대쉬 효과음_auda(09-17)보다 나중 파일.
        /// </summary>
        public static readonly (string cue, string[] files)[] Overrides0918c =
        {
            ("Music_Combat_Loop", new[] { "웨이브테마곡2" }),
            ("Music_Boss_Loop", new[] { "보스테마곡2" }),
            ("Player_Dash", new[] { "대쉬효과음2" }),
        };

        /// <summary>
        /// 2026-09-18 휘두름 다양화. whoosh모음을 5조각으로 잘라 최고점을 맞췄다(가벼움 0.12s, 무거움 0.30s). 기존 휘두르는소리(최고점 0.09s)는 가벼운 묶음.
        /// 가벼움_01 밝고 빠름 · 가벼움_02 중간 · 무거움_01 중간 무게의 느린 휘두름 · 무거움_02 저역 99% 묵직한 바람 · 무거움_03 저역 83% 무거움.
        /// </summary>
        public static readonly (string cue, string[] files)[] SwingOverrides =
        {
            ("Enemy_Swordsman_Swing", new[] { "휘두름_가벼움_01", "휘두름_가벼움_02", "휘두르는소리" }),
            ("Enemy_Assassin_Swing", new[] { "휘두름_가벼움_01", "휘두르는소리" }),
            ("Enemy_ShieldGuard_Swing", new[] { "휘두름_가벼움_02", "휘두름_가벼움_01" }),
            ("Enemy_HammerBrute_Swing", new[] { "휘두름_무거움_02", "휘두름_무거움_03" }),
            ("Enemy_Chief_Swing", new[] { "휘두름_무거움_01", "휘두름_무거움_03" }),
        };

        /// <summary>같은 조각을 쓰는 적끼리도 들리는 결이 다르게: (큐, 기본 피치, 피치 지터). 단검은 높고 빠르게, 망치는 낮게.</summary>
        public static readonly (string cue, float pitch, float jitter)[] SwingPitch =
        {
            ("Enemy_Swordsman_Swing", 1.00f, 0.08f),
            ("Enemy_Assassin_Swing", 1.15f, 0.08f),
            ("Enemy_ShieldGuard_Swing", 0.95f, 0.08f),
            ("Enemy_HammerBrute_Swing", 0.90f, 0.06f),
            ("Enemy_Chief_Swing", 0.97f, 0.06f),
        };

        /// <summary>
        /// 2026-09-18 2차 수정본(AudioResource/Repaired, 기록 Docs/Audio/repair-2026-09-18/manifest.json의 second_pass) 연결.
        /// 같은 이름으로 덮어쓴 수정본(HeavyStep_01~03, TowerDestroy_02, ShieldHit)은 GUID가 그대로라 여기 없다.
        /// </summary>
        public static readonly (string cue, string[] files)[] Repair2 =
        {
            ("Enemy_Chief_Bomb_Explosion", new[] { "BombExplosion" }),
            ("Wave_Start", new[] { "WaveStart" }),
            ("Env_Lightning", new[] { "Thunder_Near", "천둥-먼 거리", "Thunder_Mid_Alternative" }),
            ("Player_Recall_Arrive", new[] { "RecallArrive" }),
            ("Player_Revive", new[] { "Revive" }),
            ("Core_Ping", new[] { "CorePing" }),
            ("Player_SandBurst", new[] { "SandBurst" }),
            ("Player_Dash", new[] { "Dash" }),
            ("Player_SandStorm_Cast", new[] { "SandStorm_Cast" }),
            ("Player_Updraft_Charge_Loop", new[] { "Charge_Loop" }),
            ("Player_Fall_Loop", new[] { "Fall_Loop" }),
            ("Env_DesertWind_Loop", new[] { "DesertWind_Loop" }),
            ("Player_LevelUp_Fanfare", new[] { "LevelUpFanfare" }),
            ("Enemy_Swordsman_Swing", new[] { "휘두름_가벼움_01", "휘두름_가벼움_02", "Swing_Light_03" }),
            ("Enemy_Assassin_Swing", new[] { "휘두름_가벼움_01", "Swing_Light_03" }),
            ("Player_Pierce_Charge_Loop", new string[0]),
        };

        /// <summary>2차 수정 일괄 적용 (배치: -executeMethod SandGuard.Audio.Editor.AudioResourceMap.ApplyRepair2).</summary>
        [MenuItem("SandGuard/Audio/Apply Repair 2026-09-18 (second pass)")]
        public static void ApplyRepair2()
        {
            ApplyTable(Repair2, "AUDIO_REPAIR2_APPLIED");
            ClearIntentionallyEmpty();
            ForceMonoFor3D();
        }

        /// <summary>3D 큐에서만 쓰는 스테레오 클립은 Force To Mono. 2D 큐(UI·음악·앰비언스)에서도 쓰는 클립은 그대로 둔다.</summary>
        [MenuItem("SandGuard/Audio/Force Mono For 3D-Only Clips")]
        public static void ForceMonoFor3D()
        {
            var spatialUse = new Dictionary<string, bool>(); // 클립 경로 → 모든 사용처가 3D인가
            foreach (var guid in AssetDatabase.FindAssets("t:SfxCue", new[] { SfxCueBuilder.CueRoot }))
            {
                var cue = AssetDatabase.LoadAssetAtPath<SfxCue>(AssetDatabase.GUIDToAssetPath(guid));
                if (cue == null || cue.clips == null) continue;
                foreach (var c in cue.clips)
                {
                    if (c == null) continue;
                    string p = AssetDatabase.GetAssetPath(c);
                    spatialUse[p] = (spatialUse.TryGetValue(p, out var prev) ? prev : true) && cue.spatial;
                }
            }
            int n = 0;
            foreach (var kv in spatialUse)
            {
                if (!kv.Value) continue;
                var imp = AssetImporter.GetAtPath(kv.Key) as AudioImporter;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(kv.Key);
                if (imp == null || clip == null || clip.channels < 2 || imp.forceToMono) continue;
                imp.forceToMono = true;
                imp.SaveAndReimport();
                n++;
            }
            Debug.Log($"AUDIO_FORCE_MONO set={n}");
        }

        [MenuItem("SandGuard/Audio/Apply Whoosh Swings 2026-09-18")]
        public static void ApplySwingOverrides()
        {
            ApplyTable(SwingOverrides, "AUDIO_SWING_OVERRIDES_APPLIED");
            foreach (var (cueName, pitch, jitter) in SwingPitch)
            {
                var cue = AssetDatabase.LoadAssetAtPath<SfxCue>(SfxCueBuilder.PathOf(cueName));
                if (cue == null) continue;
                cue.pitch = pitch; cue.pitchJitter = jitter; cue.maxVoices = Mathf.Max(cue.maxVoices, 4);
                EditorUtility.SetDirty(cue);
            }
            AssetDatabase.SaveAssets();
            AudioWiring.WireEnemyCombatVisuals(); // 적마다 휘두름 이벤트 시각을 새 최고점에 맞춰 다시 심는다
        }

        [MenuItem("SandGuard/Audio/Apply Overrides 2026-09-18 c (latest versions)")]
        public static void ApplyOverrides0918c()
        {
            SetMusicStreaming();
            ApplyTable(Overrides0918c, "AUDIO_OVERRIDES_0918C_APPLIED");
        }

        [MenuItem("SandGuard/Audio/Apply Overrides 2026-09-18 b (recall, beam, music, absorb)")]
        public static void ApplyOverrides0918b()
        {
            SetMusicStreaming();
            ApplyTable(Overrides0918b, "AUDIO_OVERRIDES_0918B_APPLIED");
        }

        /// <summary>30초 넘는 음악 파일은 Streaming으로 임포트한다 (3분짜리 곡을 통째로 메모리에 풀지 않도록). 이미 Streaming이면 그대로.</summary>
        public static void SetMusicStreaming()
        {
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Dir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null || clip.length < 30f) continue;
                var imp = AssetImporter.GetAtPath(path) as AudioImporter;
                if (imp == null) continue;
                var s = imp.defaultSampleSettings;
                if (s.loadType == AudioClipLoadType.Streaming) continue;
                s.loadType = AudioClipLoadType.Streaming;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.6f;
                imp.defaultSampleSettings = s;
                imp.SaveAndReimport();
                n++;
                Debug.Log($"[Audio] Streaming 임포트: {Path.GetFileName(path)} ({clip.length:0}s)");
            }
            Debug.Log($"AUDIO_MUSIC_STREAMING set={n}");
        }

        [MenuItem("SandGuard/Audio/Apply Overrides 2026-09-18 (replace existing clips once)")]
        public static void ApplyOverrides() => ApplyTable(Overrides, "AUDIO_OVERRIDES_APPLIED");

        static void ApplyTable((string cue, string[] files)[] table, string tag)
        {
            var files = LoadAll();
            int changed = 0, missing = 0;
            foreach (var (cueName, names) in table)
            {
                var cue = AssetDatabase.LoadAssetAtPath<SfxCue>(SfxCueBuilder.PathOf(cueName));
                if (cue == null) { Debug.LogWarning($"[Audio] 큐가 없다: {cueName}"); continue; }
                var clips = new List<AudioClip>();
                foreach (var n in names)
                {
                    if (files.TryGetValue(n, out var clip)) clips.Add(clip);
                    else { missing++; Debug.LogWarning($"[Audio] 파일이 없다: {cueName} ← {n}"); }
                }
                bool same = cue.clips != null && cue.clips.Length == clips.Count;
                if (same) for (int i = 0; i < clips.Count; i++) if (cue.clips[i] != clips[i]) { same = false; break; }
                if (same) continue;
                cue.clips = clips.ToArray();
                EditorUtility.SetDirty(cue);
                changed++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"{tag} changed={changed} missingFiles={missing}");
            Apply(); // 빈 큐 채우기 + 보고서 갱신
        }

        static Dictionary<string, AudioClip> LoadAll()
        {
            var all = new Dictionary<string, AudioClip>();
            if (!Directory.Exists(Dir)) return all;
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Dir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                all[Path.GetFileNameWithoutExtension(path)] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            return all;
        }

        /// <summary>큐 전체를 돌며 SFX_ 클립을 지우고, 빈 큐를 대응표로 채운다. 결과를 로그와 Docs/Audio/audio-resource-map.md에 남긴다.</summary>
        [MenuItem("SandGuard/Audio/Apply AudioResource Map (clear SFX_ clips)")]
        public static void Apply()
        {
            var files = LoadAll();
            var used = new HashSet<string>();
            var report = new StringBuilder();
            int cleared = 0, filled = 0;
            var empty = new List<string>();
            var missingFiles = new List<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:SfxCue", new[] { SfxCueBuilder.CueRoot }))
            {
                var cue = AssetDatabase.LoadAssetAtPath<SfxCue>(AssetDatabase.GUIDToAssetPath(guid));
                bool changed = false;
                if (cue.clips != null && cue.clips.Length > 0)
                {
                    var keep = new List<AudioClip>();
                    foreach (var c in cue.clips) if (c != null && !c.name.StartsWith("SFX_")) keep.Add(c);
                    if (keep.Count != cue.clips.Length) { cue.clips = keep.ToArray(); cleared++; changed = true; }
                }
                foreach (var (c, wrong, right) in Corrections)
                    if (cue.name == c && cue.clips != null && cue.clips.Length == 1 && cue.clips[0] != null && cue.clips[0].name == wrong && files.TryGetValue(right, out var fixedClip))
                    { cue.clips = new[] { fixedClip }; changed = true; Debug.Log($"[Audio] {c}: {wrong} → {right}"); }
                if (!cue.HasClips)
                {
                    var names = Find(cue.name);
                    if (names != null)
                    {
                        var clips = new List<AudioClip>();
                        foreach (var n in names)
                        {
                            if (files.TryGetValue(n, out var clip)) { clips.Add(clip); used.Add(n); }
                            else missingFiles.Add($"{cue.name} ← {n}");
                        }
                        if (clips.Count > 0) { cue.clips = clips.ToArray(); filled++; changed = true; }
                    }
                }
                if (!cue.HasClips) empty.Add(cue.name);
                else foreach (var c in cue.clips) if (c != null) used.Add(c.name); // 담당자가 직접 넣은 것도 '사용 중'
                if (changed) EditorUtility.SetDirty(cue);
            }
            AssetDatabase.SaveAssets();

            var unused = new List<string>();
            foreach (var f in files.Keys) if (!used.Contains(f)) unused.Add(f);
            empty.Sort(); unused.Sort();

            report.AppendLine("# AudioResource 대응 결과");
            report.AppendLine();
            report.AppendLine($"생성: {System.DateTime.Now:yyyy-MM-dd HH:mm}. 표: `Assets/8.Audio/Editor/AudioResourceMap.cs`.");
            report.AppendLine();
            report.AppendLine("## 대응된 큐");
            report.AppendLine();
            foreach (var (cue, names) in Map) report.AppendLine($"- `{cue}` ← {string.Join(", ", names)}");
            report.AppendLine();
            report.AppendLine($"## 클립이 없는 큐 ({empty.Count}개) — 담당자가 채울 것");
            report.AppendLine();
            foreach (var e in empty) report.AppendLine($"- `{e}`" + (SfxCatalog.Find(e) != null ? $": {SfxCatalog.Find(e).Note}" : ""));
            report.AppendLine();
            report.AppendLine($"## AudioResource에 있지만 쓰지 않은 파일 ({unused.Count}개)");
            report.AppendLine();
            foreach (var u in unused) report.AppendLine($"- {u}");
            if (missingFiles.Count > 0)
            {
                report.AppendLine(); report.AppendLine("## 표에는 있는데 파일이 없는 항목"); report.AppendLine();
                foreach (var m in missingFiles) report.AppendLine($"- {m}");
            }
            Directory.CreateDirectory("Docs/Audio");
            File.WriteAllText("Docs/Audio/audio-resource-map.md", report.ToString(), new UTF8Encoding(false));
            Debug.Log($"AUDIO_RESOURCE_MAPPED cleared={cleared} filled={filled} empty={empty.Count} unusedFiles={unused.Count} missingFiles={missingFiles.Count}");
        }

        /// <summary>
        /// 자동 채우기 대상. 교체 표에서 일부러 비운 큐(빈 배열)는 대응표에 있어도 다시 채우지 않는다.
        /// (2026-09-18 관통탄 충전 루프를 비운 직후 자동 채우기가 '기 모으기'를 되돌려 넣은 문제)
        /// </summary>
        static string[] Find(string cue)
        {
            if (IntentionallyEmpty(cue)) return null;
            foreach (var (c, files) in Map) if (c == cue) return files;
            return null;
        }

        static (string cue, string[] files)[][] AllTables() => new[] { Overrides, Overrides0918b, Overrides0918c, ExplosionOverrides, SwingOverrides, Repair2 };

        /// <summary>
        /// 여러 교체 표가 같은 큐를 다르게 정했을 때는 나중 표가 이긴다. 그래서 "비운다"는 마지막 표가 비울 때만 참이다.
        /// </summary>
        static bool IntentionallyEmpty(string cue)
        {
            (string cue, string[] files)? last = null;
            foreach (var table in AllTables()) foreach (var row in table) if (row.cue == cue) last = row;
            return last.HasValue && last.Value.files.Length == 0;
        }

        [MenuItem("SandGuard/Audio/Clear Intentionally Empty Cues")]
        public static void ClearIntentionallyEmpty()
        {
            int n = 0;
            foreach (var table in AllTables())
                foreach (var (c, files) in table)
                {
                    if (files.Length != 0 || !IntentionallyEmpty(c)) continue;
                    var cue = AssetDatabase.LoadAssetAtPath<SfxCue>(SfxCueBuilder.PathOf(c));
                    if (cue == null || !cue.HasClips) continue;
                    cue.clips = new AudioClip[0]; EditorUtility.SetDirty(cue); n++;
                    Debug.Log($"[Audio] 비움: {c}");
                }
            AssetDatabase.SaveAssets();
            Debug.Log($"AUDIO_CLEARED_EMPTY count={n}");
        }
    }
}

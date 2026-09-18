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
            ("UI_Click", new[] { "UI버튼 클릭음_auda" }),
            ("Player_HardLand", new[] { "강한착지음_auda" }),
            ("Env_DesertWind_Loop", new[] { "기본 사막환경 배경음_auda" }),
            ("Player_ManaBolt_Fire", new[] { "기본공격_auda" }),
            ("Player_Dash", new[] { "대쉬 효과음_auda" }),
            ("Player_AirJump", new[] { "더블점프_auda" }),
            ("Player_LevelUp", new[] { "레벨업_auda" }),
            ("UI_Fail", new[] { "마나부족스킬잠금건설불가_auda" }),
            ("Music_Menu_Loop", new[] { "메인화면 배경음" }),
            ("Player_Footstep_Sand", new[] { "발걸음2_auda", "발자국 사운드", "플레이어 발걸음 _auda" }),
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
            ("Player_Pierce_Charge_Loop", new[] { "기 모으기" }),
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
            ("Player_Footstep_Sand", new[] { "플레이어 발걸음 _auda", "발걸음2_auda", "발자국 사운드" }),
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

        static string[] Find(string cue)
        {
            foreach (var (c, files) in Map) if (c == cue) return files;
            return null;
        }
    }
}

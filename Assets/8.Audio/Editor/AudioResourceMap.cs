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
        };

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

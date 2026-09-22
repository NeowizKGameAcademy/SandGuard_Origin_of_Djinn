using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace SandGuard.Audio.Editor
{
    /// <summary>
    /// 큐 에셋을 만든다. 담당자의 인스펙터 편집을 지키는 규칙:
    ///   - SfxCatalog의 모든 항목에 큐가 있게 한다 (없으면 빈 큐로 생성). 이미 있는 큐의 필드는 절대 덮어쓰지 않는다.
    ///   - 클립은 두 곳에서만 채운다: `Assets/8.Audio/Generated`의 이름 규칙 파일(SFX_&lt;분류&gt;_&lt;이름&gt;_NN)과
    ///     `AudioResourceMap`의 대응표. 둘 다 **클립 목록이 비어 있을 때만** 채운다.
    ///   - 코드 합성(Synth)·자리표시(Placeholder) 폴더는 더 이상 큐에 넣지 않는다.
    /// </summary>
    public static class SfxCueBuilder
    {
        public const string CueRoot = "Assets/8.Audio/Cues";
        public const string GeneratedDir = "Assets/8.Audio/Generated";
        public const string MixerPath = "Assets/8.Audio/SandGuard.mixer";
        static readonly Regex NamePattern = new Regex(@"^SFX_([A-Za-z]+)_(.+?)(?:_(\d{2,3}))?$");

        static readonly (string prefix, System.Action<SfxCue> apply)[] PrefixDefaults =
        {
            ("UI_", c => { c.spatial = false; c.volume = 0.6f; c.maxVoices = 2; c.pitchJitter = 0.02f; c.volumeJitterDb = 0.5f; }),
            ("Env_", c => { c.volume = 0.6f; c.minDistance = 6f; c.maxDistance = 80f; c.maxVoices = 2; c.loopFade = 1.5f; }),
            ("Core_", c => { c.volume = 0.7f; c.minDistance = 6f; c.maxDistance = 60f; c.maxVoices = 1; c.loopFade = 1f; }),
            ("Music_", c => { c.spatial = false; c.volume = 0.5f; c.maxVoices = 1; c.pitchJitter = 0f; c.volumeJitterDb = 0f; c.loopFade = 2f; }),
        };

        /// <summary>파일 이름 → 큐 이름. 규칙에 안 맞으면 null.</summary>
        public static string CueNameOf(string file)
        {
            var m = NamePattern.Match(file);
            return m.Success ? $"{m.Groups[1].Value}_{m.Groups[2].Value}" : null;
        }

        [MenuItem("SandGuard/Audio/Build Cues (Catalog + Generated + AudioResource)")]
        public static void Build()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            int created = 0, filled = 0;

            // 1) 카탈로그의 모든 큐가 존재하게 (없는 것만 빈 큐로 생성)
            foreach (var entry in SfxCatalog.Entries)
            {
                if (!File.Exists(PathOf(entry.Name))) { Ensure(entry.Name, mixer); created++; continue; }
                // 재생 확률은 새로 생긴 필드라 기존 큐는 전부 1이다. 아직 손대지 않은(=1) 큐에만 카탈로그 값을 한 번 심는다
                if (entry.Chance < 1f)
                {
                    var existing = AssetDatabase.LoadAssetAtPath<SfxCue>(PathOf(entry.Name));
                    if (existing != null && existing.chance >= 1f) { existing.chance = entry.Chance; EditorUtility.SetDirty(existing); }
                }
            }

            // 2) Generated 폴더의 이름 규칙 파일
            var groups = new SortedDictionary<string, List<AudioClip>>();
            if (Directory.Exists(GeneratedDir))
                foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { GeneratedDir }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string cueName = CueNameOf(Path.GetFileNameWithoutExtension(path));
                    if (cueName == null) { Debug.LogWarning($"[Audio] 이름 규칙에 맞지 않아 건너뜀: {path}"); continue; }
                    if (!groups.TryGetValue(cueName, out var list)) groups[cueName] = list = new List<AudioClip>();
                    list.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
                }
            foreach (var pair in groups)
            {
                bool existed = File.Exists(PathOf(pair.Key));
                var cue = Ensure(pair.Key, mixer);
                if (!existed) created++;
                if (!cue.HasClips)
                {
                    pair.Value.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                    cue.clips = pair.Value.ToArray();
                    EditorUtility.SetDirty(cue);
                    filled++;
                }
            }
            AssetDatabase.SaveAssets();

            // 3) 담당자 파일 대응표 (SFX_ 클립 제거 포함)
            AudioResourceMap.Apply();
            Debug.Log($"SFX_CUES_BUILT catalog={SfxCatalog.Entries.Length} created={created} filledFromGenerated={filled}");
        }

        public static string PathOf(string cueName)
        {
            string category = cueName.Substring(0, cueName.IndexOf('_'));
            return $"{CueRoot}/{category}/{cueName}.asset";
        }

        /// <summary>큐가 없으면 기본값으로 만든다. 있으면 그대로 돌려준다.</summary>
        static SfxCue Ensure(string name, AudioMixer mixer)
        {
            string path = PathOf(name);
            var cue = AssetDatabase.LoadAssetAtPath<SfxCue>(path);
            if (cue != null) return cue;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            cue = ScriptableObject.CreateInstance<SfxCue>();
            ApplyDefaults(cue, name);
            string category = name.Substring(0, name.IndexOf('_'));
            cue.mixerGroup = FindGroup(mixer, name.Contains("_Voice") ? "Voice" : category);
            AssetDatabase.CreateAsset(cue, path);
            return cue;
        }

        static void ApplyDefaults(SfxCue cue, string name)
        {
            cue.loop = name.EndsWith("_Loop");
            foreach (var d in PrefixDefaults) if (name.StartsWith(d.prefix)) d.apply(cue);
            var entry = SfxCatalog.Find(name);
            if (entry == null) return;
            cue.spatial = entry.Spatial; cue.minDistance = entry.Min; cue.maxDistance = entry.Max;
            cue.volume = entry.Volume; cue.maxVoices = entry.MaxVoices; cue.chance = entry.Chance;
            if (entry.Kind == PlaceholderKind.Drone || entry.Kind == PlaceholderKind.Sting) { cue.pitchJitter = 0f; cue.volumeJitterDb = 0f; }
            if (entry.Kind == PlaceholderKind.Voice) cue.pitchJitter = 0.06f;
            if (name == "Enemy_Voice_Taunt")
            {
                cue.minInterval = 0.35f; cue.loopFade = 0.1f;
                cue.pitchJitter = 0.04f; cue.rolloff = AudioRolloffMode.Linear;
            }
        }

        /// <summary>분류 이름과 같은 믹서 그룹, 없으면 SFX, 그것도 없으면 null.</summary>
        public static AudioMixerGroup FindGroup(AudioMixer mixer, string category)
        {
            if (mixer == null) return null;
            string want = category switch { "UI" => "UI", "Env" => "Environment", "Music" => "Music", "Voice" => "Voice", _ => "SFX" };
            var found = mixer.FindMatchingGroups(want);
            foreach (var g in found) if (g.name == want) return g;
            return found.Length > 0 ? found[0] : null;
        }

        public static SfxCue Load(string cueName) => AssetDatabase.LoadAssetAtPath<SfxCue>(PathOf(cueName));
    }
}

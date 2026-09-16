using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace SandGuard.Audio.Editor
{
    /// <summary>
    /// WAV 파일 이름으로 큐 에셋을 만든다. 담당자의 인스펙터 편집을 지키는 규칙:
    ///   - 없는 큐만 새로 만들고, 있는 큐는 **클립 목록이 비어 있을 때만** 채운다. 다른 필드는 절대 덮어쓰지 않는다.
    ///   - 새 큐의 기본값은 SfxCatalog(없으면 분류 접두사 표)에서 가져온다. 이미 만들어진 큐에는 적용하지 않는다.
    /// 이름 규칙: SFX_&lt;분류&gt;_&lt;이름&gt;[_NN].wav → 큐 Assets/8.Audio/Cues/&lt;분류&gt;/&lt;분류&gt;_&lt;이름&gt;.asset
    /// 소스 폴더 우선순위: Generated(담당자·외부 생성) > Synth(코드 합성) > Placeholder(자리표시).
    /// 같은 큐에 여러 폴더의 파일이 있으면 가장 앞선 폴더의 파일만 넣는다. 그래서 담당자가 Generated에 파일을 넣고
    /// 큐의 클립 목록을 비우면 다음 빌드에서 자리표시 대신 그 파일이 들어간다.
    /// </summary>
    public static class SfxCueBuilder
    {
        public const string CueRoot = "Assets/8.Audio/Cues";
        public static readonly string[] SourceDirs = { "Assets/8.Audio/Generated", "Assets/8.Audio/Synth", "Assets/8.Audio/Placeholder" };
        public const string MixerPath = "Assets/8.Audio/SandGuard.mixer";
        static readonly Regex NamePattern = new Regex(@"^SFX_([A-Za-z]+)_(.+?)(?:_(\d{2,3}))?$");

        static readonly (string prefix, System.Action<SfxCue> apply)[] PrefixDefaults =
        {
            ("UI_", c => { c.spatial = false; c.volume = 0.6f; c.maxVoices = 2; c.pitchJitter = 0.02f; c.volumeJitterDb = 0.5f; }),
            ("Env_", c => { c.volume = 0.6f; c.minDistance = 6f; c.maxDistance = 80f; c.maxVoices = 2; c.loopFade = 1.5f; }),
            ("Core_", c => { c.volume = 0.7f; c.minDistance = 6f; c.maxDistance = 60f; c.maxVoices = 1; c.loopFade = 1f; }),
            ("Music_", c => { c.spatial = false; c.volume = 0.5f; c.maxVoices = 1; c.pitchJitter = 0f; c.volumeJitterDb = 0f; c.loopFade = 2f; }),
        };

        /// <summary>파일 이름 → (큐 이름, 폴더 순위). 규칙에 안 맞으면 null.</summary>
        public static string CueNameOf(string file)
        {
            var m = NamePattern.Match(file);
            return m.Success ? $"{m.Groups[1].Value}_{m.Groups[2].Value}" : null;
        }

        [MenuItem("SandGuard/Audio/Build Cues From WAVs")]
        public static void Build()
        {
            var groups = new SortedDictionary<string, (int rank, List<AudioClip> clips)>();
            for (int rank = 0; rank < SourceDirs.Length; rank++)
            {
                string dir = SourceDirs[rank];
                if (!Directory.Exists(dir)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { dir }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string cueName = CueNameOf(Path.GetFileNameWithoutExtension(path));
                    if (cueName == null) { Debug.LogWarning($"[Audio] 이름 규칙에 맞지 않아 건너뜀: {path}"); continue; }
                    if (!groups.TryGetValue(cueName, out var g)) groups[cueName] = g = (rank, new List<AudioClip>());
                    if (g.rank != rank) continue; // 더 앞선 폴더가 이미 차지했다
                    g.clips.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
                }
            }

            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            int created = 0, filled = 0;
            foreach (var pair in groups)
            {
                string name = pair.Key;
                string category = name.Substring(0, name.IndexOf('_'));
                string dir = $"{CueRoot}/{category}";
                string path = $"{dir}/{name}.asset";
                var cue = AssetDatabase.LoadAssetAtPath<SfxCue>(path);
                if (cue == null)
                {
                    Directory.CreateDirectory(dir);
                    cue = ScriptableObject.CreateInstance<SfxCue>();
                    ApplyDefaults(cue, name);
                    cue.mixerGroup = FindGroup(mixer, name.Contains("_Voice") ? "Voice" : category);
                    AssetDatabase.CreateAsset(cue, path);
                    created++;
                }
                if (!cue.HasClips)
                {
                    pair.Value.clips.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                    cue.clips = pair.Value.clips.ToArray();
                    EditorUtility.SetDirty(cue);
                    filled++;
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"SFX_CUES_BUILT total={groups.Count} created={created} filled={filled}");
        }

        static void ApplyDefaults(SfxCue cue, string name)
        {
            cue.loop = name.EndsWith("_Loop");
            foreach (var d in PrefixDefaults) if (name.StartsWith(d.prefix)) d.apply(cue);
            var entry = SfxCatalog.Find(name);
            if (entry == null) return;
            cue.spatial = entry.Spatial; cue.minDistance = entry.Min; cue.maxDistance = entry.Max;
            cue.volume = entry.Volume; cue.maxVoices = entry.MaxVoices;
            if (entry.Kind == PlaceholderKind.Drone || entry.Kind == PlaceholderKind.Sting) { cue.pitchJitter = 0f; cue.volumeJitterDb = 0f; }
            if (entry.Kind == PlaceholderKind.Voice) cue.pitchJitter = 0.06f;
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

        public static SfxCue Load(string cueName)
        {
            string category = cueName.Substring(0, cueName.IndexOf('_'));
            return AssetDatabase.LoadAssetAtPath<SfxCue>($"{CueRoot}/{category}/{cueName}.asset");
        }
    }
}

using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SandGuard.Audio.Editor.Synth
{
    /// <summary>
    /// 합성 효과음을 WAV로 굽는다. VFX 빌더와 같은 규칙: 파일을 손으로 고치지 말고 레시피를 고쳐 다시 굽는다.
    ///
    ///   메뉴  SandGuard > Audio > Bake Synth SFX
    ///   배치  Unity.exe -batchmode -nographics -projectPath . -executeMethod SandGuard.Audio.Editor.Synth.SynthSfxBaker.BakeAll -quit
    ///
    /// 출력
    ///   Assets/8.Audio/Synth/&lt;Name&gt;.wav          — 16비트 48kHz. 루프는 Vorbis 압축 메모리, 원샷은 PCM 디컴프레스
    ///   Docs/audio-preview/synth-v1/&lt;Name&gt;.png    — 파형 + 스펙트로그램
    ///   Docs/audio-preview/synth-v1/report.md      — 지표 표
    /// </summary>
    public static class SynthSfxBaker
    {
        public const string WavDir = "Assets/8.Audio/Synth";
        public const string PreviewDir = "Docs/audio-preview/synth-v1";

        [MenuItem("SandGuard/Audio/Bake Synth SFX")]
        public static void BakeAll()
        {
            var recipes = SynthRecipes.All();
            var report = new StringBuilder();
            report.AppendLine("# 합성 SFX 베이크 보고");
            report.AppendLine();
            report.AppendLine($"생성: {System.DateTime.Now:yyyy-MM-dd HH:mm}. 레시피 `Assets/8.Audio/Editor/Synth/SynthRecipes.cs`.");
            report.AppendLine();
            report.AppendLine("| 파일 | 길이 | ch | 피크 dBFS | RMS dBFS | DC | NaN | 중심 Hz | 이음매 비율 | 머리/꼬리 dB | 판정 |");
            report.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|");

            int ok = 0;
            foreach (var r in recipes)
            {
                var clip = r.Render();
                Dsp.Normalize(clip, r.PeakDb);
                Dsp.SoftClip(clip.L); if (clip.Stereo) Dsp.SoftClip(clip.R);

                string wav = $"{WavDir}/{r.Name}.wav";
                WavWriter.Write(wav, clip);
                AudioAnalysis.WriteSpectrogramPng($"{PreviewDir}/{r.Name}.png", clip, r.Loop);

                var m = AudioAnalysis.Measure(clip, r.Loop);
                bool pass = m.Nan == 0 && m.PeakDb <= 0f && m.RmsDb > -60f && Mathf.Abs(m.Dc) < 0.02f
                            && (!r.Loop || (m.SeamRatio < 3f && Mathf.Abs(m.HeadTailDb) < 6f));
                if (pass) ok++;
                report.AppendLine($"| `{r.Name}` | {m.Seconds:0.00}s | {m.Channels} | {m.PeakDb:0.0} | {m.RmsDb:0.0} | {m.Dc:0.0000} | {m.Nan} | {m.CentroidHz:0} | "
                                  + (r.Loop ? $"{m.SeamRatio:0.00} | {m.HeadTailDb:0.0}" : "- | -") + $" | {(pass ? "OK" : "확인")} |");
                Debug.Log($"[SynthSfx] {r.Name}: {m.Seconds:0.00}s peak {m.PeakDb:0.0} rms {m.RmsDb:0.0} centroid {m.CentroidHz:0}Hz" + (r.Loop ? $" seam {m.SeamRatio:0.00}" : ""));
            }

            report.AppendLine();
            report.AppendLine("판정 기준: NaN 0, 피크 ≤ 0 dBFS, RMS > -60, |DC| < 0.02, 루프는 이음매 비율 < 3 및 머리/꼬리 RMS 차 < 6 dB.");
            report.AppendLine();
            report.AppendLine("## 레시피 메모");
            report.AppendLine();
            foreach (var r in recipes) report.AppendLine($"- `{r.Name}`: {r.Notes}");
            Directory.CreateDirectory(PreviewDir);
            File.WriteAllText($"{PreviewDir}/report.md", report.ToString(), new UTF8Encoding(false));

            AssetDatabase.Refresh();
            foreach (var r in recipes) ApplyImportSettings($"{WavDir}/{r.Name}.wav", r.Loop);
            AssetDatabase.SaveAssets();
            Debug.Log($"SYNTH_SFX_BAKED count={recipes.Count} ok={ok} wav={WavDir} preview={PreviewDir}");
        }

        public const string PlaceholderDir = "Assets/8.Audio/Placeholder";

        /// <summary>
        /// SfxCatalog의 모든 큐 중 클립 파일이 하나도 없는 것에 자리표시 클립을 굽는다.
        /// 이미 Synth/Generated/Placeholder에 파일이 있는 큐는 건너뛴다. 담당자가 Generated에 파일을 넣어도 자리표시는 남지만
        /// 큐 빌더가 Generated를 우선하므로 큐의 클립 목록을 비우면 다음 빌드에서 교체된다.
        /// </summary>
        [MenuItem("SandGuard/Audio/Bake Placeholder Clips")]
        public static void BakePlaceholders()
        {
            var have = new System.Collections.Generic.HashSet<string>();
            foreach (var dir in new[] { SfxCueBuilder.GeneratedDir, WavDir, PlaceholderDir })
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { dir }))
                {
                    var name = SfxCueBuilder.CueNameOf(Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)));
                    if (name != null) have.Add(name);
                }
            }
            int made = 0;
            var loops = new System.Collections.Generic.List<string>();
            foreach (var entry in SfxCatalog.Entries)
            {
                if (have.Contains(entry.Name)) continue;
                var clip = PlaceholderRecipes.Render(entry.Kind, entry.Name);
                bool longform = entry.Loop || entry.Kind == PlaceholderKind.Drone;
                Dsp.Normalize(clip, longform ? -12 : -6);
                Dsp.SoftClip(clip.L); if (clip.Stereo) Dsp.SoftClip(clip.R);
                string wav = $"{PlaceholderDir}/SFX_{entry.Name}_01.wav";
                WavWriter.Write(wav, clip);
                if (longform) loops.Add(wav);
                made++;
            }
            AssetDatabase.Refresh();
            foreach (var entry in SfxCatalog.Entries)
            {
                string wav = $"{PlaceholderDir}/SFX_{entry.Name}_01.wav";
                if (File.Exists(wav)) ApplyImportSettings(wav, entry.Loop || entry.Kind == PlaceholderKind.Drone);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"SFX_PLACEHOLDERS_BAKED made={made} skipped={SfxCatalog.Entries.Length - made} dir={PlaceholderDir}");
        }

        /// <summary>원샷: PCM + DecompressOnLoad(지연 없음). 루프: Vorbis 0.7 + CompressedInMemory(메모리 절약).</summary>
        static void ApplyImportSettings(string assetPath, bool loop)
        {
            var imp = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            if (imp == null) { Debug.LogWarning($"[SynthSfx] 임포터를 찾지 못했다: {assetPath}"); return; }
            var s = imp.defaultSampleSettings;
            s.loadType = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = loop ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            s.quality = 0.7f;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            imp.defaultSampleSettings = s;
            imp.forceToMono = false;
            imp.loadInBackground = false;
            imp.SaveAndReimport();
        }
    }
}

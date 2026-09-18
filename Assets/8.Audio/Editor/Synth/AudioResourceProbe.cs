using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SandGuard.Audio.Editor.Synth
{
    /// <summary>
    /// AudioResource 파일을 들을 수 없는 쪽(에이전트)이 자리를 고르기 위한 측정. 이름에 필터 문자열이 들어간 클립만.
    /// 길이, 피크, RMS, 스펙트럼 중심, 피크까지 걸린 시간(어택), 저역(150Hz 이하) 에너지 비율, 꼬리(피크 -30dB까지) 길이 + 스펙트로그램 PNG.
    /// 배치: -executeMethod SandGuard.Audio.Editor.Synth.AudioResourceProbe.Run  (환경 변수 PROBE_FILTER, 기본 "폭발")
    /// </summary>
    public static class AudioResourceProbe
    {
        public static void Run()
        {
            string filter = System.Environment.GetEnvironmentVariable("PROBE_FILTER");
            if (string.IsNullOrEmpty(filter)) filter = "폭발";
            string outDir = "Docs/audio-preview/resource-probe";
            Directory.CreateDirectory(outDir);
            var sb = new StringBuilder("| 파일 | 길이 | ch | 피크 dB | RMS dB | 중심 Hz | 어택 ms | 저역 비율 | 꼬리 s |\n|---|---:|---:|---:|---:|---:|---:|---:|---:|\n");
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioResourceMap.Dir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                if (!name.Contains(filter)) continue;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;
                if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
                var raw = new float[clip.samples * clip.channels];
                if (!clip.GetData(raw, 0)) { sb.AppendLine($"| {name} | 읽기 실패 | | | | | | | |"); continue; }

                // 모노로 합치고 48kHz로 단순 리샘플(분석용)
                int n = clip.samples, ch = clip.channels;
                var mono = new float[n];
                for (int i = 0; i < n; i++) { float s = 0; for (int c = 0; c < ch; c++) s += raw[i * ch + c]; mono[i] = s / ch; }
                float ratio = (float)clip.frequency / Dsp.Sr;
                int m = Mathf.Max(1, (int)(n / ratio));
                var x = new float[m];
                for (int i = 0; i < m; i++) x[i] = mono[Mathf.Min(n - 1, (int)(i * ratio))];

                var c1 = new Clip(x);
                var metrics = AudioAnalysis.Measure(c1, false);
                int peakAt = 0; float peak = 0;
                for (int i = 0; i < m; i++) if (Mathf.Abs(x[i]) > peak) { peak = Mathf.Abs(x[i]); peakAt = i; }
                // 꼬리: 피크 이후 50ms RMS가 피크의 -30dB 아래로 처음 내려가는 시점
                int w = Dsp.Samples(0.05); float tailEnd = (float)m / Dsp.Sr;
                for (int i = peakAt; i + w < m; i += w)
                {
                    double s = 0; for (int k = 0; k < w; k++) s += x[i + k] * x[i + k];
                    if (System.Math.Sqrt(s / w) < peak * 0.0316) { tailEnd = (float)(i - peakAt) / Dsp.Sr; break; }
                }
                var lo = (float[])x.Clone(); Dsp.Filter(lo, Dsp.FilterKind.Lowpass, 150, 0.707);
                double eLo = 0, eAll = 0; for (int i = 0; i < m; i++) { eLo += lo[i] * lo[i]; eAll += x[i] * x[i]; }

                sb.AppendLine($"| {name} | {(float)m / Dsp.Sr:0.00}s | {ch} | {metrics.PeakDb:0.0} | {metrics.RmsDb:0.0} | {metrics.CentroidHz:0} | {peakAt * 1000f / Dsp.Sr:0} | {(eAll > 0 ? eLo / eAll : 0):0.00} | {tailEnd:0.00} |");
                AudioAnalysis.WriteSpectrogramPng($"{outDir}/{name}.png", c1, false);
            }
            File.WriteAllText($"{outDir}/report.md", sb.ToString(), new UTF8Encoding(false));
            Debug.Log("AUDIO_PROBE\n" + sb);
        }
    }
}

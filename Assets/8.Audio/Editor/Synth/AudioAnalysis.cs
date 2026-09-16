using System;
using System.IO;
using UnityEngine;

namespace SandGuard.Audio.Editor.Synth
{
    /// <summary>
    /// 소리는 들을 수 없으니 숫자와 그림으로 검증한다.
    /// 지표: 피크·RMS(dBFS), DC 오프셋, NaN 수, 스펙트럼 중심(Hz), 루프 이음매 불연속.
    /// 그림: 파형 개요 + 로그 주파수 스펙트로그램 PNG.
    /// </summary>
    public static class AudioAnalysis
    {
        public struct Metrics
        {
            public float PeakDb, RmsDb, Dc, CentroidHz, SeamRatio, HeadTailDb;
            public int Nan;
            public double Seconds;
            public int Channels;
        }

        public static Metrics Measure(Clip c, bool loop)
        {
            var m = new Metrics { Seconds = (double)c.Length / Dsp.Sr, Channels = c.Channels };
            double peak = 0, sq = 0, sum = 0; int nan = 0; long cnt = 0;
            for (int ch = 0; ch < c.Channels; ch++)
                foreach (var v in c[ch])
                {
                    if (float.IsNaN(v) || float.IsInfinity(v)) { nan++; continue; }
                    peak = Math.Max(peak, Math.Abs(v)); sq += v * v; sum += v; cnt++;
                }
            m.PeakDb = (float)(20 * Math.Log10(Math.Max(peak, 1e-9)));
            m.RmsDb = (float)(20 * Math.Log10(Math.Max(Math.Sqrt(sq / Math.Max(1, cnt)), 1e-9)));
            m.Dc = (float)(sum / Math.Max(1, cnt));
            m.Nan = nan;
            m.CentroidHz = Centroid(c.L);
            if (loop)
            {
                // 이음매: 마지막→첫 샘플의 점프를 평균 샘플 간 변화량으로 나눈다. 1 근처면 이음매가 안 들린다
                double dsum = 0; var x = c.L;
                for (int i = 1; i < x.Length; i++) dsum += Math.Abs(x[i] - x[i - 1]);
                double meanDelta = dsum / (x.Length - 1);
                m.SeamRatio = (float)(Math.Abs(x[0] - x[x.Length - 1]) / Math.Max(meanDelta, 1e-9));
                int w = Dsp.Samples(0.05);
                m.HeadTailDb = (float)(20 * Math.Log10(Rms(x, 0, w) / Math.Max(Rms(x, x.Length - w, w), 1e-9)));
            }
            return m;
        }

        static double Rms(float[] x, int start, int len)
        {
            double s = 0; for (int i = start; i < start + len && i < x.Length; i++) s += x[i] * x[i];
            return Math.Sqrt(s / len);
        }

        /// <summary>FFT 크기 가중 평균 주파수. 근거리 발사가 원거리보다 밝은지 같은 비교에 쓴다.</summary>
        public static float Centroid(float[] x)
        {
            const int N = 4096; int hop = Math.Max(N, x.Length / 64);
            double num = 0, den = 0;
            var re = new double[N]; var im = new double[N];
            for (int s = 0; s + N <= x.Length; s += hop)
            {
                for (int i = 0; i < N; i++) { re[i] = x[s + i] * Hann(i, N); im[i] = 0; }
                Fft(re, im);
                for (int k = 1; k < N / 2; k++)
                {
                    double mag = Math.Sqrt(re[k] * re[k] + im[k] * im[k]);
                    num += mag * k * Dsp.Sr / N; den += mag;
                }
            }
            return den > 0 ? (float)(num / den) : 0;
        }

        static double Hann(int i, int n) => 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (n - 1));

        /// <summary>제자리 radix-2 FFT.</summary>
        public static void Fft(double[] re, double[] im)
        {
            int n = re.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j) { (re[i], re[j]) = (re[j], re[i]); (im[i], im[j]) = (im[j], im[i]); }
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = -2 * Math.PI / len, wr = Math.Cos(ang), wi = Math.Sin(ang);
                for (int i = 0; i < n; i += len)
                {
                    double cr = 1, ci = 0;
                    for (int j = 0; j < len / 2; j++)
                    {
                        int a = i + j, b = i + j + len / 2;
                        double tr = re[b] * cr - im[b] * ci, ti = re[b] * ci + im[b] * cr;
                        re[b] = re[a] - tr; im[b] = im[a] - ti; re[a] += tr; im[a] += ti;
                        double ncr = cr * wr - ci * wi; ci = cr * wi + ci * wr; cr = ncr;
                    }
                }
            }
        }

        /// <summary>
        /// 위: 파형 개요(열마다 min/max). 아래: 로그 주파수(40Hz~20kHz) 스펙트로그램, -90~0dB.
        /// 루프면 오른쪽 끝에 처음 0.5초를 이어 붙여 이음매를 눈으로 볼 수 있게 한다.
        /// </summary>
        public static void WriteSpectrogramPng(string path, Clip c, bool loop)
        {
            var x = c.L;
            if (loop)
            {
                int extra = Dsp.Samples(0.5);
                var ext = new float[x.Length + extra];
                Array.Copy(x, ext, x.Length); Array.Copy(x, 0, ext, x.Length, extra);
                x = ext;
            }
            const int N = 2048, waveH = 80, specH = 320, gap = 6;
            int width = Math.Min(1200, Math.Max(200, x.Length / 256));
            int hop = Math.Max(1, (x.Length - N) / width);
            var tex = new Texture2D(width, waveH + gap + specH, TextureFormat.RGB24, false);
            var px = new Color32[tex.width * tex.height];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(12, 12, 16, 255);

            // 파형
            int per = Math.Max(1, x.Length / width);
            for (int col = 0; col < width; col++)
            {
                float lo = 1, hi = -1;
                for (int i = col * per; i < (col + 1) * per && i < x.Length; i++) { lo = Math.Min(lo, x[i]); hi = Math.Max(hi, x[i]); }
                int y0 = (int)((lo * 0.5f + 0.5f) * (waveH - 1)), y1 = (int)((hi * 0.5f + 0.5f) * (waveH - 1));
                for (int y = Math.Max(0, y0); y <= Math.Min(waveH - 1, y1); y++) px[(specH + gap + y) * width + col] = new Color32(120, 220, 210, 255);
            }
            // 이음매 표시선
            if (loop)
            {
                int seamCol = (int)((double)c.Length / x.Length * width);
                for (int y = 0; y < tex.height; y++) if (seamCol < width) px[y * width + seamCol] = new Color32(255, 80, 80, 255);
            }

            // 스펙트로그램
            var re = new double[N]; var im = new double[N];
            double fMin = 40, fMax = 20000;
            for (int col = 0; col < width; col++)
            {
                int s = Math.Min(col * hop, Math.Max(0, x.Length - N));
                for (int i = 0; i < N; i++) { re[i] = (s + i < x.Length ? x[s + i] : 0) * Hann(i, N); im[i] = 0; }
                Fft(re, im);
                for (int y = 0; y < specH; y++)
                {
                    double f = fMin * Math.Pow(fMax / fMin, (double)y / (specH - 1));
                    double kf = f * N / Dsp.Sr; int k0 = (int)kf, k1 = Math.Min(N / 2 - 1, k0 + 1);
                    double m0 = Mag(re, im, k0), m1 = Mag(re, im, k1);
                    double mag = m0 + (m1 - m0) * (kf - k0);
                    double db = 20 * Math.Log10(Math.Max(mag / (N / 4.0), 1e-9));
                    double v = Math.Max(0, Math.Min(1, (db + 90) / 90));
                    px[y * width + col] = Heat(v);
                }
            }
            tex.SetPixels32(px); tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        static double Mag(double[] re, double[] im, int k) => Math.Sqrt(re[k] * re[k] + im[k] * im[k]);

        /// <summary>검정 → 보라 → 주황 → 흰색.</summary>
        static Color32 Heat(double v)
        {
            double r, g, b;
            if (v < 0.33) { double t = v / 0.33; r = 0.35 * t; g = 0; b = 0.6 * t; }
            else if (v < 0.66) { double t = (v - 0.33) / 0.33; r = 0.35 + 0.65 * t; g = 0.45 * t; b = 0.6 * (1 - t); }
            else { double t = (v - 0.66) / 0.34; r = 1; g = 0.45 + 0.55 * t; b = t; }
            return new Color32((byte)(r * 255), (byte)(g * 255), (byte)(b * 255), 255);
        }
    }
}

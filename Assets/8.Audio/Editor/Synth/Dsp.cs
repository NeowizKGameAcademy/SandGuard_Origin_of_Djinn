using System;

namespace SandGuard.Audio.Editor.Synth
{
    /// <summary>재현 가능한 난수. 같은 시드면 같은 파형이 나온다 (재베이크 시 diff 최소화).</summary>
    public sealed class Rng
    {
        uint s;
        public Rng(int seed) { s = (uint)seed * 2654435761u + 0x9E3779B9u; if (s == 0) s = 1; }
        public uint NextUInt() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return s; }
        /// <summary>[0,1)</summary>
        public double Next() => NextUInt() / 4294967296.0;
        public double Range(double a, double b) => a + (b - a) * Next();
        /// <summary>1 ± amount 배율. 피치·감쇠 지터용.</summary>
        public double Jitter(double amount) => 1.0 + Range(-amount, amount);
        public Rng Fork() => new Rng((int)NextUInt());
    }

    /// <summary>모노 또는 스테레오 48kHz 버퍼. R이 null이면 모노.</summary>
    public sealed class Clip
    {
        public float[] L, R;
        public int Length => L.Length;
        public bool Stereo => R != null;
        public Clip(int n, bool stereo) { L = new float[n]; R = stereo ? new float[n] : null; }
        public Clip(float[] mono) { L = mono; }
        public Clip(float[] l, float[] r) { L = l; R = r; }
        public float[] this[int ch] => ch == 0 ? L : R;
        public int Channels => Stereo ? 2 : 1;
    }

    public enum Noise { White, Pink, Brown }
    public enum Wave { Sine, Triangle, Saw, Square }

    /// <summary>
    /// 모든 레시피가 공유하는 저수준 도구. 전부 float[] 을 제자리에서 다루고 48kHz 고정이다.
    /// 시간은 초, 주파수는 Hz. 난수는 반드시 주입된 <see cref="Rng"/>만 쓴다.
    /// </summary>
    public static class Dsp
    {
        public const int Sr = 48000;
        public static int Samples(double seconds) => (int)Math.Round(seconds * Sr);
        public static double Db(double db) => Math.Pow(10.0, db / 20.0);

        /* ------------------------------------------------------------ */
        /* 잡음                                                          */
        /* ------------------------------------------------------------ */

        /// <summary>white: 평탄 / pink: -3dB/oct (Paul Kellet 근사) / brown: -6dB/oct 누설 적분.</summary>
        public static float[] MakeNoise(int n, Noise kind, Rng rng)
        {
            var o = new float[n];
            double b0 = 0, b1 = 0, b2 = 0, b3 = 0, b4 = 0, b5 = 0, b6 = 0, y = 0;
            for (int i = 0; i < n; i++)
            {
                double w = rng.Next() * 2.0 - 1.0;
                switch (kind)
                {
                    case Noise.White: o[i] = (float)w; break;
                    case Noise.Pink:
                        b0 = 0.99886 * b0 + w * 0.0555179; b1 = 0.99332 * b1 + w * 0.0750759;
                        b2 = 0.96900 * b2 + w * 0.1538520; b3 = 0.86650 * b3 + w * 0.3104856;
                        b4 = 0.55000 * b4 + w * 0.5329522; b5 = -0.7616 * b5 - w * 0.0168980;
                        o[i] = (float)((b0 + b1 + b2 + b3 + b4 + b5 + b6 + w * 0.5362) * 0.11);
                        b6 = w * 0.115926; break;
                    case Noise.Brown:
                        y = 0.995 * y + w * 0.05; o[i] = (float)y; break;
                }
            }
            if (kind == Noise.Brown) Normalize(o, 1f);
            return o;
        }

        /// <summary>드문 충격 알갱이. 파편·모래알·불씨용. density는 초당 개수.</summary>
        public static float[] Grains(int n, double density, double pingHz, double pingTau, Rng rng)
        {
            var o = new float[n];
            double p = density / Sr;
            for (int i = 0; i < n; i++)
            {
                if (rng.Next() >= p) continue;
                double f = pingHz * rng.Jitter(0.35), a = rng.Range(0.3, 1.0), tau = pingTau * rng.Jitter(0.4);
                int len = Math.Min(n - i, Samples(tau * 6));
                for (int k = 0; k < len; k++)
                {
                    double t = (double)k / Sr;
                    o[i + k] += (float)(a * Math.Exp(-t / tau) * Math.Sin(2 * Math.PI * f * t));
                }
            }
            return o;
        }

        /* ------------------------------------------------------------ */
        /* 발진기                                                        */
        /* ------------------------------------------------------------ */

        /// <summary>주파수가 시간에 따라 변하는 발진기. 위상 누적이라 스윕이 끊기지 않는다.</summary>
        public static float[] Osc(int n, Wave wave, Func<double, double> freqAt, double phase0 = 0)
        {
            var o = new float[n];
            double ph = phase0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / Sr;
                double x = ph - Math.Floor(ph); // 0..1
                double v;
                switch (wave)
                {
                    case Wave.Triangle: v = 4.0 * Math.Abs(x - 0.5) - 1.0; break;
                    case Wave.Saw: v = 2.0 * x - 1.0; break;
                    case Wave.Square: v = x < 0.5 ? 1.0 : -1.0; break;
                    default: v = Math.Sin(2 * Math.PI * x); break;
                }
                o[i] = (float)v;
                ph += freqAt(t) / Sr;
            }
            return o;
        }

        public static float[] Sine(int n, double f, double phase0 = 0) => Osc(n, Wave.Sine, _ => f, phase0);

        /// <summary>f0에서 f1로 지수 스윕 (dur 뒤에는 f1 유지).</summary>
        public static float[] Sweep(int n, Wave wave, double f0, double f1, double dur)
            => Osc(n, wave, t => t >= dur ? f1 : f0 * Math.Pow(f1 / f0, t / dur));

        /// <summary>지수 감쇠하는 단일 부분음. 종·크리스탈의 기본 단위.</summary>
        public static float[] Partial(int n, double f, double tau, double amp, double phase0 = 0)
        {
            var o = Sine(n, f, phase0);
            for (int i = 0; i < n; i++) o[i] *= (float)(amp * Math.Exp(-i / (tau * Sr)));
            return o;
        }

        /* ------------------------------------------------------------ */
        /* 포락선                                                        */
        /* ------------------------------------------------------------ */

        public static float[] ExpDecay(int n, double tau, double delay = 0)
        {
            var e = new float[n]; int d = Samples(delay);
            for (int i = d; i < n; i++) e[i] = (float)Math.Exp(-(i - d) / (tau * Sr));
            return e;
        }

        /// <summary>attack(선형) → 지수 감쇠. 스웰·스팅어용.</summary>
        public static float[] AttackDecay(int n, double attack, double tau, double delay = 0)
        {
            var e = new float[n]; int d = Samples(delay), a = Math.Max(1, Samples(attack));
            for (int i = d; i < n; i++)
            {
                int k = i - d;
                e[i] = k < a ? (float)k / a : (float)Math.Exp(-(k - a) / (tau * Sr));
            }
            return e;
        }

        public static float[] Apply(float[] buf, float[] env)
        {
            int n = Math.Min(buf.Length, env.Length);
            for (int i = 0; i < n; i++) buf[i] *= env[i];
            for (int i = n; i < buf.Length; i++) buf[i] = 0;
            return buf;
        }

        public static float[] Apply(float[] buf, Func<double, double> gainAt)
        {
            for (int i = 0; i < buf.Length; i++) buf[i] *= (float)gainAt((double)i / Sr);
            return buf;
        }

        public static float[] FadeIn(float[] buf, double seconds)
        {
            int n = Math.Min(buf.Length, Samples(seconds));
            for (int i = 0; i < n; i++) buf[i] *= (float)i / n;
            return buf;
        }

        public static float[] FadeOut(float[] buf, double seconds)
        {
            int n = Math.Min(buf.Length, Samples(seconds)), s = buf.Length - n;
            for (int i = 0; i < n; i++) buf[s + i] *= 1f - (float)i / n;
            return buf;
        }

        /* ------------------------------------------------------------ */
        /* 필터                                                          */
        /* ------------------------------------------------------------ */

        public enum FilterKind { Lowpass, Highpass, Bandpass, Notch, Peak }

        /// <summary>RBJ 바이쿼드. 계수를 블록마다 갱신하면 스윕도 된다.</summary>
        public sealed class Biquad
        {
            double b0, b1, b2, a1, a2, z1, z2;
            public void Set(FilterKind kind, double fc, double q, double gainDb = 0)
            {
                fc = Math.Max(10, Math.Min(fc, Sr * 0.49));
                double w = 2 * Math.PI * fc / Sr, cw = Math.Cos(w), sw = Math.Sin(w), alpha = sw / (2 * Math.Max(0.05, q));
                double A = Math.Pow(10, gainDb / 40), B0, B1, B2, A0, A1, A2;
                switch (kind)
                {
                    case FilterKind.Highpass: B0 = (1 + cw) / 2; B1 = -(1 + cw); B2 = (1 + cw) / 2; A0 = 1 + alpha; A1 = -2 * cw; A2 = 1 - alpha; break;
                    case FilterKind.Bandpass: B0 = alpha; B1 = 0; B2 = -alpha; A0 = 1 + alpha; A1 = -2 * cw; A2 = 1 - alpha; break;
                    case FilterKind.Notch: B0 = 1; B1 = -2 * cw; B2 = 1; A0 = 1 + alpha; A1 = -2 * cw; A2 = 1 - alpha; break;
                    case FilterKind.Peak: B0 = 1 + alpha * A; B1 = -2 * cw; B2 = 1 - alpha * A; A0 = 1 + alpha / A; A1 = -2 * cw; A2 = 1 - alpha / A; break;
                    default: B0 = (1 - cw) / 2; B1 = 1 - cw; B2 = (1 - cw) / 2; A0 = 1 + alpha; A1 = -2 * cw; A2 = 1 - alpha; break;
                }
                b0 = B0 / A0; b1 = B1 / A0; b2 = B2 / A0; a1 = A1 / A0; a2 = A2 / A0;
            }
            public float Tick(float x)
            {
                double y = b0 * x + z1;
                z1 = b1 * x - a1 * y + z2;
                z2 = b2 * x - a2 * y;
                return (float)y;
            }
            public void Process(float[] buf) { for (int i = 0; i < buf.Length; i++) buf[i] = Tick(buf[i]); }
        }

        public static float[] Filter(float[] buf, FilterKind kind, double fc, double q = 0.707, double gainDb = 0)
        {
            var f = new Biquad(); f.Set(kind, fc, q, gainDb); f.Process(buf); return buf;
        }

        /// <summary>컷오프(그리고 필요하면 Q)가 시간에 따라 변하는 필터. 64샘플마다 계수 갱신.</summary>
        public static float[] FilterSweep(float[] buf, FilterKind kind, Func<double, double> fcAt, Func<double, double> qAt = null, int block = 64)
        {
            var f = new Biquad();
            for (int i = 0; i < buf.Length; i++)
            {
                if (i % block == 0) { double t = (double)i / Sr; f.Set(kind, fcAt(t), qAt?.Invoke(t) ?? 0.707); }
                buf[i] = f.Tick(buf[i]);
            }
            return buf;
        }

        /// <summary>단순 1극 로우패스. 댐핑용.</summary>
        public static float[] OnePole(float[] buf, double fc)
        {
            double a = Math.Exp(-2 * Math.PI * fc / Sr); float y = 0;
            for (int i = 0; i < buf.Length; i++) { y = (float)((1 - a) * buf[i] + a * y); buf[i] = y; }
            return buf;
        }

        /* ------------------------------------------------------------ */
        /* 비선형·믹스                                                   */
        /* ------------------------------------------------------------ */

        public static float[] Saturate(float[] buf, double drive)
        {
            double norm = Math.Tanh(drive);
            for (int i = 0; i < buf.Length; i++) buf[i] = (float)(Math.Tanh(buf[i] * drive) / norm);
            return buf;
        }

        public static float[] Gain(float[] buf, double g) { for (int i = 0; i < buf.Length; i++) buf[i] *= (float)g; return buf; }

        /// <summary>dst += src × gain, src를 dst의 at초 위치에 놓는다.</summary>
        public static float[] Mix(float[] dst, float[] src, double gain = 1, double at = 0)
        {
            int off = Samples(at), n = Math.Min(src.Length, dst.Length - off);
            for (int i = 0; i < n; i++) dst[off + i] += (float)(src[i] * gain);
            return dst;
        }

        public static float[] Mul(float[] dst, float[] src) { int n = Math.Min(dst.Length, src.Length); for (int i = 0; i < n; i++) dst[i] *= src[i]; return dst; }

        public static float Peak(float[] buf) { float p = 0; foreach (var v in buf) p = Math.Max(p, Math.Abs(v)); return p; }

        public static float[] Normalize(float[] buf, double peak)
        {
            float p = Peak(buf); if (p <= 1e-9f) return buf;
            return Gain(buf, peak / p);
        }

        /// <summary>양 채널을 같은 배율로 정규화해 정위를 유지한다.</summary>
        public static Clip Normalize(Clip c, double peakDb)
        {
            float p = Math.Max(Peak(c.L), c.Stereo ? Peak(c.R) : 0); if (p <= 1e-9f) return c;
            double g = Db(peakDb) / p; Gain(c.L, g); if (c.Stereo) Gain(c.R, g); return c;
        }

        /// <summary>단순 리미터: 피크가 ceiling을 넘는 곳만 tanh로 눌러 클리핑을 막는다.</summary>
        public static float[] SoftClip(float[] buf, double ceiling = 0.98)
        {
            for (int i = 0; i < buf.Length; i++)
            {
                double x = buf[i];
                if (Math.Abs(x) > ceiling * 0.8) buf[i] = (float)(Math.Sign(x) * (ceiling * 0.8 + (ceiling * 0.2) * Math.Tanh((Math.Abs(x) - ceiling * 0.8) / (ceiling * 0.2))));
            }
            return buf;
        }

        /* ------------------------------------------------------------ */
        /* 공간                                                          */
        /* ------------------------------------------------------------ */

        /// <summary>
        /// Schroeder 잔향(콤 4 + 올패스 2, 콤 안에 댐핑). 웻 신호만 돌려준다.
        /// decay = RT60(초), damp = 고역 감쇠 컷오프(Hz), predelay(초).
        /// </summary>
        public static float[] Reverb(float[] dry, double decay, double damp = 4000, double predelay = 0.01, int seed = 7)
        {
            int n = dry.Length;
            double[] combMs = { 29.7, 37.1, 41.1, 43.7 };
            var rng = new Rng(seed);
            var acc = new float[n];
            double dampA = Math.Exp(-2 * Math.PI * damp / Sr);
            foreach (var ms in combMs)
            {
                int d = Samples(ms * 0.001 * rng.Jitter(0.08));
                double fb = Math.Pow(10, -3.0 * d / Sr / decay);
                var line = new float[d]; int idx = 0; float lp = 0;
                for (int i = 0; i < n; i++)
                {
                    float y = line[idx];
                    lp = (float)((1 - dampA) * y + dampA * lp);
                    line[idx] = dry[i] + (float)(fb * lp);
                    acc[i] += y * 0.25f;
                    if (++idx >= d) idx = 0;
                }
            }
            foreach (var ms in new[] { 5.0, 1.7 })
            {
                int d = Samples(ms * 0.001); double g = 0.7;
                var line = new float[d]; int idx = 0;
                for (int i = 0; i < n; i++)
                {
                    float x = acc[i], y = line[idx];
                    float o = (float)(-g * x + y);
                    line[idx] = x + (float)(g * o);
                    acc[i] = o;
                    if (++idx >= d) idx = 0;
                }
            }
            if (predelay > 0)
            {
                var shifted = new float[n]; Mix(shifted, acc, 1, predelay); acc = shifted;
            }
            return acc;
        }

        /// <summary>피드백 에코.</summary>
        public static float[] Echo(float[] dry, double delay, double feedback, int repeats)
        {
            var o = (float[])dry.Clone();
            for (int r = 1; r <= repeats; r++) Mix(o, dry, Math.Pow(feedback, r), delay * r);
            return o;
        }

        /* ------------------------------------------------------------ */
        /* 루프                                                          */
        /* ------------------------------------------------------------ */

        /// <summary>
        /// 길이 N+X로 렌더한 버퍼의 꼬리 X를 머리에 등파워 크로스페이드해 길이 N의 심리스 루프를 만든다.
        /// 잡음처럼 주기적으로 만들 수 없는 층에 쓴다. (주기 층은 모든 주파수를 1/T의 배수로 잡으면 그 자체로 이음매가 없다.)
        /// </summary>
        public static float[] LoopCrossfade(float[] rendered, int loopLen)
        {
            int x = rendered.Length - loopLen;
            if (x <= 0) throw new ArgumentException("렌더 길이가 루프 길이 + 크로스페이드보다 짧다.");
            var o = new float[loopLen];
            Array.Copy(rendered, 0, o, 0, loopLen);
            for (int i = 0; i < x; i++)
            {
                double t = (double)i / x, w = Math.Sin(t * Math.PI / 2), v = Math.Cos(t * Math.PI / 2);
                o[i] = (float)(rendered[i] * w + rendered[loopLen + i] * v);
            }
            return o;
        }

        /// <summary>루프 길이 T초에서 이음매 없이 반복되는 LFO. cycles는 정수여야 한다.</summary>
        public static Func<double, double> PeriodicLfo(double loopSeconds, int cycles, double phase = 0)
            => t => Math.Sin(2 * Math.PI * (cycles * t / loopSeconds + phase));

        /// <summary>주파수를 1/T의 배수로 반올림해 주기 층이 정확히 이어지게 한다.</summary>
        public static double LoopFreq(double f, double loopSeconds) => Math.Round(f * loopSeconds) / loopSeconds;

        public static double SmoothStep(double e0, double e1, double x)
        {
            double t = Math.Max(0, Math.Min(1, (x - e0) / (e1 - e0))); return t * t * (3 - 2 * t);
        }
    }
}

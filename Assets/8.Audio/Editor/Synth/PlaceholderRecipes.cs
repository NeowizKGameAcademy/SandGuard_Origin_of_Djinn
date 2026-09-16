using System;
using static SandGuard.Audio.Editor.Synth.Dsp;

namespace SandGuard.Audio.Editor.Synth
{
    /// <summary>
    /// 담당자가 교체할 자리표시 클립. 종류별로 성격이 다르고 이름 해시로 피치가 달라 "어느 큐가 울렸는지" 귀로 구분된다.
    /// 완성도는 목표가 아니다. 길이·루프 여부·대략의 톤만 맞춘다.
    /// </summary>
    public static class PlaceholderRecipes
    {
        /// <summary>이름으로 0.8~1.25 사이 피치 배율을 만든다. 같은 이름은 항상 같은 배율.</summary>
        static double PitchOf(string name)
        {
            uint h = 2166136261;
            foreach (char c in name) { h ^= c; h *= 16777619; }
            return 0.8 + (h % 1000) / 1000.0 * 0.45;
        }

        public static Clip Render(SandGuard.Audio.Editor.PlaceholderKind kind, string name)
        {
            double p = PitchOf(name);
            int seed = name.GetHashCode() & 0x7fffffff;
            switch (kind)
            {
                case SandGuard.Audio.Editor.PlaceholderKind.Impact: return Impact(seed, p);
                case SandGuard.Audio.Editor.PlaceholderKind.Whoosh: return Whoosh(seed, p);
                case SandGuard.Audio.Editor.PlaceholderKind.Zap: return Zap(seed, p);
                case SandGuard.Audio.Editor.PlaceholderKind.Ping: return Ping(seed, p);
                case SandGuard.Audio.Editor.PlaceholderKind.Loop: return Loop(seed, p);
                case SandGuard.Audio.Editor.PlaceholderKind.Drone: return Drone(seed, p);
                case SandGuard.Audio.Editor.PlaceholderKind.Sting: return Sting(seed, p);
                case SandGuard.Audio.Editor.PlaceholderKind.Voice: return Voice(seed, p);
                default: return Tick(seed, p);
            }
        }

        static Clip Impact(int seed, double p)
        {
            var rng = new Rng(seed); int n = Samples(0.5); var m = new float[n];
            Mix(m, Apply(MakeNoise(n, Noise.White, rng.Fork()), ExpDecay(n, 0.001)), 0.8);
            var body = MakeNoise(n, Noise.White, rng.Fork());
            FilterSweep(body, FilterKind.Lowpass, t => 4000 * p * Math.Pow(300.0 / 4000.0, Math.Min(1, t / 0.25)), _ => 0.8);
            Apply(body, ExpDecay(n, 0.12)); Mix(m, body, 0.7);
            Mix(m, Apply(Sweep(n, Wave.Sine, 160 * p, 60 * p, 0.1), ExpDecay(n, 0.09)), 0.8);
            Mix(m, Reverb(m, 0.5, 4000, 0.01, seed), 0.2);
            Filter(m, FilterKind.Highpass, 40); FadeOut(m, 0.05); return new Clip(m);
        }

        static Clip Whoosh(int seed, double p)
        {
            var rng = new Rng(seed); int n = Samples(0.45); var m = MakeNoise(n, Noise.White, rng.Fork());
            FilterSweep(m, FilterKind.Bandpass, t => 500 * p * Math.Pow(5, Math.Sin(Math.Min(1, t / 0.4) * Math.PI)), _ => 1.2);
            Apply(m, AttackDecay(n, 0.12, 0.12));
            Mix(m, Apply(Sine(n, 70 * p), AttackDecay(n, 0.05, 0.08, 0.1)), 0.4);
            FadeOut(m, 0.04); return new Clip(m);
        }

        static Clip Zap(int seed, double p)
        {
            var rng = new Rng(seed); int n = Samples(0.35); var m = new float[n];
            var s = Sweep(n, Wave.Saw, 1400 * p, 320 * p, 0.14); Filter(s, FilterKind.Lowpass, 3500); Apply(s, ExpDecay(n, 0.07)); Mix(m, s, 0.6);
            Mix(m, Partial(n, 2200 * p, 0.09, 0.3, rng.Next())); Mix(m, Partial(n, 3300 * p, 0.05, 0.15, rng.Next()));
            var air = MakeNoise(n, Noise.White, rng.Fork()); Filter(air, FilterKind.Bandpass, 2500 * p, 1.5); Apply(air, ExpDecay(n, 0.05)); Mix(m, air, 0.35);
            Filter(m, FilterKind.Highpass, 60); FadeOut(m, 0.03); return new Clip(m);
        }

        static Clip Ping(int seed, double p)
        {
            var rng = new Rng(seed); int n = Samples(0.25); var m = new float[n];
            Mix(m, Partial(n, 1320 * p, 0.09, 0.6, rng.Next())); Mix(m, Partial(n, 1980 * p, 0.06, 0.3, rng.Next()));
            Mix(m, Apply(MakeNoise(n, Noise.White, rng.Fork()), ExpDecay(n, 0.0006)), 0.3);
            FadeOut(m, 0.02); return new Clip(m);
        }

        static Clip Tick(int seed, double p)
        {
            var rng = new Rng(seed); int n = Samples(0.09); var m = new float[n];
            var nz = MakeNoise(n, Noise.White, rng.Fork()); Filter(nz, FilterKind.Bandpass, 1800 * p, 1.0); Apply(nz, ExpDecay(n, 0.012)); Mix(m, nz, 0.8);
            Mix(m, Apply(Sine(n, 220 * p), ExpDecay(n, 0.02)), 0.4);
            FadeOut(m, 0.01); return new Clip(m);
        }

        /// <summary>4초 잡음 루프. 컷오프가 이름 피치를 따른다.</summary>
        static Clip Loop(int seed, double p)
        {
            const double T = 4.0; var rng = new Rng(seed); int n = Samples(T), nx = Samples(T + 0.8);
            var ch = new float[2][];
            for (int c = 0; c < 2; c++)
            {
                var nz = MakeNoise(nx, Noise.Pink, rng.Fork());
                FilterSweep(nz, FilterKind.Bandpass, t => 600 * p * (1 + 0.5 * Math.Sin(2 * Math.PI * t / T)), _ => 0.7);
                ch[c] = LoopCrossfade(nz, n);
            }
            var tone = Sine(n, LoopFreq(110 * p, T)); Apply(tone, PeriodicLfoGain(T, 2));
            Mix(ch[0], tone, 0.15); Mix(ch[1], tone, 0.15);
            return new Clip(ch[0], ch[1]);
        }

        static Func<double, double> PeriodicLfoGain(double T, int cycles) { var l = PeriodicLfo(T, cycles); return t => 0.7 + 0.3 * l(t); }

        /// <summary>8초 음악 자리표시: 근음·5도·옥타브 드론 + 100BPM 펄스. 이름 해시로 근음이 다르다.</summary>
        static Clip Drone(int seed, double p)
        {
            const double T = 9.6; // 100 BPM × 16박
            var rng = new Rng(seed); int n = Samples(T);
            double root = LoopFreq(110 * p, T);
            var m = new float[n];
            Mix(m, Sine(n, root), 0.4); Mix(m, Sine(n, LoopFreq(root * 1.5, T)), 0.25); Mix(m, Sine(n, LoopFreq(root * 2, T)), 0.2);
            Mix(m, Osc(n, Wave.Saw, _ => LoopFreq(root * 0.5, T)), 0.12); Filter(m, FilterKind.Lowpass, 900);
            Apply(m, PeriodicLfoGain(T, 4));
            for (int b = 0; b < 16; b++)
            {
                var pulse = Apply(MakeNoise(Samples(0.12), Noise.White, rng.Fork()), ExpDecay(Samples(0.12), 0.02));
                Filter(pulse, FilterKind.Lowpass, b % 4 == 0 ? 600 : 2500);
                Mix(m, pulse, b % 4 == 0 ? 0.6 : 0.25, b * 0.6);
                if (b % 4 == 0) Mix(m, Apply(Sweep(Samples(0.2), Wave.Sine, 120, 50, 0.1), ExpDecay(Samples(0.2), 0.08)), 0.5, b * 0.6);
            }
            var L = (float[])m.Clone(); var R = (float[])m.Clone();
            Mix(L, Reverb(m, 1.0, 3000, 0.02, seed), 0.15); Mix(R, Reverb(m, 1.0, 3000, 0.02, seed + 1), 0.15);
            return new Clip(LoopCrossfade(Pad(L, Samples(0.5)), n), LoopCrossfade(Pad(R, Samples(0.5)), n));
        }

        /// <summary>루프 크로스페이드를 위해 머리를 꼬리에 이어 붙인다 (주기 층이라 사실상 연속).</summary>
        static float[] Pad(float[] x, int extra)
        {
            var o = new float[x.Length + extra]; Array.Copy(x, o, x.Length); Array.Copy(x, 0, o, x.Length, extra); return o;
        }

        static Clip Sting(int seed, double p)
        {
            var rng = new Rng(seed); int n = Samples(1.4); var m = new float[n];
            double[] notes = { 1, 1.25, 1.5, 2 };
            for (int i = 0; i < notes.Length; i++)
            {
                Mix(m, Partial(n, 440 * p * notes[i], 0.4, 0.5, rng.Next()), 0.6, i * 0.1);
                Mix(m, Partial(n, 440 * p * notes[i] * 2.76, 0.12, 0.15, rng.Next()), 0.6, i * 0.1);
            }
            Mix(m, Apply(Sine(n, 110 * p), AttackDecay(n, 0.05, 0.3)), 0.3);
            var L = (float[])m.Clone(); var R = (float[])m.Clone();
            Mix(L, Reverb(m, 1.2, 5000, 0.01, seed), 0.3); Mix(R, Reverb(m, 1.2, 5000, 0.01, seed + 1), 0.3);
            FadeOut(L, 0.2); FadeOut(R, 0.2); return new Clip(L, R);
        }

        /// <summary>목소리 자리: 톱니파 + 포먼트 대역 2개 + 비브라토. "사람이 소리치는 것 같은" 정도.</summary>
        static Clip Voice(int seed, double p)
        {
            var rng = new Rng(seed); int n = Samples(0.45);
            double f0 = 140 * p;
            var src = Osc(n, Wave.Saw, t => f0 * (1 + 0.04 * Math.Sin(2 * Math.PI * 6 * t)) * (1 - 0.25 * Math.Max(0, t - 0.25)));
            Mix(src, MakeNoise(n, Noise.White, rng.Fork()), 0.15);
            var f1 = (float[])src.Clone(); Filter(f1, FilterKind.Bandpass, 650, 4); var f2 = (float[])src.Clone(); Filter(f2, FilterKind.Bandpass, 1250 * p, 5);
            var m = new float[n]; Mix(m, f1, 1.0); Mix(m, f2, 0.6); Saturate(m, 2.0);
            Apply(m, AttackDecay(n, 0.03, 0.18)); Filter(m, FilterKind.Highpass, 90);
            FadeOut(m, 0.05); return new Clip(m);
        }
    }
}

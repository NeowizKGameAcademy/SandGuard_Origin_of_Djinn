using System;
using System.Collections.Generic;
using static SandGuard.Audio.Editor.Synth.Dsp;

namespace SandGuard.Audio.Editor.Synth
{
    /// <summary>베이커가 굽는 한 파일. Name은 확장자 없는 파일 이름.</summary>
    public sealed class Recipe
    {
        public string Name;
        public bool Loop;
        /// <summary>정규화 목표 피크(dBFS). 루프·베드는 낮게 둔다.</summary>
        public double PeakDb;
        public Func<Clip> Render;
        public string Notes;
    }

    /// <summary>
    /// 1단계 검증용 5종. 값을 바꾸고 `SandGuard > Audio > Bake Synth SFX`를 다시 실행하면 된다.
    /// 마력·크리스탈 계열(청록)은 부분음 족보를 공유한다: 기본음 × {1, 1.5, 2.52, 3.0} 부근의 비화성 비율.
    /// </summary>
    public static class SynthRecipes
    {
        /* 마력 크리스탈 부분음 비율 — 종처럼 살짝 비화성이라 "유리·크리스탈"로 읽힌다 */
        static readonly double[] CrystalRatios = { 1.0, 1.498, 2.516, 3.011, 4.63 };
        static readonly double[] CrystalAmps = { 1.0, 0.55, 0.35, 0.22, 0.08 };

        public static List<Recipe> All()
        {
            var list = new List<Recipe>();
            for (int i = 0; i < 4; i++)
            {
                int v = i + 1;
                list.Add(new Recipe { Name = $"SFX_Player_ManaBolt_Fire_{v:00}", PeakDb = -1, Render = () => ManaBoltFire(100 + v), Notes = "마나탄 발사: 클릭 + 하강 바디 + 크리스탈 부분음 + 공기 whoosh" });
            }
            for (int i = 0; i < 3; i++)
            {
                int v = i + 1;
                list.Add(new Recipe { Name = $"SFX_UI_Click_{v:00}", PeakDb = -3, Render = () => UiClick(200 + v, false), Notes = "UI 클릭: 두 부분음 핑 + 틱" });
            }
            list.Add(new Recipe { Name = "SFX_UI_Hover_01", PeakDb = -8, Render = () => UiClick(210, true), Notes = "UI 호버: 클릭보다 얇고 높음" });
            list.Add(new Recipe { Name = "SFX_Core_Hum_Loop", Loop = true, PeakDb = -8, Render = () => CoreHumLoop(300), Notes = "코어 크리스탈 험 6초 루프. 주기 층은 1/T 배수 주파수, 숨결 층만 크로스페이드" });
            list.Add(new Recipe { Name = "SFX_Env_DesertWind_Loop", Loop = true, PeakDb = -10, Render = () => DesertWindLoop(400), Notes = "사막 바람 베드 12초 루프: 갈색 잡음 2층 + 휘파람 + 돌풍 2회" });
            list.Add(new Recipe { Name = "SFX_Player_LevelUp_01", PeakDb = -1, Render = () => LevelUp(500), Notes = "레벨업: 상승 아르페지오 종 + 스파클 + 서브 스웰 + 잔향" });
            return list;
        }

        /* ============================================================ */
        /* 1. 마나탄 발사                                                */
        /* ============================================================ */

        /// <summary>
        /// 총성의 층 구조를 마력에 옮긴 것: 트랜지언트(순간) + 바디(무게) + 크리스탈(정체성) + 공기(움직임) + 잔향(공간).
        /// 매 변형은 피치 ±4%, 감쇠 ±15%, 부분음 위상 랜덤.
        /// </summary>
        public static Clip ManaBoltFire(int seed)
        {
            var rng = new Rng(seed);
            int n = Samples(0.45);
            var mix = new float[n];
            double pitch = rng.Jitter(0.04), decay = rng.Jitter(0.15);

            // 1) 트랜지언트: 1ms 백색 잡음 + 한 사이클 사인 "스냅"
            var click = Apply(MakeNoise(n, Noise.White, rng.Fork()), ExpDecay(n, 0.0008));
            Mix(mix, click, 0.6);
            Mix(mix, Apply(Sine(n, 3200 * pitch), ExpDecay(n, 0.0015)), 0.5);

            // 2) 바디: 900→260Hz 지수 스윕, 포화. "발사되는 무게"
            var body = Sweep(n, Wave.Triangle, 900 * pitch, 260 * pitch, 0.09);
            Apply(body, ExpDecay(n, 0.045 * decay));
            Saturate(body, 2.5);
            Mix(mix, body, 0.7);

            // 3) 크리스탈 부분음: 기본 1046Hz(C6) 부근, 비화성 비율. 정체성은 여기 있다
            double f0 = 1046 * pitch;
            for (int k = 0; k < CrystalRatios.Length; k++)
            {
                double f = f0 * CrystalRatios[k] * rng.Jitter(0.006);
                double tau = (0.11 - 0.015 * k) * decay;
                Mix(mix, Partial(n, f, tau, CrystalAmps[k] * 0.32, rng.Next()));
            }
            // 반짝임: 아주 높은 부분음, 짧게
            Mix(mix, Partial(n, 8400 * pitch, 0.02, 0.10, rng.Next()));

            // 4) 공기: 대역통과 잡음이 3.5k→1.2k로 내려가며 사라짐 — "슉"
            var air = MakeNoise(n, Noise.White, rng.Fork());
            FilterSweep(air, FilterKind.Bandpass, t => 3500 * pitch * Math.Pow(1200.0 / 3500.0, Math.Min(1, t / 0.14)), _ => 1.4);
            Apply(air, AttackDecay(n, 0.004, 0.05 * decay));
            Mix(mix, air, 0.45);

            // 5) 짧은 잔향: 마력이 공간에 남는 느낌만 살짝
            var wet = Reverb(mix, 0.35, 5000, 0.006, seed);
            Mix(mix, wet, 0.14);

            Filter(mix, FilterKind.Highpass, 60);
            FadeOut(mix, 0.05);
            return new Clip(mix);
        }

        /* ============================================================ */
        /* 2. UI 클릭 / 호버                                             */
        /* ============================================================ */

        /// <summary>돌 원판 UI에 맞춘 소리: 짧은 두 부분음 핑 + 접촉 틱 + 아주 작은 저역 바디.</summary>
        public static Clip UiClick(int seed, bool hover)
        {
            var rng = new Rng(seed);
            int n = Samples(hover ? 0.09 : 0.12);
            var mix = new float[n];
            double pitch = rng.Jitter(0.02);

            if (hover)
            {
                Mix(mix, Partial(n, 2200 * pitch, 0.018, 0.5, rng.Next()));
                Mix(mix, Partial(n, 3300 * pitch, 0.010, 0.2, rng.Next()));
                Mix(mix, Apply(MakeNoise(n, Noise.White, rng.Fork()), ExpDecay(n, 0.0005)), 0.25);
            }
            else
            {
                Mix(mix, Partial(n, 1760 * pitch, 0.028, 0.7, rng.Next()));
                Mix(mix, Partial(n, 2637 * pitch, 0.020, 0.4, rng.Next()));
                Mix(mix, Partial(n, 4400 * pitch, 0.008, 0.15, rng.Next()));
                // 접촉 틱 + 저역 바디(돌을 누르는 느낌)
                Mix(mix, Apply(MakeNoise(n, Noise.White, rng.Fork()), ExpDecay(n, 0.0006)), 0.5);
                Mix(mix, Apply(Sweep(n, Wave.Sine, 420, 180, 0.02), ExpDecay(n, 0.012)), 0.35);
            }
            Filter(mix, FilterKind.Highpass, 120);
            FadeOut(mix, 0.01);
            return new Clip(mix);
        }

        /* ============================================================ */
        /* 3. 코어 크리스탈 험 (루프)                                    */
        /* ============================================================ */

        /// <summary>
        /// T=6초. 드론과 반짝임은 모든 주파수·LFO를 1/T 배수로 잡아 수학적으로 이음매가 없다.
        /// 숨결(잡음) 층만 T+1초 렌더 후 크로스페이드. 스테레오: 반짝임을 좌우로 나눠 배치.
        /// </summary>
        public static Clip CoreHumLoop(int seed)
        {
            const double T = 6.0;
            var rng = new Rng(seed);
            int n = Samples(T);
            var L = new float[n]; var R = new float[n];

            // 드론: 55Hz 기본 + 배음, 약간 어긋난 쌍둥이(비트 2회/T)로 살아 움직이게
            double f = LoopFreq(55, T), f2 = LoopFreq(55, T) + 2.0 / T;
            var drone = new float[n];
            Mix(drone, Sine(n, f), 0.55);
            Mix(drone, Sine(n, f2), 0.35);
            Mix(drone, Sine(n, LoopFreq(110, T)), 0.30);
            Mix(drone, Sine(n, LoopFreq(165, T)), 0.12);
            Saturate(drone, 1.4);
            var breathe = PeriodicLfo(T, 2);
            Apply(drone, t => 0.85 + 0.15 * breathe(t));
            Mix(L, drone, 0.5); Mix(R, drone, 0.5);

            // 반짝임: 크리스탈 비율 부분음, 각자 다른 주기의 진폭 LFO로 트윙클. 좌우 교차 배치
            double b = LoopFreq(1320, T);
            int[] cycles = { 3, 5, 7, 4, 9 };
            for (int k = 0; k < CrystalRatios.Length; k++)
            {
                double pf = LoopFreq(b * CrystalRatios[k], T);
                var vib = PeriodicLfo(T, 5 + k, rng.Next());
                var p = Osc(n, Wave.Sine, t => pf * (1 + 0.0025 * vib(t)), rng.Next());
                var lfo = PeriodicLfo(T, cycles[k], rng.Next());
                Apply(p, t => Math.Pow(Math.Max(0, 0.5 + 0.5 * lfo(t)), 2.2));
                double amp = 0.09 * CrystalAmps[k];
                double pan = (k % 2 == 0) ? 0.3 : 0.7; // 0=L 1=R
                Mix(L, p, amp * Math.Cos(pan * Math.PI / 2));
                Mix(R, p, amp * Math.Sin(pan * Math.PI / 2));
            }

            // 숨결: 핑크 잡음 저역, 느린 LFO. 좌우 다른 시드 → 폭
            int nx = Samples(T + 1.0);
            var swell = PeriodicLfo(T, 2, 0.25);
            for (int ch = 0; ch < 2; ch++)
            {
                var nz = MakeNoise(nx, Noise.Pink, rng.Fork());
                Filter(nz, FilterKind.Lowpass, 520, 0.8);
                Filter(nz, FilterKind.Highpass, 70);
                Apply(nz, t => 0.5 + 0.5 * swell(t));
                var looped = LoopCrossfade(nz, n);
                Mix(ch == 0 ? L : R, looped, 0.10);
            }

            // 아주 높은 공기층: 마력 "지잉". 대역통과 잡음, 매우 작게, 역시 크로스페이드
            var hiss = MakeNoise(nx, Noise.White, rng.Fork());
            Filter(hiss, FilterKind.Bandpass, 6200, 3.0);
            var hlfo = PeriodicLfo(T, 3, 0.6);
            Apply(hiss, t => 0.6 + 0.4 * hlfo(t));
            var hissL = LoopCrossfade(hiss, n);
            Mix(L, hissL, 0.035); Mix(R, hissL, 0.035);

            return new Clip(L, R);
        }

        /* ============================================================ */
        /* 4. 사막 바람 베드 (루프)                                      */
        /* ============================================================ */

        /// <summary>
        /// T=12초, 크로스페이드 2초. 갈색 잡음 2층의 컷오프를 서로 다른 속도의 LFO로 흔들고,
        /// 돌풍 포락선이 레벨과 컷오프를 같이 연다. 휘파람은 고Q 대역통과가 중심을 천천히 옮긴다.
        /// </summary>
        public static Clip DesertWindLoop(int seed)
        {
            const double T = 12.0, X = 2.0;
            var rng = new Rng(seed);
            int n = Samples(T), nx = Samples(T + X);

            // 돌풍: 2개, 각각 2.5~3.5초 raised-cosine
            var gusts = new List<(double at, double len, double amt)>();
            gusts.Add((rng.Range(1.5, 3.5), rng.Range(2.5, 3.5), rng.Range(0.6, 0.9)));
            gusts.Add((rng.Range(7.0, 9.0), rng.Range(2.5, 3.5), rng.Range(0.4, 0.7)));
            double Gust(double t)
            {
                double g = 0;
                foreach (var (at, len, amt) in gusts)
                {
                    double u = (t - at) / len;
                    if (u > 0 && u < 1) g += amt * 0.5 * (1 - Math.Cos(2 * Math.PI * u));
                }
                return g;
            }
            var lfoA = new Func<double, double>(t => Math.Sin(2 * Math.PI * 0.071 * t) * 0.5 + Math.Sin(2 * Math.PI * 0.113 * t + 1.3) * 0.5);
            var lfoB = new Func<double, double>(t => Math.Sin(2 * Math.PI * 0.043 * t + 0.7));

            var chans = new float[2][];
            for (int ch = 0; ch < 2; ch++)
            {
                var mix = new float[nx];

                // 층 1: 낮은 바람 몸통. 컷오프 260~900Hz + 돌풍이 +900Hz까지 연다
                var bed1 = MakeNoise(nx, Noise.Brown, rng.Fork());
                FilterSweep(bed1, FilterKind.Lowpass, t => 260 + 320 * (lfoA(t) + 1) + 900 * Gust(t), _ => 0.9);
                Filter(bed1, FilterKind.Highpass, 40);
                Apply(bed1, t => 0.55 + 0.25 * lfoA(t) + 0.9 * Gust(t));
                Mix(mix, bed1, 0.9);

                // 층 2: 모래가 흐르는 중역 질감. 핑크 잡음 1.2~2.4kHz 대역
                var bed2 = MakeNoise(nx, Noise.Pink, rng.Fork());
                FilterSweep(bed2, FilterKind.Bandpass, t => 1500 + 500 * lfoB(t) + 1200 * Gust(t), _ => 0.6);
                Apply(bed2, t => 0.35 + 0.15 * lfoB(t) + 0.8 * Gust(t));
                Mix(mix, bed2, 0.22);

                // 휘파람: 고Q 대역통과, 중심이 천천히 이동, 돌풍 때만 앞으로 나온다
                var wh = MakeNoise(nx, Noise.White, rng.Fork());
                double ph = rng.Range(0, 6.28);
                FilterSweep(wh, FilterKind.Bandpass, t => 1400 + 700 * Math.Sin(2 * Math.PI * 0.09 * t + ph) + 600 * Gust(t), _ => 14);
                Apply(wh, t => Math.Pow(Math.Max(0, Math.Sin(2 * Math.PI * 0.05 * t + ph)), 3) * 0.5 + 1.2 * Gust(t));
                Mix(mix, wh, 0.10);

                // 모래알: 드문 알갱이, 돌풍에 비례
                var gr = Grains(nx, 40, 5200, 0.004, rng.Fork());
                Apply(gr, t => 0.15 + 1.0 * Gust(t));
                Mix(mix, gr, 0.12);

                chans[ch] = LoopCrossfade(mix, n);
            }
            return new Clip(chans[0], chans[1]);
        }

        /* ============================================================ */
        /* 5. 레벨업                                                    */
        /* ============================================================ */

        /// <summary>
        /// 금빛 상승 아르페지오(A4 C#5 E5 A5) 종 부분음 → 화음 스웰, 스파클 잡음 상승, 서브 스웰, 긴 잔향.
        /// VFX_LevelUp(금색 링 3겹 + 나선 입자 2초)에 맞춘 2.4초.
        /// </summary>
        public static Clip LevelUp(int seed)
        {
            var rng = new Rng(seed);
            int n = Samples(2.4);
            var mix = new float[n];
            double[] notes = { 440.0, 554.37, 659.25, 880.0 };
            double[] onsets = { 0.0, 0.09, 0.18, 0.27 };

            // 종 부분음: 기본 + 2.76배(비화성) + 5.4배. 아르페지오는 짧게, 마지막 화음은 길게
            for (int i = 0; i < notes.Length; i++)
            {
                double f = notes[i];
                var bell = new float[n];
                Mix(bell, Partial(n, f, 0.35, 0.55, rng.Next()));
                Mix(bell, Partial(n, f * 2.76, 0.16, 0.22, rng.Next()));
                Mix(bell, Partial(n, f * 5.40, 0.07, 0.10, rng.Next()));
                Mix(mix, bell, 0.6, onsets[i]);
            }
            // 화음 스웰: 네 음을 동시에, 느린 attack, 긴 꼬리
            var chord = new float[n];
            foreach (var f in notes) Mix(chord, Partial(n, f, 0.9, 0.5, rng.Next()));
            Mix(chord, Partial(n, 1760, 0.6, 0.18, rng.Next()));
            Apply(chord, AttackDecay(n, 0.12, 0.6));
            Mix(mix, chord, 0.45, 0.30);

            // 스파클: 백색 잡음 대역이 2k→9k로 올라가며 반짝임. 알갱이도 같이
            var sp = MakeNoise(n, Noise.White, rng.Fork());
            FilterSweep(sp, FilterKind.Bandpass, t => 2000 * Math.Pow(4.5, Math.Min(1, t / 0.6)), _ => 2.5);
            Apply(sp, AttackDecay(n, 0.15, 0.28));
            Mix(mix, sp, 0.18);
            var gr = Grains(n, 90, 7000, 0.006, rng.Fork());
            Apply(gr, AttackDecay(n, 0.1, 0.35, 0.1));
            Mix(mix, gr, 0.3);

            // 링 whoosh: 갈색 잡음, 로우패스가 400→3000으로 열림 — 링이 퍼지는 움직임
            var wh = MakeNoise(n, Noise.Brown, rng.Fork());
            Filter(wh, FilterKind.Highpass, 200);
            FilterSweep(wh, FilterKind.Lowpass, t => 400 * Math.Pow(7.5, Math.Min(1, t / 0.5)), _ => 0.9);
            Apply(wh, AttackDecay(n, 0.08, 0.3));
            Mix(mix, wh, 0.25);

            // 서브 스웰: 110→220Hz, 바닥에서 솟는 느낌
            var sub = Sweep(n, Wave.Sine, 110, 220, 0.4);
            Apply(sub, AttackDecay(n, 0.05, 0.3));
            Mix(mix, sub, 0.3);

            // 잔향: 좌우 다른 시드로 폭을 만든다
            var L = (float[])mix.Clone(); var R = (float[])mix.Clone();
            Mix(L, Reverb(mix, 1.3, 5500, 0.015, seed), 0.32);
            Mix(R, Reverb(mix, 1.3, 5500, 0.015, seed + 1), 0.32);
            Filter(L, FilterKind.Highpass, 50); Filter(R, FilterKind.Highpass, 50);
            FadeOut(L, 0.25); FadeOut(R, 0.25);
            return new Clip(L, R);
        }
    }
}

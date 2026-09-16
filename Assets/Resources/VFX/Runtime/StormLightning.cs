using UnityEngine;

namespace DesertTower.VFX
{
    /// <summary>폭풍 벽에 치는 마법 번개. 줄기의 위치·시각을 여기서 정해 셰이더(SandGuard/VFX/StormLightning)와
    /// 번쩍임 조명에 같이 넘긴다. 환경 연출뿐이며 데미지·충돌·내비게이션 변경은 없다.</summary>
    [ExecuteAlways]
    public sealed class StormLightning : MonoBehaviour
    {
        public const int Bolts = 4;
        public Renderer[] boltLayers;
        [Tooltip("번개가 칠 때 신전을 물들이는 조명. 세기는 여기서 매 프레임 정한다.")]
        public Light flashLight;
        [Min(0.1f)] public float minInterval = 1.5f, maxInterval = 4.5f;
        [Min(0.05f), Tooltip("한 줄기가 보이는 시간(초). 실제로는 0.7~1.3배로 흔들린다.")] public float duration = 0.45f;
        [Min(0)] public float lightIntensity = 3f;
        public int seed = 7;
        public bool animate = true;
        [Min(0)] public float previewTime = 3f;

        readonly Vector4[] bolts = new Vector4[Bolts];   // x 둘레 위치, y 씨앗, z 시작 시각, w 지속 시간
        readonly float[] nextAt = new float[Bolts];
        System.Random rng;
        MaterialPropertyBlock block;
        float now;
        static readonly int BoltsId = Shader.PropertyToID("_Bolts"), TimeId = Shader.PropertyToID("_LightningTime");

        void OnEnable()
        {
            rng = new System.Random(seed);
            now = animate ? Time.realtimeSinceStartup : previewTime;
            for (int i = 0; i < Bolts; i++) { bolts[i] = new Vector4(0, 0, -1000, duration); nextAt[i] = now + (float)rng.NextDouble() * maxInterval; }
            if (!animate) Pose(previewTime); else Apply();
        }

        void Update()
        {
            if (!animate) { Pose(previewTime); return; }
            now = Time.realtimeSinceStartup;
            for (int i = 0; i < Bolts; i++) if (now >= nextAt[i]) Strike(i);
            Apply();
        }

        /// <summary>줄기가 칠 때마다. 소리(크랙·천둥) 연결용. 미리보기(animate=false)에서는 부르지 않는다.</summary>
        public UnityEngine.Events.UnityEvent onStrike = new UnityEngine.Events.UnityEvent();

        void Strike(int i)
        {
            float d = duration * (0.7f + 0.6f * (float)rng.NextDouble());
            bolts[i] = new Vector4((float)rng.NextDouble(), (float)rng.NextDouble(), now, d);
            nextAt[i] = now + d + Mathf.Lerp(minInterval, maxInterval, (float)rng.NextDouble());
            if (Application.isPlaying) onStrike.Invoke();
        }

        /// <summary>미리보기·촬영용: 시각 t 에 네 줄기가 막 친 상태로 둔다.</summary>
        public void Pose(float t)
        {
            now = t;
            for (int i = 0; i < Bolts; i++) bolts[i] = new Vector4((i + .5f) / Bolts, .13f + .21f * i, t - .06f - .05f * i, duration);
            Apply();
        }

        /// <summary>셰이더의 envelope 와 같은 식. 조명과 줄기가 같이 깜빡이게 한다.</summary>
        public static float Envelope(float t)
        {
            if (t < 0 || t > 1) return 0;
            float rise = Mathf.Clamp01(t / .08f);
            float decay = t < .08f ? 1 : Mathf.Exp(-(t - .08f) * 5.5f);
            float flicker = .7f + .3f * Mathf.Sin(t * 95) * Mathf.Sin(t * 37 + 1.3f);
            return rise * decay * flicker;
        }

        void Apply()
        {
            if (block == null) block = new MaterialPropertyBlock();
            float flash = 0;
            for (int i = 0; i < Bolts; i++) flash += Envelope((now - bolts[i].z) / Mathf.Max(bolts[i].w, .01f));
            if (flashLight) flashLight.intensity = lightIntensity * Mathf.Min(flash, 1.5f);
            if (boltLayers == null) return;
            foreach (var layer in boltLayers)
            {
                if (!layer) continue;
                layer.GetPropertyBlock(block);
                block.SetVectorArray(BoltsId, bolts);
                block.SetFloat(TimeId, now);
                layer.SetPropertyBlock(block);
            }
        }
    }
}

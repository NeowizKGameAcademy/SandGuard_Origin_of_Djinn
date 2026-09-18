using UnityEngine;

namespace DesertTower.VFX
{
    /// <summary>
    /// 사막 폭풍 시전 순간 "신전 바깥을 도는 폭풍의 힘을 빌려오는" 연출. <see cref="Trigger"/>에 씬의 <see cref="TempleSandstorm"/>(봉인 폭풍)을 찾아
    /// 흐름 속도·소용돌이(_Swirl)·밝기(_LightColor)를 attack→hold→release 포락선으로 잠깐 끌어올렸다가 되돌린다.
    /// 시간은 스스로 굴려 <see cref="TempleSandstorm.previewTime"/>에 넣으므로(animate를 끈다) 속도가 변해도 무늬가 튀지 않는다.
    /// 봉인 폭풍이 없는 씬에서는 아무 일도 하지 않는다.
    /// </summary>
    public sealed class VfxStormSurge : MonoBehaviour
    {
        [Tooltip("비우면 씬에서 TempleSandstorm을 찾는다")] public TempleSandstorm storm;
        [Min(1f), Tooltip("정점에서 흐름 속도 배수")] public float speedBoost = 5f;
        [Range(0f, 1f), Tooltip("정점에서 _Swirl 값(원래 값보다 작으면 원래 값 유지)")] public float swirlBoost = 0.8f;
        [Min(1f), Tooltip("정점에서 밝은 모래색 배수")] public float brighten = 1.7f;
        [Min(0.01f)] public float attack = 0.25f;
        [Min(0f)] public float hold = 0.6f;
        [Min(0.01f)] public float release = 1.6f;
        public bool IsSurging => t >= 0f;
        public float Level { get; private set; }
        static readonly int SwirlId = Shader.PropertyToID("_Swirl"), LightColorId = Shader.PropertyToID("_LightColor");
        float t = -1f, clock, baseSpeed;
        bool searched, drivingClock;
        MaterialPropertyBlock block;
        Color[] baseLight; float[] baseSwirl;

        public void Trigger()
        {
            if (storm == null && !searched) { storm = FindFirstObjectByType<TempleSandstorm>(); searched = true; }
            if (storm == null) return;
            if (!drivingClock)
            {
                // 지금 무늬 시점에서 이어받아 직접 굴린다. 되돌릴 때도 우리가 계속 굴리므로 튐이 없다.
                baseSpeed = storm.animationSpeed;
                clock = storm.animate ? Time.realtimeSinceStartup * baseSpeed : storm.previewTime;
                storm.animate = false;
                drivingClock = true;
                CacheBase();
            }
            t = 0f;
        }

        void CacheBase()
        {
            var layers = storm.sandLayers;
            int n = layers != null ? layers.Length : 0;
            baseLight = new Color[n]; baseSwirl = new float[n];
            for (int i = 0; i < n; i++)
            {
                var m = layers[i] != null ? layers[i].sharedMaterial : null;
                baseLight[i] = m != null && m.HasProperty(LightColorId) ? m.GetColor(LightColorId) : Color.white;
                baseSwirl[i] = m != null && m.HasProperty(SwirlId) ? m.GetFloat(SwirlId) : 0f;
            }
        }

        void Update()
        {
            if (!drivingClock || storm == null) return;
            float dt = Time.deltaTime;
            if (t >= 0f)
            {
                t += dt;
                float total = attack + hold + release;
                Level = t < attack ? t / attack : t < attack + hold ? 1f : Mathf.Clamp01(1f - (t - attack - hold) / release);
                if (t >= total) { t = -1f; Level = 0f; }
            }
            clock += dt * baseSpeed * Mathf.Lerp(1f, speedBoost, Level);
            storm.previewTime = clock;
            Apply(Level);
        }

        void Apply(float level)
        {
            var layers = storm.sandLayers;
            if (layers == null || baseLight == null || baseLight.Length != layers.Length) return;
            block ??= new MaterialPropertyBlock();
            for (int i = 0; i < layers.Length; i++)
            {
                var r = layers[i];
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetFloat(SwirlId, Mathf.Lerp(baseSwirl[i], Mathf.Max(baseSwirl[i], swirlBoost), level));
                block.SetColor(LightColorId, baseLight[i] * Mathf.Lerp(1f, brighten, level));
                r.SetPropertyBlock(block);
            }
        }

        void OnDisable()
        {
            if (!drivingClock || storm == null) return;
            t = -1f; Level = 0f; Apply(0f);
            storm.animate = true; storm.animationSpeed = baseSpeed; drivingClock = false;
        }
    }
}

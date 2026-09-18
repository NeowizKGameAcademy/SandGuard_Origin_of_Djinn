using UnityEngine;

namespace DesertTower.VFX
{
    /// <summary>
    /// 흔적 귀환 표식 제어기. 게임플레이(PlayerRecall)가 남은 시간 비율을 <see cref="SetRemaining"/>으로 넘기면 바닥 룬이 서서히 어두워지고,
    /// <see cref="Release"/>(귀환·만료)에 파티클 방출을 멈추고 룬을 <see cref="releaseTime"/> 동안 번지며 지운다. 색은 MaterialPropertyBlock의 _BaseColor로만 바꾼다.
    /// </summary>
    public sealed class VfxRecallMark : MonoBehaviour
    {
        [Tooltip("남은 시간에 따라 어두워지고 풀릴 때 사라지는 렌더러(바닥 룬)")] public Renderer[] fadeRenderers;
        [Tooltip("풀릴 때 방출을 멈출 파티클. 비우면 자식 전부")] public ParticleSystem[] loops;
        [Min(0.05f)] public float releaseTime = 0.4f;
        [Tooltip("남은 비율(가로 1→0)에 따른 밝기 배수")]
        public AnimationCurve remainingToBrightness = new AnimationCurve(new Keyframe(0f, 0.3f, 0f, 1f), new Keyframe(0.25f, 0.55f, 1.2f, 1.2f), new Keyframe(1f, 1f, 0.4f, 0f));
        public float Remaining { get; private set; } = 1f;
        public bool Released { get; private set; }
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock block;
        Color[] baseColors; Vector3[] baseScales;
        float releaseT;

        void Awake()
        {
            Cache();
            if (loops == null || loops.Length == 0) loops = GetComponentsInChildren<ParticleSystem>();
        }

        void Cache()
        {
            if (fadeRenderers == null) { baseColors = null; baseScales = null; return; }
            baseColors = new Color[fadeRenderers.Length]; baseScales = new Vector3[fadeRenderers.Length];
            for (int i = 0; i < fadeRenderers.Length; i++)
            {
                var r = fadeRenderers[i];
                if (r == null) continue;
                var m = r.sharedMaterial;
                baseColors[i] = m != null && m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : Color.white;
                baseScales[i] = r.transform.localScale;
            }
        }

        /// <summary>남은 시간 비율(1 = 방금 생성, 0 = 만료).</summary>
        public void SetRemaining(float remaining01)
        {
            Remaining = Mathf.Clamp01(remaining01);
            if (!Released) Apply(remainingToBrightness.Evaluate(Remaining), 1f);
        }

        /// <summary>귀환했거나 만료됐다: 방출을 멈추고 룬을 번지며 지운다.</summary>
        public void Release()
        {
            if (Released) return;
            Released = true; releaseT = 0f;
            if (loops != null) foreach (var ps in loops) if (ps != null) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        void Update()
        {
            if (!Released) return;
            releaseT += Time.deltaTime;
            float u = Mathf.Clamp01(releaseT / releaseTime);
            Apply(remainingToBrightness.Evaluate(Remaining) * (1f - u), 1f + 0.35f * u);
            if (u >= 1f && fadeRenderers != null) foreach (var r in fadeRenderers) if (r != null) r.enabled = false;
        }

        void Apply(float brightness, float scale)
        {
            if (fadeRenderers == null) return;
            if (baseColors == null || baseColors.Length != fadeRenderers.Length) Cache();
            block ??= new MaterialPropertyBlock();
            for (int i = 0; i < fadeRenderers.Length; i++)
            {
                var r = fadeRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(block);
                var c = baseColors[i];
                block.SetColor(BaseColorId, new Color(c.r * brightness, c.g * brightness, c.b * brightness, c.a));
                r.SetPropertyBlock(block);
                r.transform.localScale = baseScales[i] * scale;
            }
        }
    }
}

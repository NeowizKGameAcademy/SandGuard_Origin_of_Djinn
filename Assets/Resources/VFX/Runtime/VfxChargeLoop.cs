using UnityEngine;

namespace DesertTower.VFX
{
    /// <summary>
    /// 충전형 루프 이펙트 제어기. Begin()으로 켜고 SetIntensity(0~1)로 세기를 올리며 End()로 방출을 멈춘다(남은 입자는 스스로 사라진다).
    /// 세기는 방출량·크기·속도 배수로 반영된다. UnityEvent로 꽂아 쓴다: PlayerUpdraft.onChargeStarted → Begin, onCharging(float) → SetIntensity,
    /// onChargeCancelled/onLaunched → End. 프리팹의 파티클은 미리보기를 위해 Play On Awake여도 되며, 여기서 시작 시 멈춰 둔다.
    /// </summary>
    public sealed class VfxChargeLoop : MonoBehaviour
    {
        [Tooltip("비우면 자식의 모든 파티클 시스템")]
        public ParticleSystem[] Systems;
        [Min(0f), Tooltip("세기 0일 때 방출량 배수")] public float MinRate = 0.25f;
        [Min(0f), Tooltip("세기 1일 때 방출량 배수")] public float MaxRate = 1.6f;
        [Min(0f)] public float MinSpeed = 0.7f;
        [Min(0f)] public float MaxSpeed = 1.5f;
        [Min(0f)] public float MinSize = 0.8f;
        [Min(0f)] public float MaxSize = 1.25f;
        [Tooltip("세기 → 배수 곡선. 끝으로 갈수록 급격히")]
        public AnimationCurve Ramp = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0.6f), new Keyframe(1f, 1f, 2f, 0f));
        public bool IsPlaying { get; private set; }
        public float Intensity { get; private set; }
        float[] _baseRate, _baseSpeed, _baseSize;

        void Awake()
        {
            if (Systems == null || Systems.Length == 0) Systems = GetComponentsInChildren<ParticleSystem>(true);
            _baseRate = new float[Systems.Length]; _baseSpeed = new float[Systems.Length]; _baseSize = new float[Systems.Length];
            for (int i = 0; i < Systems.Length; i++)
            {
                var ps = Systems[i]; if (ps == null) continue;
                _baseRate[i] = ps.emission.rateOverTimeMultiplier;
                _baseSpeed[i] = ps.main.startSpeedMultiplier;
                _baseSize[i] = ps.main.startSizeMultiplier;
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        public void Begin()
        {
            IsPlaying = true;
            SetIntensity(0f);
            foreach (var ps in Systems) if (ps != null) { ps.Clear(false); ps.Play(false); }
        }

        public void End()
        {
            if (!IsPlaying) return;
            IsPlaying = false;
            foreach (var ps in Systems) if (ps != null) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        public void SetIntensity(float intensity)
        {
            Intensity = Mathf.Clamp01(intensity);
            float k = Ramp.Evaluate(Intensity);
            for (int i = 0; i < Systems.Length; i++)
            {
                var ps = Systems[i]; if (ps == null) continue;
                var emission = ps.emission; emission.rateOverTimeMultiplier = _baseRate[i] * Mathf.Lerp(MinRate, MaxRate, k);
                var main = ps.main;
                main.startSpeedMultiplier = _baseSpeed[i] * Mathf.Lerp(MinSpeed, MaxSpeed, k);
                main.startSizeMultiplier = _baseSize[i] * Mathf.Lerp(MinSize, MaxSize, k);
            }
        }

        void OnDisable() => End();
    }
}

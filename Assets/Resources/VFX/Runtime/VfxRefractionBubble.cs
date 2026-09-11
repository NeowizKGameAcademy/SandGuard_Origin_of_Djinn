using UnityEngine;

namespace DesertTower.VFX
{
    /// <summary>
    /// 몸 주변 볼록 렌즈. 구체 렌더러(ScreenRefraction 셰이더)의 반지름과 굴절 세기를 충전량으로 키우고, 발사 순간 크게 부풀며 사라지는 펄스를 낸다.
    /// UnityEvent로 꽂아 쓴다: onChargeStarted → Begin, onCharging(float) → SetIntensity, onChargeCancelled → End, onLaunched → Pulse.
    /// 대기 중에는 렌더러를 꺼 두므로 비용이 없다.
    /// </summary>
    public sealed class VfxRefractionBubble : MonoBehaviour
    {
        [Tooltip("비우면 이 오브젝트의 렌더러")]
        public Renderer Target;
        [Header("충전")]
        [Min(0f)] public float MinRadius = 0.45f;
        [Min(0f), Tooltip("3인칭 카메라(3.5m)에서 화면 절반을 덮지 않도록 1m 안팎으로 둔다")] public float MaxRadius = 0.95f;
        [Min(0f), Tooltip("가득 찼을 때 굴절 세기(셰이더 _Strength)")] public float MaxStrength = 0.09f;
        [Tooltip("세기 → 배수 곡선. 끝으로 갈수록 급격히")]
        public AnimationCurve Ramp = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0.6f), new Keyframe(1f, 1f, 2f, 0f));
        [Min(0.01f), Tooltip("충전 중 반지름·세기가 따라붙는 시간")] public float FollowTime = 0.08f;
        [Header("발사 펄스")]
        [Min(0f)] public float PulseRadius = 2.2f;
        [Min(0f)] public float PulseStrength = 0.14f;
        [Min(0.01f)] public float PulseDuration = 0.35f;
        [Header("복귀")]
        [Min(0.01f), Tooltip("취소 뒤 사라지는 시간")] public float ReleaseTime = 0.15f;

        public bool IsActive { get; private set; }
        public bool IsPulsing => pulseRemaining > 0f;
        public float Intensity { get; private set; }
        public float CurrentRadius { get; private set; }
        public float CurrentStrength { get; private set; }
        static readonly int StrengthId = Shader.PropertyToID("_Strength");
        MaterialPropertyBlock block;
        float pulseRemaining, radiusVelocity, strengthVelocity;

        void Awake()
        {
            if (Target == null) Target = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
            Apply(0f, 0f);
            if (Target != null) Target.enabled = false;
        }

        public void Begin() { IsActive = true; Intensity = 0f; pulseRemaining = 0f; if (Target != null) Target.enabled = true; }
        public void SetIntensity(float intensity) => Intensity = Mathf.Clamp01(intensity);
        public void End() { IsActive = false; Intensity = 0f; }
        /// <summary>발사: 지금 크기에서 PulseRadius까지 부풀며 세기가 0으로 빠진다.</summary>
        public void Pulse()
        {
            IsActive = false; Intensity = 0f;
            pulseRemaining = PulseDuration;
            if (Target != null) Target.enabled = true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float targetRadius, targetStrength;
            if (pulseRemaining > 0f)
            {
                pulseRemaining = Mathf.Max(0f, pulseRemaining - dt);
                float t = 1f - pulseRemaining / PulseDuration;       // 0 → 1
                float ease = 1f - (1f - t) * (1f - t);                 // 빠르게 부풀고 느려진다
                Apply(Mathf.Lerp(CurrentRadius, PulseRadius, ease), PulseStrength * (1f - t));
                if (pulseRemaining <= 0f) Hide();
                return;
            }
            if (IsActive)
            {
                float k = Ramp.Evaluate(Intensity);
                targetRadius = Mathf.Lerp(MinRadius, MaxRadius, k); targetStrength = MaxStrength * k;
                Apply(Mathf.SmoothDamp(CurrentRadius, targetRadius, ref radiusVelocity, FollowTime, Mathf.Infinity, dt),
                      Mathf.SmoothDamp(CurrentStrength, targetStrength, ref strengthVelocity, FollowTime, Mathf.Infinity, dt));
                return;
            }
            if (Target == null || !Target.enabled) return;
            Apply(CurrentRadius, Mathf.MoveTowards(CurrentStrength, 0f, dt * Mathf.Max(MaxStrength, PulseStrength) / ReleaseTime));
            if (CurrentStrength <= 0.0001f) Hide();
        }

        void Apply(float radius, float strength)
        {
            CurrentRadius = radius; CurrentStrength = strength;
            transform.localScale = Vector3.one * Mathf.Max(0.01f, radius * 2f); // 구체 메시 지름 1
            if (Target == null) return;
            Target.GetPropertyBlock(block);
            block.SetFloat(StrengthId, strength);
            Target.SetPropertyBlock(block);
        }

        void Hide()
        {
            Apply(MinRadius, 0f); radiusVelocity = strengthVelocity = 0f;
            if (Target != null) Target.enabled = false;
        }

        void OnDisable() { pulseRemaining = 0f; IsActive = false; Hide(); }
    }
}

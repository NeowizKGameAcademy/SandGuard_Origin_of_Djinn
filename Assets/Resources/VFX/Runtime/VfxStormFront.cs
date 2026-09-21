using UnityEngine;

namespace DesertTower.VFX
{
    /// <summary>
    /// 바깥으로 퍼지는 충격 링 연출의 제어기. 게임플레이(사막 폭풍)가 매 프레임 <see cref="SetFront"/>로 링의 바깥 반경·두께·최대 반경을 넘긴다.
    /// <see cref="ringScaled"/>는 반경 1m 기준으로 만든 메시(벽)라 XZ를 링 반경으로 스케일하고,
    /// <see cref="frontEmitters"/>는 Circle/Donut 모양 파티클이라 shape.radius를 링 반경(두께의 중앙)으로 옮긴다.
    /// 반경이 커질수록 둘레가 길어지므로 방출량을 둘레 비율(<see cref="rateScalesWithRadius"/>)로 올린다.
    /// 외부 호출이 없으면(미리보기·쇼케이스) <see cref="selfExpandSpeed"/>로 스스로 퍼진다.
    /// </summary>
    public class VfxStormFront : MonoBehaviour
    {
        [Tooltip("반경 1m 기준 벽/고리. XZ 스케일 = 링 반경")] public Transform[] ringScaled;
        [Tooltip("링을 따라가는 파티클. shape.radius = 링 중앙 반경")] public ParticleSystem[] frontEmitters;
        [Tooltip("반경에 비례해 frontEmitters의 초당 방출량을 올린다(반경 1m일 때의 값 × 반경)")] public bool rateScalesWithRadius = true;
        [Min(0f), Tooltip("frontEmitters의 방출량 상한(초당)")] public float maxRate = 600f;
        [Tooltip("_RingTiles·_SwirlScale을 받을 벽 렌더러. 비우면 ringScaled 아래에서 찾는다")] public Renderer[] ringRenderers;
        [Min(0f), Tooltip("둘레 무늬 하나의 목표 폭(m). 링이 커져도 이 폭이 유지되도록 셰이더 _RingTiles를 올린다. 0이면 셰이더를 건드리지 않는다")] public float patternWidth = 24f;
        [Min(1f), Tooltip("둘레 무늬 반복 수의 하한. 반경이 작을 때 무늬가 한두 개로 줄지 않게 한다")] public float minTiles = 8f;
        [Min(0f), Tooltip("링을 따라 도는 파티클의 접선 속도(m/s). 반경으로 나눠 궤도 각속도(rad/s)로 넣는다. 0이면 넣지 않는다")] public float orbitSpeed = 0f;
        [Tooltip("벽 무늬의 시간을 직접 굴려 회전 속도를 반경에 맞춘다. 비우면 ringScaled 아래에서 찾는다")] public TempleSandstorm wallFlow;
        [Range(0f, 1f), Tooltip("0이면 접선 속도 유지(반경이 커질수록 한 바퀴가 느려진다), 1이면 각속도 유지(한 바퀴 도는 시간이 반경과 무관하게 일정). 링이 멀어질 때 회전이 멎어 보이면 올린다")] public float spinKeepsAngularSpeed = 0.85f;
        [Range(0f, 1f), Tooltip("링이 최대 반경의 이 비율을 넘으면 벽과 먼지가 잦아들기 시작한다. 1이면 페이드 없음")] public float fadeStart = 0.8f;
        [Range(0f, 1f), Tooltip("최대 반경에서 남는 진하기. 0이면 완전히 흩어져 사라진다")] public float fadeEnd = 0f;
        [Min(0f), Tooltip("0보다 크면 외부 호출 없이 스스로 이 속도(m/s)로 퍼진다(미리보기용). 게임플레이가 SetFront를 부르면 꺼진다")] public float selfExpandSpeed = 0f;
        [Min(0.1f)] public float selfMaxRadius = 12f;
        [Min(0.1f)] public float selfThickness = 4f;
        public float Front { get; private set; }
        public float Thickness { get; private set; }
        public float MaxRadius { get; private set; }
        /// <summary>링 반경(0~1). 연출이 끝머리를 구분할 때 쓴다.</summary>
        public float Progress => MaxRadius > 0f ? Mathf.Clamp01(Front / MaxRadius) : 0f;
        float[] baseRates;
        float selfTime;
        bool drivenExternally;
        MaterialPropertyBlock wallProperties;
        static readonly int RingTilesId = Shader.PropertyToID("_RingTiles"), SwirlScaleId = Shader.PropertyToID("_SwirlScale");
        const float ShaderBaseTiles = 10f;   // 셰이더 _RingTiles 기본값. 이 값일 때가 스크롤 속도의 기준이다
        static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        float spinFactor = 1f, wallClock, wallBaseSpeed;
        bool wallClockRunning;
        float[] baseOpacity;

        /// <summary>끝머리에서 잦아드는 정도(1 = 그대로, 0 = 완전히 흩어짐). 링이 최대 반경에 닿으며 멈춰 보이지 않게 한다.</summary>
        public float Fade => fadeStart >= 1f ? 1f : Mathf.SmoothStep(1f, fadeEnd, Mathf.InverseLerp(fadeStart, 1f, Progress));

        void Awake() { CacheRates(); CacheWall(); }

        void CacheWall()
        {
            if (wallFlow == null && ringScaled != null)
                foreach (var t in ringScaled)
                    if (t != null && (wallFlow = t.GetComponentInChildren<TempleSandstorm>()) != null) break;
            if ((ringRenderers == null || ringRenderers.Length == 0) && ringScaled != null)
            {
                var found = new System.Collections.Generic.List<Renderer>();
                foreach (var t in ringScaled)
                    if (t != null) found.AddRange(t.GetComponentsInChildren<Renderer>(true));
                ringRenderers = found.ToArray();
            }
            ringRenderers ??= System.Array.Empty<Renderer>();
            // 끝머리 페이드는 머티리얼의 원래 _Opacity에 곱한다. 층마다 값이 다를 수 있으므로 각각 기억해 둔다.
            baseOpacity = new float[ringRenderers.Length];
            for (int i = 0; i < ringRenderers.Length; i++)
            {
                var material = ringRenderers[i] != null ? ringRenderers[i].sharedMaterial : null;
                baseOpacity[i] = material != null && material.HasProperty(OpacityId) ? material.GetFloat(OpacityId) : 1f;
            }
        }

        /// <summary>
        /// 벽 무늬의 시간을 직접 굴린다. <see cref="TempleSandstorm.animationSpeed"/>를 바꾸면 시간이 realtime에 곱해져 있어
        /// 속도를 바꾸는 순간 무늬가 튄다. 그래서 <see cref="VfxStormSurge"/>와 같은 방식으로 시계를 넘겨받아 누적한다.
        /// </summary>
        void DriveWallClock(float dt)
        {
            if (wallFlow == null || !Application.isPlaying) return;
            if (!wallClockRunning)
            {
                wallBaseSpeed = wallFlow.animationSpeed;
                wallClock = wallFlow.animate ? Time.realtimeSinceStartup * wallBaseSpeed : wallFlow.previewTime;
                wallFlow.animate = false;
                wallClockRunning = true;
            }
            wallClock += dt * wallBaseSpeed * spinFactor;
            wallFlow.previewTime = wallClock;
        }

        // 풀 재사용·씬 종료에 대비해 넘겨받은 시계를 돌려준다.
        void OnDisable()
        {
            if (!wallClockRunning || wallFlow == null) return;
            wallFlow.animate = true; wallFlow.animationSpeed = wallBaseSpeed;
            wallClockRunning = false;
        }

        void CacheRates()
        {
            if (frontEmitters == null) { baseRates = null; return; }
            baseRates = new float[frontEmitters.Length];
            for (int i = 0; i < frontEmitters.Length; i++)
                baseRates[i] = frontEmitters[i] != null ? frontEmitters[i].emission.rateOverTime.constant : 0f;
        }

        void Update() { Tick(Time.deltaTime); DriveWallClock(Time.deltaTime); }

        /// <summary>외부 호출이 없을 때 스스로 퍼진다. 미리보기 렌더러가 에디트 모드에서 직접 부른다.</summary>
        public void Tick(float dt)
        {
            if (drivenExternally || selfExpandSpeed <= 0f) return;
            selfTime += dt;
            Apply(Mathf.Min(selfMaxRadius, selfTime * selfExpandSpeed), selfThickness, selfMaxRadius);
        }

        /// <summary>게임플레이가 부른다. 이후 스스로 퍼지지 않는다.</summary>
        public virtual void SetFront(float front, float thickness, float maxRadius)
        {
            drivenExternally = Application.isPlaying;
            Apply(front, thickness, maxRadius);
        }

        void Apply(float front, float thickness, float maxRadius)
        {
            Front = Mathf.Max(0f, front); Thickness = Mathf.Max(0f, thickness); MaxRadius = maxRadius;
            float mid = Mathf.Max(0.01f, Front - Thickness * 0.5f);
            if (ringScaled != null)
                foreach (var t in ringScaled)
                    if (t != null) { var s = t.localScale; t.localScale = new Vector3(Mathf.Max(0.01f, Front), s.y, Mathf.Max(0.01f, Front)); }
            ApplyWallPattern(mid);
            if (frontEmitters == null) return;
            if (baseRates == null || baseRates.Length != frontEmitters.Length) CacheRates();
            for (int i = 0; i < frontEmitters.Length; i++)
            {
                var ps = frontEmitters[i];
                if (ps == null) continue;
                var shape = ps.shape;
                shape.radius = mid;
                if (shape.shapeType == ParticleSystemShapeType.Donut) shape.donutRadius = Mathf.Max(0.05f, Thickness * 0.5f);
                if (orbitSpeed > 0f)
                {
                    // 궤도는 각속도(rad/s)라 반경이 커지면 접선 속도가 같이 치솟는다. 접선 속도가 일정하도록 반경으로 나눈다.
                    // 세 축은 반드시 같은 MinMaxCurve 모드여야 한다("Orbital Velocity curves must all be in the same mode").
                    // float을 그대로 대입하면 그 축만 Constant가 되므로, 빌더가 쓰는 TwoConstants로 세 축을 함께 넣는다.
                    var velocity = ps.velocityOverLifetime;
                    velocity.enabled = true;
                    float radians = orbitSpeed / mid * spinFactor;   // 벽과 같은 회전감을 쓰도록 spinKeepsAngularSpeed를 함께 반영한다
                    velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
                    velocity.orbitalY = new ParticleSystem.MinMaxCurve(radians, radians);
                    velocity.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
                }
                if (!rateScalesWithRadius) continue;
                var emission = ps.emission;
                // 끝머리에서는 벽과 함께 방출도 잦아든다. 이미 떠 있는 먼지는 수명이 다할 때까지 흩어지며 남는다.
                emission.rateOverTime = Mathf.Min(maxRate, baseRates[i] * Mathf.Max(1f, mid)) * Fade;
            }
        }

        /// <summary>
        /// 벽 메시는 반지름 1로 구워 XZ만 늘리므로, 정규화 UV를 그대로 쓰면 무늬가 반경에 비례해 늘어나 회전이 멎어 보이고
        /// 소용돌이 변형(오브젝트 공간 진폭)은 반대로 부풀어 형체를 잃는다. 둘 다 반경으로 되돌린다.
        /// </summary>
        void ApplyWallPattern(float mid)
        {
            if (patternWidth <= 0f) return;
            if (ringRenderers == null) CacheWall();
            if (ringRenderers.Length == 0) return;
            float tiles = Mathf.Max(minTiles, 2f * Mathf.PI * mid / patternWidth);
            float swirlScale = 1f / Mathf.Max(0.01f, mid);
            // 무늬가 촘촘해진 만큼 시계를 빠르게 굴리면 한 바퀴 도는 시간(각속도)이 유지된다. 그대로 두면 무늬 하나가
            // 지나가는 시간만 일정하고 각속도는 1/반경으로 떨어져, 링이 멀어질수록 회전이 멎어 보인다.
            spinFactor = Mathf.Lerp(1f, tiles / ShaderBaseTiles, spinKeepsAngularSpeed);
            wallProperties ??= new MaterialPropertyBlock();
            float fade = Fade;
            if (baseOpacity == null || baseOpacity.Length != ringRenderers.Length) CacheWall();
            for (int i = 0; i < ringRenderers.Length; i++)
            {
                var r = ringRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(wallProperties);   // TempleSandstorm의 _StormTime, VfxStormSurge의 _Swirl을 보존한다
                wallProperties.SetFloat(RingTilesId, tiles);
                wallProperties.SetFloat(SwirlScaleId, swirlScale);
                wallProperties.SetFloat(OpacityId, baseOpacity[i] * fade);
                r.SetPropertyBlock(wallProperties);
            }
        }
    }
}

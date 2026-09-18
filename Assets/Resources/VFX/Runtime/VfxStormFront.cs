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

        void Awake() => CacheRates();

        void CacheRates()
        {
            if (frontEmitters == null) { baseRates = null; return; }
            baseRates = new float[frontEmitters.Length];
            for (int i = 0; i < frontEmitters.Length; i++)
                baseRates[i] = frontEmitters[i] != null ? frontEmitters[i].emission.rateOverTime.constant : 0f;
        }

        void Update() => Tick(Time.deltaTime);

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
            if (frontEmitters == null) return;
            if (baseRates == null || baseRates.Length != frontEmitters.Length) CacheRates();
            for (int i = 0; i < frontEmitters.Length; i++)
            {
                var ps = frontEmitters[i];
                if (ps == null) continue;
                var shape = ps.shape;
                shape.radius = mid;
                if (shape.shapeType == ParticleSystemShapeType.Donut) shape.donutRadius = Mathf.Max(0.05f, Thickness * 0.5f);
                if (!rateScalesWithRadius) continue;
                var emission = ps.emission;
                emission.rateOverTime = Mathf.Min(maxRate, baseRates[i] * Mathf.Max(1f, mid));
            }
        }
    }
}

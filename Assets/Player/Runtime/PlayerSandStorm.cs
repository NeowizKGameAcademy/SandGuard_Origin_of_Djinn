using System;
using System.Collections.Generic;
using DesertTower.VFX;
using UnityEngine;

namespace SandGuard.Player
{
    /// <summary>
    /// ⑯ 사막 폭풍: 신전 바깥을 도는 폭풍의 힘을 잠시 빌려, 코어(없으면 시전자)에서 시작한 모래 충격 링이 <see cref="expandSpeed"/>로 바깥으로 퍼진다.
    /// 링(두께 <see cref="ringThickness"/>)이 지나가는 적대 대상은 한 번씩 피해(원인 "player.storm")를 받고 바깥으로 밀리며(<see cref="IDisplaceable.Knockback"/>) 잠시 둔화된다(<see cref="ISlowable"/>).
    /// 반경 <see cref="PlayerSandZone.radius"/>까지 닿으면 끝난다. 명중은 hitSink(PlayerBasicAttack.ReportHit)로 보고해 마나 순환이 적용된다.
    /// 연출은 자식의 <see cref="VfxStormFront"/>가 매 프레임 링 반경을 받아 따라간다.
    /// </summary>
    public sealed class PlayerSandStorm : PlayerSandZone
    {
        [Min(0.1f), Tooltip("링이 바깥으로 퍼지는 속도(m/s)")] public float expandSpeed = 14f;
        [Min(0.1f), Tooltip("링 두께(m). 이 띠 안에 든 적이 맞는다")] public float ringThickness = 4f;
        [Min(0f), Tooltip("링이 지나갈 때 한 번 주는 피해")] public float damage = 15f;
        [Min(0f), Tooltip("바깥으로 미는 속도(m/s). 0이면 밀지 않는다")] public float knockback = 7f;
        [Range(0f, 1f), Tooltip("둔화 비율. 0.5면 속도 50%")] public float slowFactor = 0.5f;
        [Min(0f), Tooltip("둔화 지속(초)")] public float slowDuration = 3f;
        [Min(0f), Tooltip("링이 훑는 높이(시작점 위로, m)")] public float sweepHeight = 30f;
        [Min(0f), Tooltip("링이 훑는 깊이(시작점 아래로, m). 코어가 신전 꼭대기(약 54m)에 있으므로 마당 바닥까지 닿게 넉넉히")] public float sweepDepth = 70f;
        /// <summary>실제 피해를 준 명중을 받을 곳. 비워도 된다.</summary>
        public Action<PlayerHitInfo> hitSink;
        /// <summary>지금 링 바깥 가장자리의 반경(m).</summary>
        public float Front { get; private set; }
        /// <summary>링이 건드린 적대 개체 수(피해 여부와 무관).</summary>
        public int TotalStruck { get; private set; }
        /// <summary>실제 피해가 들어간 명중 수.</summary>
        public int TotalHits { get; private set; }
        /// <summary>(이번 프레임에 링이 새로 건드린 개체 수). 0인 프레임에는 오지 않는다.</summary>
        public event Action<int> Swept;
        readonly HashSet<object> struck = new HashSet<object>();
        readonly Collider[] buffer = new Collider[512];
        VfxStormFront vfx;
        bool vfxSearched;

        protected override void Tick(float dt)
        {
            Front = Mathf.Min(radius, Elapsed * expandSpeed);
            float back = Front - ringThickness;
            if (!vfxSearched) { vfx = GetComponentInChildren<VfxStormFront>(); vfxSearched = true; }
            if (vfx != null) vfx.SetFront(Front, ringThickness, radius);
            if (Front <= 0f) return;
            Vector3 center = transform.position;
            // 높이와 무관하게 수평 거리로만 판정한다: 시작점 아래 sweepDepth부터 위 sweepHeight까지의 세로 캡슐.
            int count = Physics.OverlapCapsuleNonAlloc(center + Vector3.down * sweepDepth, center + Vector3.up * sweepHeight, Front, buffer, mask, QueryTriggerInteraction.Ignore);
            if (count == buffer.Length) Debug.LogWarning("사막 폭풍이 훑는 콜라이더가 버퍼(512)를 넘었습니다. skillMask를 좁히세요.", this);
            int swept = 0;
            for (int i = 0; i < count; i++)
            {
                var collider = buffer[i];
                if (!Hostile(collider, out ICombatTarget target)) continue;
                IDamageable receiver = target != null ? target.DamageReceiver : collider.GetComponentInParent<IDamageable>();
                var displaceable = collider.GetComponentInParent<IDisplaceable>();
                var slowable = collider.GetComponentInParent<ISlowable>();
                if (target == null && receiver == null && displaceable == null && slowable == null) continue; // 지형·장식: 할 일이 없다
                Vector3 point = ClosestPointSafe(collider, center);
                Vector3 flat = point - center; flat.y = 0f;
                float distance = flat.magnitude;
                if (distance > Front || distance < back) continue;
                object key = (object)target ?? (object)receiver ?? (object)displaceable ?? (object)slowable ?? collider;
                if (!struck.Add(key)) continue;
                swept++;
                Vector3 outward = distance > 0.0001f ? flat / distance : transform.forward;
                if (knockback > 0f && displaceable != null) displaceable.Knockback(outward * knockback);
                if (slowFactor > 0f && slowDuration > 0f && slowable != null) slowable.Slow(slowFactor, slowDuration);
                if (receiver == null || damage <= 0f) continue;
                var result = receiver.TakeDamage(new DamageInfo(damage, faction, causeId: "player.storm", hitPosition: point, hitDirection: outward));
                if (!result.WasApplied || result.AppliedDamage <= 0f) continue;
                TotalHits++;
                hitSink?.Invoke(new PlayerHitInfo(target, receiver, result.AppliedDamage, result.WasKilled, point, outward, "player.storm"));
            }
            if (swept > 0) { TotalStruck += swept; Swept?.Invoke(swept); }
        }

        protected override void OnEnd() { if (vfx != null) vfx.SetFront(radius, ringThickness, radius); }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SandGuard.Player
{
    /// <summary>
    /// ⑯ 사막 폭풍: 지속 시간 동안 tickInterval마다 반경 안 적대 대상에 피해(원인 "player.storm")를 주고 둔화(<see cref="ISlowable"/>)를 건다.
    /// 첫 틱은 생기자마자다. 둔화는 다음 틱까지만 유지되므로 폭풍을 벗어나면 곧 풀린다. 명중은 hitSink(PlayerBasicAttack.ReportHit)로 보고해 마나 순환이 적용된다.
    /// </summary>
    public sealed class PlayerSandStorm : PlayerSandZone
    {
        [Min(0.05f)] public float tickInterval = 0.5f;
        [Min(0f), Tooltip("틱당 피해")] public float tickDamage = 2.5f;
        [Range(0f, 1f), Tooltip("둔화 비율. 0.6이면 속도 40%")] public float slowFactor = 0.6f;
        /// <summary>실제 피해를 준 명중을 받을 곳. 비워도 된다.</summary>
        public Action<PlayerHitInfo> hitSink;
        public int Ticks { get; private set; }
        public int TotalHits { get; private set; }
        /// <summary>(이번 틱에 피해를 준 적 수).</summary>
        public event Action<int> Ticked;
        float nextTick;

        protected override void Tick(float dt)
        {
            if (Elapsed < nextTick) return;
            nextTick += tickInterval;
            int hits = 0;
            var damaged = new HashSet<IDamageable>();
            var slowed = new HashSet<ISlowable>();
            Vector3 center = transform.position;
            foreach (var collider in Overlap())
            {
                if (!Hostile(collider, out ICombatTarget target)) continue;
                var slowable = collider.GetComponentInParent<ISlowable>();
                if (slowable != null && slowed.Add(slowable)) slowable.Slow(slowFactor, tickInterval * 2f + 0.05f);
                IDamageable receiver = target != null ? target.DamageReceiver : collider.GetComponentInParent<IDamageable>();
                if (receiver == null || !damaged.Add(receiver)) continue;
                Vector3 point = collider.ClosestPoint(center);
                Vector3 direction = point - center; direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
                var result = receiver.TakeDamage(new DamageInfo(tickDamage, faction, causeId: "player.storm", hitPosition: point, hitDirection: direction));
                if (!result.WasApplied || result.AppliedDamage <= 0f) continue;
                hits++;
                hitSink?.Invoke(new PlayerHitInfo(target, receiver, result.AppliedDamage, result.WasKilled, point, direction, "player.storm"));
            }
            Ticks++; TotalHits += hits;
            Ticked?.Invoke(hits);
        }
    }
}

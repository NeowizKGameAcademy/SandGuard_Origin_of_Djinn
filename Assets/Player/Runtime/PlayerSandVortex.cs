using System.Collections.Generic;
using UnityEngine;

namespace SandGuard.Player
{
    /// <summary>
    /// ⑮ 모래 소용돌이: 반경 안의 적(<see cref="IDisplaceable"/>)을 매 프레임 중심으로 끌어당기고, 끝나는 순간 모인 적을 위로 쳐올린다(<see cref="IDisplaceable.Launch"/>).
    /// 반경 안 적에게 주기적으로 피해를 준다. 중심 근처(innerRadius)에 오면 더 당기지 않아 겹치지 않는다. 끄는 동안 높이는 건드리지 않는다.
    /// 예전 방식(끝날 때 속박)은 <see cref="endShackle"/>을 0보다 크게 두면 같이 건다.
    /// </summary>
    public sealed class PlayerSandVortex : PlayerSandZone
    {
        [Min(0f)] public float damage = 2f;
        [Min(0.01f)] public float damageInterval = 0.5f;
        public System.Action<PlayerHitInfo> hitSink;
        readonly HashSet<IDamageable> damaged = new HashSet<IDamageable>();
        int damageTicks;
        [Min(0f), Tooltip("초당 끌어당기는 거리(m)")] public float pullSpeed = 4f;
        [Min(0f), Tooltip("이 반경 안에 오면 더 당기지 않는다")] public float innerRadius = 0.35f;
        [Min(0f), Tooltip("끝날 때 반경 안 적을 위로 쳐올리는 속도(m/s). 0이면 띄우지 않는다")] public float endLaunch = 4.5f;
        [Min(0f), Tooltip("끝날 때 반경 안 적을 묶는 시간(예전 방식). 0이면 묶지 않는다")] public float endShackle = 0f;
        readonly HashSet<IDisplaceable> pulled = new HashSet<IDisplaceable>();
        /// <summary>마지막 프레임에 끌어당긴 적 수.</summary>
        public int PulledCount { get; private set; }
        /// <summary>끝날 때 실제로 띄운 적 수.</summary>
        public int LaunchedCount { get; private set; }
        /// <summary>끝날 때 묶은 적 수.</summary>
        public int ShackledCount { get; private set; }

        protected override void Tick(float dt)
        {
            pulled.Clear();
            Vector3 center = transform.position;
            foreach (var collider in Overlap())
            {
                if (!Hostile(collider, out _)) continue;
                var target = collider.GetComponentInParent<IDisplaceable>();
                if (target == null || !pulled.Add(target)) continue;
                Vector3 toCenter = center - ((Component)target).transform.position; toCenter.y = 0f;
                float distance = toCenter.magnitude;
                if (distance <= innerRadius) continue;
                float step = Mathf.Min(pullSpeed * dt, distance - innerRadius);
                target.Displace(toCenter / distance * step);
            }
            PulledCount = pulled.Count;
            // 첫 피해는 한 주기 뒤에 발생하며, 마지막 프레임은 지속 시간까지만 센다.
            float time = Mathf.Min(Elapsed + dt, duration);
            float interval = Mathf.Max(0.01f, damageInterval);
            while ((damageTicks + 1) * interval <= time)
            {
                damageTicks++;
                DamageTargets();
            }
        }

        void DamageTargets()
        {
            if (damage <= 0f) return;
            damaged.Clear();
            Vector3 center = transform.position;
            foreach (var collider in Overlap())
            {
                if (!Hostile(collider, out ICombatTarget target)) continue;
                IDamageable receiver = target != null ? target.DamageReceiver : collider.GetComponentInParent<IDamageable>();
                if (receiver == null || !damaged.Add(receiver)) continue;
                Vector3 point = ClosestPointSafe(collider, center);
                Vector3 direction = (center - point).normalized;
                var result = receiver.TakeDamage(new DamageInfo(damage, faction, causeId: "player.vortex", hitPosition: point, hitDirection: direction));
                if (result.WasApplied && result.AppliedDamage > 0f)
                    hitSink?.Invoke(new PlayerHitInfo(target, receiver, result.AppliedDamage, result.WasKilled, point, direction, "player.vortex"));
            }
        }

        protected override void OnEnd()
        {
            if (endLaunch <= 0f && endShackle <= 0f) return;
            var launched = new HashSet<IDisplaceable>();
            var shackled = new HashSet<IRestrainable>();
            foreach (var collider in Overlap())
            {
                if (!Hostile(collider, out _)) continue;
                if (endLaunch > 0f)
                {
                    var target = collider.GetComponentInParent<IDisplaceable>();
                    if (target != null && launched.Add(target) && target.Launch(Vector3.up * endLaunch)) LaunchedCount++;
                }
                if (endShackle > 0f)
                {
                    var target = collider.GetComponentInParent<IRestrainable>();
                    if (target != null && shackled.Add(target)) target.Restrain(endShackle);
                }
            }
            ShackledCount = shackled.Count;
        }

        // 풀 재사용: 지난 시전의 집계를 지운다.
        protected override void OnReset()
        {
            pulled.Clear();
            damaged.Clear(); damageTicks = 0; hitSink = null;
            PulledCount = 0; LaunchedCount = 0; ShackledCount = 0;
        }
    }
}

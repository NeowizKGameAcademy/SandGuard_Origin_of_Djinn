using System.Collections.Generic;
using UnityEngine;

namespace SandGuard.Player
{
    /// <summary>
    /// ⑮ 모래 소용돌이: 반경 안의 적(<see cref="IDisplaceable"/>)을 매 프레임 중심으로 끌어당기고, 끝날 때 모인 적을 잠시 묶는다(<see cref="IRestrainable"/>).
    /// 중심 근처(innerRadius)에 오면 더 당기지 않아 겹치지 않는다. 높이는 건드리지 않는다.
    /// </summary>
    public sealed class PlayerSandVortex : PlayerSandZone
    {
        [Min(0f), Tooltip("초당 끌어당기는 거리(m)")] public float pullSpeed = 3f;
        [Min(0f), Tooltip("이 반경 안에 오면 더 당기지 않는다")] public float innerRadius = 0.35f;
        [Min(0f), Tooltip("끝날 때 반경 안 적을 묶는 시간. 0이면 묶지 않는다")] public float endShackle = 0.5f;
        readonly HashSet<IDisplaceable> pulled = new HashSet<IDisplaceable>();
        /// <summary>마지막 프레임에 끌어당긴 적 수.</summary>
        public int PulledCount { get; private set; }
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
        }

        protected override void OnEnd()
        {
            if (endShackle <= 0f) return;
            var seen = new HashSet<IRestrainable>();
            foreach (var collider in Overlap())
            {
                if (!Hostile(collider, out _)) continue;
                var target = collider.GetComponentInParent<IRestrainable>();
                if (target == null || !seen.Add(target)) continue;
                target.Restrain(endShackle);
            }
            ShackledCount = seen.Count;
        }
    }
}

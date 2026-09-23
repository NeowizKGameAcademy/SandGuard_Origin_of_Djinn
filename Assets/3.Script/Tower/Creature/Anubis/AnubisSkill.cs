using System;
using System.Collections.Generic;
using SandGuard.Enemy;
using UnityEngine;

namespace Tower
{
    public sealed class AnubisSkill : MonoBehaviour
    {
        [Min(0f)] public float range = 10f;
        [Range(0f, 360f)] public float angle = 360f;
        [Min(0f)] public float damage = 150f;
        [Min(0f)] public float stunDuration = 1.5f;
        [Min(0f)] public float horizontalPower = 5f;
        public LayerMask targetMask = ~0;
        private Collider[] buffer = new Collider[64];
        private readonly HashSet<Guid> affected = new HashSet<Guid>();

        public void Cast(Vector3 origin, Vector3 forward, float maxHeight = 1.5f)
        {
            affected.Clear();
            int count;
            while (true)
            {
                count = Physics.OverlapSphereNonAlloc(origin, range, buffer, targetMask, QueryTriggerInteraction.Ignore);
                if (count < buffer.Length) break;
                Array.Resize(ref buffer, buffer.Length * 2);
            }
            var health = GetComponent<AnubisHealth>();
            for (int i = 0; i < count; i++)
            {
                var target = buffer[i].GetComponentInParent<ICombatTarget>();
                if (!MinionAttackSequence.HeightReachable(transform, target, maxHeight)
                    || target.FactionId == "Ally" || target.DamageReceiver == null
                    || !affected.Add(target.EntityId)) continue;
                Vector3 direction = target.HitPosition - origin;
                direction.y = 0f;
                if (angle < 360f && direction.sqrMagnitude > 0.001f
                    && Vector3.Angle(forward, direction) > angle * 0.5f) continue;
                var result = target.DamageReceiver.TakeDamage(new DamageInfo(damage, "Ally", null,
                    "tower.anubis.shockwave", target.HitPosition, direction.normalized));
                if (result.WasKilled) { health?.RewardKill(); continue; }
                if (!result.WasApplied || !target.IsTargetable) continue;
                if (target is Component component)
                {
                    component.GetComponentInParent<EnemyBrain>()?.Stun(stunDuration);
                    component.GetComponentInParent<IDisplaceable>()?.Knockback(direction.normalized * horizontalPower);
                }
            }
            Array.Clear(buffer, 0, count);
        }
    }
}

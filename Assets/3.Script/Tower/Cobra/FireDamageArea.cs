using System;
using UnityEngine;

namespace Tower
{
    [DisallowMultipleComponent]
    public class FireDamageArea : TowerAreaEffect
    {
        protected override bool HasConfig => Status.cobra != null;
        protected override float TickInterval => Status.cobra.tickInterval;

        protected override void ApplyEffect(ICombatTarget target, Collider other, float interval)
        {
            float damage = Status.cobra.tickDamage;

            if (target.DamageReceiver == null || float.IsNaN(damage) || float.IsInfinity(damage) || damage < 0f)
                return;

            Vector3 point = target.HitPosition;
            Vector3 direction = point - Origin;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;

            target.DamageReceiver.TakeDamage(new DamageInfo(damage, Faction,
                Owner != null ? Owner.EntityId : (Guid?)null, "tower.fire", point, direction));
        }
    }
}

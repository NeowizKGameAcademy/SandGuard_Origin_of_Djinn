using System;
using System.Collections.Generic;
using DesertTower.VFX;
using UnityEngine;

namespace SandGuard.Enemy
{
    /// <summary>투척 뒤에는 시전자와 독립적으로 비행하고, 도화선이 끝나면 한 번 폭발한다.</summary>
    [RequireComponent(typeof(ChiefBombProp))]
    public sealed class ChiefBombProjectile : MonoBehaviour
    {
        ChiefBombProp prop;
        string faction;
        Guid source;
        float remaining, damage, radius;
        float towerDisableDuration;
        LayerMask mask;
        bool armed, detonated;
        public bool IsArmed => armed;
        public void Launch(Vector3 velocity, EnemyHealth owner, float fuse, float amount, float blastRadius, LayerMask hitMask, float disableDuration = 5f)
        {
            prop = GetComponent<ChiefBombProp>();
            faction = owner.FactionId; source = owner.EntityId;
            remaining = Mathf.Max(.05f, fuse); damage = amount; radius = blastRadius; mask = hitMask;
            towerDisableDuration = float.IsNaN(disableDuration) || float.IsInfinity(disableDuration) ? 0f : Mathf.Max(0f, disableDuration);
            armed = true; detonated = false;
            prop.Release(velocity);
            if (prop.hitCollider)
                foreach (var collider in owner.GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(prop.hitCollider, collider);
        }
        void Update()
        {
            if (!armed || detonated) return;
            remaining -= Time.deltaTime;
            if (remaining <= 0) Detonate();
        }
        public void Detonate()
        {
            if (!armed || detonated) return;
            detonated = true; armed = false;
            Vector3 center = transform.position;
            var seen = new HashSet<IDamageable>();
            foreach (var collider in Physics.OverlapSphere(center, radius, mask, QueryTriggerInteraction.Ignore))
            {
                var target = collider.GetComponentInParent<ICombatTarget>();
                var receiver = collider.GetComponentInParent<IDamageable>();
                if (target != null) receiver = target.IsTargetable && target.FactionId != faction ? target.DamageReceiver : null;
                if (receiver == null || !seen.Add(receiver)) continue;
                Vector3 point = collider.ClosestPoint(center);
                Vector3 direction = point - center;
                if (direction.sqrMagnitude < .0001f) direction = Vector3.up;
                var cause = new DamageInfo(damage, faction, source, "chief.bomb", point, direction.normalized);
                var result = receiver.TakeDamage(cause);
                if (towerDisableDuration > 0f && result.WasApplied && target != null &&
                    target.Kind == CombatTargetKind.Tower && target.IsTargetable)
                    CombatEffectSignals.RequestTowerDisable(new TowerDisableRequest(target.EntityId, towerDisableDuration, cause));
            }
            if (prop.explosionPrefab)
                PrefabPool.Release(PrefabPool.Spawn(prop.explosionPrefab, center, Quaternion.identity), 4f);
            Destroy(gameObject);
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tower
{
    public class TowerHitBox : MonoBehaviour, ICombatTarget
    {
        [Header("Tower Body")]
        [SerializeField] private TowerHealth towerHealth;

        [Header("Hitbox")]
        [SerializeField] private Collider hitCollider;

        private readonly List<TowerHealth> candidates = new List<TowerHealth>();

        private ICombatTarget Target => towerHealth != null ? (ICombatTarget)towerHealth : null;

        Guid ICombatTarget.EntityId => Target != null ? Target.EntityId : Guid.Empty;
        string ICombatTarget.FactionId => Target != null ? Target.FactionId : "Ally";
        CombatTargetKind ICombatTarget.Kind => CombatTargetKind.Tower;

        Vector3 ICombatTarget.HitPosition => hitCollider != null ? hitCollider.bounds.center : transform.position;

        bool ICombatTarget.IsTargetable => isActiveAndEnabled
            && hitCollider != null
            && hitCollider.enabled
            && !hitCollider.isTrigger
            && hitCollider.gameObject.activeInHierarchy
            && Target != null
            && Target.IsTargetable;

        IDamageable ICombatTarget.DamageReceiver => Target != null ? Target.DamageReceiver : null;
        ILifeState ICombatTarget.LifeState => Target != null ? Target.LifeState : null;

        private void Awake()
        {
            if (hitCollider == null)
                TryGetComponent(out hitCollider);
        }

        private void OnEnable()
        {
            RefreshTower();
        }

        private void OnTransformChildrenChanged()
        {
            RefreshTower();
        }

        private void LateUpdate()
        {
            if (!IsAvailable(towerHealth))
                RefreshTower();
        }

        private bool IsAvailable(TowerHealth health)
        {
            return health != null
                && health.transform != transform
                && health.transform.IsChildOf(transform)
                && ((ICombatTarget)health).IsTargetable;
        }

        private void RefreshTower()
        {
            if (IsAvailable(towerHealth))
                return;

            towerHealth = null;
            GetComponentsInChildren<TowerHealth>(false, candidates);

            foreach (var candidate in candidates)
            {
                if (!IsAvailable(candidate))
                    continue;

                towerHealth = candidate;

                Transform towerRoot = candidate.transform;

                while (towerRoot.parent != transform)
                    towerRoot = towerRoot.parent;

                towerRoot.localPosition = Vector3.zero;

                break;
            }

            candidates.Clear();
        }

        public void Bind(TowerHealth health)
        {
            towerHealth = health;
        }
    }
}

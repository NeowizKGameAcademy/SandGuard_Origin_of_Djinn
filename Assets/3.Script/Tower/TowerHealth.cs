using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tower
{
    public class TowerHealth : MonoBehaviour, ICombatTarget, IDamageable, ILifeState, IHealth, IDamageEvents
    {
        [Header("Status")]
        [SerializeField] private TowerStatus status;

        [Header("HP")]
        [SerializeField] private float HP;

        //ICombatTarget
        private readonly Guid entityId = Guid.NewGuid();
        private LifeState state = LifeState.Alive;

        Guid ICombatTarget.EntityId => entityId;
        string ICombatTarget.FactionId => "Ally";
        CombatTargetKind ICombatTarget.Kind => CombatTargetKind.Tower;

        Vector3 ICombatTarget.HitPosition => throw new NotImplementedException();
        bool ICombatTarget.IsTargetable => gameObject.activeInHierarchy && state == LifeState.Alive;

        IDamageable ICombatTarget.DamageReceiver => this;

        ILifeState ICombatTarget.LifeState => this;
        LifeState ILifeState.State => state;

        float IHealth.CurrentHealth => HP;
        float IHealth.MaxHealth => status.maxHP;

        private void Awake()
        {
            if(status == null)
                TryGetComponent(out status);

            HP = status.maxHP;
            state = LifeState.Alive;
        }

        event Action<LifeStateChangedInfo> StateChanged;
        event Action<DeathInfo> Died;
        event Action<Guid> Revived;
        event Action<Guid> Despawned;
        event Action<HealthChangedInfo> HealthChanged;
        event Action<DamageAppliedInfo> Damaged;

        DamageResult IDamageable.TakeDamage(DamageInfo damage)
        {
            throw new NotImplementedException();
            HP -= damage.Amount;
        }
    }
}

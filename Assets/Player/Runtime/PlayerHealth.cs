using System;
using UnityEngine;

namespace SandGuard.Player
{
    public sealed class PlayerHealth : MonoBehaviour, IHealth, IDamageable, IDamageEvents, ILifeState, ICombatTarget, IDespawnable
    {
        [SerializeField, Min(1f)] float maxHealth = 100f;
        [SerializeField] string factionId = "Ally";
        public Transform hitPoint;
        public Collider[] disableOnDeath = Array.Empty<Collider>();
        public float CurrentHealth { get; private set; }
        public float MaxHealth => maxHealth;
        public Guid EntityId { get; private set; }
        public string FactionId => factionId;
        public CombatTargetKind Kind => CombatTargetKind.Player;
        public Vector3 HitPosition => hitPoint != null ? hitPoint.position : transform.position + Vector3.up;
        public bool IsTargetable => isActiveAndEnabled && State == global::LifeState.Alive;
        public IDamageable DamageReceiver => this;
        public ILifeState LifeState => this;
        public global::LifeState State { get; private set; } = global::LifeState.Alive;
        public event Action<HealthChangedInfo> HealthChanged;
        public event Action<DamageAppliedInfo> Damaged;
        public event Action<LifeStateChangedInfo> StateChanged;
        public event Action<DeathInfo> Died;
        // 부활 계약은 유지하지만 이번 구현은 부활하지 않는다.
        public event Action<Guid> Revived { add { } remove { } }
        public event Action<Guid> Despawned;
        bool notifying;
        void Awake() { EntityId = Guid.NewGuid(); CurrentHealth = maxHealth; }
        void OnValidate()
        {
            if (float.IsNaN(maxHealth) || float.IsInfinity(maxHealth) || maxHealth < 1f) maxHealth = 100f;
            if (string.IsNullOrWhiteSpace(factionId)) factionId = "Ally";
        }
        public DamageResult TakeDamage(DamageInfo damage)
        {
            if (!damage.IsValid) return DamageResult.Rejected(DamageStatus.InvalidRequest);
            if (State != global::LifeState.Alive) return DamageResult.Rejected(DamageStatus.NotAlive);
            if (damage.SourceFactionId == factionId) return DamageResult.Rejected(DamageStatus.NonHostile);
            if (!isActiveAndEnabled || Time.timeScale <= 0f || notifying) return DamageResult.Rejected(DamageStatus.Protected);
            float previous = CurrentHealth;
            float applied = Mathf.Min(previous, damage.Amount);
            CurrentHealth -= applied;
            bool killed = applied > 0f && CurrentHealth <= 0f;
            if (killed)
            {
                State = global::LifeState.Incapacitated;
                foreach (var collider in disableOnDeath) if (collider != null) collider.enabled = false;
            }
            var result = DamageResult.Applied(applied, killed);
            notifying = true;
            try
            {
                if (previous != CurrentHealth) HealthChanged?.Invoke(new HealthChangedInfo(EntityId, previous, CurrentHealth, maxHealth, maxHealth));
                Damaged?.Invoke(new DamageAppliedInfo(EntityId, damage, result));
                if (killed)
                {
                    StateChanged?.Invoke(new LifeStateChangedInfo(EntityId, global::LifeState.Alive, State));
                    Died?.Invoke(new DeathInfo(EntityId, "player", factionId, 0, damage));
                }
            }
            finally { notifying = false; }
            return result;
        }
        public bool TryDespawn()
        {
            if (notifying || State == global::LifeState.Removed) return false;
            MarkRemoved(); gameObject.SetActive(false); Destroy(gameObject); return true;
        }
        void MarkRemoved()
        {
            var previous = State; State = global::LifeState.Removed;
            StateChanged?.Invoke(new LifeStateChangedInfo(EntityId, previous, State));
            Despawned?.Invoke(EntityId);
        }
        void OnDestroy() { if (EntityId != Guid.Empty && State != global::LifeState.Removed) MarkRemoved(); }
    }
}

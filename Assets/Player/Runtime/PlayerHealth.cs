using System;
using UnityEngine;

namespace SandGuard.Player
{
    public sealed class PlayerHealth : MonoBehaviour, IHealth, IDamageable, IDamageEvents, ILifeState, ICombatTarget, IDespawnable, IRevivable
    {
        [SerializeField, Min(1f)] float maxHealth = 100f;
        [SerializeField] string factionId = "Ally";
        public Transform hitPoint;
        public Collider[] disableOnDeath = Array.Empty<Collider>();
        [Min(0f), Tooltip("부활 직후 피해를 거부하고 적의 대상에서 빠지는 시간(초). 0이면 보호 없음")]
        public float reviveProtection = DefaultReviveProtection;
        /// <summary>기획 기본값: 부활 뒤 2초 무적.</summary>
        public const float DefaultReviveProtection = 2f;
        [Tooltip("스탯 수정자. DamageTaken 배수를 받는 피해에 곱한다. 비우면 같은 오브젝트에서 찾는다")]
        public PlayerStats stats;
        float protectedUntil = -1f;
        /// <summary>부활 보호 중인지. 보호 중에는 피해를 거부하고 적의 대상에서 빠진다.</summary>
        public bool IsProtected => Time.time < protectedUntil;
        public float CurrentHealth { get; private set; }
        public float MaxHealth => maxHealth;
        public Guid EntityId { get; private set; }
        public string FactionId => factionId;
        public CombatTargetKind Kind => CombatTargetKind.Player;
        public Vector3 HitPosition => hitPoint != null ? hitPoint.position : transform.position + Vector3.up;
        public bool IsTargetable => isActiveAndEnabled && State == global::LifeState.Alive && !IsProtected;
        public IDamageable DamageReceiver => this;
        public ILifeState LifeState => this;
        public global::LifeState State { get; private set; } = global::LifeState.Alive;
        public event Action<HealthChangedInfo> HealthChanged;
        public event Action<DamageAppliedInfo> Damaged;
        public event Action<LifeStateChangedInfo> StateChanged;
        public event Action<DeathInfo> Died;
        public event Action<Guid> Revived;
        public event Action<Guid> Despawned;
        bool notifying;
        void Awake() { EntityId = Guid.NewGuid(); CurrentHealth = maxHealth; if (stats == null) stats = GetComponent<PlayerStats>(); }
        /// <summary>수정자를 적용한 받는 피해 배수. 기본 1.</summary>
        public float DamageTakenMultiplier => stats != null ? stats.Evaluate(PlayerStat.DamageTaken, 1f) : 1f;
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
            if (!isActiveAndEnabled || Time.timeScale <= 0f || notifying || IsProtected) return DamageResult.Rejected(DamageStatus.Protected);
            float previous = CurrentHealth;
            float applied = Mathf.Min(previous, damage.Amount * DamageTakenMultiplier);
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
        /// <summary>무력화 상태에서 최대 체력으로 되살린다. 위치·대기 시간은 부르는 쪽(<see cref="PlayerRespawner"/>)이 정한다.</summary>
        public bool TryRevive()
        {
            if (State != global::LifeState.Incapacitated || notifying || !isActiveAndEnabled) return false;
            float previous = CurrentHealth;
            State = global::LifeState.Alive;
            CurrentHealth = maxHealth;
            protectedUntil = reviveProtection > 0f ? Time.time + reviveProtection : -1f;
            foreach (var collider in disableOnDeath) if (collider != null) collider.enabled = true;
            notifying = true;
            try
            {
                HealthChanged?.Invoke(new HealthChangedInfo(EntityId, previous, CurrentHealth, maxHealth, maxHealth));
                StateChanged?.Invoke(new LifeStateChangedInfo(EntityId, global::LifeState.Incapacitated, State));
                Revived?.Invoke(EntityId);
            }
            finally { notifying = false; }
            return true;
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

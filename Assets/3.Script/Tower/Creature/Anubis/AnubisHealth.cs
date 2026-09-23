using System;
using UnityEngine;

namespace Tower
{
    public sealed class AnubisHealth : MonoBehaviour, ICombatTarget, IDamageable, IHealth, ILifeState, IDamageEvents
    {
        [SerializeField] private AnubisController controller;
        private readonly Guid entityId = Guid.NewGuid();
        private float health = 1f;
        private float maxHealth = 1f;
        private LifeState state = LifeState.Alive;
        private readonly System.Collections.Generic.Queue<(float time, float amount)> killHealing = new();
        private float recentHealing;

        public float CurrentHealth => health;
        public float MaxHealth => maxHealth;
        public LifeState State => state;
        Guid ICombatTarget.EntityId => entityId;
        string ICombatTarget.FactionId => "Ally";
        CombatTargetKind ICombatTarget.Kind => CombatTargetKind.Minion;
        Vector3 ICombatTarget.HitPosition => transform.position + Vector3.up;
        bool ICombatTarget.IsTargetable => isActiveAndEnabled && state == LifeState.Alive;
        IDamageable ICombatTarget.DamageReceiver => this;
        ILifeState ICombatTarget.LifeState => this;
        public event Action<HealthChangedInfo> HealthChanged;
        public event Action<DamageAppliedInfo> Damaged;
        public event Action<LifeStateChangedInfo> StateChanged;
        public event Action<DeathInfo> Died;
        public event Action<Guid> Revived { add { } remove { } }
        public event Action<Guid> Despawned;

        private void Awake()
        {
            if (controller == null) TryGetComponent(out controller);
            maxHealth = controller?.Config?.maxHP ?? 1f;
            health = maxHealth;
            if (GetComponent<Collider>() == null)
            {
                var collider = gameObject.AddComponent<CapsuleCollider>();
                collider.center = new Vector3(0f, .13f, 0f);
                collider.height = .25f;
                collider.radius = .08f;
            }
        }

        public DamageResult TakeDamage(DamageInfo damage)
        {
            if (!damage.IsValid) return DamageResult.Rejected(DamageStatus.InvalidRequest);
            if (state != LifeState.Alive) return DamageResult.Rejected(DamageStatus.NotAlive);
            if (damage.SourceFactionId == "Ally") return DamageResult.Rejected(DamageStatus.NonHostile);
            float previous = health;
            float reduction = Mathf.Clamp01(controller?.Config?.damageReduction ?? 0f);
            float applied = Mathf.Min(previous, damage.Amount * (1f - reduction));
            health -= applied;
            bool killed = applied > 0f && health <= 0f;
            var result = DamageResult.Applied(applied, killed);
            if (applied > 0f)
            {
                HealthChanged?.Invoke(new HealthChangedInfo(entityId, previous, health, maxHealth, maxHealth));
                Damaged?.Invoke(new DamageAppliedInfo(entityId, damage, result));
            }
            if (!killed) return result;
            state = LifeState.Dying;
            StateChanged?.Invoke(new LifeStateChangedInfo(entityId, LifeState.Alive, state));
            Died?.Invoke(new DeathInfo(entityId, "tower.anubis", "Ally", 0, damage));
            controller?.Died();
            return result;
        }

        /// <summary>아누비스가 직접 처치했을 때만 회복한다. 최근 1초의 실제 회복량에 상한을 둔다.</summary>
        public void RewardKill()
        {
            var config = controller?.Config;
            if (state != LifeState.Alive || config == null) return;
            float now = Time.time;
            while (killHealing.Count > 0 && now - killHealing.Peek().time >= 1f)
                recentHealing -= killHealing.Dequeue().amount;
            float amount = Mathf.Min(Mathf.Max(0f, config.healPerKill), maxHealth - health,
                Mathf.Max(0f, config.maxKillHealingPerSecond - recentHealing));
            if (amount <= 0f) return;
            float previous = health;
            health += amount;
            recentHealing += amount;
            killHealing.Enqueue((now, amount));
            HealthChanged?.Invoke(new HealthChangedInfo(entityId, previous, health, maxHealth, maxHealth));
        }
    }
}

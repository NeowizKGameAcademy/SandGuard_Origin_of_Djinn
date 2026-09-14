using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace SandGuard.Facility
{
    /// <summary>타워·시설의 체력과 공격 대상 정보. 적은 ICombatTarget으로 찾고 IDamageable로 때린다.</summary>
    /// <remarks>
    /// 적의 대상 탐색과 근접 판정은 충돌체로 하므로 트리거가 아닌 충돌체가 자식 어딘가에 있어야 한다.
    /// 체력 0이면 충돌체를 끄고 Dying → removeDelay 뒤 Removed(Despawned 알림) → 제거. 건설 서비스는 Despawned에서 슬롯을 비운다.
    /// 받침 위 본체에 붙인다. 받침은 파괴되지 않고 남는다.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class FacilityHealth : MonoBehaviour, ICombatTarget, IDamageable, IHealth, ILifeState, IDamageEvents, IRepairable
    {
        public enum RemovalMode { Destroy, Deactivate }

        [Min(1f)] public float maxHealth = 150f;
        public string factionId = "Ally";
        public string definitionId = "facility";
        [Tooltip("적의 우선순위를 정한다. 타워 2, 길을 막으면 0")]
        public CombatTargetKind kind = CombatTargetKind.Tower;
        [Min(0f), Tooltip("체력 0 이후 본체를 제거하기까지의 시간. 파괴 연출은 사망 순간 원본을 숨기므로 짧아도 된다")]
        public float removeDelay = 0.5f;
        [Tooltip("Destroy: 본체를 파괴한다(건설 시스템). Deactivate: 본체를 끈다(다른 스크립트가 본체 참조를 들고 있는 레벨 배치 타워)")]
        public RemovalMode removal = RemovalMode.Destroy;
        public UnityEvent onDied = new UnityEvent();

        Guid entityId = Guid.NewGuid();
        readonly List<Collider> bodies = new List<Collider>();

        public Guid EntityId => entityId;
        public string FactionId => factionId;
        public CombatTargetKind Kind => kind;
        /// <summary>몸통 충돌체의 중심. 없으면 오브젝트 위치.</summary>
        public Vector3 HitPosition
        {
            get
            {
                foreach (var body in bodies) if (body != null && body.enabled) return body.bounds.center;
                return transform.position;
            }
        }
        public bool IsTargetable => State == LifeState.Alive;
        public IDamageable DamageReceiver => this;
        ILifeState ICombatTarget.LifeState => this;
        public LifeState State { get; private set; } = LifeState.Alive;
        public bool IsAlive => State == LifeState.Alive;
        public float CurrentHealth { get; private set; }
        public float MaxHealth => maxHealth;

        public event Action<HealthChangedInfo> HealthChanged;
        public event Action<DamageAppliedInfo> Damaged;
        public event Action<LifeStateChangedInfo> StateChanged;
        public event Action<DeathInfo> Died;
        /// <summary>시설은 부활하지 않는다(수리는 체력 회복으로 따로 다룬다).</summary>
        public event Action<Guid> Revived { add { } remove { } }
        public event Action<Guid> Despawned;

        void Awake()
        {
            CurrentHealth = maxHealth;
            foreach (var collider in GetComponentsInChildren<Collider>(true)) if (!collider.isTrigger) bodies.Add(collider);
            if (bodies.Count == 0) Debug.LogWarning(name + ": 트리거가 아닌 충돌체가 없어 적이 찾지 못합니다.", this);
        }

        /// <summary>건설 시 시설 ID·정의·최대 체력을 맞춘다. 피해를 받기 전에만 부른다.</summary>
        public void Configure(Guid id, string definition, float health)
        {
            if (id != Guid.Empty) entityId = id;
            if (!string.IsNullOrEmpty(definition)) definitionId = definition;
            maxHealth = Mathf.Max(1f, health);
            CurrentHealth = maxHealth;
        }

        /// <summary>살아 있는 시설의 체력을 최대치까지 회복하고 실제 회복량을 돌려준다. 비용·가능 여부는 부르는 서비스가 확인한다.</summary>
        public float Repair(float amount)
        {
            if (State != LifeState.Alive || !(amount > 0f)) return 0f;
            float previous = CurrentHealth;
            CurrentHealth = Mathf.Min(maxHealth, previous + amount);
            float applied = CurrentHealth - previous;
            if (applied > 0f) HealthChanged?.Invoke(new HealthChangedInfo(entityId, previous, CurrentHealth, maxHealth, maxHealth));
            return applied;
        }

        public DamageResult TakeDamage(DamageInfo damage)
        {
            if (!damage.IsValid) return DamageResult.Rejected(DamageStatus.InvalidRequest);
            if (State != LifeState.Alive) return DamageResult.Rejected(DamageStatus.NotAlive);
            if (damage.SourceFactionId == factionId) return DamageResult.Rejected(DamageStatus.NonHostile);
            float previous = CurrentHealth;
            float applied = Mathf.Min(previous, damage.Amount);
            CurrentHealth = previous - applied;
            bool killed = applied > 0f && CurrentHealth <= 0f;
            if (killed) { State = LifeState.Dying; SetBodiesEnabled(false); } // 알림을 받는 쪽은 항상 최종 상태를 본다.
            var result = DamageResult.Applied(applied, killed);
            if (applied > 0f) HealthChanged?.Invoke(new HealthChangedInfo(entityId, previous, CurrentHealth, maxHealth, maxHealth));
            Damaged?.Invoke(new DamageAppliedInfo(entityId, damage, result));
            if (killed)
            {
                StateChanged?.Invoke(new LifeStateChangedInfo(entityId, LifeState.Alive, LifeState.Dying));
                Died?.Invoke(new DeathInfo(entityId, definitionId, factionId, 0, damage));
                onDied.Invoke();
                StartCoroutine(RemoveAfter(removeDelay));
            }
            return result;
        }

        IEnumerator RemoveAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            State = LifeState.Removed;
            StateChanged?.Invoke(new LifeStateChangedInfo(entityId, LifeState.Dying, LifeState.Removed));
            Despawned?.Invoke(entityId);
            if (removal == RemovalMode.Deactivate) gameObject.SetActive(false); else Destroy(gameObject);
        }

        void SetBodiesEnabled(bool value)
        {
            foreach (var body in bodies) if (body != null) body.enabled = value;
        }
    }
}

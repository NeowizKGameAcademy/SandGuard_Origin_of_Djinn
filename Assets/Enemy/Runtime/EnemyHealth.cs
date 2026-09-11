using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace SandGuard.Enemy
{
    /// <summary>적 한 마리의 체력·진영·생존 상태. 피해는 IDamageable, 조회는 ICombatTarget으로 다룬다.</summary>
    /// <remarks>체력 0이 되면 알림 전에 충돌체를 끄고 Dying이 된 뒤, removeDelay 뒤에 제거된다. 적은 부활하지 않는다.</remarks>
    [DisallowMultipleComponent]
    public sealed class EnemyHealth : MonoBehaviour, ICombatTarget, IDamageable, IHealth, ILifeState, IDamageEvents, IDespawnable
    {
        [Min(1f)] public float maxHealth = 60f;
        public string factionId = "Enemy";
        public string definitionId = "enemy.basic";
        [Tooltip("거리·시야 판정 기준점. 발이 아닌 몸통 위치")]
        public Vector3 hitOffset = new Vector3(0f, 1f, 0f);
        [Min(0f), Tooltip("체력 0 이후 오브젝트를 제거하기까지의 연출 시간")]
        public float removeDelay = 1.5f;
        public UnityEvent onDied = new UnityEvent();

        Guid entityId = Guid.NewGuid();
        Collider[] colliders;
        Coroutine removal;

        public Guid EntityId => entityId;
        public string FactionId => factionId;
        public CombatTargetKind Kind => CombatTargetKind.Enemy;
        public Vector3 HitPosition => transform.TransformPoint(hitOffset);
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
        /// <summary>적은 부활하지 않으므로 알리지 않는다.</summary>
        public event Action<Guid> Revived { add { } remove { } }
        public event Action<Guid> Despawned;
        /// <summary>풀이 지정하면 제거 시 파괴 대신 이것을 부른다. 비어 있으면 Destroy한다.</summary>
        public Action<EnemyHealth> ReleaseHandler { get; set; }

        void Awake()
        {
            CurrentHealth = maxHealth;
            colliders = GetComponentsInChildren<Collider>(true);
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
            if (killed) { State = LifeState.Dying; SetCombatEnabled(false); } // 알림을 받는 쪽은 항상 최종 상태를 본다.
            var result = DamageResult.Applied(applied, killed);
            if (applied > 0f) HealthChanged?.Invoke(new HealthChangedInfo(entityId, previous, CurrentHealth, maxHealth, maxHealth));
            Damaged?.Invoke(new DamageAppliedInfo(entityId, damage, result));
            if (killed)
            {
                StateChanged?.Invoke(new LifeStateChangedInfo(entityId, LifeState.Alive, LifeState.Dying));
                Died?.Invoke(new DeathInfo(entityId, definitionId, factionId, 0, damage));
                onDied.Invoke();
                removal = StartCoroutine(RemoveAfter(removeDelay));
            }
            return result;
        }

        /// <summary>사망으로 처리하지 않고 제거한다. 코어 흡수, 웨이브 정리 등에 쓴다.</summary>
        public bool TryDespawn()
        {
            if (State == LifeState.Removed) return false;
            if (removal != null) { StopCoroutine(removal); removal = null; }
            Remove();
            return true;
        }

        IEnumerator RemoveAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            removal = null;
            Remove();
        }

        void Remove()
        {
            if (State == LifeState.Removed) return;
            LifeState previous = State;
            State = LifeState.Removed;
            SetCombatEnabled(false);
            StateChanged?.Invoke(new LifeStateChangedInfo(entityId, previous, LifeState.Removed));
            Despawned?.Invoke(entityId);
            if (ReleaseHandler != null) ReleaseHandler(this); else Destroy(gameObject);
        }

        /// <summary>풀 재사용: 새 ID로 다시 살아난다. 알림은 내지 않는다.</summary>
        public void ResetForReuse()
        {
            if (removal != null) { StopCoroutine(removal); removal = null; }
            entityId = Guid.NewGuid();
            State = LifeState.Alive;
            CurrentHealth = maxHealth;
            SetCombatEnabled(true);
        }

        void SetCombatEnabled(bool value)
        {
            if (colliders == null) return;
            foreach (var collider in colliders) if (collider != null) collider.enabled = value;
        }
    }
}

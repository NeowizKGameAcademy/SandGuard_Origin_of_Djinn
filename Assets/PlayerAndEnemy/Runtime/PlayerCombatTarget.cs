using System;
using System.Collections;
using SandGuard.Player;
using UnityEngine;

namespace SandGuard.Integration
{
    /// <summary>
    /// 플레이어의 임시 전투 개체(유지형). 적이 플레이어를 대상으로 삼고 때릴 수 있게 ICombatTarget/IDamageable을 제공한다.
    /// 체력 0이면 무력화되어 이동·공격이 꺼지고, autoReviveDelay 뒤 최대 체력으로 부활하며 잠시 보호된다.
    /// 공통 Health 컴포넌트가 구현되면 이 컴포넌트를 그것으로 교체한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCombatTarget : MonoBehaviour, ICombatTarget, IDamageable, IHealth, ILifeState, IDamageEvents, IRevivable
    {
        [Min(1f)] public float maxHealth = 100f;
        public string factionId = "Ally";
        public Vector3 hitOffset = new Vector3(0f, 1.1f, 0f);
        [Min(0f), Tooltip("무력화 뒤 자동 부활까지의 시간. 0이면 자동 부활하지 않는다")]
        public float autoReviveDelay = 5f;
        [Min(0f), Tooltip("부활 직후 피해를 거부하고 대상에서 빠지는 시간")]
        public float reviveProtection = 2f;
        [Tooltip("비우면 같은 오브젝트에서 찾는다")]
        public PlayerMotor motor;
        public PlayerBasicAttack attack;

        readonly Guid entityId = Guid.NewGuid();
        float protectedUntil = -1f;
        int lifeSequence;
        Coroutine autoRevive;

        public Guid EntityId => entityId;
        public string FactionId => factionId;
        public CombatTargetKind Kind => CombatTargetKind.Player;
        public Vector3 HitPosition => transform.TransformPoint(hitOffset);
        public bool IsProtected => Time.time < protectedUntil;
        public bool IsTargetable => State == LifeState.Alive && !IsProtected;
        public IDamageable DamageReceiver => this;
        ILifeState ICombatTarget.LifeState => this;
        public LifeState State { get; private set; } = LifeState.Alive;
        public float CurrentHealth { get; private set; }
        public float MaxHealth => maxHealth;
        public int LifeSequence => lifeSequence;

        public event Action<HealthChangedInfo> HealthChanged;
        public event Action<DamageAppliedInfo> Damaged;
        public event Action<LifeStateChangedInfo> StateChanged;
        public event Action<DeathInfo> Died;
        public event Action<Guid> Revived;
        /// <summary>플레이어는 게임에서 제거되지 않는다.</summary>
        public event Action<Guid> Despawned { add { } remove { } }

        void Awake()
        {
            CurrentHealth = maxHealth;
            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (attack == null) attack = GetComponent<PlayerBasicAttack>();
        }

        public DamageResult TakeDamage(DamageInfo damage)
        {
            if (!damage.IsValid) return DamageResult.Rejected(DamageStatus.InvalidRequest);
            if (State != LifeState.Alive) return DamageResult.Rejected(DamageStatus.NotAlive);
            if (damage.SourceFactionId == factionId) return DamageResult.Rejected(DamageStatus.NonHostile);
            if (IsProtected) return DamageResult.Rejected(DamageStatus.Protected);
            float previous = CurrentHealth;
            float applied = Mathf.Min(previous, damage.Amount);
            CurrentHealth = previous - applied;
            bool killed = applied > 0f && CurrentHealth <= 0f;
            if (killed) { State = LifeState.Incapacitated; SetControls(false); }
            var result = DamageResult.Applied(applied, killed);
            if (applied > 0f) HealthChanged?.Invoke(new HealthChangedInfo(entityId, previous, CurrentHealth, maxHealth, maxHealth));
            Damaged?.Invoke(new DamageAppliedInfo(entityId, damage, result));
            if (killed)
            {
                StateChanged?.Invoke(new LifeStateChangedInfo(entityId, LifeState.Alive, LifeState.Incapacitated));
                Died?.Invoke(new DeathInfo(entityId, "player", factionId, lifeSequence, damage));
                if (autoReviveDelay > 0f) autoRevive = StartCoroutine(ReviveAfter(autoReviveDelay));
            }
            return result;
        }

        public bool TryRevive()
        {
            if (State != LifeState.Incapacitated) return false;
            if (autoRevive != null) { StopCoroutine(autoRevive); autoRevive = null; }
            float previous = CurrentHealth;
            State = LifeState.Alive;
            CurrentHealth = maxHealth;
            protectedUntil = Time.time + reviveProtection;
            lifeSequence++;
            SetControls(true);
            HealthChanged?.Invoke(new HealthChangedInfo(entityId, previous, CurrentHealth, maxHealth, maxHealth));
            StateChanged?.Invoke(new LifeStateChangedInfo(entityId, LifeState.Incapacitated, LifeState.Alive));
            Revived?.Invoke(entityId);
            return true;
        }

        IEnumerator ReviveAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            autoRevive = null;
            TryRevive();
        }

        void SetControls(bool value)
        {
            if (motor != null) motor.MovementEnabled = value;
            if (attack != null) attack.CombatEnabled = value;
        }
    }
}

using System;
using UnityEngine;

namespace SandGuard.Enemy
{
    /// <summary>적 테스트 씬 전용 표적. 코어·벽·플레이어 대역으로 쓰며 실제 구현과는 독립이다.</summary>
    public sealed class EnemyTestTarget : MonoBehaviour, ICombatTarget, IFacilityKind, IDamageable, ILifeState, IDamageEvents
    {
        public CombatTargetKind kind = CombatTargetKind.Player;
        public string factionId = "Ally";
        [Tooltip("타워 대역으로 쓸 때의 시설 종류 ID. 예: tower.cobra")]
        public string definitionId = "";
        [Min(1f)] public float maxHealth = 50f;
        [Tooltip("피격 기준점. 기본은 오브젝트 중심")]
        public Vector3 hitOffset;
        /// <summary>첫 피해 전에는 maxHealth와 같다. AddComponent 뒤에 maxHealth를 바꿔도 반영된다.</summary>
        public float CurrentHealth => current < 0f ? maxHealth : current;
        public int HitCount { get; private set; }
        readonly Guid entityId = Guid.NewGuid();
        float current = -1f;

        public Guid EntityId => entityId;
        public string FactionId => factionId;
        public CombatTargetKind Kind => kind;
        public string DefinitionId => definitionId;
        public Vector3 HitPosition => transform.TransformPoint(hitOffset);
        public bool IsTargetable => State == LifeState.Alive;
        public IDamageable DamageReceiver => this;
        ILifeState ICombatTarget.LifeState => this;
        public LifeState State { get; private set; } = LifeState.Alive;
        public event Action<LifeStateChangedInfo> StateChanged;
        public event Action<DeathInfo> Died;
        public event Action<Guid> Revived { add { } remove { } }
        public event Action<Guid> Despawned;
        public event Action<DamageAppliedInfo> Damaged;

        public DamageResult TakeDamage(DamageInfo damage)
        {
            if (!damage.IsValid) return DamageResult.Rejected(DamageStatus.InvalidRequest);
            if (State != LifeState.Alive) return DamageResult.Rejected(DamageStatus.NotAlive);
            if (damage.SourceFactionId == factionId) return DamageResult.Rejected(DamageStatus.NonHostile);
            float applied = Mathf.Min(CurrentHealth, damage.Amount);
            current = CurrentHealth - applied; HitCount++;
            bool killed = applied > 0f && CurrentHealth <= 0f;
            var result = DamageResult.Applied(applied, killed);
            if (killed) State = LifeState.Removed; // 알림을 받는 쪽은 최종 상태를 본다.
            Damaged?.Invoke(new DamageAppliedInfo(entityId, damage, result));
            if (killed)
            {
                StateChanged?.Invoke(new LifeStateChangedInfo(entityId, LifeState.Alive, LifeState.Removed));
                Died?.Invoke(new DeathInfo(entityId, "test." + kind, factionId, 0, damage));
                Despawned?.Invoke(entityId);
                gameObject.SetActive(false); // NavMeshObstacle이 꺼지면 막혔던 길이 열린다. 구독자가 빠지기 전에 알림을 끝낸다.
            }
            return result;
        }
    }
}

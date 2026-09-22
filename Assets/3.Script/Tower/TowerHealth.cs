using System;
using System.Collections;
using UnityEngine;

namespace Tower
{
    [RequireComponent(typeof(TowerStatus))]
    // [통합 추가 2026-09-21] IRepairable을 더했습니다. 기존 필드·메서드는 그대로입니다.
    //   왜: 건설 시스템의 수리 메뉴(FacilityBuildService.TryRepair)는 시설의 IRepairable에 회복을 맡깁니다.
    //       이 인터페이스가 없으면 타워는 수리 대상으로 잡히지 않아 수리 UI 자체가 뜨지 않습니다.
    public class TowerHealth : MonoBehaviour, ICombatTarget, IFacilityKind, IDamageable, ILifeState, IHealth, IDamageEvents, IRepairable
    {
        [Header("Status")]
        [SerializeField] private TowerStatus status;

        [Header("HP")]
        [SerializeField] private float HP;

        [Header("Death")]
        [SerializeField] private GameObject towerRoot;
        [Min(0f)] [SerializeField] private float removeDelay = 0.5f;

        private Collider[] bodies;

        private readonly Guid entityId = Guid.NewGuid();
        private LifeState state = LifeState.Alive;

        Guid ICombatTarget.EntityId => entityId;
        string ICombatTarget.FactionId => "Ally";
        CombatTargetKind ICombatTarget.Kind => CombatTargetKind.Tower;

        /// <summary>시설 카탈로그와 같은 규칙의 종류 ID다. 사망 알림에 쓰는 값과 같다.</summary>
        public string DefinitionId => status != null ? "tower." + status.towerType.ToString().ToLowerInvariant() : string.Empty;

        Vector3 ICombatTarget.HitPosition
        {
            get
            {
                if (bodies != null)
                    foreach (var body in bodies)
                        if (body != null && !body.isTrigger && body.enabled && body.gameObject.activeInHierarchy)
                            return body.bounds.center;

                return transform.position;
            }
        }

        bool ICombatTarget.IsTargetable => isActiveAndEnabled && state == LifeState.Alive;

        IDamageable ICombatTarget.DamageReceiver => this;

        ILifeState ICombatTarget.LifeState => this;
        LifeState ILifeState.State => state;

        public float CurrentHealth => HP;
        public float MaxHealth => status != null && !float.IsNaN(status.maxHP) && !float.IsInfinity(status.maxHP) ? Mathf.Max(1f, status.maxHP) : 1f;

        private void Awake()
        {
            if (status == null)
                TryGetComponent(out status);

            towerRoot = ResolveTowerRoot();

            bodies = towerRoot.GetComponentsInChildren<Collider>(true);

            HP = MaxHealth;
            state = LifeState.Alive;
        }

        private GameObject ResolveTowerRoot()
        {
            var hitBox = GetComponentInParent<TowerHitBox>();
            if (hitBox != null && hitBox.transform != transform)
            {
                var root = transform;
                while (root.parent != null && root.parent != hitBox.transform)
                    root = root.parent;

                if (root.parent == hitBox.transform)
                    return root.gameObject;
            }

            // TowerHitBox 없이 단독으로 쓰는 프리팹은 명시적 본체를 존중한다.
            return towerRoot != null ? towerRoot : gameObject;
        }

        public event Action<LifeStateChangedInfo> StateChanged;
        public event Action<DeathInfo> Died;
        public event Action<Guid> Revived { add { } remove { } }
        public event Action<Guid> Despawned;
        public event Action<HealthChangedInfo> HealthChanged;
        public event Action<DamageAppliedInfo> Damaged;

        public DamageResult TakeDamage(DamageInfo damage)
        {
            if (!damage.IsValid) 
                return DamageResult.Rejected(DamageStatus.InvalidRequest);

            if (state != LifeState.Alive) 
                return DamageResult.Rejected(DamageStatus.NotAlive);

            if (!isActiveAndEnabled) 
                return DamageResult.Rejected(DamageStatus.Protected);

            if (damage.SourceFactionId == "Ally") 
                return DamageResult.Rejected(DamageStatus.NonHostile);

            float previous = HP;
            float applied = Mathf.Min(previous, damage.Amount);
            HP = previous - applied;
            bool killed = applied > 0f && HP <= 0f;

            if (killed)
            {
                state = LifeState.Dying;
                foreach (var body in bodies)
                    if (body != null && !body.isTrigger) body.enabled = false;
            }

            var result = DamageResult.Applied(applied, killed);

            if (applied > 0f)
                HealthChanged?.Invoke(new HealthChangedInfo(entityId, previous, HP, MaxHealth, MaxHealth));

            Damaged?.Invoke(new DamageAppliedInfo(entityId, damage, result));

            if (killed)
            {
                StateChanged?.Invoke(new LifeStateChangedInfo(entityId, LifeState.Alive, LifeState.Dying));
                Died?.Invoke(new DeathInfo(entityId, DefinitionId, "Ally", 0, damage));

                if (state == LifeState.Dying)
                {
                    if (isActiveAndEnabled)
                        StartCoroutine(RemoveAfterDelay());

                    else 
                        TowerRemoved();
                }
            }
            return result;
        }

        // [통합 추가 2026-09-21] 살아 있는 타워의 체력을 회복시키고 실제 회복량을 돌려줍니다.
        //   비용(마나)과 수리 가능 여부는 부르는 쪽(FacilityBuildService)이 먼저 확인하므로 여기서는 체력만 다룹니다.
        //   TakeDamage와 같은 방식으로 HealthChanged를 알려 체력바·HUD가 그대로 따라오게 합니다.
        public float Repair(float amount)
        {
            if (state != LifeState.Alive || !(amount > 0f))
                return 0f;

            float previous = HP;
            HP = Mathf.Min(MaxHealth, previous + amount);
            float applied = HP - previous;

            if (applied > 0f)
                HealthChanged?.Invoke(new HealthChangedInfo(entityId, previous, HP, MaxHealth, MaxHealth));

            return applied;
        }

        private IEnumerator RemoveAfterDelay()
        {
            yield return new WaitForSeconds(Mathf.Max(0f, removeDelay));
            TowerRemoved();

            towerRoot.SetActive(false);
        }

        private void OnDisable()
        {
            StopAllCoroutines();

            if (state == LifeState.Dying)
                TowerRemoved();
        }

        private void OnDestroy()
        {
            TowerRemoved();
        }

        private void TowerRemoved()
        {
            if (state == LifeState.Removed)
                return;

            var previous = state;
            state = LifeState.Removed;

            StateChanged?.Invoke(new LifeStateChangedInfo(entityId, previous, state));
            Despawned?.Invoke(entityId);
        }
    }
}

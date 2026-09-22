using System;
using System.Collections;
using UnityEngine;

namespace Tower
{
    [RequireComponent(typeof(TowerStatus))]
    public class TowerHealth : MonoBehaviour, ICombatTarget, IDamageable, ILifeState, IHealth, IDamageEvents, IRepairable
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
                string definitionId = "tower." + status.towerType.ToString().ToLowerInvariant();
                Died?.Invoke(new DeathInfo(entityId, definitionId, "Ally", 0, damage));

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

        float IRepairable.Repair(float amount)
        {
            if (state != LifeState.Alive || amount <= 0f)
                return 0f;

            float previous = HP;
            HP = Mathf.Min(MaxHealth, HP + amount);

            float repaired = HP - previous;

            if (repaired > 0f)
                HealthChanged?.Invoke(
                    new HealthChangedInfo(entityId, previous, HP, MaxHealth, MaxHealth)
                );

            return repaired;
        }
    }
}

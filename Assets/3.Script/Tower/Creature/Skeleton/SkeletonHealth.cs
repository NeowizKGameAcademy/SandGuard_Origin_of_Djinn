using System;
using UnityEngine;

/// <summary>스켈레톤 소환수의 체력과 전투 대상 정보를 제공한다.</summary>
public sealed class SkeletonHealth : MonoBehaviour, ICombatTarget, IDamageable, IHealth, ILifeState, IDamageEvents
{
    [Header("Components")]
    [SerializeField] private Collider[] bodies = Array.Empty<Collider>();

    private readonly Guid entityId = Guid.NewGuid();
    private LifeState state = LifeState.Alive;
    private float health = 1f;
    private float maxHealth = 1f;
    private int lifeSequence;

    public Guid EntityId => entityId;
    public float CurrentHealth => health;
    public float MaxHealth => maxHealth;
    public LifeState State => state;

    Guid ICombatTarget.EntityId => entityId;
    string ICombatTarget.FactionId => "Ally";
    CombatTargetKind ICombatTarget.Kind => CombatTargetKind.Minion;
    Vector3 ICombatTarget.HitPosition => GetHitPosition();
    bool ICombatTarget.IsTargetable => isActiveAndEnabled && state == LifeState.Alive;
    IDamageable ICombatTarget.DamageReceiver => this;
    ILifeState ICombatTarget.LifeState => this;

    public event Action<HealthChangedInfo> HealthChanged;
    public event Action<DamageAppliedInfo> Damaged;
    public event Action<LifeStateChangedInfo> StateChanged;
    public event Action<DeathInfo> Died;
    public event Action<Guid> Revived;
    public event Action<Guid> Despawned;

    private void Awake()
    {
        if (bodies == null || bodies.Length == 0) bodies = GetComponentsInChildren<Collider>(true);
    }

    private void OnDisable()
    {
        if (state == LifeState.Removed) return;
        LifeState previous = state;
        state = LifeState.Removed;
        StateChanged?.Invoke(new LifeStateChangedInfo(entityId, previous, state));
        Despawned?.Invoke(entityId);
    }

    public void ResetForSpawn(float configuredMaxHealth)
    {
        maxHealth = !float.IsNaN(configuredMaxHealth) && !float.IsInfinity(configuredMaxHealth) ? Mathf.Max(1f, configuredMaxHealth) : 1f;
        health = maxHealth;
        lifeSequence++;
        LifeState previous = state;
        state = LifeState.Alive;
        SetBodiesEnabled(true);
        if (previous != LifeState.Alive)
        {
            StateChanged?.Invoke(new LifeStateChangedInfo(entityId, previous, state));
            Revived?.Invoke(entityId);
        }
        HealthChanged?.Invoke(new HealthChangedInfo(entityId, 0f, health, maxHealth, maxHealth));
    }

    public DamageResult TakeDamage(DamageInfo damage)
    {
        if (!damage.IsValid) return DamageResult.Rejected(DamageStatus.InvalidRequest);
        if (state != LifeState.Alive) return DamageResult.Rejected(DamageStatus.NotAlive);
        if (!isActiveAndEnabled) return DamageResult.Rejected(DamageStatus.Protected);
        if (damage.SourceFactionId == "Ally") return DamageResult.Rejected(DamageStatus.NonHostile);

        float previous = health;
        float applied = Mathf.Min(previous, damage.Amount);
        health -= applied;
        bool killed = applied > 0f && health <= 0f;
        var result = DamageResult.Applied(applied, killed);
        if (applied > 0f)
        {
            HealthChanged?.Invoke(new HealthChangedInfo(entityId, previous, health, maxHealth, maxHealth));
            Damaged?.Invoke(new DamageAppliedInfo(entityId, damage, result));
        }
        if (killed)
        {
            state = LifeState.Dying;
            SetBodiesEnabled(false);
            StateChanged?.Invoke(new LifeStateChangedInfo(entityId, LifeState.Alive, state));
            Died?.Invoke(new DeathInfo(entityId, "tower.skeleton", "Ally", lifeSequence, damage));
        }
        return result;
    }

    private Vector3 GetHitPosition()
    {
        foreach (Collider body in bodies)
            if (body != null && body.enabled && body.gameObject.activeInHierarchy) return body.bounds.center;
        return transform.position;
    }

    private void SetBodiesEnabled(bool enabled)
    {
        foreach (Collider body in bodies)
            if (body != null && !body.isTrigger) body.enabled = enabled;
    }
}

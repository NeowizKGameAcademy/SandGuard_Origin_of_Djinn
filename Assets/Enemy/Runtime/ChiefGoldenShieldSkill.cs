using DesertTower.VFX;
using UnityEngine;

namespace SandGuard.Enemy
{
    /// <summary>교전 시 소환하는 정면 방패. 판정은 방패병의 EnemyShield를 재사용한다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth), typeof(EnemyShield))]
    public sealed class ChiefGoldenShieldSkill : MonoBehaviour
    {
        public EnemyShield shield;
        public VfxGoldenShield vfxPrefab;
        public Vector3 localPosition = new Vector3(0f, 1.15f, .65f);
        [Min(.01f)] public float summonDuration = .35f;
        [Min(.01f)] public float activeDuration = 4f;
        [Min(0f)] public float cooldown = 8f;
        [Min(.1f)] public float castRange = 8f;
        public enum Phase { Ready, Summoning, Active, Dismissing, Cooldown }
        public Phase State { get; private set; }
        public bool IsCasting => State == Phase.Summoning;
        public float Remaining { get; private set; }
        public int CastCount { get; private set; }
        public VfxGoldenShield Visual => visual;
        EnemyHealth health;
        EnemyBrain brain;
        EnemyMotor motor;
        EnemyMeleeAttack melee;
        ChiefBombThrowSkill bombSkill;
        VfxGoldenShield visual;

        void Awake()
        {
            health = GetComponent<EnemyHealth>(); brain = GetComponent<EnemyBrain>();
            motor = GetComponent<EnemyMotor>(); melee = GetComponent<EnemyMeleeAttack>();
            bombSkill = GetComponent<ChiefBombThrowSkill>();
            if (!shield) shield = GetComponent<EnemyShield>();
        }
        void OnEnable()
        {
            ResetForReuse();
            health.StateChanged += OnLifeChanged;
            shield.Guarded += OnGuarded;
        }
        void OnDisable()
        {
            if (health) health.StateChanged -= OnLifeChanged;
            if (shield) shield.Guarded -= OnGuarded;
            Cancel();
        }
        void OnLifeChanged(LifeStateChangedInfo info) { if (info.CurrentState != LifeState.Alive) Cancel(); }
        void OnGuarded(DamageInfo _) { if (visual) visual.PulseHit(); }

        public bool TryUse(ICombatTarget target)
        {
            if (!isActiveAndEnabled || !health.IsAlive || !vfxPrefab || State != Phase.Ready ||
                (brain && (!brain.enabled || !brain.AIEnabled)) || (motor && motor.IsDetached) ||
                (melee && melee.IsAttacking) || (bombSkill && bombSkill.IsCasting) || target == null || !target.IsTargetable ||
                target.FactionId == health.FactionId ||
                Vector3.Distance(health.HitPosition, target.HitPosition) > castRange) return false;
            if (!visual)
            {
                visual = Instantiate(vfxPrefab, transform);
                visual.transform.localPosition = localPosition;
                visual.transform.localRotation = Quaternion.identity;
            }
            visual.summonTime = summonDuration;
            visual.gameObject.SetActive(true); visual.Restart();
            shield.enabled = false;
            State = Phase.Summoning; Remaining = summonDuration; CastCount++;
            melee?.Cancel(); motor?.Stop();
            return true;
        }
        void Update()
        {
            if (!health.IsAlive || (brain && (!brain.enabled || !brain.AIEnabled)) || (motor && motor.IsDetached))
            { Cancel(); return; }
            if (State == Phase.Ready) return;
            Remaining = Mathf.Max(0f, Remaining - Time.deltaTime);
            if (Remaining > 0f) return;
            switch (State)
            {
                case Phase.Summoning:
                    shield.enabled = true; State = Phase.Active; Remaining = activeDuration; break;
                case Phase.Active:
                    shield.enabled = false; visual.Dismiss(); State = Phase.Dismissing; Remaining = visual.dismissTime; break;
                case Phase.Dismissing:
                    visual.gameObject.SetActive(false); State = Phase.Cooldown; Remaining = cooldown; break;
                case Phase.Cooldown: State = Phase.Ready; break;
            }
        }
        public void Cancel()
        {
            if (shield) shield.enabled = false;
            if (visual) visual.gameObject.SetActive(false);
            if (State == Phase.Summoning || State == Phase.Active || State == Phase.Dismissing)
            { State = Phase.Cooldown; Remaining = cooldown; }
        }
        public void ResetForReuse()
        {
            Cancel(); State = Phase.Ready; Remaining = 0f; CastCount = 0;
        }
    }
}

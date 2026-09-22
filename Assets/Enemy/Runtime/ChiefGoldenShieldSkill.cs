using DesertTower.VFX;
using UnityEngine;

namespace SandGuard.Enemy
{
    /// <summary>체력이 낮아지면 소환하는 정면 방패. 판정은 방패병의 EnemyShield를 재사용한다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth), typeof(EnemyShield))]
    public sealed class ChiefGoldenShieldSkill : MonoBehaviour
    {
        public EnemyShield shield;
        public VfxGoldenShield vfxPrefab;
        public Vector3 localPosition = new Vector3(0f, 1.15f, .65f);
        [Min(.01f)] public float summonDuration = .35f;
        [Min(.01f)] public float activeDuration = 600f;
        [Min(0), Tooltip("한 생애에 쓸 수 있는 횟수. 0이면 제한 없이 쿨다운마다 다시 쓴다")]
        public int maxUses = 1;
        [Range(0f, 1f), Tooltip("남은 체력 비율이 이 값 이하가 되어야 시전한다. 0.7이면 체력이 70% 이하일 때. 1이면 조건 없음")]
        public float healthThreshold = 0.7f;
        [Min(0f)] public float cooldown = 8f;
        public enum Phase { Ready, Summoning, Active, Dismissing, Cooldown }
        public Phase State { get; private set; }
        public bool IsCasting => State == Phase.Summoning;
        public float Remaining { get; private set; }
        public int CastCount { get; private set; }
        /// <summary>쓸 수 있는 횟수를 다 썼다. 소환을 시작한 순간 한 번으로 센다(도중에 끊겨도 돌려주지 않는다).</summary>
        public bool IsSpent => maxUses > 0 && CastCount >= maxUses;
        /// <summary>시전할 만큼 체력을 잃었다. 궁지에 몰렸을 때 꺼내는 방패라 처음부터 쓰지 않는다.</summary>
        public bool IsHealthLowEnough => health && health.CurrentHealth <= health.MaxHealth * healthThreshold;
        public VfxGoldenShield Visual => visual;
        /// <summary>모델이 EnemyScaleBuilder로 커진 배율.</summary>
        public float BodyScale { get { var visuals = GetComponent<EnemyVisuals>(); return visuals ? visuals.BodyScale : 1f; } }
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

        public bool TryUse()
        {
            if (!isActiveAndEnabled || !health.IsAlive || !vfxPrefab || State != Phase.Ready || IsSpent || !IsHealthLowEnough ||
                (brain && (!brain.enabled || !brain.AIEnabled)) || (motor && motor.IsDetached) ||
                (melee && melee.IsAttacking) || (bombSkill && bombSkill.IsCasting)) return false;
            if (!visual)
            {
                visual = Instantiate(vfxPrefab, transform);
                // localPosition과 연출 크기는 사람 크기(모델 스케일 1) 기준이다. 커진 몸에 맞춰 같은 배율로 옮기고 키운다.
                float body = BodyScale;
                visual.transform.localPosition = localPosition * body;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = vfxPrefab.transform.localScale * body;
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
            // 내구도가 다한 방패는 남은 시간과 상관없이 곧바로 걷는다. 껍데기만 떠 있으면 막히는지 아닌지 알 수 없다.
            if (State == Phase.Active && shield.IsBroken) { BeginDismiss(); return; }
            Remaining = Mathf.Max(0f, Remaining - Time.deltaTime);
            if (Remaining > 0f) return;
            switch (State)
            {
                case Phase.Summoning:
                    shield.enabled = true; State = Phase.Active; Remaining = activeDuration; break;
                case Phase.Active:
                    BeginDismiss(); break;
                case Phase.Dismissing:
                    visual.gameObject.SetActive(false); State = Phase.Cooldown; Remaining = cooldown; break;
                case Phase.Cooldown: State = Phase.Ready; break;
            }
        }
        void BeginDismiss()
        {
            shield.enabled = false; visual.Dismiss();
            State = Phase.Dismissing; Remaining = visual.dismissTime;
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

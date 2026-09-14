using UnityEngine;

namespace SandGuard.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class ChiefBombThrowSkill : MonoBehaviour
    {
        public ChiefBombProp bombPrefab;
        [Min(.1f)] public float range = 8f;
        [Min(0f)] public float cooldown = 6f;
        [Min(.1f)] public float flightTime = 1f;
        [Min(.05f)] public float fuseAfterLanding = .35f;
        [Min(0f)] public float damage = 20f;
        [Min(.1f)] public float blastRadius = 2.5f;
        public LayerMask damageMask = ~0;
        public Vector3 handOffset = new Vector3(0f, -.05f, .05f);
        public bool IsCasting { get; private set; }
        public float CooldownRemaining { get; private set; }
        public int ThrowCount { get; private set; }
        public ChiefBombProp HeldBomb => held;
        EnemyHealth health;
        EnemyBrain brain;
        EnemyMotor motor;
        EnemyMeleeAttack melee;
        EnemyVisuals visuals;
        ChiefGoldenShieldSkill shieldSkill;
        ChiefBombProp held;
        Vector3 aimPoint;
        float elapsed;
        bool enteredThrow;
        bool releaseRequested;
        GameObject weapon;

        void Awake()
        {
            health = GetComponent<EnemyHealth>(); brain = GetComponent<EnemyBrain>(); motor = GetComponent<EnemyMotor>();
            melee = GetComponent<EnemyMeleeAttack>(); visuals = GetComponent<EnemyVisuals>(); shieldSkill = GetComponent<ChiefGoldenShieldSkill>();
        }
        void OnEnable() { ResetForReuse(); health.StateChanged += OnLifeChanged; }
        void OnDisable() { if (health) health.StateChanged -= OnLifeChanged; Cancel(); }
        void OnLifeChanged(LifeStateChangedInfo info) { if (info.CurrentState != LifeState.Alive) Cancel(); }
        public bool TryUse(ICombatTarget target)
        {
            if (!isActiveAndEnabled || !health.IsAlive || !bombPrefab || IsCasting || CooldownRemaining > 0 ||
                (brain && (!brain.enabled || !brain.AIEnabled)) || (motor && motor.IsDetached) ||
                (shieldSkill && shieldSkill.IsCasting) || (melee && melee.IsAttacking) ||
                target == null || !target.IsTargetable || target.FactionId == health.FactionId ||
                Vector3.Distance(health.HitPosition, target.HitPosition) > range) return false;
            var animator = visuals ? visuals.Animator : null;
            if (!animator || !animator.isHuman || !animator.HasState(0, Animator.StringToHash("Base Layer.Throw"))) return false;
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (!hand) return false;
            aimPoint = target.HitPosition; // 시전 시작 위치를 겨냥하므로 이동해서 피할 수 있다.
            var flat = Vector3.ProjectOnPlane(aimPoint - transform.position, Vector3.up);
            if (flat.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(flat);
            held = Instantiate(bombPrefab); held.Hold(hand); held.transform.position += hand.rotation * handOffset; held.SetFuseLit(true);
            foreach (var child in hand.GetComponentsInChildren<Transform>(true))
                if (child.name == "ChiefScimitar") { weapon = child.gameObject; weapon.SetActive(false); break; }
            IsCasting = true; elapsed = 0; enteredThrow = false;
            melee?.Cancel(); motor?.Stop();
            animator.ResetTrigger("Attack"); animator.Play("Base Layer.Throw", 0, 0f);
            return true;
        }
        /// <summary>Throw 클립의 실제 손 이탈 프레임에서만 호출한다. 중복 이벤트는 무시한다.</summary>
        public void ReleaseBomb()
        {
            if (!IsCasting || !held || !health.IsAlive) return;
            releaseRequested = true;
        }
        void LateUpdate()
        {
            // 이벤트 콜백 중에는 이번 프레임의 뼈 변환이 아직 적용 중일 수 있다.
            // Animator 평가가 끝난 손 위치에서 떼어내야 이전 자세에서 폭탄이 튀어나오지 않는다.
            if (!releaseRequested) return;
            releaseRequested = false;
            if (!IsCasting || !held || !health.IsAlive) return;
            float time = Mathf.Max(.1f, flightTime);
            Vector3 velocity = (aimPoint - held.transform.position - .5f * Physics.gravity * time * time) / time;
            var projectile = held.GetComponent<ChiefBombProjectile>() ?? held.gameObject.AddComponent<ChiefBombProjectile>();
            projectile.Launch(velocity, health, time + fuseAfterLanding, damage, blastRadius, damageMask);
            held = null; ThrowCount++;
        }
        void Update()
        {
            if (!health.IsAlive || (brain && (!brain.enabled || !brain.AIEnabled)) || (motor && motor.IsDetached)) { Cancel(); return; }
            CooldownRemaining = Mathf.Max(0, CooldownRemaining - Time.deltaTime);
            if (!IsCasting) return;
            elapsed += Time.deltaTime;
            var animator = visuals ? visuals.Animator : null;
            if (!animator) { Cancel(); return; }
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName("Throw")) enteredThrow = true;
            if ((enteredThrow && (!state.IsName("Throw") || state.normalizedTime >= 1f)) || elapsed > 10f)
                Cancel(); // 이벤트 누락 시 손에 든 폭탄을 제거한다. 보이지 않는 투척을 하지 않는다.
        }
        public void Cancel()
        {
            releaseRequested = false;
            if (held) { Destroy(held.gameObject); held = null; }
            if (weapon) weapon.SetActive(true); weapon = null;
            if (IsCasting)
            {
                CooldownRemaining = cooldown;
                var animator = visuals ? visuals.Animator : null;
                if (health && health.IsAlive && animator && animator.isActiveAndEnabled)
                    animator.CrossFade("Base Layer.Locomotion", .1f);
            }
            IsCasting = false;
        }
        public void ResetForReuse() { Cancel(); CooldownRemaining = 0; ThrowCount = 0; }
    }
}

using UnityEngine;

namespace Tower
{
    public sealed class AnubisController : MonoBehaviour
    {
        [SerializeField] private TowerStatus status;
        [SerializeField] private TargetSelector selector;
        [SerializeField] private AnubisSkill skill;
        [SerializeField] private AnubisHealth health;
        [SerializeField] private Animator animator;
        [Min(0f)] [SerializeField] private float stopDistance = 1.5f;
        [Min(0f)] [SerializeField] private float deathDespawnDelay = 3f;
        [Min(0f)] [SerializeField] private float maxAttackHeight = 1.5f;
        [Range(0f, 1f)] [SerializeField] private float attackImpactTime = 0.45f;
        [Range(0f, 1f)] [SerializeField] private float skillImpactTime = 0.45f;
        [SerializeField] private MinionReturnSettings returnSettings = new MinionReturnSettings();
        private readonly MinionAttackSequence attackSequence = new MinionAttackSequence();
        private readonly MinionReturnToTower returnToTower = new MinionReturnToTower();
        private Vector3 groundedSpawnPoint;
        private bool hasSpawnPoint;
        private static Collider[] splashBuffer = new Collider[32];
        private readonly System.Collections.Generic.HashSet<System.Guid> splashed =
            new System.Collections.Generic.HashSet<System.Guid>();

        public bool CanAttackHeight(ICombatTarget target) =>
            MinionAttackSequence.HeightReachable(transform, target, maxAttackHeight);

        public void InitializeSpawn(Vector3 point)
        {
            groundedSpawnPoint = point;
            hasSpawnPoint = true;
            returnToTower.Initialize(point);
            attackSequence.Cancel();
        }

        private void OnDisable() => attackSequence.Cancel();

        private float attackTimer;
        private float knockbackTimer;
        private bool dying;

        private static readonly int IsMoving = Animator.StringToHash("isMoving");
        private static readonly int Attack = Animator.StringToHash("Attack");
        private static readonly int UseSkill = Animator.StringToHash("UseSkill");
        private static readonly int Die = Animator.StringToHash("Die");

        public AnubisTowerConfig Config => status != null ? status.anubis : null;

        private void Awake()
        {
            if (status == null) 
                status = GetComponentInParent<TowerStatus>();

            if (selector == null) 
                TryGetComponent(out selector);

            if (skill == null) 
                TryGetComponent(out skill);

            if (health == null) 
                TryGetComponent(out health);

            if (animator == null)
                TryGetComponent(out animator);
        }

        private void LateUpdate()
        {
            // 추적/귀환/대기 모두 지면을 따라가며 사망 애니메이션 중에는 위치를 고정한다.
            if (!dying)
            {
                transform.position = MinionGrounding.Project(transform, transform.position);
                if (attackSequence.Tick(animator, Time.deltaTime)) ApplyAttackImpact();
            }
        }

        private void Update()
        {
            if (Config == null || dying)
                return;

            knockbackTimer -= Time.deltaTime;
            attackTimer -= Time.deltaTime;
            if (!hasSpawnPoint)
                InitializeSpawn(MinionGrounding.Project(transform, Config.spawnPoint != null ? Config.spawnPoint.position : transform.position));
            if (returnToTower.Step(transform, status != null ? status.transform.position : groundedSpawnPoint,
                status != null ? status.detectRange : 12f, returnSettings,
                Config.moveSpeed * 2f, Time.deltaTime, out bool returningMove))
            {
                attackSequence.Cancel();
                animator?.ResetTrigger(Attack);
                animator?.ResetTrigger(UseSkill);
                SetMoving(returningMove);
                return;
            }
            if (attackSequence.Active) { SetMoving(false); return; }

            var target = selector != null ? selector.Target : null;

            // if (target == null) // 기존에는 높이 차이가 큰 적도 수평 사거리만 맞으면 공격했다.
            if (target == null || !CanAttackHeight(target))
            {
                SetMoving(false);
                ReturnToSpawnPoint();
                return;
            }

            Vector3 targetPosition = target.HitPosition;
            targetPosition.y = transform.position.y;
            Vector3 direction = targetPosition - transform.position;

            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Config.rotateSpeed * Time.deltaTime);

            if (direction.sqrMagnitude > stopDistance * stopDistance)
            {
                SetMoving(true);
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, Config.moveSpeed * Time.deltaTime);
                return;
            }

            SetMoving(false);

            // attackTimer -= Time.deltaTime; // Update 상단에서 이동 중에도 감소하도록 옮겼다.

            if (attackTimer > 0f || target.DamageReceiver == null) 
                return;

            attackTimer = Config.attackInterval;

            // 기존 즉시 피해는 실제 애니메이션 진행률의 타격 시점으로 이동했다.
            // target.DamageReceiver.TakeDamage(new DamageInfo(Config.attack, "Ally", null, "tower.anubis", target.HitPosition, direction.normalized));

            if (knockbackTimer > 0f)
            {
                animator?.SetTrigger(Attack);
                attackSequence.Begin(target, "Mutant Swiping", attackImpactTime);
                return;
            }

            animator?.SetTrigger(UseSkill);
            // skill?.Cast(transform.position, transform.forward); // 넉백도 준비 동작이 아닌 타격 시점에 실행.
            attackSequence.Begin(target, "Standing Melee Attack Backhand", skillImpactTime, true);
            knockbackTimer = Config.knockBackCooldown;
        }

        private void ApplyAttackImpact()
        {
            // 충격파는 자신을 중심으로 발동하므로 준비 중 주 대상이 죽거나 이동해도 빗나가지 않는다.
            if (attackSequence.IsSkill && skill != null)
            {
                skill.Cast(transform.position, transform.forward, maxAttackHeight);
                return;
            }
            var target = attackSequence.Target;
            if (!MinionAttackSequence.CanHit(transform, target, stopDistance, maxAttackHeight)) return;
            Vector3 direction = target.HitPosition - transform.position;
            direction.y = 0f;
            var result = target.DamageReceiver.TakeDamage(new DamageInfo(Config.attack, "Ally", null,
                "tower.anubis", target.HitPosition, direction.normalized));
            if (result.WasKilled) health?.RewardKill();
            ApplySplash(target);
        }

        /// <summary>주 대상을 맞힌 지점 주위의 다른 적에게도 비율만큼 나눠 준다. 반경이 0이면 하지 않는다.</summary>
        /// <remarks>일반 공격에만 적용한다. 스킬은 자신 중심의 충격파로 별도 판정한다.</remarks>
        private void ApplySplash(ICombatTarget primary)
        {
            float amount = Config.attack * Config.splashRatio;

            if (Config.splashRadius <= 0f || amount <= 0f)
                return;

            Vector3 center = primary.HitPosition;
            splashed.Clear();
            splashed.Add(primary.EntityId); // 주 대상은 이미 온전한 피해를 받았다.
            // 값을 넣지 않은 기존 에셋은 마스크가 Nothing으로 읽혀 아무도 맞지 않는다. 그 경우는 전부로 본다.
            int mask = Config.splashMask == 0 ? ~0 : Config.splashMask.value;
            int count;

            // 신전 지형 콜라이더가 버퍼를 채우면 옆에 있던 적이 조용히 빠진다. 꽉 차면 넓혀 다시 센다.
            while (true)
            {
                count = Physics.OverlapSphereNonAlloc(center, Config.splashRadius, splashBuffer,
                    mask, QueryTriggerInteraction.Ignore);

                if (count < splashBuffer.Length)
                    break;

                System.Array.Resize(ref splashBuffer, splashBuffer.Length * 2);
            }

            for (int i = 0; i < count; i++)
            {
                // 한 대상의 몸과 방패가 따로 겹쳐도 한 번만 맞힌다. 높이 제한은 주 공격과 같은 규칙을 쓴다.
                var other = splashBuffer[i].GetComponentInParent<ICombatTarget>();

                if (!MinionAttackSequence.HeightReachable(transform, other, maxAttackHeight)
                    || other.FactionId == "Ally" || !splashed.Add(other.EntityId)
                    || other.DamageReceiver == null || !InSplashArc(other))
                    continue;

                Vector3 outward = other.HitPosition - center;
                outward.y = 0f;
                var result = other.DamageReceiver.TakeDamage(new DamageInfo(amount, "Ally", null, "tower.anubis.splash",
                    other.HitPosition, outward.sqrMagnitude > 0.001f ? outward.normalized : transform.forward));
                if (result.WasKilled) health?.RewardKill();
            }

            System.Array.Clear(splashBuffer, 0, count);
        }

        /// <summary>바라보는 쪽 splashAngle도 안에 있다. 각도는 아누비스 위치에서 재므로 등 뒤는 번지지 않는다.</summary>
        private bool InSplashArc(ICombatTarget other)
        {
            if (Config.splashAngle >= 360f)
                return true;

            Vector3 flat = other.HitPosition - transform.position;
            flat.y = 0f;
            return flat.sqrMagnitude < 0.001f || Vector3.Angle(transform.forward, flat) <= Config.splashAngle * 0.5f;
        }

        private void ReturnToSpawnPoint()
        {
            Transform spawnPoint = Config.spawnPoint;
            if (spawnPoint == null)
                return;

            Vector3 destination = spawnPoint.position;
            destination.y = transform.position.y;
            Vector3 direction = destination - transform.position;

            if (direction.sqrMagnitude > 0.001f)
            {
                SetMoving(true);
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(direction), Config.rotateSpeed * 5f * Time.deltaTime);
            }
            else
            {
                SetMoving(false);
            }

            transform.position = Vector3.MoveTowards(transform.position, destination,
                Config.moveSpeed * 2f * Time.deltaTime);
        }

        public void Died()
        {
            if (dying) return;
            dying = true;
            attackSequence.Cancel();
            SetMoving(false);
            animator?.SetTrigger(Die);
            StartCoroutine(DespawnAfterDeath());
        }

        private System.Collections.IEnumerator DespawnAfterDeath()
        {
            yield return new WaitForSeconds(deathDespawnDelay);
            GetComponentInParent<AnubisSummoner>()?.Gone();
            Destroy(gameObject);
        }

        private void SetMoving(bool value)
        {
            if (animator != null) animator.SetBool(IsMoving, value);
        }
    }
}

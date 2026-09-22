using System;
using UnityEngine;

namespace Tower
{
    /// <summary>레거시 CreatureMove와 같이 적을 쫓고, 없으면 소환 위치로 돌아간다.</summary>
    public sealed class SkeletonMovement : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private TargetSelector targetSelector;
        [SerializeField] private SkeletonTargetSelector skeletonTargetSelector;
        [SerializeField] private SkeletonController controller;
        [SerializeField] private SkeletonHealth health;

        [Header("Combat")]
        [Min(0f)] [SerializeField] private float stopDistance = 1.5f;
        [Min(0f)] [SerializeField] private float maxAttackHeight = 1.5f;
        [Range(0f, 1f)] [SerializeField] private float attackImpactTime = 0.45f;
        [SerializeField] private MinionReturnSettings returnSettings = new MinionReturnSettings();

        private readonly MinionAttackSequence attackSequence = new MinionAttackSequence();
        private readonly MinionReturnToTower returnToTower = new MinionReturnToTower();
        private TowerStatus towerStatus;

        public bool CanAttackHeight(ICombatTarget target) =>
            MinionAttackSequence.HeightReachable(transform, target, maxAttackHeight);

        private Vector3 summonPoint;
        private float attackTimer;

        private void Awake()
        {
            if (targetSelector == null) TryGetComponent(out targetSelector);
            if (skeletonTargetSelector == null) TryGetComponent(out skeletonTargetSelector);
            if (controller == null) TryGetComponent(out controller);
            if (health == null) TryGetComponent(out health);
        }

        private void OnEnable() => attackTimer = 0f;

        private void OnDisable() => attackSequence.Cancel();

        private void LateUpdate()
        {
            // 추적/귀환은 기존 수평 이동을 유지하고, 대기/공격 중에도 실제 바닥 높이를 반영한다.
            if (controller != null && controller.IsInitialized)
            {
                transform.position = MinionGrounding.Project(transform, transform.position);
                if (attackSequence.Tick(controller.Animator, Time.deltaTime)) ApplyAttackImpact();
            }
            else attackSequence.Cancel();
        }

        public void Initialize(SkeletonController owner, Vector3 point)
        {
            controller = owner;
            summonPoint = point;
            attackTimer = 0f;
            attackSequence.Cancel();
            returnToTower.Initialize(point);
            towerStatus = GetComponentInParent<TowerStatus>();
        }

        private void Update()
        {
            if (controller == null || !controller.IsInitialized || controller.Config == null) return;

            attackTimer -= Time.deltaTime;
            if (returnToTower.Step(transform, towerStatus != null ? towerStatus.transform.position : summonPoint,
                towerStatus != null ? towerStatus.detectRange : 12f, returnSettings,
                controller.Config.moveSpeed * 2f, Time.deltaTime, out bool returningMove))
            {
                attackSequence.Cancel();
                controller.Animator?.ResetTrigger("Attack");
                controller.SetMoving(returningMove);
                return;
            }
            if (attackSequence.Active) { controller.SetMoving(false); return; }

            ICombatTarget target = skeletonTargetSelector != null
                ? skeletonTargetSelector.Target
                : targetSelector != null ? targetSelector.Target : null;
            // if (target == null) // 기존: 다른 층의 적도 수평 거리만으로 공격했다.
            if (target == null || !CanAttackHeight(target))
            {
                ReturnToSummonPoint();
                return;
            }
            FollowAndAttack(target);
        }

        private void FollowAndAttack(ICombatTarget target)
        {
            var config = controller.Config;
            Vector3 destination = target.HitPosition;
            destination.y = transform.position.y;
            Vector3 offset = destination - transform.position;
            float distanceSq = offset.sqrMagnitude;
            float range = Mathf.Max(0f, stopDistance);

            if (distanceSq > 0.0001f) RotateTowards(offset, config.rotateSpeed);
            if (distanceSq > range * range)
            {
                controller.SetMoving(true);
                transform.position = Vector3.MoveTowards(transform.position, destination, Mathf.Max(0f, config.moveSpeed) * Time.deltaTime);
                // attackTimer = 0f; // 추적할 때 공격 쿨다운을 없애 연속 타격하던 동작 보존.
                return;
            }

            controller.SetMoving(false);
            // attackTimer -= Time.deltaTime; // 이제 Update에서 이동/복귀 중에도 한 번만 감소한다.
            if (attackTimer > 0f || target.DamageReceiver == null) return;
            if (float.IsNaN(config.attackInterval) || float.IsInfinity(config.attackInterval) || config.attackInterval <= 0f) return;
            if (float.IsNaN(config.attack) || float.IsInfinity(config.attack) || config.attack < 0f) return;

            attackTimer = config.attackInterval;
            // Vector3 direction = distanceSq > 0.0001f ? offset.normalized : transform.forward;
            controller.TriggerAttack();
            // 기존: 트리거를 보낸 프레임에 즉시 피해를 줘 준비 동작과 타격이 어긋났다.
            // target.DamageReceiver.TakeDamage(new DamageInfo(config.attack, "Ally", health != null ? health.EntityId : (Guid?)null,
            //     "tower.skeleton", target.HitPosition, direction));
            attackSequence.Begin(target, "Standing Melee Attack Downward", attackImpactTime);
        }

        private void ApplyAttackImpact()
        {
            var target = attackSequence.Target;
            if (!MinionAttackSequence.CanHit(transform, target, stopDistance, maxAttackHeight)) return;
            Vector3 direction = target.HitPosition - transform.position;
            direction.y = 0f;
            target.DamageReceiver.TakeDamage(new DamageInfo(controller.Config.attack, "Ally",
                health != null ? health.EntityId : (Guid?)null, "tower.skeleton", target.HitPosition, direction.normalized));
        }

        private void ReturnToSummonPoint()
        {
            var config = controller.Config;
            Vector3 destination = summonPoint;
            destination.y = transform.position.y;
            Vector3 offset = destination - transform.position;
            if (offset.sqrMagnitude > 0.0001f) RotateTowards(offset, config.rotateSpeed * 5f);
            controller.SetMoving(offset.sqrMagnitude > 0.0001f);
            transform.position = Vector3.MoveTowards(transform.position, destination, Mathf.Max(0f, config.moveSpeed) * 2f * Time.deltaTime);
            // attackTimer = 0f; // 귀환으로 공격 쿨다운을 초기화하지 않는다.
        }

        private void RotateTowards(Vector3 direction, float speed)
        {
            if (direction.sqrMagnitude <= 0.0001f) return;
            Quaternion rotation = Quaternion.LookRotation(direction.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Mathf.Max(0f, speed) * Time.deltaTime);
        }
    }
}

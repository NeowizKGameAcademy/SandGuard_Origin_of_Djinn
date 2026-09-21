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

        public void Initialize(SkeletonController owner, Vector3 point)
        {
            controller = owner;
            summonPoint = point;
            attackTimer = 0f;
        }

        private void Update()
        {
            if (controller == null || !controller.IsInitialized || controller.Config == null) return;

            ICombatTarget target = skeletonTargetSelector != null
                ? skeletonTargetSelector.Target
                : targetSelector != null ? targetSelector.Target : null;
            if (target == null)
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
                attackTimer = 0f;
                return;
            }

            controller.SetMoving(false);
            attackTimer -= Time.deltaTime;
            if (attackTimer > 0f || target.DamageReceiver == null) return;
            if (float.IsNaN(config.attackInterval) || float.IsInfinity(config.attackInterval) || config.attackInterval <= 0f) return;
            if (float.IsNaN(config.attack) || float.IsInfinity(config.attack) || config.attack < 0f) return;

            attackTimer = config.attackInterval;
            Vector3 direction = distanceSq > 0.0001f ? offset.normalized : transform.forward;
            controller.TriggerAttack();
            target.DamageReceiver.TakeDamage(new DamageInfo(config.attack, "Ally", health != null ? health.EntityId : (Guid?)null,
                "tower.skeleton", target.HitPosition, direction));
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
            attackTimer = 0f;
        }

        private void RotateTowards(Vector3 direction, float speed)
        {
            if (direction.sqrMagnitude <= 0.0001f) return;
            Quaternion rotation = Quaternion.LookRotation(direction.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Mathf.Max(0f, speed) * Time.deltaTime);
        }
    }
}

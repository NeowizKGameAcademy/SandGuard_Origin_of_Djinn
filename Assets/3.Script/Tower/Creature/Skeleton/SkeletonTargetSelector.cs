using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tower
{
    /// <summary>
    /// 관 타워의 탐지 결과를 공유하면서, 한 적을 한 스켈레톤만 추적하도록 예약한다.
    /// 풀링된 스켈레톤도 활성화 시 부모 타워의 TargetDetector를 다시 찾는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkeletonTargetSelector : MonoBehaviour
    {
        [SerializeField] private TargetDetector detector;
        [Min(0f)] [SerializeField] private float searchInterval = 0.2f;

        private static readonly Dictionary<Guid, SkeletonTargetSelector> claims = new();

        private ICombatTarget currentTarget;
        private float searchTimer;
        private SkeletonMovement movement;

        public ICombatTarget Target
        {
            get
            {
                if (!IsTargetValid(currentTarget))
                {
                    ReleaseTarget();
                    return null;
                }

                return currentTarget;
            }
        }

        private void Awake() => ResolveDetector();

        private void OnEnable()
        {
            ResolveDetector();
            searchTimer = 0f;
        }

        private void OnDisable() => ReleaseTarget();

        private void LateUpdate()
        {
            searchTimer -= Time.deltaTime;
            if (searchTimer > 0f && Target != null)
                return;

            SelectTarget();
            searchTimer = Mathf.Max(0f, searchInterval);
        }

        private void SelectTarget()
        {
            ResolveDetector();
            if (detector == null || !detector.isActiveAndEnabled)
            {
                ReleaseTarget();
                return;
            }

            ICombatTarget best = null;
            float closestDistance = float.PositiveInfinity;
            // 예약되지 않은 적이 하나도 없을 때 쓰는 차선책. 보스만 남으면 한 마리도 달려들지 않던 문제를 막는다.
            ICombatTarget claimedFallback = null;
            float closestClaimed = float.PositiveInfinity;

            foreach (ICombatTarget candidate in detector.Targets)
            {
                // if (!IsTargetValid(candidate) || IsClaimedByAnother(candidate)) // 기존: 예약된 적은 후보에서 아예 빠졌다.
                if (!IsTargetValid(candidate))
                    continue;

                float distance = (candidate.HitPosition - transform.position).sqrMagnitude;
                if (IsClaimedByAnother(candidate))
                {
                    if (distance < closestClaimed)
                    {
                        closestClaimed = distance;
                        claimedFallback = candidate;
                    }

                    continue;
                }

                if (distance >= closestDistance)
                    continue;

                closestDistance = distance;
                best = candidate;
            }

            bool shared = false;
            if (best == null)
            {
                best = claimedFallback;
                shared = true;
            }

            if (best == currentTarget)
                return;

            ReleaseTarget();
            if (best == null)
                return;

            currentTarget = best;
            // 차선책으로 붙을 때는 먼저 예약한 스켈레톤의 소유권을 빼앗지 않는다. 빼앗으면 서로 재탐색만 반복한다.
            if (!shared)
                claims[best.EntityId] = this;
        }

        private bool IsTargetValid(ICombatTarget candidate)
        {
            return candidate != null
                && (movement == null || movement.CanAttackHeight(candidate))
                && detector != null
                && detector.Contains(candidate)
                && detector.IsValidTarget(candidate);
        }

        private bool IsClaimedByAnother(ICombatTarget candidate)
        {
            if (!claims.TryGetValue(candidate.EntityId, out SkeletonTargetSelector owner))
                return false;

            if (owner == null || !owner.isActiveAndEnabled || owner.currentTarget == null)
            {
                claims.Remove(candidate.EntityId);
                return false;
            }

            return owner != this;
        }

        private void ReleaseTarget()
        {
            if (currentTarget != null && claims.TryGetValue(currentTarget.EntityId, out SkeletonTargetSelector owner) && owner == this)
                claims.Remove(currentTarget.EntityId);

            currentTarget = null;
        }

        private void ResolveDetector()
        {
            if (movement == null) TryGetComponent(out movement);
            if (detector == null)
                detector = GetComponentInParent<TargetDetector>();
        }
    }
}

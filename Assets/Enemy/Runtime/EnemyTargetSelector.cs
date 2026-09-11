using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SandGuard.Enemy
{
    /// <summary>주변의 적대 대상을 모아 PriorityTargetPolicy로 하나를 고른다. 길이 막혀 있으면 막은 시설을 최우선 후보에 넣는다.</summary>
    /// <remarks>
    /// 우선순위: 차단 시설 0, 플레이어·소환수 1, 타워 2, 코어 3. 벽은 길을 막았을 때만 후보가 된다.
    /// 후보마다 NavMesh 경로를 확인해 갈 수 없는 대상은 고르지 않는다. 현재 대상은 계속 상대할 수 있으면 유지한다.
    /// </remarks>
    public sealed class EnemyTargetSelector : MonoBehaviour, ITargetSelector
    {
        public EnemyHealth self;
        public EnemyMotor motor;
        public EnemyMeleeAttack attack;
        [Min(0.5f)] public float detectionRadius = 8f;
        [Min(1f), Tooltip("현재 대상이 이 거리 밖으로 벗어나면 놓아 준다")]
        public float loseRadius = 12f;
        [Min(0.5f), Tooltip("길이 막혔을 때 경로 끝 주변에서 차단 시설을 찾는 반경. 못 찾으면 한 번 2배로 넓힌다")]
        public float blockerSearchRadius = 2f;
        [Tooltip("코어가 공격 가능하면 사거리 안에서 공격한다. 도착 즉시 흡수 방식이면 끈다")]
        public bool attackCore = true;
        public LayerMask targetMask = ~0;
        [Min(4)] public int maxColliders = 32;

        struct Entry { public ICombatTarget Target; public Collider Collider; public int Priority; public Vector3 Approach; }

        readonly PriorityTargetPolicy policy = new PriorityTargetPolicy();
        readonly List<TargetCandidate> candidates = new List<TargetCandidate>();
        readonly Dictionary<Guid, Entry> entries = new Dictionary<Guid, Entry>();
        Entry current;
        Collider[] buffer;
        NavMeshPath path;

        public TargetSelection CurrentSelection { get; private set; }
        /// <summary>현재 대상을 공격하러 갈 NavMesh 위 위치. 대상이 없으면 null이다.</summary>
        public Vector3? ApproachPosition { get; private set; }

        public TargetSelection SelectTarget()
        {
            candidates.Clear(); entries.Clear();
            Guid? currentId = CurrentSelection.HasTarget ? CurrentSelection.Target.EntityId : (Guid?)null;
            Gather(transform.position, detectionRadius, false);
            if (motor != null && motor.PathState == MovementPathState.Partial)
            {
                // 경로 끝에 닿기 전에도 실제로 공격 가능한 차단 대상을 선택한다.
                bool requireAttackRange = !motor.IsAtPathEnd;
                int before = candidates.Count;
                Gather(motor.PathEndPosition, blockerSearchRadius, true, requireAttackRange);
                if (candidates.Count == before) Gather(motor.PathEndPosition, blockerSearchRadius * 2f, true, requireAttackRange);
            }
            // 탐지 반경 밖으로 나간 현재 대상도 놓아 줄 거리 안이면 계속 판단한다. 차단 시설은 거리와 무관하다.
            if (currentId.HasValue && !entries.ContainsKey(currentId.Value) && current.Collider != null
                && (current.Priority == 0 || Vector3.Distance(transform.position, current.Target.HitPosition) <= loseRadius))
                Consider(current.Target, current.Collider, current.Priority);

            Guid? chosen = policy.Select(currentId, candidates);
            if (chosen.HasValue && entries.TryGetValue(chosen.Value, out Entry entry))
            {
                current = entry;
                CurrentSelection = TargetSelection.ForTarget(entry.Target);
                ApproachPosition = entry.Approach;
            }
            else ClearTarget();
            return CurrentSelection;
        }

        public void ClearTarget()
        {
            CurrentSelection = default;
            ApproachPosition = null;
            current = default;
        }

        void Gather(Vector3 center, float radius, bool blockers, bool requireAttackRange = false)
        {
            buffer ??= new Collider[maxColliders];
            int count = Physics.OverlapSphereNonAlloc(center, radius, buffer, targetMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var target = buffer[i].GetComponentInParent<ICombatTarget>();
                if (target == null || entries.ContainsKey(target.EntityId)) continue;
                if (requireAttackRange && (attack == null || !attack.IsInRange(target))) continue;
                int priority = Priority(target.Kind, blockers);
                if (priority >= 0) Consider(target, buffer[i], priority);
            }
        }

        int Priority(CombatTargetKind kind, bool blockers)
        {
            switch (kind)
            {
                case CombatTargetKind.Player:
                case CombatTargetKind.Minion: return 1;
                case CombatTargetKind.Tower: return blockers ? 0 : 2;
                case CombatTargetKind.Wall: return blockers ? 0 : -1;
                case CombatTargetKind.Core: return attackCore ? 3 : -1;
                default: return -1;
            }
        }

        void Consider(ICombatTarget target, Collider collider, int priority)
        {
            if (self == null || target.EntityId == self.EntityId || collider == null) return;
            bool hostile = target.IsTargetable && target.FactionId != self.FactionId;
            Vector3 approach = transform.position;
            bool reachable = hostile && TryFindApproach(collider, out approach);
            entries[target.EntityId] = new Entry { Target = target, Collider = collider, Priority = priority, Approach = reachable ? approach : transform.position };
            candidates.Add(new TargetCandidate(target.EntityId, reachable, reachable, priority,
                (target.HitPosition - transform.position).sqrMagnitude));
        }

        /// <summary>대상 충돌체에 가장 가까운 NavMesh 지점까지 길이 있는지 확인한다. 이미 사거리 안이면 지금 자리로 충분하다.</summary>
        bool TryFindApproach(Collider collider, out Vector3 approach)
        {
            approach = transform.position;
            float range = attack != null ? attack.range : 1.5f;
            Vector3 origin = transform.position;
            Vector3 closest = collider.ClosestPoint(origin);
            // 모든 대상에 공격 시작/명중 판정과 동일한 기준점·충돌체·시야 검사를 쓴다.
            var target = collider.GetComponentInParent<ICombatTarget>();
            if (attack != null ? attack.IsInRange(target) : EnemyMotor.Planar(origin, closest) <= range) return true;
            if (motor == null || !motor.IsOnNavMesh) return false;
            if (!NavMesh.SamplePosition(closest, out NavMeshHit hit, range + 1f, motor.Agent.areaMask)) return false;
            path ??= new NavMeshPath();
            if (!NavMesh.CalculatePath(origin, hit.position, motor.Agent.areaMask, path) || path.corners.Length == 0) return false;
            Vector3 end = path.corners[path.corners.Length - 1];
            if (path.status == NavMeshPathStatus.PathComplete) { approach = end; return true; }
            if (path.status == NavMeshPathStatus.PathPartial && EnemyMotor.Planar(end, collider.ClosestPoint(end)) <= range) { approach = end; return true; }
            return false;
        }
    }
}

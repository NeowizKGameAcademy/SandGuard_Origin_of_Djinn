using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SandGuard.Enemy
{
    /// <summary>주변의 적대 대상을 모아 PriorityTargetPolicy로 하나를 고른다. 길이 막혀 있으면 막은 시설을 최우선 후보에 넣는다.</summary>
    /// <remarks>
    /// 기본 우선순위: 차단 시설 0, 플레이어 1, 미니언 2, 타워 3, 코어 4. 종류별 값은 인스펙터(프리팹 변형)에서 적마다 바꿀 수 있다. 벽은 길을 막았을 때만 후보가 된다.
    /// 후보마다 NavMesh 경로를 확인해 갈 수 없는 대상은 고르지 않는다. 현재 대상은 계속 상대할 수 있으면 유지하되,
    /// 더 높은 순위(작은 값)의 대상이 공격 가능해지면 그쪽으로 바꾼다. 차단 시설은 이 갈아타기에 끼지 않는다.
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
        [Tooltip("끄면 타워를 대상으로 고르지 않는다(암살자). 길을 막은 타워는 끼어 멈추지 않도록 그대로 차단 시설로 다룬다")]
        public bool attackTowers = true;
        [Header("어그로 순위 (작을수록 먼저, 1 이상)"), Min(1)] public int playerPriority = 1;
        [Min(1)] public int minionPriority = 2;
        [Min(1), Tooltip("길을 막은 타워는 이 값과 무관하게 0순위(차단 시설)로 다룬다")] public int towerPriority = 3;
        [Min(1)] public int corePriority = 4;
        public LayerMask targetMask = ~0;
        [Min(4)] public int maxColliders = 32;
        [Min(0f), Tooltip("대상까지의 접근 경로 계산 결과를 이 시간(초) 동안 다시 쓴다. 0이면 판단마다 새로 계산한다. 웨이브에 적이 100마리 넘게 몰리면 경로 계산이 프레임을 먹는다")]
        public float approachReuseSeconds = 0.6f;
        [Min(0f), Tooltip("대상 충돌체의 가장 가까운 점이 이만큼 움직이면(움직이는 플레이어 등) 재사용하지 않고 다시 계산한다")]
        public float approachReuseDistance = 0.75f;

        struct Entry { public ICombatTarget Target; public Collider Collider; public int Priority; public Vector3 Approach; }
        struct CachedApproach { public float Time; public Vector3 Origin, Closest, Approach; public bool Reachable; }

        readonly PriorityTargetPolicy policy = new PriorityTargetPolicy();
        readonly List<TargetCandidate> candidates = new List<TargetCandidate>();
        readonly Dictionary<Guid, Entry> entries = new Dictionary<Guid, Entry>();
        readonly Dictionary<Collider, CachedApproach> approachCache = new Dictionary<Collider, CachedApproach>();
        Entry current;
        Collider[] buffer;
        NavMeshPath path;

        public TargetSelection CurrentSelection { get; private set; }
        /// <summary>현재 대상을 공격하러 갈 NavMesh 위 위치. 대상이 없으면 null이다.</summary>
        public Vector3? ApproachPosition { get; private set; }
        /// <summary>
        /// 접근 지점이 허용 범위 안인지 묻는다(예: 경로에서 너무 먼 대상 제외). false면 갈 수 없는 대상처럼 고르지 않는다.
        /// null이면 제한이 없다. 길을 막은 차단 시설(우선순위 0)은 경로 위에 있으므로 묻지 않는다.
        /// </summary>
        public Func<Vector3, bool> ApproachFilter { get; set; }

        // 풀로 돌아갔다 다른 자리에서 되살아나므로 이전 삶의 경로 계산을 버린다.
        void OnDisable() => approachCache.Clear();

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
            if (currentId.HasValue) YieldToHigherPriority(currentId.Value);

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
                case CombatTargetKind.Player: return Mathf.Max(1, playerPriority);
                case CombatTargetKind.Minion: return Mathf.Max(1, minionPriority);
                case CombatTargetKind.Tower: return blockers ? 0 : attackTowers ? Mathf.Max(1, towerPriority) : -1;
                case CombatTargetKind.Wall: return blockers ? 0 : -1;
                case CombatTargetKind.Core: return attackCore ? Mathf.Max(1, corePriority) : -1;
                default: return -1;
            }
        }

        /// <summary>
        /// 순위가 더 높은(값이 작은) 대상이 공격 가능하면 현재 대상의 유지를 풀어 정책이 그쪽을 고르게 한다.
        /// 차단 시설(0)은 길을 뚫는 특수 후보라 현재 대상을 밀어내지도, 밀려나지도 않는다.
        /// </summary>
        void YieldToHigherPriority(Guid currentId)
        {
            int currentIndex = -1, best = int.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate.EntityId == currentId) { currentIndex = i; continue; }
                if (candidate.IsEligible && candidate.Priority > 0 && candidate.Priority < best) best = candidate.Priority;
            }
            if (currentIndex < 0) return;
            var c = candidates[currentIndex];
            if (c.Priority > 0 && c.Priority > best)
                candidates[currentIndex] = new TargetCandidate(c.EntityId, c.IsEligible, false, c.Priority, c.DistanceSquared);
        }

        void Consider(ICombatTarget target, Collider collider, int priority)
        {
            if (self == null || target.EntityId == self.EntityId || collider == null) return;
            bool hostile = target.IsTargetable && target.FactionId != self.FactionId;
            Vector3 approach = transform.position;
            bool reachable = hostile && TryFindApproach(collider, out approach)
                && (priority == 0 || ApproachFilter == null || ApproachFilter(approach));
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
            if (approachReuseSeconds > 0f && approachCache.TryGetValue(collider, out CachedApproach cached)
                && Time.time - cached.Time <= approachReuseSeconds
                && (cached.Closest - closest).sqrMagnitude <= approachReuseDistance * approachReuseDistance
                && (cached.Origin - origin).sqrMagnitude <= 9f) // 밀려나거나 떨어져 멀리 옮겨졌으면 다시 본다
            {
                approach = cached.Reachable ? cached.Approach : origin;
                return cached.Reachable;
            }
            bool reachable = CalculateApproach(collider, origin, closest, range, out approach);
            if (approachReuseSeconds > 0f)
            {
                if (approachCache.Count >= 16) approachCache.Clear(); // 사라진 대상의 항목이 쌓이지 않게 가끔 비운다
                approachCache[collider] = new CachedApproach { Time = Time.time, Origin = origin, Closest = closest, Approach = approach, Reachable = reachable };
            }
            return reachable;
        }

        bool CalculateApproach(Collider collider, Vector3 origin, Vector3 closest, float range, out Vector3 approach)
        {
            approach = origin;
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

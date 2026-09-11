using UnityEngine;
using UnityEngine.AI;

namespace SandGuard.Enemy
{
    /// <summary>NavMeshAgent 기반 이동. 목적지만 받고 길 찾기와 장애물 우회는 NavMesh가 맡는다.</summary>
    /// <remarks>끝까지 갈 수 있는 Complete와 중간까지만 갈 수 있는 Partial을 구분한다. Partial의 끝에 서 있으면 길이 막힌 것이다.</remarks>
    [RequireComponent(typeof(NavMeshAgent))]
    [DefaultExecutionOrder(-100)]
    public sealed class EnemyMotor : MonoBehaviour, IMovementController
    {
        [Min(0f)] public float moveSpeed = 3.5f;
        [Min(0.05f), Tooltip("이 거리 안이면 도착으로 본다")]
        public float arrivalDistance = 0.5f;
        [Min(0.1f), Tooltip("목적지를 NavMesh 위로 옮길 때 허용하는 거리")]
        public float sampleRadius = 3f;
        NavMeshAgent agent;
        NavMeshPath scratch;
        Vector3 destination;

        public NavMeshAgent Agent => agent;
        public Vector3 Position => transform.position;
        public bool HasDestination { get; private set; }
        public MovementPathState PathState { get; private set; }
        public Vector3 PathEndPosition { get; private set; }
        public bool HasArrived => HasDestination && PathState == MovementPathState.Complete && Planar(Position, destination) <= arrivalDistance;
        /// <summary>현재 경로로 갈 수 있는 끝에 서 있다. PathState가 Partial이면 길이 막혀 멈춘 상태다.</summary>
        public bool IsAtPathEnd => HasDestination && PathState != MovementPathState.None && PathState != MovementPathState.Invalid
            && Planar(Position, PathEndPosition) <= arrivalDistance;
        public bool IsOnNavMesh => agent != null && agent.enabled && agent.isOnNavMesh;
        public Vector3 Velocity => agent != null && agent.enabled ? agent.velocity : Vector3.zero;
        bool restrained;
        float displacedUntil, speedMultiplier = 1f;
        bool Held => restrained || Time.time < displacedUntil;
        /// <summary>속박(모래 족쇄). 목적지·경로 상태는 그대로 계산하되 에이전트만 제자리에 세운다. 두뇌의 판단 흐름은 바뀌지 않는다.</summary>
        public bool Restrained
        {
            get => restrained;
            set
            {
                restrained = value;
                if (IsOnNavMesh && HasDestination) agent.isStopped = Held;
            }
        }
        /// <summary>이동 속도 배수(둔화). 1이 기본. 에이전트 속도에 바로 반영된다.</summary>
        public float SpeedMultiplier
        {
            get => speedMultiplier;
            set
            {
                speedMultiplier = Mathf.Max(0f, value);
                if (agent != null) agent.speed = moveSpeed * speedMultiplier;
            }
        }
        /// <summary>낙하 담당(<see cref="EnemyFall"/>). 있으면 가장자리 너머로 밀릴 때 절벽 판정을 맡기고, NavMesh 밖의 밀림도 넘긴다.</summary>
        public EnemyFall Fall { get; set; }
        /// <summary>낙하를 위해 에이전트를 떼어 낸 상태. 죽어서 끈 것과 구분한다.</summary>
        public bool IsDetached { get; private set; }
        /// <summary>외부 힘에 밀린다(소용돌이). 이번 프레임 자기 이동은 멈추고 delta만큼 NavMesh 위에서 옮긴다. 가장자리 너머가 절벽이면 떨어진다. NavMesh 밖이면 그냥 옮긴다.</summary>
        public void Displace(Vector3 delta)
        {
            if (!IsOnNavMesh)
            {
                if (Fall != null && Fall.IsOffMesh) Fall.Push(delta); else transform.position += delta;
                return;
            }
            Vector3 planar = delta; planar.y = 0f;
            if (Fall != null && planar.sqrMagnitude > 1e-8f)
            {
                // 가장자리에 막히기 직전에 미리 본다. 절벽이면 EnemyFall이 에이전트를 떼고 밀림을 이어받는다.
                float lookahead = Mathf.Max(planar.magnitude, 0.15f);
                if (agent.Raycast(transform.position + planar.normalized * lookahead, out NavMeshHit edge) && Fall.TryLeaveLedge(edge.position, delta)) return;
            }
            displacedUntil = Time.time + 0.1f; // 밀림이 이어지는 동안은 자기 걸음을 멈춘다. 끊기면 Update가 다시 걷게 한다
            if (HasDestination) agent.isStopped = true;
            agent.Move(delta);
        }
        /// <summary>낙하를 위해 에이전트를 떼어 낸다. 위치는 부르는 쪽(<see cref="EnemyFall"/>)이 직접 옮긴다.</summary>
        public void Detach()
        {
            if (IsDetached) return;
            Stop();
            if (agent != null) agent.enabled = false;
            IsDetached = true;
        }
        /// <summary>떼어 낸 에이전트를 지정한 NavMesh 지점에 다시 붙인다. 속박·둔화는 그대로 둔다. NavMesh에 올라서지 못하면 false.</summary>
        public bool Reattach(Vector3 position)
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            agent.enabled = true;
            agent.Warp(position);
            HasDestination = false; PathState = MovementPathState.None; destination = position; PathEndPosition = position;
            if (!agent.isOnNavMesh) { agent.enabled = false; return false; }
            IsDetached = false;
            agent.isStopped = Held;
            return true;
        }
        void Update()
        {
            if (displacedUntil > 0f && Time.time >= displacedUntil)
            {
                displacedUntil = 0f;
                if (IsOnNavMesh && HasDestination && !restrained) agent.isStopped = false;
            }
        }

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.speed = moveSpeed * speedMultiplier;
            agent.autoBraking = true;
            agent.autoRepath = true;
            PathEndPosition = transform.position;
        }

        public bool TrySetDestination(Vector3 target)
        {
            if (!IsFinite(target) || !IsOnNavMesh) return false;
            if (!NavMesh.SamplePosition(target, out NavMeshHit hit, sampleRadius, agent.areaMask)) return false;
            if (HasDestination && PathState != MovementPathState.Invalid && Planar(hit.position, destination) <= 0.5f)
            { Refresh(); return true; }
            scratch ??= new NavMeshPath();
            if (!agent.CalculatePath(hit.position, scratch) || scratch.status == NavMeshPathStatus.PathInvalid || scratch.corners.Length == 0) return false;
            if (!agent.SetPath(scratch)) return false;
            agent.isStopped = Held;
            destination = hit.position;
            HasDestination = true;
            Apply(scratch.status, scratch.corners);
            return true;
        }

        public void Stop()
        {
            if (IsOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
            HasDestination = false;
            PathState = MovementPathState.None;
            PathEndPosition = Position;
        }

        /// <summary>NavMesh가 바뀌어 에이전트가 스스로 다시 계산한 경로 상태를 읽어 온다.</summary>
        public void Refresh()
        {
            if (!HasDestination || !IsOnNavMesh || agent.pathPending) return;
            if (!agent.hasPath) { PathState = MovementPathState.Invalid; PathEndPosition = Position; return; }
            Apply(agent.pathStatus, agent.path.corners);
        }

        /// <summary>멈춰 있을 때 대상을 바라본다. 이동 중에는 에이전트가 방향을 정한다.</summary>
        public void Face(Vector3 point, float turnSpeed)
        {
            Vector3 direction = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
            if (direction.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);
        }

        /// <summary>사망 등으로 이동을 완전히 끈다. 다른 에이전트의 회피 대상에서도 빠진다.</summary>
        public void Disable()
        {
            Stop();
            if (agent != null) agent.enabled = false;
        }

        /// <summary>풀 재사용: 지정 위치에 다시 세우고 이동을 켠다.</summary>
        public void Enable(Vector3 position)
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            agent.enabled = true;
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas)) position = hit.position;
            agent.Warp(position);
            IsDetached = false;
            restrained = false; displacedUntil = 0f; SpeedMultiplier = 1f; // 풀 재사용: 상태이상은 새 개체로 넘기지 않는다
            if (agent.isOnNavMesh) agent.isStopped = false;
            HasDestination = false; PathState = MovementPathState.None; destination = position; PathEndPosition = position;
        }

        void Apply(NavMeshPathStatus status, Vector3[] corners)
        {
            PathState = status == NavMeshPathStatus.PathComplete ? MovementPathState.Complete
                : status == NavMeshPathStatus.PathPartial ? MovementPathState.Partial : MovementPathState.Invalid;
            PathEndPosition = PathState == MovementPathState.Invalid || corners.Length == 0 ? Position : corners[corners.Length - 1];
        }

        public static float Planar(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }
        static bool IsFinite(Vector3 v) => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)
            || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
    }
}

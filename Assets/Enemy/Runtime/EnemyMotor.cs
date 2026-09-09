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

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.speed = moveSpeed;
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
            agent.isStopped = false;
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

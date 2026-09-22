using UnityEngine;
using UnityEngine.AI;

namespace Tower
{
    [System.Serializable]
    public sealed class MinionReturnSettings
    {
        [Tooltip("0이면 타워 탐지 범위 사용. 타워 중심에서 벗어날 수 있는 수평 거리.")]
        [Min(0f)] public float radius = 0f;
        [Tooltip("소환 위치보다 이만큼 낮아지면 추적을 중단하고 복귀.")]
        [Min(0.1f)] public float fallHeight = 2f;
        [Tooltip("경로 없음/이동 정체가 이 시간 지속되면 소환 위치로 이동.")]
        [Min(0.1f)] public float stuckTimeout = 2f;
        [Min(0.1f)] public float maxWalkTime = 10f;
    }

    /// <summary>플랫폼 이탈 시 NavMesh 경로로 복귀하고, 경로가 없거나 막히면 소환점으로 이동.</summary>
    public sealed class MinionReturnToTower
    {
        public bool Active { get; private set; }
        private Vector3 home;
        private Vector3 lastPosition;
        private float elapsed;
        private float stuck;
        private float repath;
        // Unity 네이티브 객체는 프리팹 역직렬화 중 생성하지 않고 Step에서 지연 생성한다.
        private NavMeshPath path;
        private Vector3[] corners;
        private int corner;

        public void Initialize(Vector3 spawn) { home = spawn; Active = false; }

        public bool Step(Transform unit, Vector3 towerCenter, float towerRange,
            MinionReturnSettings settings, float speed, float dt, out bool moving)
        {
            moving = false;
            Vector3 radial = unit.position - towerCenter;
            radial.y = 0f;
            float radius = settings.radius > 0f ? settings.radius : Mathf.Max(1f, towerRange);
            if (!Active)
            {
                bool supported = MinionGrounding.TryProject(unit, unit.position, out _)
                    || NavMesh.SamplePosition(unit.position, out _, 0.3f, NavMesh.AllAreas);
                if (supported && home.y - unit.position.y < settings.fallHeight && radial.sqrMagnitude <= radius * radius)
                    return false;
                Active = true;
                elapsed = stuck = repath = 0f;
                lastPosition = unit.position;
                corners = null;
            }

            elapsed += dt;
            stuck = (unit.position - lastPosition).sqrMagnitude > 0.0025f ? 0f : stuck + dt;
            if ((unit.position - lastPosition).sqrMagnitude > 0.0025f) lastPosition = unit.position;
            if ((unit.position - home).sqrMagnitude < 0.25f)
            {
                Active = false;
                return true;
            }
            if (stuck >= settings.stuckTimeout || elapsed >= settings.maxWalkTime)
            {
                // 아래층의 현재 Y로 보정하지 않고, 보존한 소환점 높이에서 지면을 찾는다.
                unit.position = MinionGrounding.Project(unit, home);
                Active = false;
                return true;
            }

            repath -= dt;
            if (repath <= 0f)
            {
                if (path == null) path = new NavMeshPath();
                repath = 0.5f;
                corners = null;
                if (NavMesh.SamplePosition(unit.position, out var from, 0.75f, NavMesh.AllAreas)
                    && NavMesh.SamplePosition(home, out var to, 0.75f, NavMesh.AllAreas)
                    && NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path)
                    && path.status == NavMeshPathStatus.PathComplete)
                {
                    corners = path.corners;
                    corner = 1;
                }
            }
            if (corners == null || corner >= corners.Length) return true;

            Vector3 destination = corners[corner];
            if ((destination - unit.position).sqrMagnitude < 0.04f && ++corner < corners.Length)
                destination = corners[corner];
            Vector3 next = Vector3.MoveTowards(unit.position, destination, Mathf.Max(0f, speed) * dt);
            // 이동 중 생긴 벽/장애물도 통과하지 않고 정체 타이머로 처리한다.
            Vector3 direction = next - unit.position;
            if (direction.sqrMagnitude > 0.000001f
                && !Physics.Raycast(unit.position + Vector3.up * 0.5f, direction.normalized,
                    direction.magnitude + 0.1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                unit.position = next;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.0001f) unit.rotation = Quaternion.LookRotation(direction);
                moving = true;
            }
            return true;
        }
    }
}

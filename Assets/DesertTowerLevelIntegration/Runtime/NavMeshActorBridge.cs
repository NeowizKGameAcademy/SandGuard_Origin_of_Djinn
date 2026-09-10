using UnityEngine;
using UnityEngine.AI;

namespace DesertTower.LevelIntegration
{
    /// <summary>AI가 없는 시험 프리팹용. 완성된 적 AI에는 전용 브리지를 사용하세요.</summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class NavMeshActorBridge : ActorBridge
    {
        NavMeshAgent agent;
        bool alive = true;
        public override bool Alive => alive && gameObject.activeInHierarchy;
        public override bool Prepare(out string error)
        {
            agent = GetComponent<NavMeshAgent>();
            error = agent.enabled && agent.isOnNavMesh ? null : "적이 NavMesh 위에 없습니다.";
            if (error != null) return false;
            agent.stoppingDistance = .1f; return true;
        }
        public override void Travel(Vector3 point)
        {
            if (agent && agent.enabled && agent.isOnNavMesh) { agent.isStopped = false; agent.SetDestination(point); }
        }
        public override void Halt() { if (agent && agent.enabled && agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); } }
        public void NotifyDeath() { alive = false; Halt(); }
        public override void Remove() { alive = false; Halt(); Destroy(gameObject); }
    }
}

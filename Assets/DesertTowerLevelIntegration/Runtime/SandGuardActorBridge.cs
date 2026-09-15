using SandGuard.Enemy;
using UnityEngine;

namespace DesertTower.LevelIntegration
{
    /// <summary>
    /// 경로 러너와 적 AI의 연결. 러너는 목표(다음 노드)와 도착·흡수를 정하고,
    /// 대상 선택·공격·막힌 길·낙하 대응은 <see cref="EnemyBrain"/>이 그대로 판단한다.
    /// </summary>
    [RequireComponent(typeof(EnemyBrain), typeof(EnemyMotor), typeof(EnemyHealth))]
    public sealed class SandGuardActorBridge : ActorBridge
    {
        EnemyBrain brain; EnemyMotor motor; EnemyHealth health;
        EnemyTargetSelector selector; EnemyMeleeAttack attack;
        public override bool Alive => health && health.IsAlive && gameObject.activeInHierarchy;
        public override bool Prepare(out string error)
        {
            brain = GetComponent<EnemyBrain>(); motor = GetComponent<EnemyMotor>(); health = GetComponent<EnemyHealth>();
            selector = GetComponent<EnemyTargetSelector>(); attack = GetComponent<EnemyMeleeAttack>();
            if (!motor || !health || !brain || !motor.IsOnNavMesh) { error = "Enemy 부품/베이크된 NavMesh를 확인하세요."; return false; }
            // 첫 Travel 전까지는 objective(EnemyObjective)로 먼저 걸어가지 않게 멈춰 둔다.
            brain.enabled = true; brain.ReleaseSteering(); brain.AIEnabled = false;
            if (selector) selector.attackCore = false; // 코어는 러너가 도착 즉시 흡수한다.
            motor.Agent.stoppingDistance = .1f;
            error = null; return true;
        }
        public override void Travel(Vector3 point)
        {
            if (!Alive) return;
            brain.AIEnabled = true;
            brain.Steer(point);
        }
        public override void Halt()
        {
            if (brain) { brain.ReleaseSteering(); brain.AIEnabled = false; }
            // Brain은 다음 Update에서 멈추므로, 러너가 같은 프레임에 흡수·정리해도 되도록 바로 세운다.
            if (motor) motor.Stop(); if (attack) attack.Cancel(); if (selector) selector.ClearTarget();
        }
        public override void Remove() { Halt(); if (health) health.TryDespawn(); else Destroy(gameObject); }
    }
}

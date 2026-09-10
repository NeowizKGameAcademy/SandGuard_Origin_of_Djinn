using SandGuard.Enemy;
using UnityEngine;

namespace DesertTower.LevelIntegration
{
    /// <summary>기존 Brain 대신 경로를 지시하고 탐색·공격·체력 컴포넌트를 재사용합니다.</summary>
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
            // Disable the competing destination writer; do not alter source prefabs.
            brain.enabled = false;
            if (selector) selector.attackCore = false;
            motor.Agent.stoppingDistance = .1f;
            error = null; return true;
        }
        public override void Travel(Vector3 point)
        {
            if (!Alive) return;
            motor.Refresh();
            var selection = selector ? selector.SelectTarget() : default;
            if (selection.HasTarget)
            {
                var target = selection.Target;
                if (attack && attack.IsInRange(target)) { motor.Stop(); motor.Face(target.HitPosition, 540); attack.TryAttack(target); return; }
                if (attack && attack.IsAttacking) return;
                motor.TrySetDestination(selector.ApproachPosition ?? target.HitPosition); return;
            }
            if (attack) attack.Cancel();
            motor.TrySetDestination(point);
        }
        public override void Halt() { if (motor) motor.Stop(); if (attack) attack.Cancel(); if (selector) selector.ClearTarget(); }
        public override void Remove() { Halt(); if (health) health.TryDespawn(); else Destroy(gameObject); }
    }
}

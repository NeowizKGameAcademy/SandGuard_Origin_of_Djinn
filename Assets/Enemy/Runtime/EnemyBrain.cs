using System;
using UnityEngine;

namespace SandGuard.Enemy
{
    public enum EnemyBrainState { Idle, Advancing, Engaging, Blocked, Arrived, Dead }

    /// <summary>코어로 진격하다가 범위 안의 적대 대상을 공격한다. 이동·대상 선택·공격은 각 부품에 맡긴다.</summary>
    /// <remarks>
    /// 장애물 우회는 NavMesh가 처리한다. 길이 완전히 막히면 경로 끝에서 막은 시설을 찾아 공격하고, 길이 열리면 다시 진격한다.
    /// 코어 도착 처리기(ICoreInteraction)를 연결하면 도착 시 그 판단을 따르고, 없으면 despawnOnArrival에 따라 스스로 사라진다.
    /// </remarks>
    [DefaultExecutionOrder(100)]
    public sealed class EnemyBrain : MonoBehaviour
    {
        public EnemyHealth health;
        public EnemyMotor motor;
        public EnemyTargetSelector selector;
        public EnemyMeleeAttack attack;
        [Tooltip("진격 목표(코어). 비우면 씬의 EnemyObjective를 쓴다")]
        public Transform objective;
        [Min(0.05f)] public float thinkInterval = 0.2f;
        [Min(0.1f), Tooltip("목표에 이 거리 안으로 오면 도착으로 본다")]
        public float objectiveArrivalDistance = 2.5f;
        [Min(0f)] public float turnSpeed = 540f;
        [Tooltip("코어 도착 처리기가 없을 때 도착하면 스스로 사라진다 (도착 즉시 흡수 방식의 임시 대체)")]
        public bool despawnOnArrival = true;
        public bool AIEnabled { get; set; } = true;
        /// <summary>코어 도착 처리기. 연결하면 도착 시 흡수·공격 지시를 따른다.</summary>
        public ICoreInteraction CoreInteraction { get; set; }
        public EnemyBrainState State { get; private set; }
        public ICombatTarget CurrentTarget => selector != null && selector.CurrentSelection.HasTarget ? selector.CurrentSelection.Target : null;
        public event Action<EnemyBrain> ReachedObjective;
        float nextThink;

        void OnEnable() { if (health != null) health.StateChanged += OnLifeStateChanged; nextThink = 0f; }
        void OnDisable() { if (health != null) health.StateChanged -= OnLifeStateChanged; }

        void Update()
        {
            if (State == EnemyBrainState.Dead) return;
            if (!AIEnabled)
            {
                if (State != EnemyBrainState.Idle) { motor?.Stop(); attack?.Cancel(); selector?.ClearTarget(); State = EnemyBrainState.Idle; }
                return;
            }
            if (Time.time >= nextThink) { nextThink = Time.time + thinkInterval; Think(); }
            if (State == EnemyBrainState.Engaging && motor != null && !motor.HasDestination && CurrentTarget != null)
                motor.Face(CurrentTarget.HitPosition, turnSpeed);
        }

        /// <summary>대상과 목적지를 다시 판단한다. thinkInterval마다 자동으로 불린다.</summary>
        public void Think()
        {
            if (State == EnemyBrainState.Dead) return;
            if (objective == null && EnemyObjective.Current != null) objective = EnemyObjective.Current.transform;
            motor?.Refresh();
            TargetSelection selection = selector != null ? selector.SelectTarget() : default;
            if (selection.HasTarget) { Engage(selection.Target); return; }
            attack?.Cancel();
            if (objective == null || motor == null) { State = EnemyBrainState.Idle; motor?.Stop(); return; }
            if (EnemyMotor.Planar(transform.position, objective.position) <= objectiveArrivalDistance) { Arrive(); return; }
            if (State == EnemyBrainState.Blocked) motor.Stop(); // 길이 바뀌었을 수 있으니 처음부터 다시 계산한다.
            if (!motor.TrySetDestination(objective.position)) { State = EnemyBrainState.Blocked; return; }
            State = motor.PathState == MovementPathState.Partial && motor.IsAtPathEnd ? EnemyBrainState.Blocked : EnemyBrainState.Advancing;
        }

        void Engage(ICombatTarget target)
        {
            State = EnemyBrainState.Engaging;
            if (attack != null && attack.IsInRange(target)) { motor?.Stop(); attack.TryAttack(target); return; }
            if (attack != null && attack.IsAttacking) return; // 휘두르는 중에는 움직이지 않는다.
            Vector3 approach = selector != null && selector.ApproachPosition.HasValue ? selector.ApproachPosition.Value : target.HitPosition;
            if (motor != null && !motor.TrySetDestination(approach)) motor.Stop();
        }

        void Arrive()
        {
            motor.Stop();
            if (CoreInteraction != null && health != null)
            {
                CoreArrivalResult result = CoreInteraction.TryHandleArrival(health);
                if (!result.Outcome.Succeeded) return; // 일시정지 등. 다음 판단에서 다시 시도한다.
                if (result.Disposition == CoreArrivalDisposition.AttackRequired)
                {
                    if (selector != null) selector.attackCore = true; // 다음 판단에서 코어가 후보에 들어온다.
                    return;
                }
            }
            if (State == EnemyBrainState.Arrived) return;
            State = EnemyBrainState.Arrived;
            ReachedObjective?.Invoke(this);
            if (CoreInteraction == null && despawnOnArrival && health != null) health.TryDespawn();
        }

        void OnLifeStateChanged(LifeStateChangedInfo info)
        {
            if (info.CurrentState == LifeState.Alive || State == EnemyBrainState.Dead) return;
            State = EnemyBrainState.Dead;
            attack?.Cancel();
            selector?.ClearTarget();
            motor?.Disable();
        }
    }
}

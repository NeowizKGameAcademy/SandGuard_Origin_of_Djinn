using System;
using UnityEngine;
using UnityEngine.AI;

namespace SandGuard.Enemy
{
    public enum EnemyBrainState { Idle, Advancing, Engaging, Blocked, Arrived, Dead }

    /// <summary>코어로 진격하다가 범위 안의 적대 대상을 공격한다. 이동·대상 선택·공격은 각 부품에 맡긴다.</summary>
    /// <remarks>
    /// 장애물 우회는 NavMesh가 처리한다. 길이 완전히 막히면 경로 끝에서 막은 시설을 찾아 공격하고, 길이 열리면 다시 진격한다.
    /// 코어 도착 처리기(ICoreInteraction)를 연결하면 도착 시 그 판단을 따르고, 없으면 despawnOnArrival에 따라 스스로 사라진다.
    /// 웨이브 경로 진행기가 <see cref="Steer"/>로 목적지를 넘기면 objective 대신 그 지점으로 가고, 도착 판정은 진행기에 맡긴다.
    /// 조종 중에는 경로 구간에서 routeLeash보다 먼 대상은 쫓지 않는다.
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
        [Min(0.05f), Tooltip("상대 없이 진격 중이고 카메라에서 farThinkDistance보다 멀면 이 간격으로 판단한다. 화면에서 먼 적의 탐색·경로 계산을 줄인다")]
        public float farThinkInterval = 0.5f;
        [Min(0f), Tooltip("0이면 거리와 무관하게 항상 thinkInterval을 쓴다")]
        public float farThinkDistance = 30f;
        [Min(0.1f), Tooltip("목표에 이 거리 안으로 오면 도착으로 본다")]
        public float objectiveArrivalDistance = 2.5f;
        [Min(0f)] public float turnSpeed = 540f;
        [Tooltip("코어 도착 처리기가 없을 때 도착하면 스스로 사라진다 (도착 즉시 흡수 방식의 임시 대체)")]
        public bool despawnOnArrival = true;
        [Min(0f), Tooltip("경로 조종 중일 때, 접근 지점이 현재 경로 구간에서 이 거리 안인 대상만 쫓는다. 0이면 제한 없음")]
        public float routeLeash = 6f;
        public bool AIEnabled { get; set; } = true;
        /// <summary>외부 경로 진행기가 목적지를 정하고 있는지. <see cref="Steer"/>로 켜고 <see cref="ReleaseSteering"/>으로 끈다.</summary>
        public bool IsSteered { get; private set; }
        public Vector3 SteerPoint { get; private set; }
        /// <summary>코어 도착 처리기. 연결하면 도착 시 흡수·공격 지시를 따른다.</summary>
        public ICoreInteraction CoreInteraction { get; set; }
        public EnemyBrainState State { get; private set; }
        float stunnedUntil;
        public bool IsStunned => Time.time < stunnedUntil;
        public float StunRemaining => Mathf.Max(0f, stunnedUntil - Time.time);

        /// <summary>공격 준비와 이동을 중단한다. 우두머리의 기절은 최대 0.5초다.</summary>
        public void Stun(float duration)
        {
            if (!isActiveAndEnabled || duration <= 0f || float.IsNaN(duration) || float.IsInfinity(duration)
                || State == EnemyBrainState.Dead || (health != null && !health.IsAlive)) return;
            if (shieldSkill != null || bombSkill != null) duration = Mathf.Min(duration, 0.5f);
            stunnedUntil = Mathf.Max(stunnedUntil, Time.time + duration);
            motor?.Stop();
            attack?.Cancel();
            if (shieldSkill != null && shieldSkill.IsCasting) shieldSkill.Cancel();
            bombSkill?.Cancel();
            nextThink = 0f;
        }
        public ICombatTarget CurrentTarget => selector != null && selector.CurrentSelection.HasTarget ? selector.CurrentSelection.Target : null;
        public event Action<EnemyBrain> ReachedObjective;
        float nextThink;
        Vector3[] leg = new Vector3[0];
        NavMeshPath legPath;
        Func<Vector3, bool> leashFilter;
        ChiefGoldenShieldSkill shieldSkill;
        ChiefBombThrowSkill bombSkill;

        void Awake() { shieldSkill = GetComponent<ChiefGoldenShieldSkill>(); bombSkill = GetComponent<ChiefBombThrowSkill>(); }

        void OnEnable()
        {
            if (health != null) health.StateChanged += OnLifeStateChanged;
            nextThink = 0f;
            if (selector != null) selector.ApproachFilter = leashFilter ??= WithinRouteLeash;
        }
        void OnDisable()
        {
            stunnedUntil = 0f;
            if (health != null) health.StateChanged -= OnLifeStateChanged;
            if (selector != null && selector.ApproachFilter == leashFilter) selector.ApproachFilter = null;
        }

        /// <summary>
        /// 목적지를 외부에서 정한다(웨이브 경로의 다음 노드). 매 프레임 같은 지점으로 불러도 된다.
        /// 지점이 바뀌면 지금 자리에서 그 지점까지의 경로를 이탈 한도 기준선으로 기록하고 바로 다시 판단한다.
        /// </summary>
        public void Steer(Vector3 point)
        {
            if (IsSteered && (point - SteerPoint).sqrMagnitude <= 0.25f) return;
            IsSteered = true;
            SteerPoint = point;
            legPath ??= new NavMeshPath();
            leg = motor != null && motor.TryCalculatePath(point, legPath)
                ? legPath.corners
                : new[] { transform.position, point };
            nextThink = 0f;
        }

        /// <summary>외부 조종을 끝낸다. 이후에는 objective를 향한 원래 판단으로 돌아간다.</summary>
        public void ReleaseSteering()
        {
            IsSteered = false;
            leg = new Vector3[0];
        }

        /// <summary>조종 중이면 접근 지점이 현재 경로 구간(꺾인 선)에서 routeLeash 안인지 본다.</summary>
        public bool WithinRouteLeash(Vector3 approach)
        {
            if (!IsSteered || routeLeash <= 0f || leg.Length == 0) return true;
            float limit = routeLeash * routeLeash;
            if ((approach - leg[0]).sqrMagnitude <= limit) return true;
            for (int i = 1; i < leg.Length; i++)
                if (SqrDistanceToSegment(approach, leg[i - 1], leg[i]) <= limit) return true;
            return false;
        }

        static float SqrDistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float lengthSqr = ab.sqrMagnitude;
            float t = lengthSqr > 1e-6f ? Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSqr) : 0f;
            return (point - (a + ab * t)).sqrMagnitude;
        }

        void Update()
        {
            if (State == EnemyBrainState.Dead) return;
            if (IsStunned) { motor?.Stop(); return; }
            if (!AIEnabled)
            {
                if (State != EnemyBrainState.Idle) { motor?.Stop(); attack?.Cancel(); selector?.ClearTarget(); State = EnemyBrainState.Idle; }
                return;
            }
            if (Time.time >= nextThink) { nextThink = Time.time + CurrentThinkInterval(); Think(); }
            if (State == EnemyBrainState.Engaging && motor != null && !motor.HasDestination && CurrentTarget != null)
                motor.Face(CurrentTarget.HitPosition, turnSpeed);
        }

        /// <summary>싸우는 중이거나 카메라 가까이 있으면 thinkInterval, 상대 없이 멀리서 걷는 중이면 farThinkInterval.</summary>
        float CurrentThinkInterval()
        {
            if (State != EnemyBrainState.Advancing || farThinkDistance <= 0f || farThinkInterval <= thinkInterval) return thinkInterval;
            var view = Camera.main;
            if (view == null) return thinkInterval;
            return (view.transform.position - transform.position).sqrMagnitude > farThinkDistance * farThinkDistance ? farThinkInterval : thinkInterval;
        }

        /// <summary>대상과 목적지를 다시 판단한다. thinkInterval마다 자동으로 불린다.</summary>
        public void Think()
        {
            if (State == EnemyBrainState.Dead) return;
            if (IsStunned) { motor?.Stop(); attack?.Cancel(); return; }
            if (shieldSkill != null && shieldSkill.isActiveAndEnabled && shieldSkill.IsCasting)
            { motor?.Stop(); return; }
            if (bombSkill != null && bombSkill.isActiveAndEnabled && bombSkill.IsCasting)
            { motor?.Stop(); return; }
            if (motor != null && motor.IsDetached) // 떨어지는 중·복귀 중: 공중에서 휘두르지 않는다. EnemyFall이 다시 붙이면 다음 판단부터 이어 간다
            { attack?.Cancel(); selector?.ClearTarget(); State = EnemyBrainState.Idle; return; }
            if (!IsSteered && objective == null && EnemyObjective.Current != null) objective = EnemyObjective.Current.transform;
            motor?.Refresh();
            if (shieldSkill != null && shieldSkill.TryUse()) return;
            TargetSelection selection = selector != null ? selector.SelectTarget() : default;
            if (selection.HasTarget) { Engage(selection.Target); return; }
            attack?.Cancel();
            // 탐지 반경 밖이라 싸울 상대가 없어도, 폭탄 사거리 안의 타워에는 던진다. 대상은 스킬이 직접 찾는다.
            // 시전 중에는 위의 IsCasting 검사가 걸음을 멈추므로 여기서 State를 바꾸지 않는다.
            if (bombSkill != null && bombSkill.TryUse(null)) return;
            if (motor == null || (!IsSteered && objective == null)) { State = EnemyBrainState.Idle; motor?.Stop(); return; }
            // 조종 중에는 도착 판정·흡수를 진행기가 한다. 여기서는 지점까지 걷기만 한다.
            Vector3 goal = IsSteered ? SteerPoint : objective.position;
            if (!IsSteered && EnemyMotor.Planar(transform.position, goal) <= objectiveArrivalDistance) { Arrive(); return; }
            if (State == EnemyBrainState.Blocked) motor.Stop(); // 길이 바뀌었을 수 있으니 처음부터 다시 계산한다.
            if (!motor.TrySetDestination(goal)) { State = EnemyBrainState.Blocked; return; }
            State = motor.PathState == MovementPathState.Partial && motor.IsAtPathEnd ? EnemyBrainState.Blocked : EnemyBrainState.Advancing;
        }

        void Engage(ICombatTarget target)
        {
            State = EnemyBrainState.Engaging;
            if (bombSkill != null && bombSkill.TryUse(target)) return;
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

        /// <summary>풀 재사용: 사망 상태를 풀고 처음부터 판단한다.</summary>
        public void ResetForReuse()
        {
            stunnedUntil = 0f;
            State = EnemyBrainState.Idle; nextThink = 0f; AIEnabled = true;
            shieldSkill?.ResetForReuse();
            bombSkill?.ResetForReuse();
            ReleaseSteering();
            attack?.Cancel(); selector?.ClearTarget();
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace SandGuard.Enemy
{
    public enum EnemyFallState { OnNavMesh, Pushed, Falling, Recovering }

    /// <summary>
    /// 적 낙사와 NavMesh 복귀. NavMeshAgent는 스스로 떨어지지 않으므로, 밀림(모래 소용돌이)이 NavMesh 가장자리 너머 절벽으로 향하면
    /// 에이전트를 떼어 내고(<see cref="EnemyMotor.Detach"/>) 직접 중력을 적용한다.
    /// 착지 시 떨어진 높이가 <see cref="fatalDropHeight"/> 이상이면 죽고, 아니면 가장 가까운 NavMesh 지점으로 걸어가 에이전트를 다시 붙인다.
    /// 막혀서 나아가지 못하면 그 지점으로 옮기고, 바닥 없이 <see cref="voidDepth"/>만큼 떨어지면 죽은 뒤 시체 없이 바로 제거된다.
    /// </summary>
    /// <remarks>플레이어 낙사와는 다르다: 적은 밀려야만 떨어지고, 부활하지 않으며, 낙사는 플레이어의 처치 수단이다(피해 출처 <see cref="fallFactionId"/>).</remarks>
    [DefaultExecutionOrder(-95)] // 모터(-100) 다음, 상태이상(-90) 전. 밀림은 EnemyRestraint → EnemyMotor.Displace → 여기로 온다.
    public sealed class EnemyFall : MonoBehaviour
    {
        public EnemyMotor motor;
        public EnemyHealth health;
        [Header("절벽 판정")]
        [Min(0f), Tooltip("NavMesh 가장자리 너머 바닥이 이보다 낮으면 절벽으로 본다. 굽기 agentClimb(0.75)보다 커야 한다")]
        public float ledgeDrop = 0.8f;
        [Min(0.05f), Tooltip("가장자리에서 밀리는 방향으로 이 거리와 그 두 배 지점의 바닥을 본다 (에이전트 반지름에 더한다)")]
        public float ledgeProbe = 0.6f;
        [Header("낙하")]
        [Min(0f), Tooltip("이 높이 이상 떨어져 착지하면 죽는다")]
        public float fatalDropHeight = 5f;
        [Min(1f), Tooltip("떨어지기 시작한 높이에서 이만큼 아래로 내려가면 바닥이 없어도 죽고 바로 제거된다")]
        public float voidDepth = 30f;
        [Tooltip("바닥으로 보는 층. 트리거는 무시한다")]
        public LayerMask groundMask = ~0;
        [Tooltip("낙사 피해의 가해 진영. 적 진영과 달라야 피해가 들어간다")]
        public string fallFactionId = "World";
        public string fallCauseId = "fall";
        [Header("복귀")]
        [Min(0.5f), Tooltip("착지 뒤 NavMesh를 찾는 반경")]
        public float searchRadius = 8f;
        [Min(0.1f), Tooltip("이 시간 동안 나아가지 못하면 가장 가까운 NavMesh 지점(없으면 떨어지기 전 자리)으로 옮긴다")]
        public float stuckSeconds = 1.5f;
        [Min(0f), Tooltip("걸어 돌아갈 때 바닥을 따라 내려갈 수 있는 높이. 이보다 깊으면 다시 떨어진다")]
        public float stepDown = 0.5f;
        public UnityEvent onFell = new UnityEvent();
        public UnityEvent onLanded = new UnityEvent();
        public UnityEvent onReturned = new UnityEvent();

        /// <summary>바닥을 잃고 떨어지기 시작했다.</summary>
        public event Action Fell;
        /// <summary>(떨어진 높이, 치명 여부). 치명이면 사망 처리 뒤에 알린다.</summary>
        public event Action<float, bool> Landed;
        /// <summary>NavMesh에 다시 붙었다. 인자는 걸어서 돌아왔는지(false면 옮겨졌다).</summary>
        public event Action<bool> Returned;

        public EnemyFallState State { get; private set; }
        public bool IsOffMesh => State != EnemyFallState.OnNavMesh;
        /// <summary>떨어지기 시작한 높이. 착지 판정의 기준이다.</summary>
        public float FallStartY { get; private set; }
        /// <summary>마지막으로 NavMesh 위에 있던 자리. 복귀에 실패하면 여기로 옮긴다.</summary>
        public Vector3 LastNavMeshPosition { get; private set; }
        /// <summary>떨어지는 중이면 지금까지, 착지했으면 마지막 낙하의 높이.</summary>
        public float DropHeight => State == EnemyFallState.Falling ? FallStartY - transform.position.y : lastDrop;
        public int FallCount { get; private set; }
        public int ReturnCount { get; private set; }

        NavMeshAgent agent;
        float verticalVelocity, pushedUntil, stuckTimer, lastDrop;
        Vector3 lastProgress;
        static readonly RaycastHit[] hits = new RaycastHit[16];
        const float PushHold = 0.1f; // 밀림이 이어지는 것으로 보는 시간. EnemyMotor.Displace와 같다
        float Radius => agent != null ? agent.radius : 0.35f;
        float Height => agent != null ? agent.height : 2f;
        int AreaMask => agent != null ? agent.areaMask : NavMesh.AllAreas;

        void Awake()
        {
            if (motor == null) motor = GetComponent<EnemyMotor>();
            if (health == null) health = GetComponent<EnemyHealth>();
            agent = GetComponent<NavMeshAgent>();
            if (motor != null) motor.Fall = this;
            LastNavMeshPosition = transform.position;
        }

        /// <summary>
        /// 밀림이 NavMesh 가장자리에 막혔을 때 모터가 부른다. 너머가 절벽이면 에이전트를 떼고 밀림을 그대로 적용한 뒤 true를 돌려준다.
        /// 벽이거나 바닥이 이어지면 false를 돌려주고 모터는 전처럼 가장자리에 멈춘다.
        /// </summary>
        public bool TryLeaveLedge(Vector3 edgePoint, Vector3 push)
        {
            if (State != EnemyFallState.OnNavMesh || motor == null) return false;
            if (health != null && !health.IsAlive) return false;
            Vector3 direction = push; direction.y = 0f;
            if (direction.sqrMagnitude < 1e-8f) return false;
            direction.Normalize();
            Vector3 center = transform.position + Vector3.up * (Height * 0.5f);
            float far = Radius + ledgeProbe * 2f;
            Vector3 farProbe = edgePoint + direction * far;
            Vector3 toFar = farProbe - transform.position; toFar.y = 0f;
            if (Blocked(center, direction, toFar.magnitude)) return false; // 벽
            // 가까운 지점 또는 먼 지점 어느 한쪽에서 바닥이 ledgeDrop 안에 있으면 절벽이 아니다. 둘 다 없어야 절벽이다.
            if (GroundBelow(edgePoint + direction * (Radius + ledgeProbe), ledgeDrop, out _)) return false;
            if (GroundBelow(farProbe, ledgeDrop, out _)) return false;
            LeaveNavMesh();
            Push(push);
            return true;
        }

        /// <summary>지금 자리에서 에이전트를 떼어 낸다. 바닥이 없으면 떨어지고, 있으면 잠시 뒤 NavMesh로 돌아간다. 테스트·데모용.</summary>
        public void LeaveNavMesh()
        {
            if (State != EnemyFallState.OnNavMesh || motor == null) return;
            if (health != null && !health.IsAlive) return;
            LastNavMeshPosition = transform.position;
            motor.Detach();
            verticalVelocity = 0f;
            pushedUntil = Time.time + PushHold;
            State = EnemyFallState.Pushed;
        }

        /// <summary>NavMesh 밖에서 밀린다. 수평으로만 옮기고 높이는 중력이 정한다. 복귀 중이면 복귀를 멈춘다.</summary>
        public void Push(Vector3 delta)
        {
            if (!IsOffMesh) return;
            delta.y = 0f;
            transform.position += delta;
            pushedUntil = Time.time + PushHold;
            if (State == EnemyFallState.Recovering) State = EnemyFallState.Pushed;
        }

        void Update()
        {
            if (State == EnemyFallState.OnNavMesh) return;
            if (health != null && !health.IsAlive) return;
            float dt = Time.deltaTime;
            switch (State)
            {
                case EnemyFallState.Pushed:
                    if (!FollowGround()) { BeginFalling(); break; }
                    if (Time.time >= pushedUntil) BeginRecovering();
                    break;
                case EnemyFallState.Falling: Fall(dt); break;
                case EnemyFallState.Recovering: Recover(dt); break;
            }
        }

        void BeginFalling()
        {
            State = EnemyFallState.Falling;
            FallStartY = transform.position.y;
            verticalVelocity = 0f;
            FallCount++;
            Fell?.Invoke();
            onFell.Invoke();
        }

        void Fall(float dt)
        {
            verticalVelocity += Physics.gravity.y * dt;
            float drop = -verticalVelocity * dt;
            if (GroundBelow(transform.position, drop + 0.05f, out RaycastHit ground))
            {
                Vector3 position = transform.position; position.y = ground.point.y; transform.position = position;
                verticalVelocity = 0f;
                Land();
                return;
            }
            transform.position += Vector3.down * drop;
            if (FallStartY - transform.position.y >= voidDepth) Die(true);
        }

        void Land()
        {
            float drop = FallStartY - transform.position.y;
            lastDrop = drop;
            bool fatal = drop >= fatalDropHeight;
            if (fatal) Die(false); // 알림을 받는 쪽은 항상 최종 상태를 본다
            Landed?.Invoke(drop, fatal);
            onLanded.Invoke();
            if (fatal) return;
            if (Time.time < pushedUntil) State = EnemyFallState.Pushed; else BeginRecovering();
        }

        void Die(bool intoVoid)
        {
            if (health == null || !health.IsAlive) return;
            var damage = new DamageInfo(Mathf.Max(health.CurrentHealth, 1f), fallFactionId, causeId: fallCauseId,
                hitPosition: transform.position, hitDirection: Vector3.down);
            health.TakeDamage(damage);
            if (intoVoid) health.TryDespawn(); // 보이지 않는 곳으로 사라졌으니 시체 연출 없이 바로 제거한다
        }

        void BeginRecovering()
        {
            State = EnemyFallState.Recovering;
            stuckTimer = 0f;
            lastProgress = transform.position;
        }

        void Recover(float dt)
        {
            if (TryReattachHere()) return;
            bool found = NavMesh.SamplePosition(transform.position, out NavMeshHit nav, searchRadius, AreaMask);
            if (found)
            {
                Vector3 to = nav.position - transform.position; to.y = 0f;
                float distance = to.magnitude;
                if (distance > 0.01f)
                {
                    Vector3 direction = to / distance;
                    float speed = motor != null && !motor.Restrained ? motor.moveSpeed * motor.SpeedMultiplier : 0f;
                    float step = Mathf.Min(speed * dt, distance);
                    Vector3 center = transform.position + Vector3.up * (Height * 0.5f);
                    if (step > 0f && !Blocked(center, direction, Radius + step)) transform.position += direction * step;
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 540f * dt);
                }
            }
            if (!FollowGround()) { BeginFalling(); return; }
            if (EnemyMotor.Planar(transform.position, lastProgress) > 0.05f) { lastProgress = transform.position; stuckTimer = 0f; return; }
            stuckTimer += dt;
            if (stuckTimer < stuckSeconds) return;
            // 벽에 막혔거나 닿을 NavMesh가 없다: 가장 가까운 지점, 그것도 없으면 떨어지기 전 자리로 옮긴다.
            if (!Reattach(found ? nav.position : LastNavMeshPosition, false) && !Reattach(LastNavMeshPosition, false)) stuckTimer = 0f;
        }

        bool TryReattachHere()
        {
            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit nav, 1f, AreaMask)) return false;
            if (EnemyMotor.Planar(nav.position, transform.position) > 0.35f || Mathf.Abs(nav.position.y - transform.position.y) > 1f) return false;
            return Reattach(nav.position, true);
        }

        bool Reattach(Vector3 position, bool walked)
        {
            if (motor == null || !motor.Reattach(position)) return false;
            State = EnemyFallState.OnNavMesh;
            LastNavMeshPosition = position;
            ReturnCount++;
            Returned?.Invoke(walked);
            onReturned.Invoke();
            return true;
        }

        /// <summary>발밑 stepDown 안에 바닥이 있으면 그 높이에 붙이고 true, 없으면 false.</summary>
        bool FollowGround()
        {
            if (!GroundBelow(transform.position, stepDown, out RaycastHit ground)) return false;
            Vector3 position = transform.position; position.y = ground.point.y; transform.position = position;
            return true;
        }

        /// <summary>feet 아래 maxDrop 안의 가장 높은 바닥. 몸통 중심에서 아래로 여러 줄을 쏘고 자기 충돌체는 뺀다.</summary>
        bool GroundBelow(Vector3 feet, float maxDrop, out RaycastHit best)
        {
            best = default;
            float half = Height * 0.5f, spread = Radius * 0.5f;
            float distance = half + maxDrop;
            bool found = false;
            for (int i = 0; i < 5; i++)
            {
                Vector3 offset = i == 0 ? Vector3.zero : i == 1 ? new Vector3(spread, 0f, 0f) : i == 2 ? new Vector3(-spread, 0f, 0f)
                    : i == 3 ? new Vector3(0f, 0f, spread) : new Vector3(0f, 0f, -spread);
                Vector3 origin = feet + Vector3.up * half + offset;
                int count = Physics.RaycastNonAlloc(origin, Vector3.down, hits, distance, groundMask, QueryTriggerInteraction.Ignore);
                for (int h = 0; h < count; h++)
                {
                    var hit = hits[h];
                    if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;
                    if (!found || hit.point.y > best.point.y) { best = hit; found = true; }
                }
            }
            return found;
        }

        bool Blocked(Vector3 origin, Vector3 direction, float distance)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, groundMask, QueryTriggerInteraction.Ignore);
            for (int h = 0; h < count; h++)
                if (hits[h].collider != null && !hits[h].collider.transform.IsChildOf(transform)) return true;
            return false;
        }

        /// <summary>풀 재사용: 낙하 상태를 지운다. 모터는 EnemyMotor.Enable이 다시 붙인다.</summary>
        public void ResetForReuse()
        {
            State = EnemyFallState.OnNavMesh;
            verticalVelocity = 0f; pushedUntil = 0f; stuckTimer = 0f; lastDrop = 0f;
            LastNavMeshPosition = transform.position;
        }
    }
}

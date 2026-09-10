using System;
using UnityEngine;

namespace SandGuard.Player
{
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(-200)]
    public sealed class PlayerMotor : MonoBehaviour, IPlayerMobilityReader
    {
        public PlayerInputReader input;
        public Transform view;
        [Tooltip("IManaWallet 구현 컴포넌트를 연결합니다.")]
        public MonoBehaviour manaSource;
        [Tooltip("ILifeState 구현 컴포넌트를 연결합니다.")]
        public MonoBehaviour lifeSource;
        [Header("대시")]
        [Min(0)] public int dashManaCost = 10;
        [Min(0f)] public float dashCooldown = 1f;
        [Min(0.01f)] public float dashDuration = 0.2f;
        [Min(0.01f)] public float dashDistance = 4f;
        public bool allowAirDash = true;
        public float DashCooldownRemaining { get; private set; }
        public bool IsDashing => dashRemaining > 0f && Alive;
        public event Action<PlayerMobilityState> Changed;
        public event Action DashStarted;
        /// <summary>지상 점프와 공중 점프 모두. 어느 쪽인지는 <see cref="LastJumpWasAirJump"/>로 구분한다.</summary>
        public event Action Jumped;
        /// <summary>공중에서 바닥에 닿은 순간. 인자는 충돌 속도(m/s, 0 이상).</summary>
        public event Action<float> Landed;
        /// <summary>마지막 <see cref="Jumped"/>가 공중 추가 점프였는지.</summary>
        public bool LastJumpWasAirJump { get; private set; }
        /// <summary>공중에 있을 때의 수직 속도(위가 양수). 접지 중에는 0. 생성 뒤 처음 바닥에 닿기 전(스폰 낙하)에도 0을 보고해 낙하 동작이 나오지 않게 한다.</summary>
        public float VerticalSpeed => IsGrounded || !groundedSinceSpawn ? 0f : verticalVelocity;
        bool groundedSinceSpawn;
        IPlayerInput Input => input;
        IManaWallet Mana => manaSource as IManaWallet;
        bool Alive => lifeSource == null || (lifeSource as ILifeState)?.State == global::LifeState.Alive;
        float dashRemaining;
        Vector3 dashDirection;
        PlayerMobilityState lastState;
        public PlayerMobilityState State => new PlayerMobilityState(IsGrounded, RemainingAirJumps, dashManaCost,
            DashCooldownRemaining, dashCooldown, JumpAvailability(), DashAvailability());
        [Header("이동")]
        [Min(0f)] public float moveSpeed = 5f;
        [Min(0.01f), Tooltip("지상에서 입력 방향으로 붙는 속도")] public float groundAcceleration = 60f;
        [Min(0.01f), Tooltip("지상에서 입력을 떼면 멈추는 속도. 가속보다 크게 두면 멈춤이 즉각적이다")] public float groundDeceleration = 90f;
        [Min(0.01f)] public float airAcceleration = 18f;
        [Min(0.01f)] public float airDeceleration = 8f;
        [Header("점프")]
        [Min(0.1f)] public float jumpHeight = 1.5f;
        [Min(0.1f)] public float gravity = 25f;
        [Min(1f), Tooltip("하강 중 중력 배수. 포물선의 뒷부분을 빠르게 만든다")] public float fallGravityMultiplier = 2f;
        [Min(1f), Tooltip("상승 중 점프 버튼을 떼면 적용하는 중력 배수 (짧게 누르면 낮게 뛴다)")] public float lowJumpGravityMultiplier = 2.5f;
        [Min(0)] public int extraAirJumps = 1;
        [Min(0f), Tooltip("모서리에서 떨어진 직후에도 지상 점프를 허용하는 시간")] public float coyoteTime = 0.1f;
        [Min(0f), Tooltip("착지 직전에 누른 점프를 기억하는 시간")] public float jumpBufferTime = 0.12f;
        [Header("접지")]
        [Min(0f), Tooltip("접지 중 바닥으로 누르는 속도")] public float groundStickSpeed = 4f;
        [Min(0f), Tooltip("경사·턱을 내려갈 때 이 거리 안의 바닥에 붙인다. 0이면 끈다")] public float groundSnapDistance = 0.35f;
        public LayerMask groundMask = ~0;
        [Header("회전")]
        [Min(0f)] public float turnSpeed = 720f;
        [Tooltip("항상 카메라 정면을 본다 (슈터식). 끄면 이동 방향을 보고 공격·대시 때만 카메라를 본다")]
        public bool alwaysFaceCamera = false;
        [Min(0f), Tooltip("공격 뒤 카메라 정면을 유지하는 시간")] public float faceCameraSeconds = 1.5f;
        public float recoveryY = -20f;
        public bool MovementEnabled { get; set; } = true;
        public bool IsGrounded { get; private set; }
        public int RemainingAirJumps { get; private set; }
        public Vector3 Velocity => controller != null ? controller.velocity : Vector3.zero;
        CharacterController controller;
        Vector3 horizontalVelocity, spawnPosition;
        Vector2 localVelocity; // 카메라 기준 (x 우, y 전). 시점을 돌려도 몸이 흐르지 않도록 로컬로 보관한다.
        float verticalVelocity, coyoteTimer, jumpBufferTimer, faceCameraTimer;

        void OnValidate()
        {
            dashManaCost = Mathf.Max(0, dashManaCost);
            if (float.IsNaN(dashCooldown) || float.IsInfinity(dashCooldown)) dashCooldown = 1f;
            dashCooldown = Mathf.Max(0f, dashCooldown);
            if (float.IsNaN(dashDuration) || float.IsInfinity(dashDuration)) dashDuration = 0.2f;
            dashDuration = Mathf.Max(0.01f, dashDuration);
            if (float.IsNaN(dashDistance) || float.IsInfinity(dashDistance)) dashDistance = 4f;
            dashDistance = Mathf.Max(0.01f, dashDistance);
            DashCooldownRemaining = Mathf.Min(DashCooldownRemaining, dashCooldown);
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            spawnPosition = transform.position;
            RemainingAirJumps = extraAirJumps;
        }
        void OnEnable() { if (Input != null) { Input.JumpPressed += QueueJump; Input.DashPressed += QueueDash; } }
        void OnDisable()
        {
            if (Input != null) { Input.JumpPressed -= QueueJump; Input.DashPressed -= QueueDash; }
            jumpBufferTimer = 0f; coyoteTimer = 0f; dashRemaining = 0f;
        }
        void QueueJump() => jumpBufferTimer = Mathf.Max(jumpBufferTime, 0.0001f);
        void QueueDash() => TryDash();
        /// <summary>부활 등으로 자원을 회복할 때 대시 쿨다운과 진행 중인 대시를 지운다.</summary>
        public void ResetDashCooldown() { DashCooldownRemaining = 0f; dashRemaining = 0f; PublishState(); }
        /// <summary>공격 등으로 잠시 카메라 정면을 보게 한다.</summary>
        public void FaceCamera() => faceCameraTimer = Mathf.Max(faceCameraTimer, faceCameraSeconds);
        bool JumpHeld => input != null && input.JumpHeld;

        ActionResult CommonAvailability()
        {
            if (!Alive) return ActionResult.Fail(ActionFailure.NotAlive);
            if (Time.timeScale <= 0f) return ActionResult.Fail(ActionFailure.Paused);
            if (!isActiveAndEnabled || !MovementEnabled || controller == null || !controller.enabled
                || input == null || !input.AcceptsInput) return ActionResult.Fail(ActionFailure.Locked);
            return ActionResult.Success();
        }
        ActionResult JumpAvailability()
        {
            var common = CommonAvailability();
            if (!common.Succeeded) return common;
            return !IsDashing && (IsGrounded || coyoteTimer > 0f || RemainingAirJumps > 0) ? ActionResult.Success() : ActionResult.Fail(ActionFailure.Locked);
        }
        ActionResult DashAvailability()
        {
            var common = CommonAvailability();
            if (!common.Succeeded) return common;
            if (DashCooldownRemaining > 0f || IsDashing) return ActionResult.Fail(ActionFailure.Cooldown);
            if (!allowAirDash && !IsGrounded) return ActionResult.Fail(ActionFailure.Locked);
            if (Mana == null) return ActionResult.Fail(ActionFailure.NotFound);
            return Mana.CurrentMana >= dashManaCost ? ActionResult.Success() : ActionResult.Fail(ActionFailure.InsufficientMana);
        }
        public ActionResult TryDash()
        {
            var available = DashAvailability();
            if (!available.Succeeded) return available;
            if (!Mana.TryReserve(dashManaCost, out var reservation)) return ActionResult.Fail(ActionFailure.InsufficientMana);
            using (reservation)
            {
                Vector3 forward = FlatForward();
                Vector2 move = Vector2.ClampMagnitude(Input.Move, 1f);
                dashDirection = move.sqrMagnitude > 0.01f
                    ? (forward * move.y + Vector3.Cross(Vector3.up, forward) * move.x).normalized : forward;
                dashRemaining = dashDuration; DashCooldownRemaining = dashCooldown;
                if (!reservation.TryCommit())
                { dashRemaining = 0f; DashCooldownRemaining = 0f; return ActionResult.Fail(ActionFailure.InvalidRequest); }
            }
            PublishState();
            if (Alive) DashStarted?.Invoke();
            return ActionResult.Success();
        }
        Vector3 FlatForward()
        {
            Vector3 forward = Vector3.ProjectOnPlane(view != null ? view.forward : transform.forward, Vector3.up).normalized;
            return forward.sqrMagnitude < 0.01f ? transform.forward : forward;
        }
        void PublishState()
        {
            var state = State;
            if (state.Equals(lastState)) return;
            lastState = state; Changed?.Invoke(state);
        }
        void LateUpdate() => PublishState();

        void Update()
        {
            DashCooldownRemaining = Mathf.Max(0f, DashCooldownRemaining - Time.deltaTime);
            faceCameraTimer = Mathf.Max(0f, faceCameraTimer - Time.deltaTime);
            if (!Alive || !MovementEnabled || input == null || !input.AcceptsInput || !controller.enabled)
            {
                jumpBufferTimer = 0f; localVelocity = Vector2.zero; horizontalVelocity = Vector3.zero;
                if (!Alive || Time.timeScale > 0f) dashRemaining = 0f;
                return;
            }
            float dt = Time.deltaTime;
            bool wasGrounded = IsGrounded;
            float impactSpeed = 0f; bool impactCaptured = false; // 이번 프레임에 공중→접지가 되었을 때의 낙하 속도
            IsGrounded = controller.isGrounded && verticalVelocity <= 0f;
            if (IsGrounded) { if (!wasGrounded) { impactSpeed = -verticalVelocity; impactCaptured = true; } Land(); }
            else coyoteTimer = Mathf.Max(0f, coyoteTimer - dt);

            Vector3 forward = FlatForward();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector2 stick = Vector2.ClampMagnitude(Input.Move, 1f);
            bool steering = stick.sqrMagnitude > 0.0001f;
            float rate = IsGrounded ? (steering ? groundAcceleration : groundDeceleration) : (steering ? airAcceleration : airDeceleration);
            localVelocity = Vector2.MoveTowards(localVelocity, stick * moveSpeed, rate * dt);
            horizontalVelocity = forward * localVelocity.y + right * localVelocity.x;

            bool jumpedThisFrame = false;
            if (jumpBufferTimer > 0f && !IsDashing)
            {
                bool groundJump = IsGrounded || coyoteTimer > 0f;
                if (groundJump || RemainingAirJumps > 0)
                {
                    if (!groundJump) RemainingAirJumps--;
                    verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
                    IsGrounded = false; coyoteTimer = 0f; jumpBufferTimer = 0f; jumpedThisFrame = true;
                    LastJumpWasAirJump = !groundJump;
                    Jumped?.Invoke();
                }
            }
            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - dt);

            float effectiveGravity = gravity;
            if (verticalVelocity < 0f) effectiveGravity *= fallGravityMultiplier;
            else if (verticalVelocity > 0f && !JumpHeld) effectiveGravity *= lowJumpGravityMultiplier;
            verticalVelocity = Mathf.Max(verticalVelocity - effectiveGravity * dt, -50f);

            Vector3 displacement = horizontalVelocity * dt;
            if (IsDashing)
            {
                float step = Mathf.Min(dt, dashRemaining);
                displacement = dashDirection * (dashDistance / dashDuration * step);
                dashRemaining = Mathf.Max(0f, dashRemaining - step);
                if (dashRemaining <= 0f)
                {
                    // 대시가 끝나면 그 방향의 이동 속도로 이어 준다. 멈췄다가 다시 가속하지 않는다.
                    localVelocity = new Vector2(Vector3.Dot(dashDirection, right), Vector3.Dot(dashDirection, forward)) * moveSpeed;
                    horizontalVelocity = dashDirection * moveSpeed;
                }
                else { localVelocity = Vector2.zero; horizontalVelocity = Vector3.zero; }
            }
            CollisionFlags flags = controller.Move(displacement + Vector3.up * verticalVelocity * dt);
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
            IsGrounded = (flags & CollisionFlags.Below) != 0 && verticalVelocity <= 0f;
            if (!IsGrounded && wasGrounded && !jumpedThisFrame && verticalVelocity <= 0f && TrySnapToGround()) IsGrounded = true;
            if (IsGrounded) { if (!wasGrounded && !impactCaptured) impactSpeed = -verticalVelocity; Land(); }
            if (IsGrounded && !wasGrounded) Landed?.Invoke(Mathf.Max(0f, impactSpeed));

            Vector3 facing = Vector3.zero;
            if (IsDashing) facing = dashDirection;
            else if (alwaysFaceCamera || faceCameraTimer > 0f || Input.PrimaryAttackHeld) facing = forward;
            else if (steering && horizontalVelocity.sqrMagnitude > 0.04f) facing = horizontalVelocity;
            if (facing.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(facing.normalized), turnSpeed * dt);
            if (transform.position.y < recoveryY) Teleport(spawnPosition);
        }

        void Land()
        {
            groundedSinceSpawn = true;
            verticalVelocity = -groundStickSpeed;
            RemainingAirJumps = extraAirJumps;
            coyoteTimer = coyoteTime;
        }

        /// <summary>바닥에서 살짝 떠 버린 경우(내리막, 작은 턱) 가까운 바닥까지 내려 붙인다. 공중 점프 낭비와 접지 깜빡임을 막는다.</summary>
        bool TrySnapToGround()
        {
            if (groundSnapDistance <= 0f) return false;
            float radius = controller.radius * 0.9f;
            Vector3 bottomSphere = transform.position + controller.center - Vector3.up * (controller.height * 0.5f - controller.radius);
            float maxDistance = groundSnapDistance + controller.skinWidth;
            RaycastHit? nearest = null;
            foreach (var hit in Physics.SphereCastAll(bottomSphere, radius, Vector3.down, maxDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance <= 0f || hit.transform.IsChildOf(transform)) continue;
                if (Vector3.Angle(hit.normal, Vector3.up) > controller.slopeLimit) continue;
                if (!nearest.HasValue || hit.distance < nearest.Value.distance) nearest = hit;
            }
            if (!nearest.HasValue) return false;
            CollisionFlags flags = controller.Move(Vector3.down * (nearest.Value.distance + controller.skinWidth));
            return (flags & CollisionFlags.Below) != 0;
        }

        public void Teleport(Vector3 position)
        {
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            transform.position = position;
            controller.enabled = wasEnabled;
            horizontalVelocity = Vector3.zero; localVelocity = Vector2.zero; verticalVelocity = 0f;
            jumpBufferTimer = 0f; coyoteTimer = 0f; dashRemaining = 0f;
            IsGrounded = false; RemainingAirJumps = extraAirJumps;
        }
    }
}

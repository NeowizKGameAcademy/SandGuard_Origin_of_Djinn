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
        [Tooltip("스탯 수정자. 비우면 같은 오브젝트에서 찾고, 없으면 아래 기본값을 그대로 쓴다")]
        public PlayerStats stats;
        [Header("대시")]
        [Min(0)] public int dashManaCost = 10;
        [Min(0f)] public float dashCooldown = 1f;
        [Min(0.01f)] public float dashDuration = 2f;
        [Min(0.01f)] public float dashDistance = 4f;
        [Tooltip("대시 속도 곡선. 가로 = 대시 진행(0~1), 세로 = 상대 속도. 곡선 아래 넓이로 정규화하므로 모양만 정하면 되고 총 거리는 dashDistance를 유지한다. 기본: 15%까지 가속해 정점, 이후 긴 꼬리로 부드럽게 감속(끝 기울기 0)")]
        public AnimationCurve dashProfile = new AnimationCurve(
            new Keyframe(0f, 0.1f, 0f, 6f), new Keyframe(0.15f, 1f, 0f, 0f), new Keyframe(0.33f, 0.72f, -1.5f, -1.5f), new Keyframe(0.65f, 0.28f, -0.6f, -0.6f), new Keyframe(1f, 0.08f, 0f, 0f));
        const int DashProfileSamples = 32;
        float[] dashProgressTable; // 정규화 누적 거리 F(t), t = i / DashProfileSamples
        float dashProfilePeak; // 곡선 최고값. DashSpeedFactor 정규화용
        [Min(0), Tooltip("공중에서 쓸 수 있는 대시 횟수(기본값). 접지하면 다시 찬다. 스킬 ④ 공중 대시가 AirDashes 수정자로 올린다")]
        public int airDashes = 0;
        public float DashCooldownRemaining { get; private set; }
        public bool IsDashing => dashRemaining > 0f && Alive;
        public event Action<PlayerMobilityState> Changed;
        public event Action DashStarted;
        public event Action Teleported;
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
        public Vector3 DashDirection => dashDirection;
        /// <summary>지금 대시 속도가 정점 대비 얼마인지(0~1). dashProfile을 그대로 따르므로 카메라·VFX가 이동과 같은 박자로 움직인다. 대시 중이 아니면 0, 곡선이 없으면(등속) 1.</summary>
        public float DashSpeedFactor
        {
            get
            {
                if (!IsDashing) return 0f;
                if (dashProgressTable == null || dashProfilePeak <= 0f) return 1f;
                return Mathf.Clamp01(Mathf.Max(0f, dashProfile.Evaluate(1f - dashRemaining / dashDuration)) / dashProfilePeak);
            }
        }
        PlayerMobilityState lastState;
        // 진행 중인 쿨다운은 발동 시점의 길이를 기준으로 진행률을 낸다. 도중에 수정자로 쿨다운이 줄어도 남은 시간이 길이를 넘지 않는다.
        float activeDashCooldown;
        public float ActiveDashCooldown => DashCooldownRemaining > 0f ? Mathf.Max(activeDashCooldown, DashCooldownRemaining) : DashCooldown;
        public PlayerMobilityState State => new PlayerMobilityState(IsGrounded, RemainingAirJumps, DashManaCost,
            DashCooldownRemaining, ActiveDashCooldown, JumpAvailability(), DashAvailability());
        // 수정자를 적용한 최종값. 아래 public 필드는 기본값이며, 게임 로직은 이 속성을 쓴다.
        public float MoveSpeed => Stat(PlayerStat.MoveSpeed, moveSpeed);
        public float JumpHeight => Stat(PlayerStat.JumpHeight, jumpHeight);
        public float DashDistance => Stat(PlayerStat.DashDistance, dashDistance);
        public float DashCooldown => Stat(PlayerStat.DashCooldown, dashCooldown);
        public int DashManaCost => stats != null ? stats.EvaluateCount(PlayerStat.DashManaCost, dashManaCost) : dashManaCost;
        public int ExtraAirJumps => SkillTreeAirJumpAllowed!=null ? (SkillTreeAirJumpAllowed()?Mathf.Max(1, stats!=null?stats.EvaluateCount(PlayerStat.ExtraAirJumps,extraAirJumps):extraAirJumps):0) : (stats != null ? stats.EvaluateCount(PlayerStat.ExtraAirJumps, extraAirJumps) : extraAirJumps);
        public int AirDashes => stats != null ? stats.EvaluateCount(PlayerStat.AirDashes, airDashes) : airDashes;
        /// <summary>이번 체공에서 아직 쓸 수 있는 공중 대시 횟수.</summary>
        public int RemainingAirDashes => Mathf.Max(0, AirDashes - airDashesUsed);
        int airDashesUsed;
        float Stat(PlayerStat stat, float baseValue) => stats != null ? stats.Evaluate(stat, baseValue) : baseValue;
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
        [Min(0), Tooltip("공중 추가 점프 횟수(기본값). 스킬 ① 더블 점프가 ExtraAirJumps 수정자로 올린다")] public int extraAirJumps = 0;
        [Min(0), Tooltip("공중 추가 점프 1회당 마나 비용. 0이면 무료이며 지상 점프에는 적용하지 않는다")]
        public int airJumpManaCost = 0;
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
        public bool HardLandingLocked { get; private set; }
        internal void SetHardLandingLock(bool value)
        {
            HardLandingLocked = value;
            if (!value) return;
            localVelocity = Vector2.zero; horizontalVelocity = Vector3.zero;
            jumpBufferTimer = 0f; dashRemaining = 0f;
        }
        public bool IsGrounded { get; private set; }
        public int RemainingAirJumps { get; private set; }
        // Include both the movement and ground-snap Move calls in the published velocity.
        public Vector3 Velocity => frameVelocity;
        Vector3 frameVelocity;
        CharacterController controller;
        Vector3 horizontalVelocity, spawnPosition;
        Vector2 localVelocity; // 카메라 기준 (x 우, y 전). 시점을 돌려도 몸이 흐르지 않도록 로컬로 보관한다.
        float verticalVelocity, coyoteTimer, jumpBufferTimer, faceCameraTimer;

        void OnValidate()
        {
            dashManaCost = Mathf.Max(0, dashManaCost);
            airJumpManaCost = Mathf.Max(0, airJumpManaCost);
            if (float.IsNaN(dashCooldown) || float.IsInfinity(dashCooldown)) dashCooldown = 1f;
            dashCooldown = Mathf.Max(0f, dashCooldown);
            if (float.IsNaN(dashDuration) || float.IsInfinity(dashDuration)) dashDuration = 0.2f;
            dashDuration = Mathf.Max(0.01f, dashDuration);
            if (float.IsNaN(dashDistance) || float.IsInfinity(dashDistance)) dashDistance = 4f;
            dashDistance = Mathf.Max(0.01f, dashDistance);
            DashCooldownRemaining = Mathf.Min(DashCooldownRemaining, Mathf.Max(dashCooldown, activeDashCooldown));
            BuildDashProfile();
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (stats == null) stats = GetComponent<PlayerStats>();
            BuildDashProfile();
            spawnPosition = transform.position;
            RemainingAirJumps = ExtraAirJumps;
        }
        void OnEnable() { if (Input != null) { Input.JumpPressed += QueueJump; Input.DashPressed += QueueDash; } }
        void OnDisable()
        {
            frameVelocity = Vector3.zero;
            if (Input != null) { Input.JumpPressed -= QueueJump; Input.DashPressed -= QueueDash; }
            jumpBufferTimer = 0f; coyoteTimer = 0f; dashRemaining = 0f;
        }
        void QueueJump() => jumpBufferTimer = Mathf.Max(jumpBufferTime, 0.0001f);
        public System.Func<bool> SkillTreeDashAllowed;
        public System.Func<bool> SkillTreeAirJumpAllowed;
        public System.Action SkillTreeDashInput;
        void QueueDash() { if(SkillTreeDashInput!=null)SkillTreeDashInput();else TryDash(); }
        /// <summary>충전(상승 기류)처럼 제자리에 붙들 때 켠다. 이동 입력·점프·대시를 받지 않지만 중력과 접지는 그대로다.</summary>
        public bool Anchored { get; set; }
        /// <summary>켜면 지상(코요테 포함) 점프를 버튼 누름에 바로 하지 않고 버린다. 상승 기류가 탭이면 <see cref="TryJump"/>, 홀드면 충전으로 처리한다. 공중 점프는 그대로다.</summary>
        public bool DeferGroundJumps { get; set; }
        /// <summary>지상(코요테 포함) 점프를 지금 한다. fullHeight면 버튼을 떼도 최대 높이로 뛴다.</summary>
        public bool TryJump(bool fullHeight = false)
        {
            if (!JumpAvailability().Succeeded || IsDashing) return false;
            if (!(IsGrounded || coyoteTimer > 0f)) return false;
            return Jump(true, fullHeight);
        }
        bool Jump(bool groundJump, bool fullHeight)
        {
            if (!groundJump && airJumpManaCost > 0 && (Mana == null || !Mana.TrySpend(airJumpManaCost)))
            {
                jumpBufferTimer = 0f;
                return false;
            }
            if (!groundJump) RemainingAirJumps--;
            verticalVelocity = Mathf.Sqrt(2f * gravity * JumpHeight);
            IsGrounded = false; coyoteTimer = 0f; jumpBufferTimer = 0f; freeAscent = fullHeight;
            LastJumpWasAirJump = !groundJump;
            Jumped?.Invoke();
            return true;
        }
        /// <summary>상승 기류 같은 발사형 도약. 인자는 목표 높이(m).</summary>
        public event Action<float> Launched;
        bool freeAscent; // 발사 상승 중에는 점프 버튼을 떼도 낮은 점프 중력을 쓰지 않는다
        /// <summary>정해진 높이까지 곧장 솟아오른다. 점프 횟수를 쓰지 않고 Jumped도 내지 않는다.</summary>
        public void LaunchVertical(float height)
        {
            if (!Alive || HardLandingLocked || controller == null || !controller.enabled) return;
            verticalVelocity = Mathf.Sqrt(2f * gravity * Mathf.Max(0.01f, height));
            IsGrounded = false; coyoteTimer = 0f; jumpBufferTimer = 0f; freeAscent = true;
            Launched?.Invoke(height);
        }
        /// <summary>대시 진행 t(0~1)까지 간 거리 비율(0~1). 곡선을 사다리꼴 적분해 표로 두고 보간한다. 곡선이 비어 있거나 넓이가 0이면 등속.</summary>
        public float DashProgress(float t)
        {
            t = Mathf.Clamp01(t);
            if (dashProgressTable == null) BuildDashProfile();
            if (dashProgressTable == null) return t;
            float x = t * DashProfileSamples; int i = Mathf.Min((int)x, DashProfileSamples - 1);
            return Mathf.Lerp(dashProgressTable[i], dashProgressTable[i + 1], x - i);
        }
        /// <summary>인스펙터에서 곡선을 바꾼 뒤 부른다. Awake·OnValidate에서 자동으로 한 번 만든다.</summary>
        public void BuildDashProfile()
        {
            dashProfilePeak = 0f;
            if (dashProfile == null || dashProfile.length == 0) { dashProgressTable = null; return; }
            var table = new float[DashProfileSamples + 1];
            float previous = Mathf.Max(0f, dashProfile.Evaluate(0f));
            dashProfilePeak = previous;
            for (int i = 1; i <= DashProfileSamples; i++)
            {
                float value = Mathf.Max(0f, dashProfile.Evaluate((float)i / DashProfileSamples));
                table[i] = table[i - 1] + 0.5f * (previous + value) / DashProfileSamples;
                dashProfilePeak = Mathf.Max(dashProfilePeak, value);
                previous = value;
            }
            float area = table[DashProfileSamples];
            if (area <= 0.0001f) { dashProgressTable = null; return; }
            for (int i = 0; i <= DashProfileSamples; i++) table[i] /= area;
            dashProgressTable = table;
        }
        /// <summary>부활 등으로 자원을 회복할 때 대시 쿨다운과 진행 중인 대시를 지운다.</summary>
        public void ResetDashCooldown() { DashCooldownRemaining = 0f; dashRemaining = 0f; PublishState(); }
        /// <summary>공격 등으로 잠시 카메라 정면을 보게 한다.</summary>
        public void FaceCamera() => faceCameraTimer = Mathf.Max(faceCameraTimer, faceCameraSeconds);
        bool JumpHeld => input != null && input.JumpHeld;

        ActionResult CommonAvailability()
        {
            if (!Alive) return ActionResult.Fail(ActionFailure.NotAlive);
            if (Time.timeScale <= 0f) return ActionResult.Fail(ActionFailure.Paused);
            if (!isActiveAndEnabled || !MovementEnabled || HardLandingLocked || controller == null || !controller.enabled
                || input == null || !input.AcceptsInput) return ActionResult.Fail(ActionFailure.Locked);
            return ActionResult.Success();
        }
        ActionResult JumpAvailability()
        {
            var common = CommonAvailability();
            if (!common.Succeeded) return common;
            if (IsDashing || Anchored) return ActionResult.Fail(ActionFailure.Locked);
            if (IsGrounded || coyoteTimer > 0f) return ActionResult.Success();
            if (RemainingAirJumps <= 0 || (SkillTreeAirJumpAllowed != null && !SkillTreeAirJumpAllowed()))
                return ActionResult.Fail(ActionFailure.Locked);
            if (airJumpManaCost <= 0) return ActionResult.Success();
            if (Mana == null) return ActionResult.Fail(ActionFailure.NotFound);
            return Mana.CurrentMana >= airJumpManaCost ? ActionResult.Success() : ActionResult.Fail(ActionFailure.InsufficientMana);
        }
        ActionResult DashAvailability()
        {
            if(SkillTreeDashAllowed!=null && !SkillTreeDashAllowed())return ActionResult.Fail(ActionFailure.Locked);
            var common = CommonAvailability();
            if (!common.Succeeded) return common;
            if (Anchored) return ActionResult.Fail(ActionFailure.Locked);
            if (DashCooldownRemaining > 0f || IsDashing) return ActionResult.Fail(ActionFailure.Cooldown);
            if (!IsGrounded && RemainingAirDashes <= 0) return ActionResult.Fail(ActionFailure.Locked);
            if (Mana == null) return ActionResult.Fail(ActionFailure.NotFound);
            return Mana.CurrentMana >= DashManaCost ? ActionResult.Success() : ActionResult.Fail(ActionFailure.InsufficientMana);
        }
        public ActionResult TryDash()
        {
            var available = DashAvailability();
            if (!available.Succeeded) return available;
            if (!Mana.TryReserve(DashManaCost, out var reservation)) return ActionResult.Fail(ActionFailure.InsufficientMana);
            using (reservation)
            {
                Vector3 forward = FlatForward();
                Vector2 move = Vector2.ClampMagnitude(Input.Move, 1f);
                dashDirection = move.sqrMagnitude > 0.01f
                    ? (forward * move.y + Vector3.Cross(Vector3.up, forward) * move.x).normalized : forward;
                dashRemaining = dashDuration; DashCooldownRemaining = DashCooldown; activeDashCooldown = DashCooldownRemaining;
                if (!reservation.TryCommit())
                { dashRemaining = 0f; DashCooldownRemaining = 0f; return ActionResult.Fail(ActionFailure.InvalidRequest); }
                if (!IsGrounded) airDashesUsed++;
                verticalVelocity = 0f;
                freeAscent = false;
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
            frameVelocity = Vector3.zero;
            DashCooldownRemaining = Mathf.Max(0f, DashCooldownRemaining - Time.deltaTime);
            faceCameraTimer = Mathf.Max(0f, faceCameraTimer - Time.deltaTime);
            if (!Alive || !MovementEnabled || input == null || !input.AcceptsInput || !controller.enabled)
            {
                jumpBufferTimer = 0f; localVelocity = Vector2.zero; horizontalVelocity = Vector3.zero;
                if (!Alive || Time.timeScale > 0f) dashRemaining = 0f;
                return;
            }
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            bool wasGrounded = IsGrounded;
            float impactSpeed = 0f; bool impactCaptured = false; // 이번 프레임에 공중→접지가 되었을 때의 낙하 속도
            IsGrounded = controller.isGrounded && verticalVelocity <= 0f;
            if (IsGrounded) { if (!wasGrounded) { impactSpeed = -verticalVelocity; impactCaptured = true; } Land(); }
            else coyoteTimer = Mathf.Max(0f, coyoteTimer - dt);

            Vector3 forward = FlatForward();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector2 stick = Anchored || HardLandingLocked ? Vector2.zero : Vector2.ClampMagnitude(Input.Move, 1f);
            bool steering = stick.sqrMagnitude > 0.0001f;
            float rate = IsGrounded ? (steering ? groundAcceleration : groundDeceleration) : (steering ? airAcceleration : airDeceleration);
            localVelocity = Vector2.MoveTowards(localVelocity, stick * MoveSpeed, rate * dt);
            if (HardLandingLocked) localVelocity = Vector2.zero;
            horizontalVelocity = forward * localVelocity.y + right * localVelocity.x;

            bool jumpedThisFrame = false;
            if (Anchored || HardLandingLocked) jumpBufferTimer = 0f;
            if (jumpBufferTimer > 0f && !IsDashing)
            {
                bool groundJump = IsGrounded || coyoteTimer > 0f;
                if (groundJump && DeferGroundJumps) jumpBufferTimer = 0f; // 상승 기류가 탭/홀드를 가른 뒤 TryJump로 점프시킨다
                else if (groundJump || (RemainingAirJumps > 0 && (SkillTreeAirJumpAllowed==null || SkillTreeAirJumpAllowed()))) jumpedThisFrame = Jump(groundJump, false);
            }
            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - dt);

            // Keep this frame's decision even when the dash timer expires before Move below.
            bool dashingThisFrame = IsDashing;
            if (dashingThisFrame) verticalVelocity = 0f;
            else
            {
                float effectiveGravity = gravity;
                if (verticalVelocity < 0f) effectiveGravity *= fallGravityMultiplier;
                else if (verticalVelocity > 0f && !JumpHeld && !freeAscent) effectiveGravity *= lowJumpGravityMultiplier;
                verticalVelocity = Mathf.Max(verticalVelocity - effectiveGravity * dt, -50f);
            }
            if (verticalVelocity <= 0f) freeAscent = false;

            Vector3 displacement = horizontalVelocity * dt;
            if (IsDashing)
            {
                float step = Mathf.Min(dt, dashRemaining);
                // 속도 곡선을 따라 빠르게 붙었다가 끝에서 풀린다. 곡선 아래 넓이로 정규화해 총 거리는 DashDistance 그대로다.
                float from = 1f - dashRemaining / dashDuration, to = 1f - (dashRemaining - step) / dashDuration;
                displacement = dashDirection * (DashDistance * (DashProgress(to) - DashProgress(from)));
                dashRemaining = Mathf.Max(0f, dashRemaining - step);
                if (dashRemaining <= 0f)
                {
                    // 대시가 끝나면 그 방향의 이동 속도로 이어 준다. 멈췄다가 다시 가속하지 않는다.
                    localVelocity = new Vector2(Vector3.Dot(dashDirection, right), Vector3.Dot(dashDirection, forward)) * MoveSpeed;
                    horizontalVelocity = dashDirection * MoveSpeed;
                }
                else { localVelocity = Vector2.zero; horizontalVelocity = Vector3.zero; }
            }
            // CharacterController's automatic step-down must not lower a horizontal dash either.
            Vector3 movementStart = transform.position;
            float savedStepOffset = controller.stepOffset;
            CollisionFlags flags;
            try
            {
                if (dashingThisFrame) controller.stepOffset = 0f;
                flags = controller.Move(displacement + Vector3.up * verticalVelocity * dt);
            }
            finally { controller.stepOffset = savedStepOffset; }
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
            IsGrounded = (flags & CollisionFlags.Below) != 0 && verticalVelocity <= 0f;
            if (!IsGrounded && wasGrounded && !jumpedThisFrame && verticalVelocity <= 0f && TrySnapToGround(dashingThisFrame)) IsGrounded = true;
            frameVelocity = (transform.position - movementStart) / dt;
            if (IsGrounded) { if (!wasGrounded && !impactCaptured) impactSpeed = -verticalVelocity; Land(); }
            if (dashingThisFrame) verticalVelocity = 0f; // Land's downward stick speed resumes only after the dash.
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
            airDashesUsed = 0;
            verticalVelocity = -groundStickSpeed;
            RemainingAirJumps = ExtraAirJumps;
            coyoteTimer = coyoteTime;
        }

        /// <summary>바닥에서 살짝 떠 버린 경우(내리막, 작은 턱) 가까운 바닥까지 내려 붙인다. 공중 점프 낭비와 접지 깜빡임을 막는다.</summary>
        bool TrySnapToGround(bool probeOnly = false)
        {
            if (!probeOnly && groundSnapDistance <= 0f) return false;
            float radius = controller.radius * 0.9f;
            Vector3 bottomSphere = transform.position + controller.center - Vector3.up * (controller.height * 0.5f - controller.radius);
            float maxDistance = (probeOnly ? 0.02f : groundSnapDistance) + controller.skinWidth;
            RaycastHit? nearest = null;
            foreach (var hit in Physics.SphereCastAll(bottomSphere, radius, Vector3.down, maxDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance <= 0f || hit.transform.IsChildOf(transform)) continue;
                if (Vector3.Angle(hit.normal, Vector3.up) > controller.slopeLimit) continue;
                if (!nearest.HasValue || hit.distance < nearest.Value.distance) nearest = hit;
            }
            if (!nearest.HasValue) return false;
            if (probeOnly) return true; // Retain flat-ground contact without moving down toward a lower ledge.
            CollisionFlags flags = controller.Move(Vector3.down * (nearest.Value.distance + controller.skinWidth));
            return (flags & CollisionFlags.Below) != 0;
        }

        public void Teleport(Vector3 position)
        {
            frameVelocity = Vector3.zero;
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            transform.position = position;
            controller.enabled = wasEnabled;
            horizontalVelocity = Vector3.zero; localVelocity = Vector2.zero; verticalVelocity = 0f;
            jumpBufferTimer = 0f; coyoteTimer = 0f; dashRemaining = 0f;
            IsGrounded = false; RemainingAirJumps = ExtraAirJumps; airDashesUsed = 0;
            Teleported?.Invoke();
        }
    }
}

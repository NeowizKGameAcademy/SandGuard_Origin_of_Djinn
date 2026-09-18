using UnityEngine;
using UnityEngine.Events;

namespace SandGuard.Player
{
    public sealed class PlayerVisuals : MonoBehaviour
    {
        [Header("외형 교체 — 게임 동작과 독립")]
        public GameObject visualPrefab;
        public Transform visualRoot;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale = Vector3.one;
        [Tooltip("외형에 발사 위치가 없으면 이 위치를 사용합니다.")]
        public Transform fallbackFirePoint;
        [Tooltip("루트에 유지되는 발사 효과용 위치. 발사 직전에 현재 손/총구 위치로 맞춥니다.")]
        public Transform fireEffectAnchor;
        public PlayerMotor motor;
        [Tooltip("IDamageEvents / ILifeState를 구현한 체력 컴포넌트")]
        public MonoBehaviour healthSource;
        [Header("애니메이터 파라미터 — 없으면 생략")]
        public string speedParameter = "Speed";
        public string groundedParameter = "Grounded";
        public string attackTrigger = "Attack";
        public string hitTrigger = "Hit";
        public string deathTrigger = "Death";
        public string dashParameter = "Dashing";
        public string jumpTrigger = "Jump";
        [Tooltip("공중 추가 점프 트리거. 컨트롤러에 없으면 Jump 트리거를 대신 쓴다")]
        public string doubleJumpTrigger = "DoubleJump";
        [Tooltip("상승 기류 충전 중 Bool / 충전량 Float / 발사 트리거")]
        public string chargingParameter = "Charging";
        public string chargeParameter = "Charge";
        public string flyTrigger = "Fly";
        [Tooltip("상승 기류 컴포넌트. 비우면 같은 오브젝트에서 찾는다")]
        public PlayerUpdraft updraft;
        [Tooltip("공중 수직 속도(위가 양수). 낙하 상태 전환에 쓴다")]
        public string verticalSpeedParameter = "VerticalSpeed";
        [Tooltip("이 속도 이상으로 착지하면 켜지는 Bool. 다시 공중에 뜨면 끈다")]
        public string hardLandParameter = "HardLand";
        [Min(0f), Tooltip("강한 착지로 판정하는 낙하 속도(m/s). 기본 점프 착지는 약 12~16, 4m 이상 낙하는 20 이상")]
        public float hardLandingSpeed = 20f;
        /// <summary>이 속도(m/s)보다 빠르게 내려갈 때만 애니메이터가 낙하로 본다. 생성 직후 내려앉기·계단·턱(0.15m 이하)에서는 낙하 동작이 나오지 않는다.</summary>
        public const float FallingSpeedThreshold = 4f;
        public string moveXParameter = "MoveX";
        public string moveZParameter = "MoveZ";
        [Min(0f), Tooltip("피격 반응(Damage Reactions) 레이어를 내리는 시간. 반응이 끝나면 0으로 내려 상체가 이동 동작을 따르게 한다")]
        public float reactionBlendTime = 0.1f;
        [Tooltip("대시 하체 레이어 이름. 대시 중에만 가중치 1")]
        public string dashLayerName = "Dash Legs";
        [Min(0f), Tooltip("대시가 끝난 뒤 하체 레이어를 내리는 시간")]
        public float dashLayerBlendTime = 0.15f;
        [Header("관통탄 충전·발사 (양팔 오버라이드 레이어. 없으면 손바닥 시전으로 대신한다)")]
        public string pierceLayerName = "Pierce Casting";
        public string pierceChargingParameter = "PierceCharging";
        public string pierceFireTrigger = "PierceFire";
        [Min(0f), Tooltip("레이어를 올리는 시간")] public float pierceBlendInTime = 0.1f;
        [Min(0f), Tooltip("레이어를 내리는 시간")] public float pierceBlendOutTime = 0.25f;
        public UnityEvent onFired = new UnityEvent();
        public UnityEvent onDamaged = new UnityEvent();
        public UnityEvent onIncapacitated = new UnityEvent();
        public UnityEvent onDashStarted = new UnityEvent();
        public UnityEvent onJumped = new UnityEvent();
        public UnityEvent onAirJumped = new UnityEvent();
        public UnityEvent onLanded = new UnityEvent();
        public UnityEvent onHardLanded = new UnityEvent();
        [SerializeField, HideInInspector] GameObject visualInstance;
        Animator animator;
        float reactionWeight, dashLayerWeight, pierceLayerWeight;
        bool pierceCharging;
        /// <summary>컨트롤러에 관통탄 레이어와 발사 트리거가 있는가.</summary>
        public bool HasPierceAnimation => animator != null && animator.GetLayerIndex(pierceLayerName) >= 0 && HasParameter(pierceFireTrigger, AnimatorControllerParameterType.Trigger);
        /// <summary>관통탄 레이어가 지금 몸을 잡고 있는가(충전 중이거나 발사 동작이 남아 있다).</summary>
        public bool PierceAnimating => pierceLayerWeight > 0.01f;
        public bool PierceCharging => pierceCharging;
        bool awaitingHardLanding, sawHardLanding;
        float hardLandingEntryWait;
        Transform muzzle;
        public PlayerSpellcasting Spellcasting { get; private set; }
        public Transform FirePoint => muzzle != null ? muzzle : fallbackFirePoint;

        void Awake() => RebuildVisual();
        void OnEnable()
        {
            if (healthSource is IDamageEvents damage) damage.Damaged += OnDamaged;
            if (healthSource is ILifeState life) life.Died += OnDied;
            if (motor != null) { motor.DashStarted += OnDash; motor.Jumped += OnJump; motor.Landed += OnLanded; motor.Launched += OnLaunched; motor.Teleported += ClearHardLandingLock; }
            if (updraft == null) updraft = GetComponent<PlayerUpdraft>();
        }
        void OnDisable()
        {
            if (healthSource is IDamageEvents damage) damage.Damaged -= OnDamaged;
            if (healthSource is ILifeState life) life.Died -= OnDied;
            if (motor != null) { motor.DashStarted -= OnDash; motor.Jumped -= OnJump; motor.Landed -= OnLanded; motor.Launched -= OnLaunched; motor.Teleported -= ClearHardLandingLock; }
            ClearHardLandingLock();
        }
        void OnLaunched(float height)
        {
            // 발사형 도약은 점프 클립 대신 Fly 자세. 남은 점프 트리거는 지운다.
            if (HasParameter(jumpTrigger, AnimatorControllerParameterType.Trigger)) animator.ResetTrigger(jumpTrigger);
            if (HasParameter(flyTrigger, AnimatorControllerParameterType.Trigger)) animator.SetTrigger(flyTrigger);
        }
        void OnDamaged(DamageAppliedInfo info)
        {
            if (HasParameter(hitTrigger, AnimatorControllerParameterType.Trigger))
            {
                animator.SetTrigger(hitTrigger);
                // 피격 첫 프레임부터 상체 반응이 보이도록 레이어를 즉시 올린다.
                int layer = animator.GetLayerIndex("Damage Reactions");
                if (layer >= 0) { reactionWeight = 1f; animator.SetLayerWeight(layer, 1f); }
            }
            onDamaged.Invoke();
        }

        /// <summary>
        /// 피격 반응 레이어는 Hit 동작이 재생되는 동안만 켠다. 모션이 없는 Empty 상태라도 가중치가 1이면 마스크 부위(척추·머리·팔)의
        /// 근육 값을 고정해 버려서, 이동 중 상체가 골반과 함께 막대처럼 흔들리는 문제가 생긴다.
        /// </summary>
        /// <summary>대시 하체 레이어는 대시 동작이 재생되는 동안만 켠다. Empty 상태에서 가중치 1이면 다리가 굳는다(피격 레이어와 같은 이유).</summary>
        /// <summary>충전 중이거나 충전·발사 상태가 재생 중이면 1, 아니면 0으로 부드럽게. 죽으면 바로 0.</summary>
        float PierceLayerWeight(int layer, bool dead)
        {
            var current = animator.GetCurrentAnimatorStateInfo(layer);
            var next = animator.GetNextAnimatorStateInfo(layer);
            bool firing = (current.IsName("Pierce Fire") && current.normalizedTime < 0.9f) || next.IsName("Pierce Fire");
            bool charging = pierceCharging || current.IsName("Pierce Charge") || next.IsName("Pierce Charge");
            float target = !dead && (firing || charging) ? 1f : 0f;
            float time = target > pierceLayerWeight ? pierceBlendInTime : pierceBlendOutTime;
            pierceLayerWeight = time <= 0f || dead ? target : Mathf.MoveTowards(pierceLayerWeight, target, Time.deltaTime / time);
            return pierceLayerWeight;
        }

        /// <summary>관통탄 충전 시작/끝. 레이어가 있으면 두 손을 모으는 자세로 들어가고 손바닥 시전 IK는 쉰다.</summary>
        public void SetPierceCharging(bool value)
        {
            pierceCharging = value;
            if (animator != null && HasParameter(pierceChargingParameter, AnimatorControllerParameterType.Bool)) animator.SetBool(pierceChargingParameter, value);
        }

        /// <summary>관통탄 발사 동작(두 손 내지르기). 레이어가 없으면 false를 돌려주고 아무것도 하지 않는다(호출자가 PlayFire로 대신한다).</summary>
        public bool PlayPierceFire()
        {
            if (!HasPierceAnimation) return false;
            if (fireEffectAnchor != null && FirePoint != null)
                fireEffectAnchor.SetPositionAndRotation(FirePoint.position, FirePoint.rotation);
            pierceCharging = false;
            if (HasParameter(pierceChargingParameter, AnimatorControllerParameterType.Bool)) animator.SetBool(pierceChargingParameter, false);
            animator.SetTrigger(pierceFireTrigger);
            if (Spellcasting != null && Spellcasting.isActiveAndEnabled) Spellcasting.Flash();
            onFired.Invoke();
            return true;
        }

        float DashLayerWeight(int layer, bool dead)
        {
            bool playing = !dead && motor != null && (motor.IsDashing
                || animator.GetCurrentAnimatorStateInfo(layer).IsName("Dash") || animator.GetNextAnimatorStateInfo(layer).IsName("Dash"));
            float target = playing ? 1f : 0f;
            dashLayerWeight = target > dashLayerWeight || dashLayerBlendTime <= 0f ? target
                : Mathf.MoveTowards(dashLayerWeight, target, Time.deltaTime / dashLayerBlendTime);
            return dashLayerWeight;
        }
        float ReactionWeight(int layer, bool dead)
        {
            var current = animator.GetCurrentAnimatorStateInfo(layer);
            bool playing = (current.IsName("Hit") && current.normalizedTime < 0.85f) || animator.GetNextAnimatorStateInfo(layer).IsName("Hit");
            float target = !dead && playing ? 1f : 0f;
            reactionWeight = reactionBlendTime <= 0f ? target : Mathf.MoveTowards(reactionWeight, target, Time.deltaTime / reactionBlendTime);
            return reactionWeight;
        }
        void OnDied(DeathInfo info)
        {
            ClearHardLandingLock();
            if (Spellcasting != null) Spellcasting.Cancel();
            if (HasParameter("Dead", AnimatorControllerParameterType.Bool)) animator.SetBool("Dead", true);
            if (HasParameter(deathTrigger, AnimatorControllerParameterType.Trigger)) animator.SetTrigger(deathTrigger);
            onIncapacitated.Invoke();
        }
        void OnDash() => onDashStarted.Invoke();
        void OnJump()
        {
            if (motor.LastJumpWasAirJump) onAirJumped.Invoke();
            else onJumped.Invoke();
            // 지상 점프는 Jump, 공중 점프는 DoubleJump(플립). 옛 컨트롤러처럼 DoubleJump가 없으면 둘 다 Jump로 재생한다.
            if (motor.LastJumpWasAirJump && HasParameter(doubleJumpTrigger, AnimatorControllerParameterType.Trigger))
                animator.SetTrigger(doubleJumpTrigger);
            else if (HasParameter(jumpTrigger, AnimatorControllerParameterType.Trigger)) animator.SetTrigger(jumpTrigger);
        }
        void OnLanded(float impactSpeed)
        {
            bool hard = impactSpeed >= hardLandingSpeed;
            // 착지 순간 아직 소비되지 않은 점프 트리거가 남아 있으면 지상에서 엉뚱한 점프 동작이 나온다.
            if (HasParameter(jumpTrigger, AnimatorControllerParameterType.Trigger)) animator.ResetTrigger(jumpTrigger);
            if (HasParameter(doubleJumpTrigger, AnimatorControllerParameterType.Trigger)) animator.ResetTrigger(doubleJumpTrigger);
            if (HasParameter(hardLandParameter, AnimatorControllerParameterType.Bool)) animator.SetBool(hardLandParameter, hard);
            if (hard && animator != null && animator.isActiveAndEnabled
                && animator.HasState(0, Animator.StringToHash("Base Layer.Hard Landing")))
            {
                awaitingHardLanding = true; sawHardLanding = false; hardLandingEntryWait = 0f;
                motor.SetHardLandingLock(true);
            }
            onLanded.Invoke();
            if (hard) onHardLanded.Invoke();
        }

        [ContextMenu("외형 다시 연결 / Rebuild Visual")]
        public void RebuildVisual()
        {
            if (visualRoot == null) return;
            if (visualInstance != null)
            {
                visualInstance.SetActive(false);
                if (Application.isPlaying) Destroy(visualInstance); else DestroyImmediate(visualInstance);
            }
            animator = null; muzzle = null; visualInstance = null; Spellcasting = null;
            if (visualPrefab == null) return;
            visualInstance = Instantiate(visualPrefab, visualRoot);
            visualInstance.name = "Visual (Replaceable)";
            visualInstance.transform.localPosition = localPosition;
            visualInstance.transform.localRotation = Quaternion.Euler(localEulerAngles);
            visualInstance.transform.localScale = localScale;
            PlayerVisualBindings bindings = visualInstance.GetComponentInChildren<PlayerVisualBindings>(true);
            animator = bindings != null && bindings.animator != null ? bindings.animator : visualInstance.GetComponentInChildren<Animator>(true);
            muzzle = bindings != null ? bindings.firePoint : null;
            Spellcasting = visualInstance.GetComponentInChildren<PlayerSpellcasting>(true);
            if (animator != null) animator.applyRootMotion = false;
        }

        // PlayerMotor updates at -200; publish before this frame's Animator evaluation.
        void Update()
        {
            if (animator == null || motor == null) return;
            bool dead = healthSource is ILifeState life && life.State != global::LifeState.Alive;
            if (HasParameter("Dead", AnimatorControllerParameterType.Bool)) animator.SetBool("Dead", dead);
            int reactionLayer = animator.GetLayerIndex("Damage Reactions");
            if (reactionLayer >= 0) animator.SetLayerWeight(reactionLayer, ReactionWeight(reactionLayer, dead));
            int dashLayer = animator.GetLayerIndex(dashLayerName);
            if (dashLayer >= 0) animator.SetLayerWeight(dashLayer, DashLayerWeight(dashLayer, dead));
            int pierceLayer = animator.GetLayerIndex(pierceLayerName);
            if (pierceLayer >= 0)
            {
                if (dead && pierceCharging) SetPierceCharging(false);
                animator.SetLayerWeight(pierceLayer, PierceLayerWeight(pierceLayer, dead));
                if (Spellcasting != null) Spellcasting.Suppressed = pierceLayerWeight > 0.01f; // 양팔 동작이 잡는 동안 오른손 조준 IK는 쉰다
            }
            if (dead && Spellcasting != null) Spellcasting.Cancel();
            Vector3 velocity = dead ? Vector3.zero : Vector3.ProjectOnPlane(motor.Velocity, Vector3.up);
            Vector3 localDirection = motor.transform.InverseTransformDirection(velocity.normalized);
            if (HasParameter(speedParameter, AnimatorControllerParameterType.Float))
                animator.SetFloat(speedParameter, velocity.magnitude);
            if (HasParameter(moveXParameter, AnimatorControllerParameterType.Float)) animator.SetFloat(moveXParameter, localDirection.x);
            if (HasParameter(moveZParameter, AnimatorControllerParameterType.Float)) animator.SetFloat(moveZParameter, localDirection.z);
            if (HasParameter(groundedParameter, AnimatorControllerParameterType.Bool))
                animator.SetBool(groundedParameter, motor.IsGrounded);
            if (HasParameter(verticalSpeedParameter, AnimatorControllerParameterType.Float))
                animator.SetFloat(verticalSpeedParameter, dead ? 0f : motor.VerticalSpeed);
            bool charging = !dead && updraft != null && updraft.IsCharging;
            if (HasParameter(chargingParameter, AnimatorControllerParameterType.Bool)) animator.SetBool(chargingParameter, charging);
            if (HasParameter(chargeParameter, AnimatorControllerParameterType.Float)) animator.SetFloat(chargeParameter, charging ? updraft.Charge : 0f);
            // 공중에 뜨면 강한 착지 표시를 지워 다음 착지에 남지 않게 한다 (대시·시전 중 착지처럼 착지 전환을 거치지 않은 경우 포함).
            if (!motor.IsGrounded && HasParameter(hardLandParameter, AnimatorControllerParameterType.Bool)) animator.SetBool(hardLandParameter, false);
            if (HasParameter(dashParameter, AnimatorControllerParameterType.Bool)) animator.SetBool(dashParameter, motor.IsDashing);
        }

        // Check after Animator evaluation so the next input frame can act as soon as the clip ends.
        void LateUpdate() => UpdateHardLandingLock();

        void ClearHardLandingLock()
        {
            awaitingHardLanding = sawHardLanding = false;
            if (motor != null) motor.SetHardLandingLock(false);
        }

        void UpdateHardLandingLock()
        {
            if (!awaitingHardLanding) return;
            if (motor == null || !motor.isActiveAndEnabled || animator == null || !animator.isActiveAndEnabled
                || (healthSource is ILifeState life && life.State != global::LifeState.Alive))
            { ClearHardLandingLock(); return; }
            if (Time.timeScale <= 0f) return;
            var current = animator.GetCurrentAnimatorStateInfo(0);
            bool currentLanding = current.IsName("Hard Landing");
            bool nextLanding = animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("Hard Landing");
            if (currentLanding || nextLanding)
            {
                sawHardLanding = true;
                // The completed clip may remain current during the 0.15s blend to locomotion.
                // That visual blend must not extend the action lock.
                var landing = nextLanding ? animator.GetNextAnimatorStateInfo(0) : current;
                if (landing.normalizedTime >= 1f) ClearHardLandingLock();
                return;
            }
            hardLandingEntryWait += Time.deltaTime;
            // Do not strand input if a different controller cannot enter the expected state.
            if (sawHardLanding || hardLandingEntryWait > .5f) ClearHardLandingLock();
        }
        public void PlayFire()
        {
            if (fireEffectAnchor != null && FirePoint != null)
                fireEffectAnchor.SetPositionAndRotation(FirePoint.position, FirePoint.rotation);
            if (Spellcasting != null && Spellcasting.isActiveAndEnabled) Spellcasting.PlayFire();
            if (HasParameter(attackTrigger, AnimatorControllerParameterType.Trigger)) animator.SetTrigger(attackTrigger);
            onFired.Invoke();
        }
        bool HasParameter(string parameter, AnimatorControllerParameterType type)
        {
            if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(parameter)) return false;
            foreach (var entry in animator.parameters) if (entry.name == parameter && entry.type == type) return true;
            return false;
        }
    }
}

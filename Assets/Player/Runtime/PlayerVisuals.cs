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
        public string moveXParameter = "MoveX";
        public string moveZParameter = "MoveZ";
        public UnityEvent onFired = new UnityEvent();
        public UnityEvent onDamaged = new UnityEvent();
        public UnityEvent onIncapacitated = new UnityEvent();
        public UnityEvent onDashStarted = new UnityEvent();
        [SerializeField, HideInInspector] GameObject visualInstance;
        Animator animator;
        Transform muzzle;
        public PlayerSpellcasting Spellcasting { get; private set; }
        public Transform FirePoint => muzzle != null ? muzzle : fallbackFirePoint;

        void Awake() => RebuildVisual();
        void OnEnable()
        {
            if (healthSource is IDamageEvents damage) damage.Damaged += OnDamaged;
            if (healthSource is ILifeState life) life.Died += OnDied;
            if (motor != null) motor.DashStarted += OnDash;
            if (motor != null) motor.Jumped += OnJump;
        }
        void OnDisable()
        {
            if (healthSource is IDamageEvents damage) damage.Damaged -= OnDamaged;
            if (healthSource is ILifeState life) life.Died -= OnDied;
            if (motor != null) motor.DashStarted -= OnDash;
            if (motor != null) motor.Jumped -= OnJump;
        }
        void OnDamaged(DamageAppliedInfo info)
        {
            if (HasParameter(hitTrigger, AnimatorControllerParameterType.Trigger)) animator.SetTrigger(hitTrigger);
            onDamaged.Invoke();
        }
        void OnDied(DeathInfo info)
        {
            if (Spellcasting != null) Spellcasting.Cancel();
            if (HasParameter("Dead", AnimatorControllerParameterType.Bool)) animator.SetBool("Dead", true);
            if (HasParameter(deathTrigger, AnimatorControllerParameterType.Trigger)) animator.SetTrigger(deathTrigger);
            onIncapacitated.Invoke();
        }
        void OnDash() => onDashStarted.Invoke();
        void OnJump()
        {
            if (HasParameter(jumpTrigger, AnimatorControllerParameterType.Trigger)) animator.SetTrigger(jumpTrigger);
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
            if (reactionLayer >= 0) animator.SetLayerWeight(reactionLayer, dead ? 0f : 1f);
            if (dead && Spellcasting != null) Spellcasting.Cancel();
            Vector3 velocity = dead ? Vector3.zero : Vector3.ProjectOnPlane(motor.Velocity, Vector3.up);
            Vector3 localDirection = motor.transform.InverseTransformDirection(velocity.normalized);
            if (HasParameter(speedParameter, AnimatorControllerParameterType.Float))
                animator.SetFloat(speedParameter, velocity.magnitude);
            if (HasParameter(moveXParameter, AnimatorControllerParameterType.Float)) animator.SetFloat(moveXParameter, localDirection.x);
            if (HasParameter(moveZParameter, AnimatorControllerParameterType.Float)) animator.SetFloat(moveZParameter, localDirection.z);
            if (HasParameter(groundedParameter, AnimatorControllerParameterType.Bool))
                animator.SetBool(groundedParameter, motor.IsGrounded);
            if (HasParameter(dashParameter, AnimatorControllerParameterType.Bool)) animator.SetBool(dashParameter, motor.IsDashing);
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

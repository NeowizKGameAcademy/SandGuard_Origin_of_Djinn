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
        public UnityEvent onFired = new UnityEvent();
        public UnityEvent onDamaged = new UnityEvent();
        public UnityEvent onIncapacitated = new UnityEvent();
        public UnityEvent onDashStarted = new UnityEvent();
        [SerializeField, HideInInspector] GameObject visualInstance;
        Animator animator;
        Transform muzzle;
        public Transform FirePoint => muzzle != null ? muzzle : fallbackFirePoint;

        void Awake() => RebuildVisual();
        void OnEnable()
        {
            if (healthSource is IDamageEvents damage) damage.Damaged += OnDamaged;
            if (healthSource is ILifeState life) life.Died += OnDied;
            if (motor != null) motor.DashStarted += OnDash;
        }
        void OnDisable()
        {
            if (healthSource is IDamageEvents damage) damage.Damaged -= OnDamaged;
            if (healthSource is ILifeState life) life.Died -= OnDied;
            if (motor != null) motor.DashStarted -= OnDash;
        }
        void OnDamaged(DamageAppliedInfo info)
        {
            if (HasParameter(hitTrigger, AnimatorControllerParameterType.Trigger)) animator.SetTrigger(hitTrigger);
            onDamaged.Invoke();
        }
        void OnDied(DeathInfo info)
        {
            if (HasParameter(deathTrigger, AnimatorControllerParameterType.Trigger)) animator.SetTrigger(deathTrigger);
            onIncapacitated.Invoke();
        }
        void OnDash() => onDashStarted.Invoke();

        [ContextMenu("외형 다시 연결 / Rebuild Visual")]
        public void RebuildVisual()
        {
            if (visualRoot == null) return;
            if (visualInstance != null)
            {
                visualInstance.SetActive(false);
                if (Application.isPlaying) Destroy(visualInstance); else DestroyImmediate(visualInstance);
            }
            animator = null; muzzle = null; visualInstance = null;
            if (visualPrefab == null) return;
            visualInstance = Instantiate(visualPrefab, visualRoot);
            visualInstance.name = "Visual (Replaceable)";
            visualInstance.transform.localPosition = localPosition;
            visualInstance.transform.localRotation = Quaternion.Euler(localEulerAngles);
            visualInstance.transform.localScale = localScale;
            PlayerVisualBindings bindings = visualInstance.GetComponentInChildren<PlayerVisualBindings>(true);
            animator = bindings != null && bindings.animator != null ? bindings.animator : visualInstance.GetComponentInChildren<Animator>(true);
            muzzle = bindings != null ? bindings.firePoint : null;
            if (animator != null) animator.applyRootMotion = false;
        }

        void LateUpdate()
        {
            if (animator == null || motor == null) return;
            if (HasParameter(speedParameter, AnimatorControllerParameterType.Float))
                animator.SetFloat(speedParameter, Vector3.ProjectOnPlane(motor.Velocity, Vector3.up).magnitude);
            if (HasParameter(groundedParameter, AnimatorControllerParameterType.Bool))
                animator.SetBool(groundedParameter, motor.IsGrounded);
            if (HasParameter(dashParameter, AnimatorControllerParameterType.Bool)) animator.SetBool(dashParameter, motor.IsDashing);
        }
        public void PlayFire()
        {
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

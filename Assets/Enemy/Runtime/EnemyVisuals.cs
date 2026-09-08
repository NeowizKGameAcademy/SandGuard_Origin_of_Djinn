using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace SandGuard.Enemy
{
    /// <summary>외형 프리팹을 VisualRoot 아래에 붙인다. 애니메이터가 없으면 무기 축을 코드로 휘두른다.</summary>
    /// <remarks>Visual Prefab만 바꾸면 게임 동작은 그대로 두고 모델을 교체할 수 있다. 모델은 +Z를 정면으로 맞춘다.</remarks>
    public sealed class EnemyVisuals : MonoBehaviour
    {
        [Header("외형 교체 — 게임 동작과 독립")]
        public GameObject visualPrefab;
        public Transform visualRoot;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale = Vector3.one;
        public EnemyMotor motor;
        public EnemyHealth health;
        [Header("애니메이터 파라미터 — 없으면 생략")]
        public string speedParameter = "Speed";
        public string attackTrigger = "Attack";
        public string dieTrigger = "Die";
        [Header("애니메이터가 없을 때의 칼 휘두르기 (무기 축의 로컬 회전)")]
        public Vector3 restEuler = new Vector3(20f, 0f, 0f);
        public Vector3 raisedEuler = new Vector3(-60f, 0f, 0f);
        public Vector3 struckEuler = new Vector3(95f, 0f, 0f);
        [Min(0.02f)] public float strikeTime = 0.08f;
        [Min(0.02f)] public float recoverTime = 0.3f;
        public UnityEvent onAttack = new UnityEvent();
        public UnityEvent onDied = new UnityEvent();
        [SerializeField, HideInInspector] GameObject visualInstance;
        Animator animator;
        Transform weaponPivot;
        Coroutine swing;
        /// <summary>외형이 지정한 공격 기준점. 없으면 null이다.</summary>
        public Transform AttackOrigin { get; private set; }

        void Awake() => RebuildVisual();
        void OnEnable() { if (health != null) health.Died += OnDied; }
        void OnDisable() { if (health != null) health.Died -= OnDied; }

        [ContextMenu("외형 다시 연결 / Rebuild Visual")]
        public void RebuildVisual()
        {
            if (visualRoot == null) return;
            if (visualInstance != null)
            {
                visualInstance.SetActive(false);
                if (Application.isPlaying) Destroy(visualInstance); else DestroyImmediate(visualInstance);
            }
            animator = null; weaponPivot = null; AttackOrigin = null; visualInstance = null;
            if (visualPrefab == null) return;
            visualInstance = Instantiate(visualPrefab, visualRoot);
            visualInstance.name = "Visual (Replaceable)";
            visualInstance.transform.localPosition = localPosition;
            visualInstance.transform.localRotation = Quaternion.Euler(localEulerAngles);
            visualInstance.transform.localScale = localScale;
            EnemyVisualBindings bindings = visualInstance.GetComponentInChildren<EnemyVisualBindings>(true);
            animator = bindings != null && bindings.animator != null ? bindings.animator : visualInstance.GetComponentInChildren<Animator>(true);
            weaponPivot = bindings != null ? bindings.weaponPivot : null;
            AttackOrigin = bindings != null ? bindings.attackOrigin : null;
            if (animator != null) animator.applyRootMotion = false;
            if (weaponPivot != null) weaponPivot.localRotation = Quaternion.Euler(restEuler);
        }

        void LateUpdate()
        {
            if (animator == null || motor == null) return;
            if (HasParameter(speedParameter, AnimatorControllerParameterType.Float))
                animator.SetFloat(speedParameter, Vector3.ProjectOnPlane(motor.Velocity, Vector3.up).magnitude);
        }

        /// <summary>공격 연출을 시작한다. windup이 지나는 순간 칼이 내려온다.</summary>
        public void PlayAttack(float windup)
        {
            if (HasParameter(attackTrigger, AnimatorControllerParameterType.Trigger)) animator.SetTrigger(attackTrigger);
            else if (weaponPivot != null)
            {
                if (swing != null) StopCoroutine(swing);
                swing = StartCoroutine(SwingWeapon(windup));
            }
            onAttack.Invoke();
        }

        public void CancelAttack()
        {
            if (swing != null) { StopCoroutine(swing); swing = null; }
            if (weaponPivot != null) weaponPivot.localRotation = Quaternion.Euler(restEuler);
        }

        IEnumerator SwingWeapon(float windup)
        {
            yield return Rotate(weaponPivot, Quaternion.Euler(raisedEuler), windup);
            yield return Rotate(weaponPivot, Quaternion.Euler(struckEuler), strikeTime);
            yield return Rotate(weaponPivot, Quaternion.Euler(restEuler), recoverTime);
            swing = null;
        }

        static IEnumerator Rotate(Transform target, Quaternion to, float duration)
        {
            Quaternion from = target.localRotation;
            for (float t = 0f; t < duration && target != null; t += Time.deltaTime)
            {
                target.localRotation = Quaternion.Slerp(from, to, t / duration);
                yield return null;
            }
            if (target != null) target.localRotation = to;
        }

        void OnDied(DeathInfo info)
        {
            CancelAttack();
            if (HasParameter(dieTrigger, AnimatorControllerParameterType.Trigger)) animator.SetTrigger(dieTrigger);
            else if (visualRoot != null) StartCoroutine(Rotate(visualRoot, Quaternion.Euler(0f, 0f, 80f), 0.4f)); // 옆으로 쓰러진다.
            onDied.Invoke();
        }

        bool HasParameter(string parameter, AnimatorControllerParameterType type)
        {
            if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(parameter)) return false;
            foreach (var entry in animator.parameters) if (entry.name == parameter && entry.type == type) return true;
            return false;
        }
    }
}

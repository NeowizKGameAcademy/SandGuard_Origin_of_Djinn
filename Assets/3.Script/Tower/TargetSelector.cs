using UnityEngine;

namespace Tower
{
    [RequireComponent(typeof(TargetDetector))]
    public class TargetSelector : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private TargetDetector detector;
        [Min(0f)] [SerializeField] private float searchInterval = 0.2f;

        private ICombatTarget currentTarget;
        private float searchTimer;

        public ICombatTarget Target => isActiveAndEnabled && detector != null
            && detector.Contains(currentTarget) ? currentTarget : null;
        public bool HasTarget => Target != null;
        public Transform TargetTransform => Target is Component component ? component.transform : null;
        public Vector3 AimPoint => Target != null ? Target.HitPosition : transform.position;

        private void Awake()
        {
            if (detector == null)
                TryGetComponent(out detector);
        }

        private void OnEnable()
        {
            searchTimer = 0f;
        }

        private void LateUpdate()
        {
            searchTimer -= Time.deltaTime;

            if (searchTimer > 0f && HasTarget)
                return;

            ChooseClosest();
            searchTimer = Mathf.Max(0f, searchInterval);
        }

        private void OnDisable()
        {
            ClearTarget();
        }

        public ICombatTarget SelectTarget()
        {
            if (isActiveAndEnabled && detector != null)
                detector.Scan();

            ChooseClosest();
            searchTimer = Mathf.Max(0f, searchInterval);
            return Target;
        }

        private void ChooseClosest()
        {
            ClearTarget();

            if (!isActiveAndEnabled || detector == null || !detector.isActiveAndEnabled)
                return;

            float closestDistance = float.PositiveInfinity;

            foreach (var candidate in detector.Targets)
            {
                if (!detector.IsValidTarget(candidate))
                    continue;

                // 기존 FindEnemy와 같이 대상 Transform까지의 거리로 선택한다.
                float distance = (((Component)candidate).transform.position - detector.transform.position).sqrMagnitude;

                if (distance >= closestDistance)
                    continue;

                closestDistance = distance;
                currentTarget = candidate;
            }
        }

        public void ClearTarget()
        {
            currentTarget = null;
        }
    }
}

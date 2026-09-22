using UnityEngine;

namespace Tower
{
    public class TargetSelector : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private TargetDetector detector;
        [Min(0f)] [SerializeField] private float searchInterval = 0.2f;

        private ICombatTarget currentTarget;
        private float searchTimer;
        private AnubisController minion;

        public ICombatTarget Target => isActiveAndEnabled && detector != null
            && detector.Contains(currentTarget) ? currentTarget : null;
        public bool HasTarget => Target != null;
        public Transform TargetTransform => Target is Component component ? component.transform : null;
        public Vector3 AimPoint => Target != null ? Target.HitPosition : transform.position;

        private void Awake()
        {
            ResolveDetector();
        }

        private void OnEnable()
        {
            // 풀에서 생성된 소환수는 Awake 시점에는 풀 오브젝트 아래에 있다가,
            // 활성화 직전에 타워 아래로 옮겨진다. 이때 부모 탐지기를 다시 찾아야 한다.
            ResolveDetector();
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
                // if (!detector.IsValidTarget(candidate)) // 아누비스는 공격 불가능한 다른 층의 적을 선택하지 않는다.
                if (!detector.IsValidTarget(candidate) || (minion != null && !minion.CanAttackHeight(candidate)))
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

        private void ResolveDetector()
        {
            if (minion == null) TryGetComponent(out minion);
            if (detector == null)
                TryGetComponent(out detector);

            if (detector == null)
                detector = GetComponentInParent<TargetDetector>();
        }
    }
}

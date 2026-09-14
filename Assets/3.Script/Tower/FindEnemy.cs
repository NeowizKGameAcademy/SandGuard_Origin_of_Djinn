using UnityEngine;

public class FindEnemy : MonoBehaviour
{
    [Header("Auto Aim")]
    [SerializeField] private DetectRange detectRange;
    [SerializeField] private float searchTime = 0.2f;

    private float autoAimRange;
    private float autoAimDistance;
    private float searchTimer;

    private Transform targetTransform;

    public Transform target => targetTransform;
    public float range => autoAimRange;

    private void Awake()
    {
        if (detectRange == null)
            detectRange = GetComponent<DetectRange>();

        if (detectRange == null)
            detectRange = GetComponentInChildren<DetectRange>();

        if (detectRange == null)
            Debug.LogError($"{name} : DetectRange를 찾을 수 없습니다.");
    }

    private void Update()
    {
        if (detectRange == null)
            return;

        autoAimRange = detectRange.range;
        autoAimDistance = autoAimRange * autoAimRange;

        // 현재 타겟이 있을 때
        if (targetTransform != null)
        {
            // 비활성화된 적이면 타겟 해제
            if (!targetTransform.gameObject.activeInHierarchy)
            {
                targetTransform = null;
                return;
            }

            float targetDistance =
                (targetTransform.position - transform.position).sqrMagnitude;

            // 사거리 밖으로 나가면 타겟 해제
            if (targetDistance > autoAimDistance)
            {
                targetTransform = null;
                return;
            }

            // 현재 타겟 유지
            return;
        }

        searchTimer -= Time.deltaTime;

        if (searchTimer <= 0f)
        {
            FindClosestTarget();
            searchTimer = searchTime;
        }
    }

    private void FindClosestTarget()
    {
        float closestDistance = autoAimDistance;
        Transform closestTarget = null;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");

        for (int i = 0; i < enemies.Length; i++)
        {
            GameObject enemy = enemies[i];

            if (enemy == null || !enemy.activeInHierarchy)
                continue;

            float distance =
                (enemy.transform.position - transform.position).sqrMagnitude;

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestTarget = enemy.transform;
            }
        }

        targetTransform = closestTarget;
    }
}
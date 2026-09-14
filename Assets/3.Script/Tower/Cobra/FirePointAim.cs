using UnityEngine;

public class FirePointAim : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private FindEnemy findEnemy;

    private void Awake()
    {
        // Inspector에서 직접 연결하지 않았을 때만 자동 탐색
        if (findEnemy == null)
        {
            findEnemy = GetComponentInParent<FindEnemy>();
        }
    }

    private void Update()
    {
        if (findEnemy == null || findEnemy.target == null)
            return;

        Vector3 direction =
            findEnemy.target.position - transform.position;

        float horizontalDistance =
            new Vector2(direction.x, direction.z).magnitude;

        float angle =
            Mathf.Atan2(direction.y, horizontalDistance)
            * Mathf.Rad2Deg;

        Vector3 rotation = transform.localEulerAngles;

        rotation.x = -angle;

        transform.localEulerAngles = rotation;
    }
}
using UnityEngine;

public class EnemyMove : MonoBehaviour
{
    [SerializeField] private EnemyDamage enemyDamage;
    [SerializeField] private Rigidbody rigidbody;

    [Header("Way Point")]
    [SerializeField] private Transform[] WayPoints;
    [SerializeField] private int Current_Waypoint = 0;

    [Header("Move")]
    [SerializeField] private float MoveSpeed = 5f;
    [SerializeField] private float Cur_MoveSpeed;

    private void OnEnable()
    {
        TryGetComponent(out enemyDamage);
        TryGetComponent(out rigidbody);

        Cur_MoveSpeed = MoveSpeed;
        Current_Waypoint = 0;
    }

    private void FixedUpdate()
    {
        if (WayPoints == null || WayPoints.Length == 0)
            return;

        Move();
    }

    private void Move()
    {
        Transform target = WayPoints[Current_Waypoint];

        if (enemyDamage.is_Slowed)
            Cur_MoveSpeed = MoveSpeed * 0.25f;
        else
            Cur_MoveSpeed = MoveSpeed;

        Vector3 NextPosition = Vector3.MoveTowards(
            rigidbody.position,
            target.position,
            Cur_MoveSpeed * Time.fixedDeltaTime
        );

        rigidbody.MovePosition(NextPosition);

        // MovePosition으로 계산한 다음 위치 기준으로 도착 판정
        if (Vector3.Distance(NextPosition, target.position) < 0.5f)
        {
            Current_Waypoint++;

            if (Current_Waypoint >= WayPoints.Length)
            {
                Current_Waypoint = 0;
            }
        }
    }
}
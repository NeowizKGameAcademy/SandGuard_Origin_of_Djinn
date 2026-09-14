using UnityEngine;

public class EnemyMove : MonoBehaviour
{
    [SerializeField] private EnemyState enemyState;
    [SerializeField] private Rigidbody rigidbody;

    [Header("Way Point")]
    [SerializeField] private Transform[] WayPoints;
    [SerializeField] private int Current_Waypoint = 0;

    [Header("Move")]
    [SerializeField] private float MoveSpeed = 5f;
    [SerializeField] private float Cur_MoveSpeed;

    private void OnEnable()
    {
        TryGetComponent(out enemyState);
        TryGetComponent(out rigidbody);

        Cur_MoveSpeed = MoveSpeed;
        Current_Waypoint = 0;
    }

    private void FixedUpdate()
    {
        if (WayPoints == null || WayPoints.Length == 0)
            return;

        SetMoveSpeed();
        Move();
    }

    private void SetMoveSpeed()
    {
        Cur_MoveSpeed = MoveSpeed * enemyState.SlowRatio;
    }

    private void Move()
    {
        Transform target = WayPoints[Current_Waypoint];

        Vector3 NextPosition = Vector3.MoveTowards(
            rigidbody.position,
            target.position,
            Cur_MoveSpeed * Time.fixedDeltaTime
        );

        rigidbody.MovePosition(NextPosition);

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
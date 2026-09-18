using UnityEngine;

public class CreatureMove : CreatureController
{
    [SerializeField] private FindEnemy enemy;

    [Header("Move")]
    [SerializeField] private float MoveSpeed = 5f;
    [SerializeField] private float StopDistance = 1.5f;
    [SerializeField] private float RotateSpeed = 0.5f;

    private void OnEnable()
    {
        TryGetComponent(out enemy);
    }

    private void Update()
    {
        if (enemy.target != null)
            Follow();
        
        else
            Return();
    }

    private void Follow()
    {
        Vector3 targetPosition = enemy.target.position;

        targetPosition.y = transform.position.y;

        float distance = (targetPosition - transform.position).sqrMagnitude;
        Vector3 direction = (targetPosition - transform.position).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(direction);

        if (distance <= StopDistance * StopDistance)
            return;

        transform.position = Vector3.MoveTowards(transform.position, targetPosition, MoveSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * RotateSpeed);
    }

    private void Return()
    {
        Vector3 direction = (summonPoint - transform.position).normalized;

        if (direction.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * RotateSpeed * 5f);
        }

        transform.position = Vector3.MoveTowards(transform.position, summonPoint, MoveSpeed * 2f * Time.deltaTime);
    }
}
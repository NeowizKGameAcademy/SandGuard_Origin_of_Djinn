using UnityEngine;

public class CreatureMove : Creature
{
    [SerializeField] private FindEnemy enemy;

    [Header("Move")]
    [SerializeField] private float MoveSpeed = 5f;
    [SerializeField] private float StopDistance = 1.5f;

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

        if (distance <= StopDistance * StopDistance)
            return;

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            MoveSpeed * Time.deltaTime
        );
    }

    private void Return()
    {
        transform.position = Vector3.MoveTowards(
            transform.position,
            summonPoint,
            MoveSpeed * Time.deltaTime
        );
    }
}
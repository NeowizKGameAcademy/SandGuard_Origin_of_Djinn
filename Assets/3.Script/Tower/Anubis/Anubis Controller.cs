using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnubisController : MonoBehaviour
{
    [SerializeField] private FindEnemy enemy;
    [SerializeField] private AnubisStatus status;

    [Header("Spawn Point")]
    [SerializeField] private Transform SpawnPoint;

    [Header("Move")]
    [SerializeField] private float StopDistance = 1.5f;
    [SerializeField] private float RotateSpeed = 0.5f;

    public float distance => StopDistance;

    private void Awake()
    {
        TryGetComponent(out enemy);
        TryGetComponent(out status);
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

        transform.position = Vector3.MoveTowards(transform.position, targetPosition, status.moveSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * RotateSpeed);
    }

    private void Return()
    {
        Vector3 direction = (SpawnPoint.position - transform.position).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(direction);

        transform.position = Vector3.MoveTowards(transform.position, SpawnPoint.position, status.moveSpeed * 2f * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * RotateSpeed * 5f);
    }
}

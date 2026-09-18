using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnubisSkill : MonoBehaviour
{
    [SerializeField] private FindEnemy findEnemy;
    [SerializeField] private AnubisStatus status;
    [SerializeField] private AnubisController controller;

    [Header("Skill")]
    [SerializeField] private float skillRange = 15f;
    [SerializeField] private float skillCooldown = 5f;

    [Header("Launch")]
    [SerializeField] private float horizontalPower = 6f;
    [SerializeField] private float verticalPower = 8f;

    [Header("Target")]
    [SerializeField] private LayerMask targetMask;

    private float skillTimer;

    private void Awake()
    {
        TryGetComponent(out findEnemy);
        TryGetComponent(out status);
        TryGetComponent(out controller);
    }
    private void Update()
    {
        skillTimer -= Time.deltaTime;

        if (skillTimer <= 0f)
        {
            UseSkill();
            skillTimer = skillCooldown;
        }
    }

    private void UseSkill()
    {
        if (findEnemy.target == null)
            return;

        Vector3 targetDirection =
            findEnemy.target.position - transform.position;

        targetDirection.y = 0f;

        if (targetDirection.sqrMagnitude > controller.distance * controller.distance)
            return;

        Collider[] hits = Physics.OverlapSphere(transform.position, skillRange, targetMask);

        foreach (Collider hit in hits)
        {
            ICombatTarget target = hit.GetComponentInParent<ICombatTarget>();

            if (target == null || !target.IsTargetable)
                continue;

            Transform targetTransform = ((Component)target).transform;

            Vector3 direction =
                targetTransform.position - transform.position;

            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.001f)
                continue;

            direction.Normalize();

            if (Vector3.Dot(transform.forward, direction) <= 0f)
                continue;

            IDisplaceable displaceable = targetTransform.GetComponentInParent<IDisplaceable>();

            if (displaceable == null)
                continue;

            Vector3 launchVelocity = direction * horizontalPower + Vector3.up * verticalPower;

            displaceable.Launch(launchVelocity);
        }
    }
}

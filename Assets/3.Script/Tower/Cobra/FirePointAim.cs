using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FirePointAim : MonoBehaviour
{
    [SerializeField] private FindEnemy findEnemy;
    [SerializeField] private Transform aimPivot;

    private void Awake()
    {
        if (findEnemy == null)
            findEnemy = GetComponentInParent<FindEnemy>();
    }

    private void Update()
    {
        if (findEnemy == null || findEnemy.targetCombat == null)
            return;

        Vector3 direction = findEnemy.aimPoint - aimPivot.position;

        float horizontalDistance = new Vector2(direction.x, direction.z).magnitude;
        float angle = Mathf.Atan2(direction.y, horizontalDistance) * Mathf.Rad2Deg;

        Vector3 rotation = aimPivot.localEulerAngles;

        rotation.x = -angle;

        aimPivot.localEulerAngles = rotation;
    }
}

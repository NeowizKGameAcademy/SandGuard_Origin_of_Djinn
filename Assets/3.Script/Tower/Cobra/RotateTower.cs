using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateTower : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private FindEnemy findEnemy;
    [SerializeField] private Rigidbody rigidbody;

    [Header("Rotation")]
    [SerializeField] private float Rotation_Speed = 100f;

    private Quaternion Last_Rotation;

    private void Awake()
    {
        TryGetComponent(out findEnemy);
        TryGetComponent(out rigidbody);

        Last_Rotation = rigidbody.rotation;
    }

    private void FixedUpdate()
    {
        if (findEnemy == null || findEnemy.target == null)
        {
            rigidbody.MoveRotation(Last_Rotation);
            return;
        }

        Vector3 direction = findEnemy.target.position - transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        if (float.IsNaN(direction.x) ||
            float.IsNaN(direction.y) ||
            float.IsNaN(direction.z))
            return;

        Quaternion target_rotation = Quaternion.LookRotation(direction.normalized);

        Quaternion rotation = Quaternion.RotateTowards(rigidbody.rotation, target_rotation, Rotation_Speed);

        Last_Rotation = rotation;

        rigidbody.MoveRotation(rotation);
    }
}
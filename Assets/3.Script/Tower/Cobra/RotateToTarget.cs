using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tower
{
    public class RotateToTarget : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private TargetSelector Target;
        [SerializeField] private Rigidbody rigidbody;

        [Header("Rotation")]
        [SerializeField] private float Rotation_Speed = 100f;

        private Quaternion Last_Rotation;

        private void OnEnable()
        {
            if(Target == null)
                TryGetComponent(out Target);

            if (rigidbody == null)
                TryGetComponent(out rigidbody);

            Last_Rotation = rigidbody.rotation;
        }

        private void FixedUpdate()
        {
            if (Target == null || Target.TargetTransform == null)
            {
                rigidbody.MoveRotation(Last_Rotation);
                return;
            }

            Vector3 direction = Target.TargetTransform.position - transform.position;

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
}

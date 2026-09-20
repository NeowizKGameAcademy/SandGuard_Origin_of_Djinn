using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tower
{
    public class AimToTarget : MonoBehaviour
    {
        [SerializeField] private TargetSelector Target;
        [SerializeField] private Transform aimPivot;

        private void Awake()
        {
            if (Target == null)
                Target = GetComponentInParent<TargetSelector>();
        }

        private void Update()
        {
            if (Target == null || Target.TargetTransform == null)
                return;

            Vector3 direction = Target.AimPoint - aimPivot.position;

            float horizontalDistance = new Vector2(direction.x, direction.z).magnitude;
            float angle = Mathf.Atan2(direction.y, horizontalDistance) * Mathf.Rad2Deg;

            Vector3 rotation = aimPivot.localEulerAngles;

            rotation.x = -angle;

            aimPivot.localEulerAngles = rotation;
        }
    }
}

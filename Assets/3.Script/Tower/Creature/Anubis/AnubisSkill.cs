using UnityEngine;

namespace Tower
{
    public sealed class AnubisSkill : MonoBehaviour
    {
        [Min(0f)] public float range = 15f;
        [Range(0f, 360f)] public float angle = 90f;
        public float horizontalPower = 6f;
        public float verticalPower = 8f;
        public LayerMask targetMask = ~0;

        public void Cast(Vector3 origin, Vector3 forward)
        {
            foreach (var collider in Physics.OverlapSphere(origin, range, targetMask))
            {
                var target = collider.GetComponentInParent<ICombatTarget>();

                if (target == null || !target.IsTargetable) 
                    continue;

                var direction = target.HitPosition - origin;
                direction.y = 0f;

                if (direction.sqrMagnitude < 0.001f || Vector3.Angle(forward, direction) > angle * 0.5f) 
                    continue;

                var displaceable = ((Component)target).GetComponentInParent<IDisplaceable>();
                displaceable?.Launch(direction.normalized * horizontalPower + Vector3.up * verticalPower);
            }
        }
    }
}

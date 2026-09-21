using UnityEngine;

namespace SandGuard.Player
{
    public sealed class PlayerAimer : MonoBehaviour
    {
        public Camera viewCamera;
        public Transform owner;
        [Min(1f)] public float maxDistance = 100f;
        public LayerMask aimMask = ~0;
        public Vector3 GetAimPoint() => GetAimPoint(maxDistance);

        public Vector3 GetAimPoint(float distance)
        {
            if (viewCamera == null) return transform.position + transform.forward * distance;
            Ray ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            Vector3 point = ray.GetPoint(distance);
            float nearest = distance;
            foreach (var hit in Physics.RaycastAll(ray, distance, aimMask, QueryTriggerInteraction.Ignore))
            {
                if (owner != null && hit.transform.IsChildOf(owner)) continue;
                if (hit.distance < nearest) { nearest = hit.distance; point = hit.point; }
            }
            return point;
        }
    }
}

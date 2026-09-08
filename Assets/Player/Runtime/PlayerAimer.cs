using UnityEngine;

namespace SandGuard.Player
{
    public sealed class PlayerAimer : MonoBehaviour
    {
        public Camera viewCamera;
        public Transform owner;
        [Min(1f)] public float maxDistance = 100f;
        public LayerMask aimMask = ~0;
        public Vector3 GetAimPoint()
        {
            if (viewCamera == null) return transform.position + transform.forward * maxDistance;
            Ray ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            Vector3 point = ray.GetPoint(maxDistance);
            float nearest = maxDistance;
            foreach (var hit in Physics.RaycastAll(ray, maxDistance, aimMask, QueryTriggerInteraction.Ignore))
            {
                if (owner != null && hit.transform.IsChildOf(owner)) continue;
                if (hit.distance < nearest) { nearest = hit.distance; point = hit.point; }
            }
            return point;
        }
    }
}

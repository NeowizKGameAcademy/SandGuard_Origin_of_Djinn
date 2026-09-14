using UnityEngine;

namespace DesertTower.VFX
{
    /// <summary>Selection-only construction guides. No visible sphere or path renderer in the game.</summary>
    public sealed class VfxLightningGuide : MonoBehaviour
    {
        public Vector3 Center;
        public float Radius;
        void OnDrawGizmosSelected()
        {
            var previous = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.3f, 0.55f, 0.7f, 0.35f);
            Gizmos.DrawWireSphere(Center, Radius);
            Gizmos.color = new Color(0.8f, 0.2f, 0.1f, 0.5f);
            foreach (var path in GetComponentsInChildren<VfxLightningPath>())
                if (path.Points != null)
                    for (int i = 1; i < path.Points.Length; i++) Gizmos.DrawLine(path.Points[i - 1], path.Points[i]);
            Gizmos.matrix = previous;
        }
    }
}

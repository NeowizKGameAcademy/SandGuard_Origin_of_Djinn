using UnityEngine;

namespace SandGuard.Enemy
{
    /// <summary>Throwable prop and visual bindings. Detonation timing/damage belong to the attack controller.</summary>
    public sealed class ChiefBombProp : MonoBehaviour
    {
        public Rigidbody body;
        public SphereCollider hitCollider;
        public Transform gripPoint;
        public Transform fuseTip;
        public GameObject fuseEffects;
        public GameObject explosionPrefab;

        public void SetFuseLit(bool lit)
        {
            if (fuseEffects) fuseEffects.SetActive(lit);
        }

        public void Hold(Transform socket)
        {
            Vector3 worldScale = transform.lossyScale;
            if (body)
            {
                body.interpolation = RigidbodyInterpolation.None;
                if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
                body.isKinematic = true;
            }
            if (hitCollider) hitCollider.enabled = false;
            transform.SetParent(socket, false);
            Vector3 parentScale = socket.lossyScale;
            transform.localScale = new Vector3(worldScale.x / parentScale.x, worldScale.y / parentScale.y, worldScale.z / parentScale.z);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            if (gripPoint) transform.position += socket.position - gripPoint.position;
            SetFuseLit(false);
        }

        public void Release(Vector3 velocity)
        {
            transform.SetParent(null, true);
            if (hitCollider) hitCollider.enabled = true;
            if (body)
            {
                body.isKinematic = false;
                body.useGravity = true;
                body.linearVelocity = velocity;
                body.angularVelocity = new Vector3(2f, 1f, 3f);
            }
            SetFuseLit(true);
        }
    }
}

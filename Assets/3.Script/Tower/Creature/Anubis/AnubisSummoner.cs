using UnityEngine;

namespace Tower
{
    public sealed class AnubisSummoner : MonoBehaviour
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private TowerStatus status;

        private AnubisController unit;
        private float respawnTimer;

        private void Awake()
        {
            if (status == null)
                TryGetComponent(out status);
        }

        private void Update()
        {
            if (unit != null)
                return;

            respawnTimer -= Time.deltaTime;

            if (respawnTimer > 0f || prefab == null || status?.anubis?.spawnPoint == null)
                return;

            var point = status.anubis.spawnPoint;
            unit = Instantiate(prefab, point.position, point.rotation, transform).GetComponent<AnubisController>();
        }

        public void Gone()
        {
            unit = null;
            respawnTimer = status != null ? status.anubis.respawnCooldown : 0f;
        }
    }
}

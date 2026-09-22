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
            // 생성된 프리팹의 자기 콜라이더를 제외하고, 첫 화면부터 발을 지면에 맞춘다.
            if (unit != null)
            {
                unit.transform.position = MinionGrounding.Project(unit.transform, unit.transform.position);
                unit.InitializeSpawn(unit.transform.position);
            }
        }

        public void Gone()
        {
            unit = null;
            respawnTimer = status != null ? status.anubis.respawnCooldown : 0f;
        }
    }
}

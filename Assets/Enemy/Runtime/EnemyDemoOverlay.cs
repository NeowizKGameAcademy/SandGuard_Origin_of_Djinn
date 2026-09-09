using UnityEngine;

namespace SandGuard.Enemy
{
    /// <summary>테스트용 안내와 적 상태 표시. 실제 HUD가 연결되면 제거할 수 있다.</summary>
    public sealed class EnemyDemoOverlay : MonoBehaviour
    {
        public GameObject enemyPrefab;
        public Transform spawnPoint;
        [Min(0f)] public float spawnRadius = 1.5f;
        EnemyBrain[] brains = new EnemyBrain[0];
        float nextScan;

        void Update()
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 0.5f;
            brains = FindObjectsByType<EnemyBrain>(FindObjectsSortMode.InstanceID);
        }

        void OnGUI()
        {
            GUI.Box(new Rect(16, 16, 460, 76 + brains.Length * 20), "SandGuard | Enemy Test");
            GUI.Label(new Rect(28, 40, 440, 24), "적은 코어로 진격하고, 범위 안의 적대 대상을 공격하며, 길이 막히면 막은 벽을 부순다.");
            if (GUI.Button(new Rect(28, 62, 140, 22), "적 추가 / Spawn") && enemyPrefab != null && spawnPoint != null) Spawn();
            for (int i = 0; i < brains.Length; i++)
            {
                var brain = brains[i];
                if (brain == null) continue;
                string target = brain.CurrentTarget != null ? brain.CurrentTarget.Kind.ToString() : "-";
                string health = brain.health != null ? Mathf.CeilToInt(brain.health.CurrentHealth) + "/" + Mathf.CeilToInt(brain.health.MaxHealth) : "?";
                GUI.Label(new Rect(28, 88 + i * 20, 440, 20), brain.name + "  " + brain.State + "  target: " + target + "  hp: " + health);
            }
        }

        public EnemyBrain Spawn()
        {
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            var instance = Instantiate(enemyPrefab, spawnPoint.position + new Vector3(offset.x, 0f, offset.y), spawnPoint.rotation);
            instance.name = "Enemy (spawned)";
            return instance.GetComponent<EnemyBrain>();
        }
    }
}

using System.Collections.Generic;
using SandGuard.Enemy;
using UnityEngine;

namespace SandGuard.Integration
{
    /// <summary>테스트용 적 공급기. 시작 시 몇 마리를 내고, 간격마다 몇 마리씩 더 내되 살아 있는 수를 제한한다. 실제 웨이브 시스템의 대체가 아니다.</summary>
    public sealed class EnemyStreamSpawner : MonoBehaviour
    {
        public GameObject enemyPrefab;
        public Transform[] spawnPoints;
        [Min(0f)] public float spawnRadius = 2f;
        [Min(0)] public int initialCount = 3;
        public bool autoSpawn = true;
        [Min(0.5f)] public float interval = 8f;
        [Min(1)] public int perWave = 2;
        [Min(1)] public int maxAlive = 8;
        [Tooltip("적의 진격 목표. 비우면 씬의 EnemyObjective를 쓴다")]
        public Transform objective;

        readonly List<GameObject> alive = new List<GameObject>();
        float next;

        public int TotalSpawned { get; private set; }
        public int AliveCount { get { alive.RemoveAll(e => e == null); return alive.Count; } }

        void Start()
        {
            for (int i = 0; i < initialCount; i++) Spawn();
            next = Time.time + interval;
        }

        void Update()
        {
            if (!autoSpawn || Time.time < next) return;
            next = Time.time + interval;
            for (int i = 0; i < perWave; i++) Spawn();
        }

        public GameObject Spawn()
        {
            if (enemyPrefab == null || spawnPoints == null || spawnPoints.Length == 0 || AliveCount >= maxAlive) return null;
            Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
            if (point == null) return null;
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            var instance = Instantiate(enemyPrefab, point.position + new Vector3(offset.x, 0f, offset.y), point.rotation);
            instance.name = "Enemy " + (++TotalSpawned);
            var brain = instance.GetComponent<EnemyBrain>();
            if (brain != null && objective != null) brain.objective = objective;
            alive.Add(instance);
            return instance;
        }

        public void ClearAll()
        {
            foreach (var enemy in alive) if (enemy != null) Destroy(enemy);
            alive.Clear();
        }
    }
}

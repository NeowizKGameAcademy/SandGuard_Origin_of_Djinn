using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tower
{
    /// <summary>관 타워가 소유한 스켈레톤의 슬롯과 재소환 흐름을 관리한다.</summary>
    public sealed class SummonManager : MonoBehaviour
    {
        [Header("Tower")]
        [SerializeField] private TowerStatus status;

        [Header("Summon")]
        [SerializeField] private GameObject Creature;
        [SerializeField] private Transform[] spawnPoints = Array.Empty<Transform>();
        [SerializeField] private Transform summonedRoot;

        private readonly List<GameObject> summoned = new List<GameObject>();

        public CoffinTowerConfig Config => status != null ? status.coffin : null;
        public GameObject CreaturePrefab => Creature;
        public IReadOnlyList<GameObject> Summoned => summoned;
        public int Capacity => Config != null ? Mathf.Max(0, Config.num) : 0;
        public int ActiveCount { get; private set; }

        public event Action<int, GameObject> SummonedAt;
        public event Action<int, GameObject> DespawnedAt;

        private void Awake()
        {
            if (status == null)
                status = GetComponentInParent<TowerStatus>();

            if (summonedRoot == null)
                summonedRoot = transform;

            EnsureSlots();
        }

        public void EnsureSlots()
        {
            int capacity = Capacity;

            while (summoned.Count < capacity)
                summoned.Add(null);

            while (summoned.Count > capacity)
                summoned.RemoveAt(summoned.Count - 1);
        }

        public Vector3 GetSpawnPosition(int index)
        {
            if (index >= 0 && index < spawnPoints.Length && spawnPoints[index] != null)
                return spawnPoints[index].position;

            if (Capacity <= 0)
                return transform.position;

            float radius = status != null ? status.detectRange * Config.distance : 0f;
            float angle = 360f * index / Capacity * Mathf.Deg2Rad;

            return transform.position + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
        }

        public void Register(int index, GameObject creature)
        {
            if (index < 0 || index >= summoned.Count || creature == null)
                return;

            if (summoned[index] != null)
                DespawnedAt?.Invoke(index, summoned[index]);

            summoned[index] = creature;
            ActiveCount = CountActive();
            SummonedAt?.Invoke(index, creature);
        }

        public void Unregister(int index)
        {
            if (index < 0 || index >= summoned.Count || summoned[index] == null)
                return;

            var creature = summoned[index];
            summoned[index] = null;
            ActiveCount = CountActive();
            DespawnedAt?.Invoke(index, creature);
        }

        private int CountActive()
        {
            int count = 0;

            foreach (var creature in summoned)
                if (creature != null && creature.activeInHierarchy)
                    count++;

            return count;
        }
    }
}

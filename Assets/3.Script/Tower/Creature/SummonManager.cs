using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tower
{
    /// <summary>관 타워가 소유한 스켈레톤의 슬롯과 재소환 흐름을 관리한다.</summary>
    public sealed class SummonManager : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private TowerStatus status;
        [SerializeField] private ObjectPooling CreaturePool;

        [Header("Summon")]
        [SerializeField] private Transform[] spawnPoints = Array.Empty<Transform>();
        [SerializeField] private Transform summonedRoot;

        private readonly List<SkeletonController> summoned = new List<SkeletonController>();

        public CoffinTowerConfig Config => status != null ? status.coffin : null;
        public IReadOnlyList<SkeletonController> Summoned => summoned;
        public int Capacity => Config != null ? Mathf.Max(0, Config.num) : 0;
        public int ActiveCount { get; private set; }

        public event Action<int, SkeletonController> SummonedAt;
        public event Action<int, SkeletonController> DespawnedAt;

        private void Awake()
        {
            if (status == null)
                status = GetComponentInParent<TowerStatus>();

            if (summonedRoot == null)
                summonedRoot = transform;

            EnsureSlots();
        }

        private void OnEnable()
        {
            SummonAll();
        }

        private void OnDisable()
        {
            DespawnAll();
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

        public void Register(int index, SkeletonController creature)
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
            {
                if (creature != null && creature.gameObject.activeInHierarchy)
                    count++;
            }

            return count;
        }

        public void SummonAll()
        {
            for (int i = 0; i < Capacity; i++)
            {
                Summon(i);
            }
        }

        public void Summon(int index)
        {
            if (index < 0 || index >= Capacity || CreaturePool == null)
                return;

            GameObject obj = CreaturePool.GetObject();

            if (obj == null)
                return;

            obj.transform.SetParent(summonedRoot, false);
            obj.transform.position = GetSpawnPosition(index);
            obj.SetActive(true);

            if (!obj.TryGetComponent(out SkeletonController creature))
            {
                obj.SetActive(false);
                return;
            }

            Register(index, creature);
        }

        private void DespawnAll()
        {
            if (summoned == null)
                return;

            for (int i = 0; i < summoned.Count; i++)
            {
                if (summoned[i] == null)
                    continue;

                summoned[i].Despawn();
                summoned[i] = null;
            }
        }

        public void Respawn(int index)
        {
            summoned[index] = null;

            StartCoroutine(RespawnCoroutine(index));
        }

        private IEnumerator RespawnCoroutine(int index)
        {
            yield return new WaitForSeconds(Config.respawnCooldown);

            Summon(index);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using DesertTower.Levels;
using SandGuard.Enemy;
using UnityEngine;
using UnityEngine.AI;

namespace SandGuard.Waves
{
    /// <summary>레벨의 WaveSet을 실행한다. 준비 시간 → 그룹별 스폰(풀에서 대여) → 전멸 시 다음 웨이브 → 마지막 웨이브 뒤 승리.</summary>
    /// <remarks>
    /// 적은 <see cref="EnemyCatalog"/>로 gameKey → 프리팹을 풀고 <see cref="EnemyPool"/>에서 꺼낸다.
    /// 집계는 IWaveStateReader 계약대로 Died/Despawned 중 먼저 온 것에서 한 번만 뺀다. 게임 단계(IGameStateReader)도 여기서 낸다.
    /// </remarks>
    [DefaultExecutionOrder(-40)]
    public sealed class WaveDirector : MonoBehaviour, IWaveStateReader, IGameStateReader
    {
        [Tooltip("비우면 씬에서 찾는다. LevelRoot.waves가 실행할 WaveSet이다")]
        public LevelRoot level;
        public EnemyCatalog catalog;
        [Tooltip("비우면 씬에서 찾고, 없으면 만든다")]
        public EnemyPool pool;
        [Tooltip("적의 진격 목표. 비우면 EnemyObjective, 그것도 없으면 그룹의 코어 마커")]
        public Transform objective;
        public bool autoStart = true;
        public bool loopWaves;
        [Min(0.5f), Tooltip("스폰 지점을 NavMesh 위로 옮길 때 허용 거리")]
        public float spawnSampleRadius = 2f;

        public event Action Changed;
        public GamePhase Phase { get; private set; } = GamePhase.Preparation;
        public bool IsPaused => false;
        public bool Started { get; private set; }
        public int SpawnedTotal { get; private set; }
        public int TotalWaves => level != null && level.waves != null ? level.waves.waves.Count : 0;
        public int WaveNumber => TotalWaves == 0 ? 0 : Mathf.Clamp(waveIndex + 1, 1, TotalWaves);
        public int AliveEnemyCount => alive.Count;
        public int PendingEnemyCount { get; private set; }
        public float? PreparationSecondsRemaining => Phase == GamePhase.Preparation ? (float?)Mathf.Max(0f, prepRemaining) : 0f;
        public IReadOnlyList<string> NextRouteIds => nextRoutes;

        int waveIndex;
        float prepRemaining;
        int groupsRunning;
        readonly HashSet<Guid> alive = new HashSet<Guid>();
        readonly List<string> nextRoutes = new List<string>();

        void Awake()
        {
            if (level == null) level = FindFirstObjectByType<LevelRoot>();
            if (pool == null) pool = FindFirstObjectByType<EnemyPool>();
            if (pool == null) pool = new GameObject("Enemy Pool").AddComponent<EnemyPool>();
        }

        void Start() { if (autoStart) Begin(); }

        void Update()
        {
            if (!Started || Phase != GamePhase.Preparation) return;
            prepRemaining -= Time.deltaTime;
            if (prepRemaining <= 0f) StartCombat();
        }

        /// <summary>첫 웨이브의 준비 단계로 들어간다. 이미 시작했으면 무시한다.</summary>
        public void Begin()
        {
            if (Started) return;
            if (level == null || level.waves == null || level.waves.waves.Count == 0) { Debug.LogWarning("WaveDirector: 실행할 WaveSet이 없습니다.", this); return; }
            if (catalog == null) { Debug.LogWarning("WaveDirector: EnemyCatalog이 없습니다.", this); return; }
            Started = true; waveIndex = 0;
            EnterPreparation();
        }

        /// <summary>남은 준비 시간을 건너뛰고 바로 전투로 들어간다. 시작 전이면 시작부터 한다.</summary>
        public void SkipPreparation()
        {
            if (!Started) Begin();
            if (Started && Phase == GamePhase.Preparation) StartCombat();
        }

        Wave CurrentWave => level.waves.waves[waveIndex];

        void EnterPreparation()
        {
            Phase = GamePhase.Preparation;
            prepRemaining = CurrentWave.preparationSeconds;
            PendingEnemyCount = 0;
            foreach (var group in CurrentWave.groups) PendingEnemyCount += Mathf.Max(1, group.count);
            nextRoutes.Clear();
            foreach (var group in CurrentWave.groups)
            {
                string id = !string.IsNullOrWhiteSpace(group.routeId) ? group.routeId : group.spawnId;
                if (!string.IsNullOrWhiteSpace(id) && !nextRoutes.Contains(id)) nextRoutes.Add(id);
            }
            Changed?.Invoke();
        }

        void StartCombat()
        {
            Phase = GamePhase.Combat;
            groupsRunning = 0;
            foreach (var group in CurrentWave.groups) { groupsRunning++; StartCoroutine(SpawnGroup(group)); }
            Changed?.Invoke();
            if (groupsRunning == 0) CheckWaveComplete();
        }

        IEnumerator SpawnGroup(SpawnGroup group)
        {
            if (group.delay > 0f) yield return new WaitForSeconds(group.delay);
            int count = Mathf.Max(1, group.count);
            string key = group.element != null ? group.element.gameKey : null;
            if (!level.TryResolveSpawnGroup(group, out SpawnBinding binding, out string error) || !catalog.TryGet(key, out GameObject prefab))
            {
                Debug.LogWarning("WaveDirector: 그룹을 건너뜁니다 — " + (error ?? ("카탈로그에 없는 gameKey '" + key + "'")), this);
                PendingEnemyCount -= count; groupsRunning--;
                Changed?.Invoke(); CheckWaveComplete();
                yield break;
            }
            for (int i = 0; i < count; i++)
            {
                Spawn(prefab, binding);
                PendingEnemyCount--;
                Changed?.Invoke();
                if (i < count - 1) yield return new WaitForSeconds(Mathf.Max(.05f, group.interval));
            }
            groupsRunning--;
            CheckWaveComplete();
        }

        void Spawn(GameObject prefab, SpawnBinding binding)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * binding.Spawn.spawnRadius;
            Vector3 position = binding.Spawn.transform.position + new Vector3(offset.x, 0f, offset.y);
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, spawnSampleRadius, NavMesh.AllAreas)) position = hit.position;
            GameObject instance = pool.Rent(prefab, position, binding.Spawn.transform.rotation);
            if (instance == null) return;
            instance.name = prefab.name + " " + (++SpawnedTotal);
            var brain = instance.GetComponent<EnemyBrain>();
            if (brain != null)
                brain.objective = objective != null ? objective : EnemyObjective.Current != null ? EnemyObjective.Current.transform : binding.Target.transform;
            var health = instance.GetComponent<EnemyHealth>();
            if (health != null) Track(health);
        }

        void Track(EnemyHealth health)
        {
            Guid id = health.EntityId;
            alive.Add(id);
            Action<DeathInfo> onDied = null; Action<Guid> onGone = null;
            void Untrack()
            {
                health.Died -= onDied; health.Despawned -= onGone;
                if (alive.Remove(id)) { Changed?.Invoke(); CheckWaveComplete(); }
            }
            onDied = _ => Untrack(); onGone = _ => Untrack();
            health.Died += onDied; health.Despawned += onGone;
        }

        void CheckWaveComplete()
        {
            if (Phase != GamePhase.Combat || groupsRunning > 0 || PendingEnemyCount > 0 || alive.Count > 0) return;
            if (waveIndex + 1 < TotalWaves) { waveIndex++; EnterPreparation(); }
            else if (loopWaves) { waveIndex = 0; EnterPreparation(); }
            else { Phase = GamePhase.Victory; nextRoutes.Clear(); Changed?.Invoke(); }
        }
    }
}

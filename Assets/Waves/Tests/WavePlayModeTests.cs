#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using DesertTower.Levels;
using NUnit.Framework;
using SandGuard.Enemy;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace SandGuard.Waves.Tests
{
    /// <summary>준비 → 스폰(풀 대여) → 전멸 → 다음 웨이브 → 풀 재사용 → 승리까지 실제 Enemy 프리팹으로 확인한다.</summary>
    public sealed class WavePlayModeTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        GameObject Track(GameObject value) { objects.Add(value); return value; }
        readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        T Asset<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); assets.Add(value); return value; }

        [SetUp] public void Setup() { Time.timeScale = 3f; }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            foreach (var value in objects) if (value != null) UnityEngine.Object.Destroy(value);
            foreach (var enemy in UnityEngine.Object.FindObjectsByType<EnemyHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None)) UnityEngine.Object.Destroy(enemy.gameObject);
            foreach (var asset in assets) UnityEngine.Object.Destroy(asset);
            objects.Clear(); assets.Clear();
            yield return null;
        }

        IEnumerator Until(Func<bool> condition, float seconds, string message)
        {
            float deadline = Time.time + seconds;
            while (!condition() && Time.time < deadline) yield return null;
            Assert.True(condition(), message);
        }

        [UnityTest] public IEnumerator RunsWavesFromLevelDataAndReusesPooledEnemies()
        {
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Generated/Enemy.prefab");
            Assert.NotNull(enemyPrefab, "Run SandGuard/Enemy/Create Missing Demo Assets first.");

            var level = Track(new GameObject("Level")).AddComponent<LevelRoot>();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetParent(level.transform, false); ground.transform.position = new Vector3(0, -.5f, 8); ground.transform.localScale = new Vector3(16, 1, 30);
            var surface = level.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children; surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders; surface.BuildNavMesh();
            var spawn = new GameObject("Spawn").AddComponent<LevelMarker>(); spawn.transform.SetParent(level.transform, false);
            spawn.id = "spawn"; spawn.kind = MarkerKind.EnemySpawn; spawn.spawnRadius = 1f;
            var coreMarker = new GameObject("Core Marker").AddComponent<LevelMarker>(); coreMarker.transform.SetParent(level.transform, false);
            coreMarker.id = "core"; coreMarker.kind = MarkerKind.Core; coreMarker.transform.position = new Vector3(0, 0, 16);
            var core = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            core.transform.position = new Vector3(0, 1, 16); core.transform.localScale = new Vector3(2, 2, 2);
            var coreTarget = core.AddComponent<EnemyTestTarget>(); coreTarget.kind = CombatTargetKind.Core; coreTarget.maxHealth = 5000f; coreTarget.factionId = "Ally";
            core.AddComponent<EnemyObjective>();
            var obstacle = core.AddComponent<NavMeshObstacle>(); obstacle.carving = true; obstacle.shape = NavMeshObstacleShape.Box; obstacle.size = Vector3.one;

            var element = Asset<LevelElementDefinition>(); element.gameKey = "test.bandit";
            var catalog = Asset<EnemyCatalog>(); catalog.entries.Add(new EnemyCatalog.Entry { gameKey = "test.bandit", prefab = enemyPrefab });
            var waves = Asset<WaveSet>();
            SpawnGroup Group() => new SpawnGroup { spawnId = "spawn", targetId = "core", element = element, count = 2, interval = .1f };
            waves.waves.Add(new Wave { label = "W1", preparationSeconds = .3f, groups = { Group() } });
            waves.waves.Add(new Wave { label = "W2", preparationSeconds = 4f, groups = { Group() } }); // 시체가 풀로 돌아올 시간(removeDelay 2.6초)
            level.waves = waves;

            var pool = Track(new GameObject("Pool")).AddComponent<EnemyPool>();
            var director = Track(new GameObject("Director")).AddComponent<WaveDirector>();
            director.level = level; director.catalog = catalog; director.pool = pool; director.autoStart = true;
            int changes = 0; director.Changed += () => changes++;
            yield return null;
            Assert.True(director.Started);
            Assert.AreEqual(GamePhase.Preparation, director.Phase);
            Assert.AreEqual(1, director.WaveNumber); Assert.AreEqual(2, director.TotalWaves);
            Assert.AreEqual(2, director.PendingEnemyCount, "Preparation shows the whole wave as pending.");
            CollectionAssert.AreEqual(new[] { "spawn" }, director.NextRouteIds);

            yield return Until(() => director.Phase == GamePhase.Combat, 3f, "Preparation must end.");
            yield return Until(() => director.AliveEnemyCount == 2 && director.PendingEnemyCount == 0, 5f, "Both enemies must spawn.");
            var first = UnityEngine.Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
            Assert.AreEqual(2, first.Length); Assert.AreEqual(2, pool.CreatedCount); Assert.AreEqual(0, pool.ReusedCount);
            foreach (var enemy in first)
            {
                Assert.True(enemy.GetComponent<EnemyMotor>().IsOnNavMesh, enemy.name + " must stand on the NavMesh.");
                Assert.AreEqual(core.transform, enemy.GetComponent<EnemyBrain>().objective, "Objective falls back to the EnemyObjective.");
            }
            var firstIds = new HashSet<Guid> { first[0].EntityId, first[1].EntityId };
            foreach (var enemy in first) enemy.TakeDamage(new DamageInfo(1000f, "Ally"));
            Assert.AreEqual(0, director.AliveEnemyCount, "Died removes the enemy from the count at once.");
            yield return Until(() => director.Phase == GamePhase.Preparation && director.WaveNumber == 2, 3f, "Clearing the wave starts the next preparation.");

            yield return Until(() => pool.IdleCount == 2, 6f, "Removed bodies must return to the pool instead of being destroyed.");
            yield return Until(() => director.Phase == GamePhase.Combat && director.AliveEnemyCount == 2, 8f, "Wave 2 must spawn.");
            Assert.AreEqual(2, pool.ReusedCount, "Wave 2 must reuse the pooled bodies.");
            Assert.AreEqual(2, pool.CreatedCount);
            var second = UnityEngine.Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
            Assert.AreEqual(2, second.Length, "Pooled bodies come back active; nothing extra is created.");
            foreach (var enemy in second)
            {
                Assert.False(firstIds.Contains(enemy.EntityId), "A reused enemy needs a fresh EntityId so the wave counts it again.");
                Assert.True(enemy.IsAlive); Assert.AreEqual(enemy.MaxHealth, enemy.CurrentHealth);
                Assert.AreNotEqual(EnemyBrainState.Dead, enemy.GetComponent<EnemyBrain>().State);
                Assert.True(enemy.GetComponent<EnemyMotor>().IsOnNavMesh, "Reused enemy must be warped onto the NavMesh.");
                Assert.True(enemy.GetComponent<Collider>().enabled, "Reused enemy must be hittable again.");
                var animator = enemy.GetComponentInChildren<Animator>();
                if (animator != null) Assert.False(animator.GetCurrentAnimatorStateInfo(0).IsName("Dead"), "Reused enemy must leave the death pose.");
            }
            yield return Until(() => Array.TrueForAll(second, e => e.GetComponent<EnemyBrain>().State == EnemyBrainState.Advancing || e.GetComponent<EnemyBrain>().State == EnemyBrainState.Engaging), 5f, "Reused enemies must advance again.");

            foreach (var enemy in second) enemy.TakeDamage(new DamageInfo(1000f, "Ally"));
            yield return Until(() => director.Phase == GamePhase.Victory, 3f, "Clearing the last wave is victory.");
            Assert.AreEqual(0, director.PendingEnemyCount); Assert.AreEqual(0, director.AliveEnemyCount);
            Assert.Greater(changes, 6);
        }
    }
}
#endif

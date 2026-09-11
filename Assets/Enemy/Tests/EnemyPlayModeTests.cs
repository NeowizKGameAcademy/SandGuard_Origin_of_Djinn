#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace SandGuard.Enemy.Tests
{
    public sealed class EnemyPlayModeTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        GameObject level;

        GameObject Track(GameObject value) { objects.Add(value); return value; }
        GameObject Cube(string name, Vector3 position, Vector3 scale)
        {
            var value = GameObject.CreatePrimitive(PrimitiveType.Cube);
            value.name = name; value.transform.SetParent(level.transform, false);
            value.transform.position = position; value.transform.localScale = scale;
            return value;
        }
        /// <summary>바닥과 고정 벽을 만든 뒤 NavMesh를 굽는다. 이후 만드는 표적은 NavMeshObstacle로 길을 막는다.</summary>
        void Bake(Vector3 groundSize, params (Vector3 position, Vector3 scale)[] walls)
        {
            level = Track(new GameObject("Level"));
            Cube("Ground", new Vector3(0, -0.5f, groundSize.z / 2f - 5f), groundSize);
            foreach (var wall in walls) Cube("Wall", wall.position, wall.scale);
            var surface = level.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
        }
        EnemyTestTarget Target(string name, CombatTargetKind kind, Vector3 position, Vector3 scale, float health, string faction = "Ally")
        {
            var value = Cube(name, position, scale);
            var target = value.AddComponent<EnemyTestTarget>();
            target.kind = kind; target.maxHealth = health; target.factionId = faction;
            var obstacle = value.AddComponent<NavMeshObstacle>();
            obstacle.carving = true; obstacle.shape = NavMeshObstacleShape.Box; obstacle.size = Vector3.one;
            return target;
        }
        EnemyTestTarget Core(Vector3 position)
        {
            var core = Target("Core", CombatTargetKind.Core, position, new Vector3(2, 2, 2), 1000f);
            core.gameObject.AddComponent<EnemyObjective>();
            return core;
        }
        GameObject Enemy(Vector3 position, string prefab = "Enemy")
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Generated/" + prefab + ".prefab");
            Assert.NotNull(asset, "Run SandGuard/Enemy/Create Missing Demo Assets first.");
            var value = Track(UnityEngine.Object.Instantiate(asset, position, Quaternion.identity));
            value.name = "Enemy";
            return value;
        }
        IEnumerator Until(Func<bool> condition, float seconds, string message)
        {
            float deadline = Time.time + seconds;
            while (!condition() && Time.time < deadline) yield return null;
            Assert.True(condition(), message);
        }

        [SetUp] public void Setup() { Time.timeScale = 3f; }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            foreach (var value in objects) if (value != null) UnityEngine.Object.Destroy(value);
            objects.Clear();
            yield return null;
        }

        [UnityTest] public IEnumerator WalksAroundWallAndAttacksCore()
        {
            Bake(new Vector3(16, 1, 30), (new Vector3(-2, 1.5f, 8), new Vector3(10, 3, 0.5f)));
            var core = Core(new Vector3(0, 1, 18));
            var enemy = Enemy(Vector3.zero);
            var brain = enemy.GetComponent<EnemyBrain>();
            float maxX = 0f;
            yield return Until(() => { maxX = Mathf.Max(maxX, enemy.transform.position.x); return core.HitCount > 0; }, 25f, "Enemy must reach and hit the core.");
            Assert.Greater(maxX, 3f, "Enemy should have gone around the wall through the gap.");
            Assert.AreEqual(EnemyBrainState.Engaging, brain.State);
        }

        [UnityTest] public IEnumerator BreaksBlockingWallThenAdvancesToCore()
        {
            Bake(new Vector3(12, 1, 30), (new Vector3(-4.5f, 1.5f, 10), new Vector3(3, 3, 0.5f)), (new Vector3(4.5f, 1.5f, 10), new Vector3(3, 3, 0.5f)));
            var wall = Target("Destructible wall", CombatTargetKind.Wall, new Vector3(0, 1.5f, 10), new Vector3(6, 3, 0.5f), 20f);
            var core = Core(new Vector3(0, 1, 18));
            var enemy = Enemy(Vector3.zero);
            var brain = enemy.GetComponent<EnemyBrain>();
            yield return Until(() => wall.HitCount > 0, 20f, "Enemy must attack the wall that blocks the only path.");
            Assert.Less(enemy.transform.position.z, 10f, "Enemy must not pass through the wall.");
            yield return Until(() => !wall.gameObject.activeSelf, 15f, "Wall must be destroyed by repeated hits.");
            yield return Until(() => core.HitCount > 0, 25f, "Enemy must advance to the core once the wall is gone.");
            Assert.AreNotEqual(EnemyBrainState.Dead, brain.State);
        }

        [UnityTest] public IEnumerator AttacksHostileInRangeAndIgnoresOwnFaction()
        {
            Bake(new Vector3(16, 1, 30));
            Core(new Vector3(0, 1, 18));
            var dummy = Target("Player dummy", CombatTargetKind.Player, new Vector3(2.5f, 1, 3), Vector3.one, 30f);
            var friend = Target("Friendly", CombatTargetKind.Player, new Vector3(-2.5f, 1, 3), Vector3.one, 30f, "Enemy");
            var enemy = Enemy(Vector3.zero);
            yield return Until(() => dummy.HitCount > 0, 15f, "Enemy must attack a hostile target inside detection range.");
            Assert.AreEqual(30f, friend.CurrentHealth, "Same-faction targets must never be attacked.");
            Assert.AreSame(dummy, enemy.GetComponent<EnemyBrain>().CurrentTarget);
        }

        [UnityTest] public IEnumerator ChiefStopsAtRangeForAttackableTargets() => StopsAtRange("Enemy_Chief", 3f);
        [UnityTest] public IEnumerator HammerBruteStopsAtRangeForAttackableTargets() => StopsAtRange("Enemy_HammerBrute", 2.3f);

        IEnumerator StopsAtRange(string prefab, float expectedRange)
        {
            Time.timeScale = 1f;
            Bake(new Vector3(12, 1, 30));
            foreach (var kind in new[] { CombatTargetKind.Player, CombatTargetKind.Minion, CombatTargetKind.Tower, CombatTargetKind.Core, CombatTargetKind.Wall })
            {
                bool blocking = kind == CombatTargetKind.Wall;
                var target = Target(kind.ToString(), kind, new Vector3(0, 1.5f, 10), new Vector3(blocking ? 12f : 1f, 3, .5f), 1000f);
                yield return new WaitForSeconds(.7f); // Allow the obstacle to carve before calculating the route.
                var enemy = Enemy(new Vector3(0, 0, 4), prefab);
                var brain = enemy.GetComponent<EnemyBrain>();
                brain.objective = target.transform;
                brain.despawnOnArrival = false;
                var attack = enemy.GetComponent<EnemyMeleeAttack>();
                var motor = enemy.GetComponent<EnemyMotor>();
                Assert.AreEqual(expectedRange, attack.range, .001f);
                yield return Until(() => attack.IsAttacking, 8f, prefab + " must start attacking " + kind);
                Assert.AreSame(target, brain.CurrentTarget);
                Assert.False(motor.HasDestination, "All target kinds must stop movement when attackable.");
                var collider = target.GetComponent<Collider>();
                float gap = Vector3.Distance(attack.Origin, collider.ClosestPoint(attack.Origin));
                Assert.Greater(gap, expectedRange - .9f, "Do not walk up against " + kind + " before attacking.");
                Assert.LessOrEqual(gap, expectedRange + .01f);
                Assert.Greater(EnemyMotor.Planar(enemy.transform.position, collider.ClosestPoint(enemy.transform.position)), 1f);
                yield return Until(() => target.HitCount > 0, 4f, "The attack must deal damage from the stopping distance.");
                enemy.SetActive(false); target.gameObject.SetActive(false);
                yield return new WaitForSeconds(.7f);
            }
        }

        [UnityTest] public IEnumerator WalksAroundDestructibleWallWhenRouteIsOpen()
        {
            Bake(new Vector3(16, 1, 30));
            var wall = Target("Detour wall", CombatTargetKind.Wall, new Vector3(-2, 1.5f, 8), new Vector3(10, 3, .5f), 1000f);
            var core = Core(new Vector3(0, 1, 18));
            yield return new WaitForSeconds(.7f);
            var enemy = Enemy(Vector3.zero, "Enemy_Chief");
            yield return Until(() => core.HitCount > 0, 25f, "Enemy must follow the open route to the core.");
            Assert.AreEqual(0, wall.HitCount, "A bypassable wall must not become a blocking target.");
        }

        [UnityTest] public IEnumerator DiesOnceAndIsRemovedAfterDelay()
        {
            Bake(new Vector3(10, 1, 10));
            var enemy = Enemy(Vector3.zero);
            var health = enemy.GetComponent<EnemyHealth>();
            health.removeDelay = 0.3f;
            int died = 0; health.Died += _ => died++;
            yield return null;
            Assert.AreEqual(DamageStatus.NonHostile, health.TakeDamage(new DamageInfo(10f, "Enemy")).Status);
            var first = health.TakeDamage(new DamageInfo(1000f, "Ally"));
            var second = health.TakeDamage(new DamageInfo(1000f, "Ally"));
            Assert.True(first.WasKilled);
            Assert.AreEqual(health.maxHealth, first.AppliedDamage);
            Assert.AreEqual(DamageStatus.NotAlive, second.Status);
            Assert.AreEqual(1, died);
            Assert.False(health.IsTargetable);
            Assert.False(enemy.GetComponent<Collider>().enabled, "Colliders must be off before death is announced.");
            Assert.AreEqual(EnemyBrainState.Dead, enemy.GetComponent<EnemyBrain>().State);
            yield return new WaitForSeconds(0.6f);
            Assert.True(enemy == null, "Dead enemy must be removed after the delay.");
        }

        [UnityTest] public IEnumerator HitAndDeathSpawnCombatVfx()
        {
            Bake(new Vector3(10, 1, 10));
            var enemy = Enemy(Vector3.zero);
            var reaction = enemy.GetComponent<DesertTower.VFX.VfxHitReaction>();
            Assert.NotNull(reaction, "Run DesertTower/VFX/Wire Combat VFX Into Demo Assets first.");
            Assert.NotNull(reaction.HitPrefab); Assert.NotNull(reaction.DeathPrefab);
            var health = enemy.GetComponent<EnemyHealth>();
            yield return null;
            health.TakeDamage(new DamageInfo(10f, "Ally", hitPosition: new Vector3(0, 1, -0.4f), hitDirection: Vector3.forward));
            yield return null;
            Assert.NotNull(GameObject.Find(reaction.HitPrefab.name + "(Clone)"), "A hit must spawn the hit prefab.");
            health.TakeDamage(new DamageInfo(1000f, "Ally"));
            yield return null;
            var death = GameObject.Find(reaction.DeathPrefab.name + "(Clone)");
            Assert.NotNull(death, "Death must spawn the death prefab.");
            Track(death);
            foreach (var clone in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) if (clone.transform.root != enemy.transform) Track(clone.transform.root.gameObject);
        }

        [UnityTest] public IEnumerator DespawnsOnArrivalWhenObjectiveIsNotAttackable()
        {
            Bake(new Vector3(10, 1, 20));
            var goal = Track(new GameObject("Goal"));
            goal.transform.position = new Vector3(0, 0, 10);
            goal.AddComponent<EnemyObjective>();
            var enemy = Enemy(Vector3.zero);
            bool reached = false;
            enemy.GetComponent<EnemyBrain>().ReachedObjective += _ => reached = true;
            yield return Until(() => reached, 15f, "Enemy must report arrival at a plain objective.");
            yield return null;
            Assert.True(enemy == null, "Without a core interaction the enemy despawns on arrival.");
        }
    }
}
#endif

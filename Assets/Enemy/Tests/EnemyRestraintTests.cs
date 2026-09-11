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
    /// <summary>모래 족쇄가 적을 묶는 쪽: EnemyRestraint가 모터를 제자리에 세우고 시간이 지나면 풀어 준다.</summary>
    public sealed class EnemyRestraintTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        GameObject level;
        GameObject Track(GameObject value) { objects.Add(value); return value; }
        IEnumerator Until(Func<bool> condition, float seconds, string message)
        {
            float deadline = Time.time + seconds;
            while (!condition() && Time.time < deadline) yield return null;
            Assert.True(condition(), message);
        }

        [SetUp] public void Setup() { Time.timeScale = 1f; }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            foreach (var value in objects) if (value != null) UnityEngine.Object.Destroy(value);
            objects.Clear();
            yield return null;
        }

        [UnityTest] public IEnumerator RestrainStopsTheEnemyInPlaceThenReleasesAndKeepsTheLongerTimer()
        {
            level = Track(new GameObject("Level"));
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetParent(level.transform, false); ground.transform.position = new Vector3(0, -0.5f, 10); ground.transform.localScale = new Vector3(16, 1, 30);
            var surface = level.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children; surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders; surface.BuildNavMesh();
            var core = GameObject.CreatePrimitive(PrimitiveType.Cube);
            core.transform.SetParent(level.transform, false); core.transform.position = new Vector3(0, 1, 18); core.transform.localScale = new Vector3(2, 2, 2);
            var target = core.AddComponent<EnemyTestTarget>(); target.kind = CombatTargetKind.Core; target.maxHealth = 1000f;
            var obstacle = core.AddComponent<NavMeshObstacle>(); obstacle.carving = true; obstacle.shape = NavMeshObstacleShape.Box; obstacle.size = Vector3.one;
            core.AddComponent<EnemyObjective>();
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Generated/Enemy.prefab");
            Assert.NotNull(asset, "Run SandGuard/Enemy/Create Missing Demo Assets first.");
            var enemy = Track(UnityEngine.Object.Instantiate(asset, Vector3.zero, Quaternion.identity));
            var motor = enemy.GetComponent<EnemyMotor>();
            var restraint = enemy.GetComponent<EnemyRestraint>();
            Assert.NotNull(restraint, "Enemy.prefab carries an EnemyRestraint (SandGuard/Enemy/Create Missing Demo Assets adds it).");
            Assert.NotNull(restraint.motor); Assert.NotNull(restraint.health);
            Assert.False(restraint.IsRestrained);
            yield return Until(() => motor.Velocity.magnitude > 0.5f, 6f, "The enemy walks toward the core.");

            int changes = 0; restraint.Changed += _ => changes++;
            restraint.Restrain(1f);
            Assert.True(restraint.IsRestrained); Assert.True(motor.Restrained); Assert.AreEqual(1, changes);
            if (restraint.vfxPrefab != null) Assert.NotNull(restraint.ActiveVfx, "The root visual is attached while restrained.");
            yield return new WaitForSeconds(0.15f); // 감속
            Vector3 held = enemy.transform.position;
            yield return new WaitForSeconds(0.4f);
            Assert.Less(EnemyMotor.Planar(held, enemy.transform.position), 0.15f, "A restrained enemy stays in place.");
            Assert.True(restraint.IsRestrained, "Still restrained after 0.55 s of a 1 s restraint.");
            restraint.Restrain(0.1f);
            Assert.Greater(restraint.RemainingSeconds, 0.3f, "A shorter restraint never cuts the running one short.");
            restraint.Restrain(1.2f);
            Assert.Greater(restraint.RemainingSeconds, 1f, "A longer restraint extends the timer.");
            Assert.AreEqual(1, changes, "Extending does not re-announce.");
            yield return Until(() => !restraint.IsRestrained, 2f, "The restraint expires on its own.");
            Assert.False(motor.Restrained); Assert.AreEqual(2, changes); Assert.IsNull(restraint.ActiveVfx);
            yield return Until(() => motor.Velocity.magnitude > 0.5f, 4f, "The enemy walks again once released.");

            // 둔화: 가장 강한 것만, 같은 세기는 시간 연장. 에이전트 속도에 바로 반영된다.
            ISlowable slowable = restraint;
            slowable.Slow(0.6f, 0.6f);
            Assert.AreEqual(0.6f, restraint.SlowFactor, 0.0001f); Assert.AreEqual(motor.moveSpeed * 0.4f, motor.Agent.speed, 0.0001f);
            slowable.Slow(0.3f, 5f);
            Assert.AreEqual(0.6f, restraint.SlowFactor, 0.0001f, "A weaker slow never replaces a stronger one.");
            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(0f, restraint.SlowFactor, 0.0001f); Assert.AreEqual(motor.moveSpeed, motor.Agent.speed, 0.0001f, "The slow expires and speed returns.");

            // 밀림: 자기 걸음을 멈추고 주어진 만큼 옮겨진다.
            Vector3 before = enemy.transform.position;
            IDisplaceable displaceable = restraint;
            displaceable.Displace(new Vector3(1.5f, 0f, 0f));
            yield return null;
            Assert.Greater(enemy.transform.position.x - before.x, 1f, "Displace moves the enemy sideways on the NavMesh.");
            yield return Until(() => motor.Velocity.magnitude > 0.5f, 3f, "The enemy resumes walking after the push ends.");
        }
    }
}
#endif

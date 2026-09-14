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
    /// <summary>레벨 배치 타워(Tower Base): 받침 충돌체가 있어도 본체를 때릴 수 있고, 본체가 부서진 뒤에도 받침은 적의 길을 막는다.</summary>
    public sealed class EnemyTowerBlockTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        GameObject Track(GameObject value) { objects.Add(value); return value; }

        void Bake()
        {
            var level = Track(new GameObject("Level"));
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetParent(level.transform, false);
            ground.transform.position = new Vector3(0, -.5f, 8f); ground.transform.localScale = new Vector3(20, 1, 30);
            var surface = level.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
        }

        GameObject Tower(Vector3 position)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/2.Model/Prefabs/Tower Base.prefab");
            Assert.NotNull(asset);
            var tower = Track(UnityEngine.Object.Instantiate(asset, position, Quaternion.identity));
            Assert.NotNull(tower.GetComponent<NavMeshObstacle>(), "Run SandGuard/Facility/Add Combat Health To Towers first.");
            return tower;
        }

        GameObject Enemy(Vector3 position)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Generated/Enemy.prefab");
            return Track(UnityEngine.Object.Instantiate(asset, position, Quaternion.identity));
        }

        IEnumerator Until(Func<bool> condition, float seconds, string message)
        {
            float deadline = Time.time + seconds;
            while (!condition() && Time.time < deadline) yield return null;
            Assert.True(condition(), message);
        }

        [SetUp] public void Setup() => Time.timeScale = 3f;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            foreach (var value in objects) if (value != null) UnityEngine.Object.Destroy(value);
            objects.Clear();
            foreach (var clone in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                if (clone.transform.root.name.StartsWith("VFX_")) UnityEngine.Object.Destroy(clone.transform.root.gameObject);
            yield return null;
        }

        [UnityTest] public IEnumerator EnemyHitsTheTowerBodyDespiteTheBaseCollider()
        {
            Bake();
            var tower = Tower(new Vector3(0, 0, 6));
            var body = tower.transform.Find("Tower (Cobra)");
            var health = body.GetComponent<IHealth>();
            float start = health.CurrentHealth;
            yield return new WaitForSeconds(.3f); // 장애물이 NavMesh를 깎은 뒤에 적을 둔다
            var enemy = Enemy(Vector3.zero);
            yield return Until(() => health.CurrentHealth < start, 20f, "The base collider must not hide the body from the enemy's melee line of sight.");
        }

        [UnityTest] public IEnumerator TheBaseStillBlocksEnemiesAfterTheBodyBreaks()
        {
            Bake();
            var tower = Tower(new Vector3(0, 0, 7));
            var body = tower.transform.Find("Tower (Cobra)");
            ((IDamageable)body.GetComponent<IHealth>()).TakeDamage(new DamageInfo(100000f, "Enemy"));
            yield return new WaitForSeconds(1f);
            Assert.False(body.gameObject.activeSelf, "Only the body breaks.");
            var solid = tower.GetComponent<BoxCollider>();
            Assert.True(solid != null && solid.enabled && !solid.isTrigger, "The base keeps a solid collider for the player.");

            var goal = Track(new GameObject("Objective"));
            goal.transform.position = new Vector3(0, 0, 16);
            goal.AddComponent<EnemyObjective>();
            var enemy = Enemy(Vector3.zero);
            enemy.GetComponent<EnemyBrain>().despawnOnArrival = false;
            bool reached = false; enemy.GetComponent<EnemyBrain>().ReachedObjective += _ => reached = true;
            Bounds footprint = solid.bounds;
            float inside = 0f;
            yield return Until(() =>
            {
                Vector3 p = enemy.transform.position;
                if (Mathf.Abs(p.x - footprint.center.x) < footprint.extents.x - .05f && Mathf.Abs(p.z - footprint.center.z) < footprint.extents.z - .05f) inside += Time.deltaTime;
                return reached;
            }, 25f, "The enemy walks around the base to its objective.");
            Assert.AreEqual(0f, inside, "The enemy never walks through the base.");
        }

        [UnityTest] public IEnumerator ACharacterControllerCannotWalkThroughTheBase()
        {
            var floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            floor.transform.position = new Vector3(0, -.5f, 0); floor.transform.localScale = new Vector3(30, 1, 30);
            var tower = Tower(new Vector3(0, 0, 5));
            var body = tower.transform.Find("Tower (Cobra)");
            ((IDamageable)body.GetComponent<IHealth>()).TakeDamage(new DamageInfo(100000f, "Enemy"));
            yield return new WaitForSeconds(1f);
            var walker = Track(new GameObject("Walker"));
            walker.transform.position = new Vector3(0, 1.05f, 0);
            var controller = walker.AddComponent<CharacterController>();
            controller.height = 2f; controller.radius = .35f; controller.stepOffset = .45f;
            Physics.SyncTransforms();
            for (int i = 0; i < 120; i++) { controller.Move(new Vector3(0, -.2f, .1f)); yield return null; }
            float baseNear = tower.GetComponent<BoxCollider>().bounds.min.z;
            Assert.Less(walker.transform.position.z, baseNear - .3f, "The player capsule stops at the base, even after the body is gone.");
        }
    }
}
#endif

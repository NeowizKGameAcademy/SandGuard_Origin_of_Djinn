#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using DesertTower.VFX;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SandGuard.Enemy.Tests
{
    /// <summary>처치 경험치: 입자로 나눠 떨어지고, 받을 수 있는 수신자에게 날아가 전부 전달되며, 제거(Despawn)는 보상이 없다.</summary>
    public sealed class EnemyExperienceDropTests
    {
        sealed class TestReceiver : MonoBehaviour, IExperienceReceiver
        {
            public int received, deliveries;
            public bool canCollect = true;
            public Vector3 CollectPosition => transform.position + Vector3.up;
            public bool CanCollect => canCollect && isActiveAndEnabled;
            public int GainExperience(int amount) { received += amount; deliveries++; return amount; }
            void OnEnable() => ExperienceReceivers.Register(this);
            void OnDisable() => ExperienceReceivers.Unregister(this);
        }

        readonly List<GameObject> objects = new List<GameObject>();
        GameObject Track(GameObject value) { objects.Add(value); return value; }

        EnemyHealth Enemy(Vector3 position, int experience, int orbs)
        {
            var template = Track(new GameObject("Orb Template"));
            var orb = template.AddComponent<ExperienceOrb>();
            var go = Track(new GameObject("Drop Enemy"));
            go.transform.position = position;
            var drop = go.AddComponent<EnemyExperienceDrop>();
            drop.experience = experience; drop.orbCount = orbs; drop.orbPrefab = orb;
            return go.GetComponent<EnemyHealth>();
        }

        TestReceiver Receiver(Vector3 position)
        {
            var go = Track(new GameObject("Receiver"));
            go.transform.position = position;
            return go.AddComponent<TestReceiver>();
        }

        static int LiveOrbs()
        {
            int count = 0;
            foreach (var orb in Object.FindObjectsByType<ExperienceOrb>(FindObjectsSortMode.None)) if (orb.IsLive) count++;
            return count;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach (var value in objects) if (value != null) Object.Destroy(value);
            objects.Clear();
            var pool = Object.FindFirstObjectByType<PrefabPool>();
            if (pool != null) Object.Destroy(pool.gameObject);
            foreach (var orb in Object.FindObjectsByType<ExperienceOrb>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.Destroy(orb.gameObject);
            yield return null;
        }

        [UnityTest] public IEnumerator KilledEnemySplitsExperienceIntoOrbsThatFlyToTheReceiver()
        {
            var receiver = Receiver(new Vector3(5f, 0f, 0f));
            var enemy = Enemy(Vector3.zero, experience: 10, orbs: 3);
            yield return null;

            enemy.TakeDamage(new DamageInfo(1000f, "Ally", causeId: "test"));
            Assert.AreEqual(3, LiveOrbs());
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(0, receiver.received, "Orbs scatter before homing.");

            for (float t = 0f; t < 4f && receiver.deliveries < 3; t += Time.deltaTime) yield return null;
            Assert.AreEqual(3, receiver.deliveries);
            Assert.AreEqual(10, receiver.received, "4 + 3 + 3");
            Assert.AreEqual(0, LiveOrbs(), "Collected orbs return to the pool.");
        }

        [UnityTest] public IEnumerator OrbsWaitWhileNobodyCanCollect()
        {
            var receiver = Receiver(new Vector3(3f, 0f, 0f));
            receiver.canCollect = false;
            var enemy = Enemy(Vector3.zero, experience: 6, orbs: 2);
            yield return null;
            enemy.TakeDamage(new DamageInfo(1000f, "Ally", causeId: "test"));
            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(0, receiver.received);
            Assert.AreEqual(2, LiveOrbs(), "Orbs hover until a receiver can collect.");

            receiver.canCollect = true;
            for (float t = 0f; t < 4f && receiver.deliveries < 2; t += Time.deltaTime) yield return null;
            Assert.AreEqual(6, receiver.received);
        }

        [UnityTest] public IEnumerator DespawnedEnemyDropsNothing()
        {
            Receiver(new Vector3(2f, 0f, 0f));
            var enemy = Enemy(Vector3.zero, experience: 10, orbs: 3);
            yield return null;
            int before = LiveOrbs(); // 앞선 적 테스트가 남긴 입자는 세지 않는다
            Assert.True(enemy.TryDespawn());
            yield return null;
            Assert.AreEqual(before, LiveOrbs());
        }

        [Test] public void EnemyPrefabsCarryTheDropWithPerTypeExperience()
        {
            var chief = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Generated/Enemy_Chief.prefab");
            var swordsman = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Generated/Enemy_Swordsman.prefab");
            Assert.NotNull(chief); Assert.NotNull(swordsman);
            var chiefDrop = chief.GetComponent<EnemyExperienceDrop>();
            Assert.NotNull(chiefDrop, "Run SandGuard/Progression/Connect Experience and Level Up.");
            Assert.NotNull(chiefDrop.orbPrefab);
            Assert.NotNull(chiefDrop.orbPrefab.transform.Find("Visual"), "The orb wears VFX_Experience_Mote.");
            Assert.Greater(chiefDrop.experience, swordsman.GetComponent<EnemyExperienceDrop>().experience);
        }
    }
}
#endif

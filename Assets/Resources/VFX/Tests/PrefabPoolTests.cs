using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DesertTower.VFX.Tests
{
    public sealed class PrefabPoolTests
    {
        readonly List<Object> objects = new List<Object>();
        T Track<T>(T value) where T : Object { objects.Add(value); return value; }

        sealed class Probe : MonoBehaviour, IPoolable
        {
            public int Rents, Returns;
            public void OnRent() => Rents++;
            public void OnReturn() => Returns++;
        }

        /// <summary>프리팹 대용. 비활성으로 두어 씬에서 돌아가지 않게 한다.</summary>
        GameObject MakePrefab()
        {
            var prefab = Track(new GameObject("Pool Prefab"));
            prefab.SetActive(false);
            prefab.transform.localScale = new Vector3(2f, 2f, 2f);
            prefab.AddComponent<Probe>();
            var child = new GameObject("Particles");
            child.transform.SetParent(prefab.transform, false);
            child.AddComponent<ParticleSystem>();
            return prefab;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (PrefabPool.Exists) Object.Destroy(PrefabPool.Instance.gameObject);
            foreach (var value in objects) if (value != null) Object.Destroy(value);
            objects.Clear();
            yield return null;
        }

        [UnityTest] public IEnumerator ReleasedInstanceComesBackWithItsScaleAndCallbacksReset()
        {
            GameObject prefab = MakePrefab();
            PrefabPool pool = PrefabPool.Instance;
            GameObject first = PrefabPool.Spawn(prefab, Vector3.one, Quaternion.identity);
            first.SetActive(true);
            first.transform.localScale = Vector3.one * 7f; // 호출자가 배율을 얹어도
            Probe probe = first.GetComponent<Probe>();
            Assert.AreEqual(1, pool.CreatedCount);
            Assert.AreEqual(1, probe.Rents);

            PrefabPool.Release(first);
            yield return null;
            Assert.IsFalse(first.activeSelf);
            Assert.AreEqual(1, probe.Returns);
            Assert.AreEqual(1, pool.IdleCount);
            Assert.AreEqual(pool.transform, first.transform.parent, "Idle instances park under the pool.");

            GameObject second = PrefabPool.Spawn(prefab, new Vector3(3f, 0f, 0f), Quaternion.identity);
            Assert.AreSame(first, second, "The idle instance is reused instead of instantiating a new one.");
            Assert.AreEqual(1, pool.CreatedCount);
            Assert.AreEqual(1, pool.ReusedCount);
            Assert.IsTrue(second.activeSelf);
            Assert.IsNull(second.transform.parent);
            Assert.AreEqual(new Vector3(3f, 0f, 0f), second.transform.position);
            Assert.AreEqual(new Vector3(2f, 2f, 2f), second.transform.localScale, "Reuse restores the prefab's authored scale.");
            Assert.AreEqual(2, probe.Rents);
        }

        [UnityTest] public IEnumerator DelayedReleaseLeavesAnInstanceThatWasRentedAgain()
        {
            GameObject prefab = MakePrefab();
            GameObject go = PrefabPool.Spawn(prefab, Vector3.zero, Quaternion.identity);
            go.SetActive(true);
            PrefabPool.Release(go, 0.1f); // 잠시 뒤 반납을 예약해 두고
            PrefabPool.Release(go);       // 그 전에 즉시 반납한 뒤 다시 빌린다
            GameObject again = PrefabPool.Spawn(prefab, Vector3.zero, Quaternion.identity);
            Assert.AreSame(go, again);

            yield return new WaitForSeconds(0.25f);
            Assert.IsTrue(again.activeSelf, "The stale delayed release must not reclaim a re-rented instance.");
            Assert.AreEqual(0, PrefabPool.Instance.IdleCount);
        }

        [UnityTest] public IEnumerator ReleasingAnObjectThePoolDidNotMakeDestroysIt()
        {
            GameObject loose = Track(new GameObject("Loose"));
            PrefabPool.Release(loose);
            yield return null;
            Assert.IsTrue(loose == null, "Callers can release anything; unpooled objects are simply destroyed.");
        }

        [UnityTest] public IEnumerator DestroyedIdleInstancesAreSkippedOnTheNextSpawn()
        {
            GameObject prefab = MakePrefab();
            GameObject go = PrefabPool.Spawn(prefab, Vector3.zero, Quaternion.identity);
            go.SetActive(true);
            PrefabPool.Release(go);
            Object.Destroy(go); // 씬 정리 등으로 보관 중인 개체가 사라져도
            yield return null;

            GameObject fresh = PrefabPool.Spawn(prefab, Vector3.zero, Quaternion.identity);
            Assert.IsTrue(fresh != null);
            Assert.AreEqual(2, PrefabPool.Instance.CreatedCount, "A missing idle instance is replaced, not handed out as null.");
        }

        [UnityTest] public IEnumerator PrewarmFillsThePoolWithoutHandingAnythingOut()
        {
            GameObject prefab = MakePrefab();
            PrefabPool pool = PrefabPool.Instance;
            pool.Prewarm(prefab, 3);
            yield return null;
            Assert.AreEqual(3, pool.IdleCount);
            Assert.AreEqual(3, pool.CreatedCount);

            GameObject go = PrefabPool.Spawn(prefab, Vector3.zero, Quaternion.identity);
            Assert.IsTrue(go.activeSelf);
            Assert.AreEqual(2, pool.IdleCount);
            Assert.AreEqual(3, pool.CreatedCount, "A prewarmed wave costs no instantiation at spawn time.");
        }
    }
}

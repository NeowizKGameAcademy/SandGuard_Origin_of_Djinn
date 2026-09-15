using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SandGuard.Enemy;
using UnityEngine;
using UnityEngine.TestTools;

namespace SandGuard.Waves.Tests
{
    /// <summary>풀의 재사용과 동시 활성 상한. 실행기 통합 때 사라진 커버리지를 풀 단위로 되살린 것이다.</summary>
    public sealed class EnemyPoolTests
    {
        readonly List<GameObject> spawned = new List<GameObject>();
        EnemyPool pool;
        GameObject template;

        T Track<T>(T go) where T : Object { spawned.Add(go as GameObject); return go; }

        [SetUp] public void Setup()
        {
            var host = new GameObject("Pool"); spawned.Add(host);
            pool = host.AddComponent<EnemyPool>();
            // 풀은 이 오브젝트를 Instantiate한다. 비활성이라 원본의 Awake는 돌지 않는다.
            template = new GameObject("Enemy Template"); spawned.Add(template);
            template.SetActive(false);
            template.AddComponent<EnemyHealth>();
        }

        [TearDown] public void Cleanup()
        {
            foreach (var go in spawned) if (go) Object.DestroyImmediate(go);
            spawned.Clear();
        }

        [Test] public void PrewarmCreatesInactiveReserve()
        {
            for (int i = 0; i < 3; i++) pool.PrewarmOne(template);
            Assert.AreEqual(3, pool.IdleCount, "예비 개체가 대기 스택에 쌓여야 한다.");
            Assert.AreEqual(3, pool.AvailableCount(template));
            Assert.AreEqual(0, pool.ActiveCount, "예비 개체는 활성으로 세지 않는다.");
            Assert.AreEqual(3, pool.CreatedCount);
        }

        [Test] public void PrewarmedInstanceIsRentedInsteadOfCreated()
        {
            pool.PrewarmOne(template);
            int createdAfterPrewarm = pool.CreatedCount;
            var rented = pool.Rent(template, Vector3.zero, Quaternion.identity);
            Assert.NotNull(rented);
            Assert.True(rented.activeInHierarchy, "빌린 개체는 활성이어야 한다.");
            Assert.AreEqual(createdAfterPrewarm, pool.CreatedCount, "예비가 있으면 새로 만들지 않는다.");
            Assert.AreEqual(0, pool.IdleCount);
            Assert.AreEqual(1, pool.ActiveCount);
        }

        [Test] public void RentReturnsNullWhenActiveLimitReached()
        {
            Assert.NotNull(pool.Rent(template, Vector3.zero, Quaternion.identity, 2));
            Assert.NotNull(pool.Rent(template, Vector3.zero, Quaternion.identity, 2));
            // 상한에 걸리면 만들지 않고 null을 준다. 감독은 이걸 보고 다음 프레임에 다시 시도한다.
            Assert.Null(pool.Rent(template, Vector3.zero, Quaternion.identity, 2), "상한을 넘겨 빌려주면 안 된다.");
            Assert.AreEqual(2, pool.ActiveCount);
        }

        [UnityTest] public IEnumerator ReleasedEnemyReturnsToPoolAndIsReused()
        {
            var first = pool.Rent(template, Vector3.one, Quaternion.identity);
            Assert.NotNull(first);
            Assert.AreEqual(1, pool.CreatedCount);
            yield return null;

            // 반납은 EnemyHealth가 들고 있는 ReleaseHandler를 통해 일어난다. 파괴가 아니다.
            Assert.True(first.GetComponent<EnemyHealth>().TryDespawn());
            Assert.True(first, "반납된 개체는 파괴되지 않는다.");
            Assert.False(first.activeInHierarchy, "반납된 개체는 비활성으로 보관된다.");
            Assert.AreEqual(0, pool.ActiveCount);
            Assert.AreEqual(1, pool.IdleCount);

            var second = pool.Rent(template, Vector3.zero, Quaternion.identity);
            Assert.AreSame(first, second, "같은 개체가 다시 나와야 한다.");
            Assert.AreEqual(1, pool.CreatedCount, "재사용은 새로 만들지 않는다.");
            Assert.AreEqual(1, pool.ReusedCount);
            var health = second.GetComponent<EnemyHealth>();
            Assert.True(health.IsAlive, "되살아난 개체는 살아 있어야 한다.");
            Assert.AreEqual(health.MaxHealth, health.CurrentHealth, "체력이 만피로 돌아와야 한다.");
        }
    }
}

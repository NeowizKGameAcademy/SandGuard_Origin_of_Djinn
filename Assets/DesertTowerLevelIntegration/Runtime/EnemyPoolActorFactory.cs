using SandGuard.Enemy;
using SandGuard.Waves;
using UnityEngine;

namespace DesertTower.LevelIntegration
{
    /// <summary>
    /// <see cref="WaveDirector"/>의 적 생성을 <see cref="EnemyPool"/>로 돌린다. 죽거나 코어에 흡수된 적은
    /// 파괴 대신 풀로 돌아가 다음 스폰에서 되살아난다. 감독과 같은 오브젝트에 붙이면 감독이 Awake에서 찾아 쓴다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyPoolActorFactory : MonoBehaviour
    {
        [Tooltip("비우면 씬에서 찾고, 없으면 만든다")]
        public EnemyPool pool;
        [Min(1), Tooltip("사망 연출 중인 개체를 포함한 동시 활성 상한. 넘으면 스폰을 다음 프레임으로 미룬다")]
        public int maxActive = 250;
        [Tooltip("웨이브 시작 전에 미리 만들어 둘 개체 수. 0이면 미리 만들지 않는다")]
        [Min(0)] public int prewarmPerPrefab;

        public int ActiveCount => pool != null ? pool.ActiveCount : 0;

        void Awake()
        {
            if (pool == null) pool = FindFirstObjectByType<EnemyPool>();
            if (pool == null) pool = new GameObject("Enemy Pool").AddComponent<EnemyPool>();
        }

        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;
            if (pool == null) return Instantiate(prefab, position, rotation);
            return pool.Rent(prefab, position, rotation, Mathf.Max(1, maxActive));
        }

        public bool PrewarmStep(GameObject prefab) => PrewarmStep(prefab, 0);

        /// <summary>
        /// 프리팹을 하나 미리 만든다. 목표는 prewarmPerPrefab과 demand 중 큰 값이다. 더 만들 게 남았으면 true.
        /// demand는 다음 웨이브가 이 프리팹을 동시에 쓸 추정치다(감독이 계산한다). prewarmPerPrefab이 0이면 미리 만들지 않는다.
        /// </summary>
        public bool PrewarmStep(GameObject prefab, int demand)
        {
            if (pool == null || prefab == null || prewarmPerPrefab <= 0) return false;
            int target = Mathf.Max(prewarmPerPrefab, demand);
            if (pool.OwnedCount(prefab) >= target) return false;
            pool.PrewarmOne(prefab);
            return pool.OwnedCount(prefab) < target;
        }

        public void Despawn(GameObject instance)
        {
            if (instance == null) return;
            // EnemyHealth가 풀의 ReleaseHandler를 들고 있으므로 Despawn 한 번이면 풀로 돌아간다.
            var health = instance.GetComponent<EnemyHealth>();
            if (health != null && health.TryDespawn()) return;
            Destroy(instance);
        }
    }
}

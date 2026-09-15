using DesertTower.VFX;
using UnityEngine;

namespace SandGuard.Enemy
{
    /// <summary>
    /// 처치(Died)되면 경험치를 여러 입자로 나눠 뿌린다. 코어 흡수·웨이브 정리 같은 제거(Despawned)는 보상이 없다.
    /// 누가 처치했는지는 보지 않는다. 입자는 받을 수 있는 가장 가까운 수신자에게 날아간다.
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class EnemyExperienceDrop : MonoBehaviour
    {
        [Min(0)] public int experience = 10;
        [Min(1), Tooltip("경험치를 나눠 담을 입자 개수. 경험치보다 많으면 경험치 수만큼만 만든다")]
        public int orbCount = 3;
        [Tooltip("ExperienceOrb가 붙은 프리팹")] public ExperienceOrb orbPrefab;
        [Tooltip("몸통 기준 튀어 오르는 수평·수직 속도")] public Vector2 burstSpeed = new Vector2(2.2f, 4.5f);

        EnemyHealth health;

        void Awake() => health = GetComponent<EnemyHealth>();
        void OnEnable() => health.Died += OnDied;
        void OnDisable() => health.Died -= OnDied;

        void OnDied(DeathInfo _) => Drop();

        /// <summary>경험치 입자를 뿌린다. 만든 입자 수를 돌려준다.</summary>
        public int Drop()
        {
            if (orbPrefab == null || experience <= 0) return 0;
            int count = Mathf.Min(orbCount, experience);
            Vector3 origin = transform.position + Vector3.up * 0.9f; // 몸통 높이
            float offset = Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                int value = experience / count + (i < experience % count ? 1 : 0);
                Quaternion around = Quaternion.Euler(0f, offset + 360f * i / count, 0f);
                Vector3 velocity = around * Vector3.forward * burstSpeed.x * Random.Range(0.7f, 1.1f) + Vector3.up * burstSpeed.y * Random.Range(0.85f, 1.1f);
                var orb = PrefabPool.Spawn(orbPrefab, origin, Quaternion.identity);
                orb.Launch(value, velocity);
            }
            return count;
        }
    }
}

using DesertTower.VFX;
using UnityEngine;
using UnityEngine.Events;

namespace DesertTower.LevelIntegration
{
    public sealed class CoreReceiver : MonoBehaviour, ILevelCoreReceiver
    {
        [Min(1)] public float maximum = 100;
        [Tooltip("적이 코어에 도달했을 때 적 발밑에서 재생할 흡수 이펙트")]
        public GameObject absorptionPrefab;
        public UnityEvent onChanged = new UnityEvent();
        public UnityEvent onDefeated = new UnityEvent();
        public float Current { get; private set; }
        public bool IsDefeated => Current <= 0;
        void Awake() { Current = maximum; }
        public bool TryAbsorb(ActorBridge enemy, float damage)
        {
            if (Time.timeScale <= 0 || IsDefeated || !enemy || !enemy.Alive || damage < 0) return false;
            Current = Mathf.Max(0, Current - damage);
            if (absorptionPrefab != null)
            {
                // 적이 풀로 돌아간 뒤에도 파편은 코어를 향해 계속 이동한다.
                var effect = PrefabPool.Spawn(absorptionPrefab, enemy.FeetPosition, Quaternion.identity);
                foreach (var attractor in effect.GetComponentsInChildren<VfxParticleAttractor>(true))
                    attractor.Target = transform;
                PrefabPool.Release(effect, 2.5f);
            }
            // Runner settles/removes the enemy after this transaction. Defeat takes precedence.
            onChanged.Invoke(); if (IsDefeated) onDefeated.Invoke(); return true;
        }
    }
}

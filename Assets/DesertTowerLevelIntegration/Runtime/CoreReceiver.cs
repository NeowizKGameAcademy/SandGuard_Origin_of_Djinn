using UnityEngine;
using UnityEngine.Events;

namespace DesertTower.LevelIntegration
{
    public sealed class CoreReceiver : MonoBehaviour, ILevelCoreReceiver
    {
        [Min(1)] public float maximum = 100;
        public UnityEvent onChanged = new UnityEvent();
        public UnityEvent onDefeated = new UnityEvent();
        public float Current { get; private set; }
        public bool IsDefeated => Current <= 0;
        void Awake() { Current = maximum; }
        public bool TryAbsorb(ActorBridge enemy, float damage)
        {
            if (Time.timeScale <= 0 || IsDefeated || !enemy || !enemy.Alive || damage < 0) return false;
            Current = Mathf.Max(0, Current - damage);
            // Runner settles/removes the enemy after this transaction. Defeat takes precedence.
            onChanged.Invoke(); if (IsDefeated) onDefeated.Invoke(); return true;
        }
    }
}

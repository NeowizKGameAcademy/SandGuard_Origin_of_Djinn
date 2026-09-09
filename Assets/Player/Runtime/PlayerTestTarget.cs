using UnityEngine;

namespace SandGuard.Player
{
    /// <summary>플레이어 테스트 씬 전용. 실제 적의 체력 구현과는 독립적이다.</summary>
    public sealed class PlayerTestTarget : MonoBehaviour, IDamageable
    {
        [Min(1f)] public float maxHealth = 50f;
        public float CurrentHealth { get; private set; }
        public int HitCount { get; private set; }
        void Awake() => CurrentHealth = maxHealth;
        public DamageResult TakeDamage(DamageInfo damage)
        {
            if (!damage.IsValid) return DamageResult.Rejected(DamageStatus.InvalidRequest);
            if (CurrentHealth <= 0f) return DamageResult.Rejected(DamageStatus.NotAlive);
            float applied = Mathf.Min(CurrentHealth, damage.Amount);
            CurrentHealth -= applied; HitCount++;
            if (CurrentHealth <= 0f) gameObject.SetActive(false);
            return DamageResult.Applied(applied, applied > 0f && CurrentHealth <= 0f);
        }
    }
}

using System;
using DesertTower.VFX;
using UnityEngine;

namespace SandGuard.Enemy
{
    /// <summary>방패를 든 적의 정면 방어. 몸 정면 frontHalfAngle 안에서 날아온 공격은 피해에 frontMultiplier를 곱한다(0이면 막는다).</summary>
    /// <remarks>
    /// 방향은 <see cref="DamageInfo.HitDirection"/>(공격이 날아가는 방향)으로 판단한다. 방향이 없는 피해(낙사 등)는 막지 않는다.
    /// 폭발·폭풍은 폭발 중심에서 적으로 향하는 방향이므로, 중심이 적 정면에 있으면 막힌다.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class EnemyShield : MonoBehaviour, IDamageModifier
    {
        [Range(0f, 180f), Tooltip("정면으로 보는 반각(도). 60이면 앞쪽 120도")]
        public float frontHalfAngle = 60f;
        [Range(0f, 1f), Tooltip("정면 피해 배율. 0이면 완전히 막고, 0.5면 절반만 받는다")]
        public float frontMultiplier = 0f;
        [Tooltip("정면으로 받았을 때의 연출(VFX_Shield_Front_Guard). +Z가 공격해 온 쪽을 향한다")]
        public GameObject guardVfx;
        [Min(0.1f)] public float guardVfxLifetime = 1f;

        public int GuardCount { get; private set; }
        /// <summary>정면 공격을 방패로 받았다(막았거나 줄였다).</summary>
        public event Action<DamageInfo> Guarded;

        /// <summary>공격이 날아가는 방향이 이 적의 정면에서 온 것인지. 높낮이는 무시한다.</summary>
        public bool IsFrontal(Vector3 hitDirection)
        {
            Vector3 incoming = -hitDirection; incoming.y = 0f;
            Vector3 forward = transform.forward; forward.y = 0f;
            if (incoming.sqrMagnitude < 1e-6f || forward.sqrMagnitude < 1e-6f) return false;
            return Vector3.Angle(forward, incoming) <= frontHalfAngle;
        }

        public float ModifyIncoming(DamageInfo damage, float amount)
        {
            if (!damage.HitDirection.HasValue || !IsFrontal(damage.HitDirection.Value)) return amount;
            GuardCount++;
            if (guardVfx != null)
            {
                Vector3 point = damage.HitPosition ?? transform.position + Vector3.up;
                Vector3 outward = -damage.HitDirection.Value;
                PrefabPool.Release(PrefabPool.Spawn(guardVfx, point, Quaternion.LookRotation(outward.normalized)), guardVfxLifetime);
            }
            Guarded?.Invoke(damage);
            return amount * frontMultiplier;
        }
    }
}

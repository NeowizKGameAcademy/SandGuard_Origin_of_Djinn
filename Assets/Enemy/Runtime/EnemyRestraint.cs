using System;
using DesertTower.VFX;
using UnityEngine;

namespace SandGuard.Enemy
{
    /// <summary>
    /// 적의 이동 상태이상: 속박(모래 족쇄), 둔화(사막 폭풍), 밀림(모래 소용돌이).
    /// 속박은 남은 시간 동안 모터를 제자리에 세우고 발밑에 속박 연출(VFX_Sand_Root)을 붙였다가 풀릴 때 거둔다. 공격과 회전은 그대로다.
    /// 둔화는 가장 강한 것 하나만 적용되고, 밀림은 그 프레임 자기 걸음을 멈추고 옮겨진다.
    /// 넉백은 방향과 세기를 한 번 받아 두고 knockbackDamping으로 감속하며 스스로 민다. 죽거나 비활성화되면 전부 즉시 풀린다.
    /// </summary>
    [DefaultExecutionOrder(-90)] // 모터(-100) 다음, 두뇌(100) 전
    public sealed class EnemyRestraint : MonoBehaviour, IRestrainable, ISlowable, IDisplaceable
    {
        public EnemyMotor motor;
        public EnemyHealth health;
        [Tooltip("발밑에 붙이는 속박 연출. VfxSandRoot가 있으면 Release로 풀고, 없으면 1초 뒤 지운다")]
        public GameObject vfxPrefab;
        [Tooltip("연출을 붙일 발 기준점. 비우면 이 오브젝트")]
        public Transform feet;
        [Min(0f), Tooltip("외부 밀림 배율. 0이면 면역, 0.5면 절반, 1이면 그대로 받는다. Displace 이동량과 Knockback/Launch 입력 속도에 적용된다")]
        public float displacementMultiplier = 1f;
        [Min(0.1f), Tooltip("넉백 속도가 초당 줄어드는 양(m/s²). 클수록 짧게 밀린다. 총 거리는 세기²/(2×이 값)")]
        public float knockbackDamping = 12f;
        float endTime = -1f;
        float slowFactor, slowEndTime = -1f;
        Vector3 knock;
        GameObject vfx;
        public bool IsRestrained => endTime >= 0f && Time.time < endTime;
        public float RemainingSeconds => IsRestrained ? endTime - Time.time : 0f;
        public GameObject ActiveVfx => vfx;
        public float SlowFactor => slowEndTime >= 0f && Time.time < slowEndTime ? slowFactor : 0f;
        /// <summary>지금 남은 넉백 속도(m/s). 밀리는 중이 아니면 0이다.</summary>
        public Vector3 KnockbackVelocity => knock;
        /// <summary>(묶임 여부) 알림. 애니메이션·UI가 구독한다.</summary>
        public event Action<bool> Changed;

        void Awake()
        {
            if (motor == null) motor = GetComponent<EnemyMotor>();
            if (health == null) health = GetComponent<EnemyHealth>();
        }

        public void Restrain(float duration)
        {
            if (duration <= 0f || float.IsNaN(duration) || float.IsInfinity(duration) || !isActiveAndEnabled) return;
            if (health != null && !health.IsAlive) return;
            float until = Time.time + duration;
            if (until <= endTime) return;
            bool was = IsRestrained;
            endTime = until;
            if (was) return;
            if (motor != null) motor.Restrained = true;
            if (vfxPrefab != null)
            {
                Transform anchor = feet != null ? feet : transform;
                vfx = PrefabPool.Spawn(vfxPrefab, anchor.position, Quaternion.identity, transform);
            }
            Changed?.Invoke(true);
        }

        /// <summary>남은 시간과 상관없이 지금 푼다.</summary>
        public void Release() => Release(false);

        void Release(bool immediate)
        {
            if (endTime < 0f) return;
            endTime = -1f;
            if (motor != null) motor.Restrained = false;
            if (vfx != null)
            {
                var root = vfx.GetComponent<VfxSandRoot>();
                // 비활성화(적이 풀로 돌아가는 중)에는 사라지는 연출을 돌릴 수 없으므로 바로 거둔다.
                if (root != null && !immediate) root.Release();
                else PrefabPool.Release(vfx, immediate ? 0f : 1f);
                vfx = null;
            }
            Changed?.Invoke(false);
        }

        public void Slow(float factor, float duration)
        {
            if (duration <= 0f || factor <= 0f || float.IsNaN(factor) || float.IsNaN(duration) || !isActiveAndEnabled) return;
            if (health != null && !health.IsAlive) return;
            factor = Mathf.Clamp01(factor);
            float current = SlowFactor;
            if (factor < current) return; // 더 약한 둔화는 무시
            float until = Time.time + duration;
            if (factor > current) { slowFactor = factor; slowEndTime = until; } // 더 강한 둔화가 덮어쓴다
            else slowEndTime = Mathf.Max(slowEndTime, until);                   // 같은 세기는 시간만 늘린다
            if (motor != null) motor.SpeedMultiplier = 1f - slowFactor;
        }

        public void Displace(Vector3 delta)
        {
            if (!isActiveAndEnabled || (health != null && !health.IsAlive) || motor == null) return;
            if (!CanBeDisplaced) return;
            motor.Displace(delta * displacementMultiplier);
        }

        bool CanBeDisplaced => displacementMultiplier > 0f && !float.IsInfinity(displacementMultiplier);

        /// <summary>방향과 세기(m/s)를 받아 두고 멈출 때까지 Update에서 스스로 민다. 미는 쪽은 한 번만 부르면 된다.</summary>
        public void Knockback(Vector3 velocity)
        {
            if (!isActiveAndEnabled || (health != null && !health.IsAlive)) return;
            if (!CanBeDisplaced) return;
            velocity *= displacementMultiplier;
            velocity.y = 0f; // 띄우기는 EnemyFall이 맡는다
            if (velocity.sqrMagnitude > knock.sqrMagnitude) knock = velocity; // 더 센 쪽이 덮어쓴다
        }

        /// <summary>포물선으로 띄워 날린다. 낙하·낙사·복귀는 EnemyFall이 맡는다. 이미 공중이거나 낙하 부품이 없으면 false.</summary>
        public bool Launch(Vector3 velocity)
        {
            if (!isActiveAndEnabled || (health != null && !health.IsAlive) || motor == null) return false;
            if (!CanBeDisplaced) return false;
            if (motor.Fall == null || !motor.Fall.Launch(velocity * displacementMultiplier)) return false;
            knock = Vector3.zero; // 땅에서 밀리던 힘은 버리고 공중의 수평 속도에 넘긴다
            return true;
        }

        void Update()
        {
            bool dead = health != null && !health.IsAlive;
            if (endTime >= 0f && (Time.time >= endTime || dead)) Release();
            if (slowEndTime >= 0f && (Time.time >= slowEndTime || dead)) ClearSlow();
            if (knock.sqrMagnitude <= 1e-6f) return;
            if (dead || motor == null) { knock = Vector3.zero; return; }
            // 밀리는 동안 모터가 자기 걸음을 멈춘다. 가장자리 너머가 절벽이면 여기서 EnemyFall로 넘어간다.
            float dt = Time.deltaTime;
            motor.Displace(knock * dt);
            knock = Vector3.MoveTowards(knock, Vector3.zero, knockbackDamping * dt);
        }

        void ClearSlow()
        {
            slowEndTime = -1f; slowFactor = 0f;
            if (motor != null) motor.SpeedMultiplier = 1f;
        }

        void OnDisable() { Release(true); ClearSlow(); knock = Vector3.zero; }
    }
}

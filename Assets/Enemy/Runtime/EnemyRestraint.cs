using System;
using DesertTower.VFX;
using UnityEngine;

namespace SandGuard.Enemy
{
    /// <summary>
    /// 적의 이동 상태이상: 속박(모래 족쇄), 둔화(사막 폭풍), 밀림(모래 소용돌이).
    /// 속박은 남은 시간 동안 모터를 제자리에 세우고 발밑에 속박 연출(VFX_Sand_Root)을 붙였다가 풀릴 때 거둔다. 공격과 회전은 그대로다.
    /// 둔화는 가장 강한 것 하나만 적용되고, 밀림은 그 프레임 자기 걸음을 멈추고 옮겨진다. 죽거나 비활성화되면 전부 즉시 풀린다.
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
        float endTime = -1f;
        float slowFactor, slowEndTime = -1f;
        GameObject vfx;
        public bool IsRestrained => endTime >= 0f && Time.time < endTime;
        public float RemainingSeconds => IsRestrained ? endTime - Time.time : 0f;
        public GameObject ActiveVfx => vfx;
        public float SlowFactor => slowEndTime >= 0f && Time.time < slowEndTime ? slowFactor : 0f;
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
                vfx = Instantiate(vfxPrefab, anchor.position, Quaternion.identity, transform);
            }
            Changed?.Invoke(true);
        }

        /// <summary>남은 시간과 상관없이 지금 푼다.</summary>
        public void Release()
        {
            if (endTime < 0f) return;
            endTime = -1f;
            if (motor != null) motor.Restrained = false;
            if (vfx != null)
            {
                var root = vfx.GetComponent<VfxSandRoot>();
                if (root != null) root.Release(); else Destroy(vfx, 1f);
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
            motor.Displace(delta);
        }

        void Update()
        {
            bool dead = health != null && !health.IsAlive;
            if (endTime >= 0f && (Time.time >= endTime || dead)) Release();
            if (slowEndTime >= 0f && (Time.time >= slowEndTime || dead)) ClearSlow();
        }

        void ClearSlow()
        {
            slowEndTime = -1f; slowFactor = 0f;
            if (motor != null) motor.SpeedMultiplier = 1f;
        }

        void OnDisable() { Release(); ClearSlow(); }
    }
}

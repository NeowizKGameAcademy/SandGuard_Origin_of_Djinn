using System.Collections;
using UnityEngine;

namespace SandGuard.Enemy
{
    /// <summary>들고 있는 칼로 사거리 안의 대상을 벤다. 맞히는 순간 대상·거리·시야를 다시 확인한다.</summary>
    public sealed class EnemyMeleeAttack : MonoBehaviour, IAttackController
    {
        public EnemyHealth self;
        public EnemyVisuals visuals;
        [Tooltip("사거리와 시야를 재는 기준점. 외형에 지정된 위치가 있으면 그것을 우선한다")]
        public Transform attackOrigin;
        [Min(0.1f)] public float range = 1.8f;
        [Min(0f)] public float damage = 10f;
        [Min(0f), Tooltip("타워에 가하는 근접 피해 배수")] public float towerDamageMultiplier = 1f;
        [Min(0f), Tooltip("플레이어에 가하는 근접 피해 배수")] public float playerDamageMultiplier = 1f;
        [Min(0f), Tooltip("미니언에 가하는 근접 피해 배수")] public float minionDamageMultiplier = 1f;
        [Min(0.05f), Tooltip("공격 시작부터 실제로 맞히기까지의 시간")]
        public float windup = 0.35f;
        [Min(0.05f), Tooltip("공격 시작 사이의 간격")]
        public float interval = 1.2f;
        public LayerMask hitMask = ~0;
        [Min(4)] public int maxColliders = 16;
        public bool CombatEnabled { get; set; } = true;
        public bool IsAttacking => swing != null;
        public float CooldownRemaining { get; private set; }
        public int HitCount { get; private set; }
        Coroutine swing;
        Collider[] buffer;
        RaycastHit[] hits;

        public Vector3 Origin
        {
            get
            {
                if (visuals != null && visuals.AttackOrigin != null) return visuals.AttackOrigin.position;
                if (attackOrigin != null) return attackOrigin.position;
                return self != null ? self.HitPosition : transform.position + Vector3.up;
            }
        }

        void Update() => CooldownRemaining = Mathf.Max(0f, CooldownRemaining - Time.deltaTime);

        public bool TryAttack(ICombatTarget target)
        {
            if (!CombatEnabled || target == null || self == null || !self.IsAlive || IsAttacking || CooldownRemaining > 0f) return false;
            if (!IsHostile(target) || FindReachable(target) == null) return false;
            CooldownRemaining = interval;
            swing = StartCoroutine(Swing(target));
            return true;
        }

        public void Cancel()
        {
            if (swing == null) return;
            StopCoroutine(swing);
            swing = null;
            if (visuals != null) visuals.CancelAttack();
        }

        /// <summary>풀 재사용: 쿨다운·기록을 비운다.</summary>
        public void ResetForReuse() { Cancel(); CooldownRemaining = 0f; HitCount = 0; CombatEnabled = true; }

        /// <summary>대상의 충돌체가 사거리 안에 있고 사이를 가리는 것이 없다.</summary>
        public bool IsInRange(ICombatTarget target) => target != null && FindReachable(target) != null;

        IEnumerator Swing(ICombatTarget target)
        {
            if (visuals != null) visuals.PlayAttack(windup);
            yield return new WaitForSeconds(windup);
            swing = null;
            if (self == null || !self.IsAlive || !IsHostile(target)) yield break;
            Collider collider = FindReachable(target);
            if (collider == null) yield break;
            Vector3 origin = Origin;
            Vector3 point = collider.ClosestPoint(origin);
            Vector3 direction = point - origin;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
            IDamageable receiver = target.DamageReceiver;
            if (receiver == null) yield break;
            float multiplier = target.Kind switch
            {
                CombatTargetKind.Tower => towerDamageMultiplier,
                CombatTargetKind.Player => playerDamageMultiplier,
                CombatTargetKind.Minion => minionDamageMultiplier,
                _ => 1f
            };
            float amount = damage * multiplier;
            DamageResult result = receiver.TakeDamage(new DamageInfo(amount, self.FactionId, self.EntityId, "enemy.melee", point, direction));
            if (result.WasApplied) HitCount++;
        }

        bool IsHostile(ICombatTarget target) => target.IsTargetable && target.FactionId != self.FactionId;

        Collider FindReachable(ICombatTarget target)
        {
            buffer ??= new Collider[maxColliders];
            Vector3 origin = Origin;
            int count = Physics.OverlapSphereNonAlloc(origin, range, buffer, hitMask, QueryTriggerInteraction.Ignore);
            Collider best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var owner = buffer[i].GetComponentInParent<ICombatTarget>();
                if (owner == null || owner.EntityId != target.EntityId) continue;
                float distance = (buffer[i].ClosestPoint(origin) - origin).sqrMagnitude;
                if (distance < bestDistance && HasLineOfSight(origin, buffer[i], target)) { best = buffer[i]; bestDistance = distance; }
            }
            return best;
        }

        bool HasLineOfSight(Vector3 origin, Collider collider, ICombatTarget target)
        {
            Vector3 delta = collider.ClosestPoint(origin) - origin;
            if (delta.sqrMagnitude < 0.0001f) return true;
            hits ??= new RaycastHit[maxColliders];
            int count = Physics.RaycastNonAlloc(origin, delta.normalized, hits, delta.magnitude, hitMask, QueryTriggerInteraction.Ignore);
            Collider blocker = null;
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.transform.IsChildOf(transform)) continue;
                // 대상을 품고 있는 콜라이더(타워가 올라선 받침 등)는 대상을 가리는 벽이 아니다.
                // 받침과 타워 히트박스는 옆면이 정확히 겹쳐 레이 거리가 같으므로, 걸러 내지 않으면
                // 물리 엔진이 돌려주는 순서에 따라 같은 공격이 됐다 안 됐다 한다.
                if (collider.transform.IsChildOf(hits[i].collider.transform)) continue;
                if (hits[i].distance < nearest) { nearest = hits[i].distance; blocker = hits[i].collider; }
            }
            if (blocker == null) return true;
            var owner = blocker.GetComponentInParent<ICombatTarget>();
            return owner != null && owner.EntityId == target.EntityId;
        }
    }
}

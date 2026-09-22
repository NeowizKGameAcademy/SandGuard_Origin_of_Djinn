using UnityEngine;

namespace Tower
{
    /// <summary>실제로 재생 중인 공격 상태의 진행률에서 한 번만 타격한다.</summary>
    public sealed class MinionAttackSequence
    {
        public ICombatTarget Target { get; private set; }
        public bool Active { get; private set; }
        public bool IsSkill { get; private set; }
        private string stateName;
        private float impactTime;
        private float elapsed;
        private bool entered;
        private bool hit;

        public void Begin(ICombatTarget target, string state, float normalizedImpact, bool skill = false)
        {
            Target = target;
            stateName = state;
            impactTime = Mathf.Clamp01(normalizedImpact);
            IsSkill = skill;
            elapsed = 0f;
            entered = hit = false;
            Active = true;
        }

        public void Cancel() { Active = false; Target = null; }

        public bool Tick(Animator animator, float deltaTime)
        {
            if (!Active) return false;
            elapsed += deltaTime;
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                // 애니메이터가 없는 테스트/임시 모델도 선딜 후 한 번만 피해를 준다.
                if (elapsed >= 0.6f) Active = false;
                if (!hit && elapsed >= 0.35f) { hit = true; return true; }
                return false;
            }

            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0))
            {
                var next = animator.GetNextAnimatorStateInfo(0);
                if (next.IsName(stateName)) state = next;
            }
            if (!state.IsName(stateName))
            {
                // 사망/다른 상태로 중단됐거나 트리거가 연결되지 않은 경우 지연 피해를 취소한다.
                if (entered || elapsed > 2f) Cancel();
                return false;
            }

            entered = true;
            if (!hit && state.normalizedTime >= impactTime)
            {
                hit = true;
                return true;
            }
            // 해당 공격 상태에서 빠져나올 때까지 재공격 트리거를 쌓지 않는다.
            return false;
        }

        public static bool HeightReachable(Transform attacker, ICombatTarget target, float maxHeight)
        {
            if (target == null || (target is Object obj && obj == null) || !target.IsTargetable) return false;
            // HitPosition은 가슴/방패 조준점이므로 플랫폼 높이 비교에는 유닛 루트(발 위치)를 쓴다.
            float targetY = target is Component component ? component.transform.position.y : target.HitPosition.y;
            return Mathf.Abs(targetY - attacker.position.y) <= Mathf.Max(0f, maxHeight);
        }

        public static bool CanHit(Transform attacker, ICombatTarget target, float range, float maxHeight)
        {
            if (!HeightReachable(attacker, target, maxHeight) || target.DamageReceiver == null) return false;
            Vector3 offset = target.HitPosition - attacker.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= range * range;
        }
    }
}

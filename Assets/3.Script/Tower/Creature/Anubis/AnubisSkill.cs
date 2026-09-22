using UnityEngine;

namespace Tower
{
    public sealed class AnubisSkill : MonoBehaviour
    {
        [Min(0f)] public float range = 15f;
        [Range(0f, 360f)] public float angle = 90f;
        // 플레이어 "모래 폭발"의 넉백 세기에 맞췄다(PlayerBasicAttack.burstLaunchOut / burstLaunchUp).
        // 기존 6/8은 높이 띄우는 대신 멀리 못 날려서 타격감이 약했다.
        public float horizontalPower = 10f;
        public float verticalPower = 5.5f;
        public LayerMask targetMask = ~0;

        // public void Cast(Vector3 origin, Vector3 forward) // 기존 호출도 유지하되 수직 사거리를 추가한다.
        public void Cast(Vector3 origin, Vector3 forward, float maxHeight = 1.5f)
        {
            var affected = new System.Collections.Generic.HashSet<System.Guid>();
            foreach (var collider in Physics.OverlapSphere(origin, range, targetMask))
            {
                var target = collider.GetComponentInParent<ICombatTarget>();

                // if (target == null || !target.IsTargetable) // 기존에는 다른 층도 넉백하고 여러 콜라이더를 중복 처리했다.
                if (!MinionAttackSequence.HeightReachable(transform, target, maxHeight)
                    || target.FactionId == "Ally" || !affected.Add(target.EntityId))
                    continue;

                var direction = target.HitPosition - origin;
                direction.y = 0f;

                if (direction.sqrMagnitude < 0.001f || Vector3.Angle(forward, direction) > angle * 0.5f) 
                    continue;

                var displaceable = ((Component)target).GetComponentInParent<IDisplaceable>();
                displaceable?.Launch(direction.normalized * horizontalPower + Vector3.up * verticalPower);
            }
        }
    }
}

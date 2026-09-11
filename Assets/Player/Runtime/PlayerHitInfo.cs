using UnityEngine;

namespace SandGuard.Player
{
    /// <summary>플레이어의 공격이 실제로 피해를 준 한 번의 명중. 벽·아군·보호 중인 대상처럼 피해가 거부된 경우에는 만들어지지 않는다.</summary>
    public readonly struct PlayerHitInfo
    {
        /// <summary>전투 개체. 단순 IDamageable(테스트 표적 등)만 있으면 null일 수 있다.</summary>
        public readonly ICombatTarget Target;
        public readonly IDamageable Receiver;
        public readonly float AppliedDamage;
        public readonly bool WasKilled;
        public readonly Vector3 Point;
        public readonly Vector3 Direction;
        /// <summary>피해 원인 ID. 기본 볼트는 "player.basic".</summary>
        public readonly string CauseId;

        public PlayerHitInfo(ICombatTarget target, IDamageable receiver, float appliedDamage, bool wasKilled, Vector3 point, Vector3 direction, string causeId)
        {
            Target = target; Receiver = receiver; AppliedDamage = appliedDamage; WasKilled = wasKilled;
            Point = point; Direction = direction; CauseId = causeId;
        }
    }
}

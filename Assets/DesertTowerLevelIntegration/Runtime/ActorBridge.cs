using UnityEngine;

namespace DesertTower.LevelIntegration
{
    /// <summary>팀원 AI와의 연결 지점. 경로 러너는 Transform을 직접 이동시키지 않습니다.</summary>
    public abstract class ActorBridge : MonoBehaviour
    {
        public abstract bool Alive { get; }
        public virtual Vector3 FeetPosition => transform.position;
        public abstract bool Prepare(out string error);
        public abstract void Travel(Vector3 point);
        public abstract void Halt();
        public abstract void Remove();
    }
    public interface ILevelCoreReceiver
    {
        bool IsDefeated { get; }
        // False means not accepted (for example paused); called again without settling enemy.
        bool TryAbsorb(ActorBridge enemy, float damage);
    }
}

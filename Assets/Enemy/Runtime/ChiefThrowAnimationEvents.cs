using UnityEngine;

namespace SandGuard.Enemy
{
    /// <summary>애니메이터의 릴리스 이벤트를 보스 루트로 전달한다.</summary>
    public sealed class ChiefThrowAnimationEvents : MonoBehaviour
    {
        public void ReleaseChiefBomb() => GetComponentInParent<ChiefBombThrowSkill>()?.ReleaseBomb();
    }
}

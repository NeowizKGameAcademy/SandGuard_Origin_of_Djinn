using UnityEngine;

namespace SandGuard.Audio
{
    /// <summary>
    /// PlayMode 테스트가 에디터 API 없이 쓰는 애니메이터. `SandGuard > Audio > Setup Everything`이 만들어
    /// `Assets/8.Audio/Tests/Resources/SfxTestAssets.asset`에 둔다. 상태: Ground(0.2초 루프) / Idle(0.1초 루프) / Falling(1초 루프) / Attack(0.3초, 0.1초에 Swing 이벤트).
    /// </summary>
    public sealed class SfxTestAssets : ScriptableObject
    {
        public RuntimeAnimatorController controller;
    }
}

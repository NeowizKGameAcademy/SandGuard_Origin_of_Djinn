using UnityEngine;

namespace SandGuard.Audio
{
    /// <summary>
    /// 애니메이션 클립 이벤트를 받는 곳. Animator와 같은 GameObject에 붙어야 한다 (Unity가 그 오브젝트의 컴포넌트에서 함수 이름을 찾는다).
    /// 클립 이벤트는 에디터 `ClipEventBuilder`가 심는다: Swing(휘두르기 시작), Impact(타격), BodyFall(쓰러진 몸이 바닥에 닿음), Footstep.
    /// 족장의 기존 이벤트 ReleaseChiefBomb도 여기서 함께 받아 던지기 소리를 낸다.
    /// </summary>
    public sealed class SfxAnimationEvents : MonoBehaviour
    {
        public SfxCue SwingCue, ImpactCue, BodyFallCue, FootstepCue, ThrowCue;
        [Tooltip("재생 위치. 비우면 이 오브젝트")] public Transform Anchor;

        public int SwingCount { get; private set; }
        public int ImpactCount { get; private set; }
        public int BodyFallCount { get; private set; }

        public void Swing() { SwingCount++; Play(SwingCue); }
        public void Impact() { ImpactCount++; Play(ImpactCue); }
        public void BodyFall() { BodyFallCount++; Play(BodyFallCue); }
        public void Footstep() => Play(FootstepCue);
        public void ReleaseChiefBomb() => Play(ThrowCue);

        void Play(SfxCue cue)
        {
            if (cue == null || !Application.isPlaying) return;
            var at = Anchor != null ? Anchor : transform;
            if (cue.spatial) SfxPlayer.Play(cue, at.position); else SfxPlayer.Play2D(cue);
        }
    }
}

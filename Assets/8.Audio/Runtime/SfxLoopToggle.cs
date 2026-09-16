using UnityEngine;

namespace SandGuard.Audio
{
    /// <summary>
    /// UnityEvent로 켜고 끄는 루프. 상승 기류 충전처럼 "시작 → (세기) → 취소/발사" 이벤트가 있는 곳에 쓴다.
    ///   Begin()          루프 시작 (+ BeginCue 원샷)
    ///   SetIntensity(f)  0~1. 루프 피치를 최대 +50%까지 올린다
    ///   End()            루프 정지 (+ EndCue 원샷)
    /// </summary>
    public sealed class SfxLoopToggle : MonoBehaviour
    {
        public SfxCue LoopCue;
        [Tooltip("시작할 때 한 번")] public SfxCue BeginCue;
        [Tooltip("끝날 때 한 번")] public SfxCue EndCue;
        [Tooltip("재생 위치. 비우면 이 오브젝트")] public Transform Anchor;
        [Range(0f, 1f), Tooltip("SetIntensity(1)에서 피치가 이 비율만큼 오른다")] public float PitchRise = 0.5f;

        public SfxVoice Voice { get; private set; }
        public bool Running => Voice != null && Voice.Active;
        Transform Where => Anchor != null ? Anchor : transform;

        public void Begin()
        {
            if (Running) return;
            if (LoopCue != null) Voice = SfxPlayer.PlayLoop(LoopCue, Where);
            PlayOnce(BeginCue);
        }

        public void SetIntensity(float value)
        {
            if (!Running || LoopCue == null) return;
            Voice.Source.pitch = LoopCue.pitch * (1f + PitchRise * Mathf.Clamp01(value));
        }

        public void End()
        {
            bool was = Running;
            if (Voice != null) { SfxPlayer.Stop(Voice); Voice = null; }
            if (was) PlayOnce(EndCue);
        }

        void OnDisable() { if (Voice != null) { SfxPlayer.Stop(Voice); Voice = null; } }

        void PlayOnce(SfxCue cue)
        {
            if (cue == null) return;
            if (cue.spatial) SfxPlayer.Play(cue, Where.position); else SfxPlayer.Play2D(cue);
        }
    }
}

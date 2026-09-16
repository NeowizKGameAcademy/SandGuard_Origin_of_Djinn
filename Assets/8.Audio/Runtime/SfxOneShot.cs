using UnityEngine;

namespace SandGuard.Audio
{
    /// <summary>
    /// UnityEvent에 꽂아 쓰는 1회 재생기. VfxOneShot의 소리판.
    /// 예: PlayerVisuals.onFired → SfxOneShot.Fire (마나탄 발사), PlayerProgression.onLevelUp → Fire (레벨업).
    /// 큐가 2D면 위치 없이 고정 소스로 낸다.
    /// </summary>
    public sealed class SfxOneShot : MonoBehaviour
    {
        public SfxCue Cue;
        [Tooltip("재생 위치. 비우면 이 오브젝트")] public Transform Anchor;
        [Tooltip("대상에 붙어 같이 움직인다 (달리는 적의 피격 등)")] public bool Attach;
        [Range(0f, 2f), Tooltip("이 자리에서만 적용하는 크기 배율")] public float VolumeScale = 1f;

        /// <summary>UnityEvent 인스펙터 연결용.</summary>
        public void Fire() => Play();

        public SfxVoice Play()
        {
            if (Cue == null) return null;
            if (!Cue.spatial) { SfxPlayer.Play2D(Cue, VolumeScale); return null; }
            Transform anchor = Anchor != null ? Anchor : transform;
            var voice = Attach ? SfxPlayer.PlayAttached(Cue, anchor) : SfxPlayer.Play(Cue, anchor.position);
            if (voice != null && VolumeScale != 1f) voice.Source.volume *= VolumeScale;
            return voice;
        }

        public SfxVoice PlayAt(Vector3 position)
        {
            if (Cue == null) return null;
            if (!Cue.spatial) { SfxPlayer.Play2D(Cue, VolumeScale); return null; }
            var voice = SfxPlayer.Play(Cue, position);
            if (voice != null && VolumeScale != 1f) voice.Source.volume *= VolumeScale;
            return voice;
        }
    }
}

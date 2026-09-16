using DesertTower.VFX;
using UnityEngine;

namespace SandGuard.Audio
{
    /// <summary>
    /// 풀에서 빌려 쓰는 AudioSource 하나. 원샷은 클립 길이 뒤 스스로 풀로 돌아가고,
    /// 루프는 <see cref="SfxPlayer.Stop"/>이 부를 때까지 산다. 따라가던 대상이 꺼지면(적이 풀로 돌아감) 스스로 정리한다.
    /// 재생 상태는 AudioSource.isPlaying이 아니라 자체 타이머로 판단해 오디오 장치가 없는 배치 모드에서도 같은 동작을 한다.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class SfxVoice : MonoBehaviour, IPoolable
    {
        public AudioSource Source { get; private set; }
        public SfxCue Cue { get; private set; }
        public bool IsLoop { get; private set; }
        public bool Active { get; private set; }
        public float StartedAt { get; private set; }
        public Transform Follow { get; private set; }

        float endAt, fadeTarget, fadeSpeed;
        bool stopping;

        void Awake()
        {
            Source = GetComponent<AudioSource>();
            Source.playOnAwake = false;
        }

        public void OnRent() { Active = false; stopping = false; }

        public void OnReturn()
        {
            if (Source != null) { Source.Stop(); Source.clip = null; Source.outputAudioMixerGroup = null; }
            Cue = null; Follow = null; Active = false; stopping = false;
        }

        public void Play(SfxCue cue, AudioClip clip, float volume, float pitch, Transform follow, bool loop)
        {
            Cue = cue; IsLoop = loop; Follow = follow; Active = true; stopping = false;
            StartedAt = Time.time;
            Source.clip = clip;
            Source.volume = loop && cue.loopFade > 0f ? 0f : volume;
            Source.pitch = pitch;
            Source.loop = loop;
            Source.priority = cue.priority;
            Source.outputAudioMixerGroup = cue.mixerGroup;
            Source.spatialBlend = cue.spatial ? 1f : 0f;
            Source.minDistance = cue.minDistance;
            Source.maxDistance = cue.maxDistance;
            Source.rolloffMode = cue.rolloff;
            Source.dopplerLevel = 0f;
            Source.Play();
            fadeTarget = volume; fadeSpeed = loop && cue.loopFade > 0f ? volume / cue.loopFade : float.PositiveInfinity;
            endAt = loop ? float.PositiveInfinity : Time.time + (clip != null ? clip.length / Mathf.Max(0.05f, pitch) : 0f) + 0.05f;
        }

        /// <summary>이 자리의 크기 배율. 루프 페이드 목표에도 적용된다.</summary>
        public void ScaleVolume(float scale)
        {
            fadeTarget *= scale;
            if (float.IsInfinity(fadeSpeed)) Source.volume = fadeTarget; else fadeSpeed *= scale;
        }

        /// <summary>루프를 페이드로 끝낸다. 페이드가 0이면 즉시 반납.</summary>
        public void StopLoop()
        {
            if (!Active) return;
            if (Cue == null || Cue.loopFade <= 0f || Source.volume <= 0.001f) { Release(); return; }
            stopping = true; fadeTarget = 0f; fadeSpeed = Source.volume / Cue.loopFade;
        }

        void Update()
        {
            if (!Active) return;
            if (Follow != null && !Follow.gameObject.activeInHierarchy) { Release(); return; }
            if (!float.IsInfinity(fadeSpeed) && !Mathf.Approximately(Source.volume, fadeTarget))
                Source.volume = Mathf.MoveTowards(Source.volume, fadeTarget, fadeSpeed * Time.unscaledDeltaTime);
            if (stopping && Source.volume <= 0.001f) { Release(); return; }
            if (!IsLoop && Time.time >= endAt) Release();
        }

        /// <summary>따라가던 대상이 꺼지면 함께 꺼진다. 그 자리에 남지 않도록 풀로 돌려보낸다.</summary>
        void OnDisable() => Release();

        /// <summary>
        /// 풀 반납은 SfxPlayer가 LateUpdate에서 모아서 한다. 부모가 활성/비활성되는 도중에 부모를 바꾸면 Unity가 오류를 내므로
        /// (적이 풀로 돌아가며 꺼질 때, 이미터가 꺼질 때) 여기서는 상태만 정리하고 반납을 미룬다.
        /// </summary>
        void Release()
        {
            if (!Active) return;
            Active = false;
            if (Source != null) Source.Stop();
            SfxPlayer.NotifyVoiceEnded(this, true);
        }
    }
}

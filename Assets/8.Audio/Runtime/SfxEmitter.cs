using UnityEngine;

namespace SandGuard.Audio
{
    public enum SfxEmitterTrigger
    {
        /// <summary>켜지면 나고 꺼지면 멈춘다. 풀에서 스폰·반납되는 VFX, 씬 배치 루프, 2D 베드.</summary>
        OnEnable,
        /// <summary>부모 VFX의 파티클이 방출을 시작하면 나고 멈추면 끝난다. 항상 켜져 있고 Play/Stop만 하는 VFX(코브라 화염, 마나 충전, 소용돌이).</summary>
        WhileParticlesEmit,
    }

    /// <summary>
    /// 켜지면 소리가 나고 꺼지면 멈추는 이미터. 세 가지 자리에 같은 컴포넌트를 쓴다.
    ///   VFX 프리팹 자식   — VFX가 풀에서 나오고 돌아갈 때 소리도 같이 (횃불·코어·폭풍·화염·시설 정지)
    ///   씬 배치          — 코어 험, 제단 룬 같은 3D 루프
    ///   2D 베드          — 큐의 spatial을 끄면 사막 바람처럼 위치 없는 앰비언스
    /// Cue가 루프가 아니면 시작 시점에 한 번만 낸다 (원샷 VFX 프리팹용).
    /// BeginCue/EndCue는 시작·끝에 한 번씩 (점화·소화, 족쇄 조임·풀림, 소용돌이 종료).
    /// </summary>
    public sealed class SfxEmitter : MonoBehaviour
    {
        public SfxCue Cue;
        [Tooltip("시작할 때 한 번")] public SfxCue BeginCue;
        [Tooltip("끝날 때 한 번 (파티클 방출 정지 또는 비활성화)")] public SfxCue EndCue;
        [Range(0f, 2f)] public float VolumeScale = 1f;
        [Min(0f), Tooltip("켜진 뒤 이만큼 기다렸다 시작 (VFX 드러남 시점 맞추기)")] public float Delay;
        public SfxEmitterTrigger Trigger = SfxEmitterTrigger.OnEnable;

        public SfxVoice Voice { get; private set; }
        public bool Running { get; private set; }
        float startAt; bool pending, wasEmitting;
        ParticleSystem[] systems;

        void OnEnable()
        {
            if (Trigger == SfxEmitterTrigger.OnEnable)
            {
                pending = true; startAt = Time.time + Delay;
                if (Delay <= 0f) Begin();
            }
            else
            {
                var host = transform.parent != null ? transform.parent : transform;
                systems = host.GetComponentsInChildren<ParticleSystem>(true);
                wasEmitting = false;
            }
        }

        void Update()
        {
            if (pending && Time.time >= startAt) Begin();
            if (Trigger != SfxEmitterTrigger.WhileParticlesEmit) return;
            bool emitting = AnyEmitting();
            if (emitting && !wasEmitting) Begin();
            else if (!emitting && wasEmitting) End(true);
            wasEmitting = emitting;
        }

        /// <summary>씬이 내려가는 중(플레이어가 이미 없음)에는 끝 소리를 내지 않는다.</summary>
        void OnDisable()
        {
            pending = false; wasEmitting = false;
            End(SfxPlayer.Exists && gameObject.scene.isLoaded);
        }

        bool AnyEmitting()
        {
            if (systems == null) return false;
            foreach (var s in systems) if (s != null && s.isEmitting) return true;
            return false;
        }

        void Begin()
        {
            pending = false;
            if (!Application.isPlaying) return;
            Running = true;
            PlayOnce(BeginCue);
            if (Cue == null) return;
            if (Cue.loop) Voice = SfxPlayer.PlayLoop(Cue, transform);
            else if (Cue.spatial) Voice = SfxPlayer.Play(Cue, transform.position);
            else { SfxPlayer.Play2D(Cue, VolumeScale); return; }
            if (Voice != null && VolumeScale != 1f) Voice.ScaleVolume(VolumeScale);
        }

        void End(bool playEndCue)
        {
            if (Voice != null) { SfxPlayer.Stop(Voice); Voice = null; }
            if (Running && playEndCue) PlayOnce(EndCue);
            Running = false;
        }

        void PlayOnce(SfxCue cue)
        {
            if (cue == null || !Application.isPlaying) return;
            if (cue.spatial) SfxPlayer.Play(cue, transform.position); else SfxPlayer.Play2D(cue);
        }
    }
}

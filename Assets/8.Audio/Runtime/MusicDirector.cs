using DesertTower.LevelIntegration;
using UnityEngine;

namespace SandGuard.Audio
{
    /// <summary>
    /// 배경음. WaveDirector 상태(준비·전투·승리·패배)에 따라 큐를 크로스페이드하고, 웨이브 클리어 스팅어를 낸다.
    /// 코어 안정도가 DangerRatio 아래로 내려가면 전투 위에 Danger 레이어를 겹친다.
    /// WaveDirector·CoreReceiver는 씬에서 나중에 생겨도 되도록 1초마다 다시 찾는다. 메뉴 씬은 MenuMode를 켠다.
    /// 보스 등장 전환은 아직 없다 (EnemyBossInfo를 오디오가 참조하지 않으려고 뺐다).
    /// </summary>
    public sealed class MusicDirector : MonoBehaviour
    {
        [Header("Cues")]
        public SfxCue Menu, Preparation, Combat, Boss, Danger, Victory, Defeat, WaveClearSting, BossSting;

        [Header("Behaviour")]
        public bool MenuMode;
        [Min(0.05f)] public float Crossfade = 1.5f;
        [Range(0f, 1f), Tooltip("코어 안정도가 이 비율 아래면 Danger 레이어")] public float DangerRatio = 0.3f;

        public SfxCue Current { get; private set; }
        public AudioSource Front => front;
        public bool DangerOn { get; private set; }

        AudioSource front, back, danger;
        WaveDirector director; CoreReceiver core; float retryAt; int lastClip = -1;

        void Awake()
        {
            front = MakeSource("Music A"); back = MakeSource("Music B"); danger = MakeSource("Music Danger");
        }

        AudioSource MakeSource(string name)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false; s.spatialBlend = 0f; s.loop = true; s.volume = 0f; s.priority = 0;
            return s;
        }

        void OnDisable() { Unsubscribe(); }

        void Update()
        {
            FindTargets();
            var target = Choose();
            if (target != Current) Switch(target);
            Fade();
            UpdateDanger();
        }

        SfxCue Choose()
        {
            if (MenuMode) return Menu;
            if (director == null) return Preparation;
            switch (director.State)
            {
                case RunState.Running: return Combat;
                case RunState.Won: return Victory;
                case RunState.Lost: return Defeat;
                default: return Preparation;
            }
        }

        void Switch(SfxCue cue)
        {
            Current = cue;
            (front, back) = (back, front);
            front.Stop();
            if (cue == null || !cue.HasClips) return;
            front.clip = cue.Pick(ref lastClip);
            front.loop = cue.loop;
            front.outputAudioMixerGroup = cue.mixerGroup;
            front.pitch = cue.pitch;
            front.volume = 0f;
            front.Play();
        }

        void Fade()
        {
            float dt = Time.unscaledDeltaTime;
            float target = Current != null ? Current.volume : 0f;
            front.volume = Mathf.MoveTowards(front.volume, target, Mathf.Max(target, 0.5f) / Crossfade * dt);
            if (back.isPlaying || back.volume > 0f)
            {
                back.volume = Mathf.MoveTowards(back.volume, 0f, 1f / Crossfade * dt);
                if (back.volume <= 0f) back.Stop();
            }
        }

        void UpdateDanger()
        {
            bool on = Danger != null && Danger.HasClips && core != null && !MenuMode
                      && Current == Combat && core.maximum > 0f && core.Current / core.maximum < DangerRatio && !core.IsDefeated;
            if (on && !danger.isPlaying)
            {
                int i = -1; danger.clip = Danger.Pick(ref i); danger.loop = Danger.loop; danger.outputAudioMixerGroup = Danger.mixerGroup; danger.Play();
            }
            DangerOn = on;
            float target = on ? Danger.volume : 0f;
            danger.volume = Mathf.MoveTowards(danger.volume, target, 0.5f / Crossfade * Time.unscaledDeltaTime);
            if (!on && danger.volume <= 0f && danger.isPlaying) danger.Stop();
        }

        void FindTargets()
        {
            if (MenuMode || Time.unscaledTime < retryAt) return;
            retryAt = Time.unscaledTime + 1f;
            if (director == null)
            {
                director = FindFirstObjectByType<WaveDirector>();
                if (director != null) director.onWaveCleared.AddListener(OnWaveCleared);
            }
            if (core == null) core = FindFirstObjectByType<CoreReceiver>();
        }

        void Unsubscribe() { if (director != null) director.onWaveCleared.RemoveListener(OnWaveCleared); director = null; }

        void OnWaveCleared() { if (WaveClearSting != null) SfxPlayer.Play2D(WaveClearSting); }

        /// <summary>보스 등장 시 외부(HUD·적 코드)에서 부른다.</summary>
        public void BossAppeared() { if (BossSting != null) SfxPlayer.Play2D(BossSting); }
    }
}

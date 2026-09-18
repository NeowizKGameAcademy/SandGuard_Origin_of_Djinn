using DesertTower.LevelIntegration;
using UnityEngine;

namespace SandGuard.Audio
{
    /// <summary>
    /// 배경음. WaveDirector 상태(준비·전투·승리·패배)에 따라 큐를 크로스페이드하고, 웨이브 클리어 스팅어를 낸다.
    /// 코어 안정도가 DangerRatio 아래로 내려가면 전투 위에 Danger 레이어를 겹친다.
    /// WaveDirector·CoreReceiver는 씬에서 나중에 생겨도 되도록 1초마다 다시 찾는다. 메뉴 씬은 MenuMode를 켠다.
    /// 전투 중 보스(EnemyBossInfo)가 살아 있으면 Boss 곡으로 바꾸고, 보스가 처음 나타나는 순간 BossSting을 한 번 낸다.
    /// </summary>
    public sealed class MusicDirector : MonoBehaviour
    {
        [Header("Cues")]
        public SfxCue Menu, Preparation, Combat, Boss, Danger, Victory, Defeat, WaveClearSting, BossSting;

        [Header("Behaviour")]
        public bool MenuMode;
        [Min(0.05f)] public float Crossfade = 1.5f;
        [Range(0f, 1f), Tooltip("코어 안정도가 이 비율 아래면 Danger 레이어")] public float DangerRatio = 0.3f;
        [Min(0f), Tooltip("루프 큐의 긴 곡은 끝나기 이만큼 전에 처음부터 다시 시작해 겹쳐 넘긴다 (곡 끝이 뚝 끊기거나 크기가 확 바뀌는 것을 가림). 0이면 Unity 기본 반복")]
        public float SongEndCrossfade = 3f;

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
            else if (NearSongEnd()) Switch(Current); // 같은 곡을 처음부터, 끝나 가는 쪽은 back에서 페이드아웃
            Fade();
            UpdateDanger();
        }

        public bool BossActive { get; private set; }
        /// <summary>지금 곡을 Unity 반복 대신 끝 크로스페이드로 잇는 중인가.</summary>
        public bool ManualLoop { get; private set; }
        public int SongRestarts { get; private set; }
        float songStartedAt;

        bool NearSongEnd()
        {
            if (!ManualLoop || Current == null || front.clip == null) return false;
            if (Time.unscaledTime - songStartedAt < 1f) return false; // 스트리밍 시작 직후 isPlaying이 늦게 켜지는 경우
            bool ending = !front.isPlaying || front.clip.length - front.time <= SongEndCrossfade;
            if (ending) SongRestarts++;
            return ending;
        }

        SfxCue Choose()
        {
            if (MenuMode) return Menu;
            UpdateBoss();
            if (director == null) return Preparation;
            switch (director.State)
            {
                case RunState.Running: return BossActive && Boss != null && Boss.HasClips ? Boss : Combat;
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
            // 긴 곡은 직접 이어 붙인다(NearSongEnd). 짧은 루프(베드·테스트용)는 Unity 반복 그대로
            ManualLoop = cue.loop && SongEndCrossfade > 0f && front.clip != null && front.clip.length > SongEndCrossfade * 4f;
            front.loop = cue.loop && !ManualLoop;
            songStartedAt = Time.unscaledTime;
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

        /// <summary>살아 있는 보스 목록(EnemyBossInfo.Active)으로 보스 상태를 갱신한다. 없음→있음 순간에 스팅어.</summary>
        void UpdateBoss()
        {
            bool now = SandGuard.Enemy.EnemyBossInfo.Active.Count > 0;
            if (now && !BossActive) BossAppeared();
            BossActive = now;
        }

        /// <summary>보스 등장 스팅어. UpdateBoss가 부르며, 외부에서 직접 불러도 된다.</summary>
        public void BossAppeared() { if (BossSting != null && !MenuMode) SfxPlayer.Play2D(BossSting); }
    }
}

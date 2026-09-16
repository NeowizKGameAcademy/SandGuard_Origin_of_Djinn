using System.Collections.Generic;
using DesertTower.VFX;
using UnityEngine;

namespace SandGuard.Audio
{
    /// <summary>
    /// 재생 입구. 씬마다 하나가 필요할 때 만들어지고 씬과 함께 사라진다 (PrefabPool과 같은 규칙).
    ///   Play(cue, position)        3D 원샷. 풀에서 SfxVoice를 빌린다
    ///   PlayAttached(cue, target)  대상에 붙는 원샷. 대상이 꺼지면 같이 정리
    ///   PlayLoop(cue, target)      루프. 돌려받은 보이스를 Stop에 넘겨 끝낸다
    ///   Play2D(cue)                UI·화면 효과. 고정 소스에 PlayOneShot, 풀 미사용
    /// 큐별 동시 보이스 한도(초과 시 가장 오래된 것 재사용)와 같은 프레임 중복 합치기를 여기서 처리한다.
    /// </summary>
    public sealed class SfxPlayer : MonoBehaviour
    {
        static SfxPlayer instance;
        public static bool Exists => instance != null;
        public static SfxPlayer Instance
        {
            get
            {
                if (instance == null) instance = FindFirstObjectByType<SfxPlayer>();
                if (instance == null) instance = new GameObject("Sfx Player").AddComponent<SfxPlayer>();
                return instance;
            }
        }

        [Tooltip("2D 원샷용 고정 소스 수")] public int flatSources = 4;
        [Tooltip("전체 끄기 (테스트·설정)")] public bool muted;

        GameObject voiceTemplate;
        AudioSource[] flat; int flatNext;
        readonly Dictionary<SfxCue, List<SfxVoice>> active = new Dictionary<SfxCue, List<SfxVoice>>();
        readonly Dictionary<SfxCue, int> lastFrame = new Dictionary<SfxCue, int>();
        readonly Dictionary<SfxCue, float> lastTime = new Dictionary<SfxCue, float>();
        readonly Dictionary<SfxCue, int> lastClip = new Dictionary<SfxCue, int>();

        /// <summary>진단용: 지금 살아 있는 풀 보이스 수.</summary>
        public int ActiveVoiceCount { get { int n = 0; foreach (var l in active.Values) n += l.Count; return n; } }
        public int ActiveVoiceCountFor(SfxCue cue) => active.TryGetValue(cue, out var l) ? l.Count : 0;
        public IReadOnlyList<SfxVoice> ActiveVoices(SfxCue cue) => active.TryGetValue(cue, out var l) ? l : (IReadOnlyList<SfxVoice>)System.Array.Empty<SfxVoice>();
        /// <summary>진단용: 마지막으로 2D 재생한 클립.</summary>
        public AudioClip LastFlatClip { get; private set; }
        public int FlatPlays { get; private set; }

        void Awake()
        {
            if (instance == null) instance = this;
            voiceTemplate = new GameObject("Sfx Voice");
            voiceTemplate.transform.SetParent(transform, false);
            voiceTemplate.SetActive(false);
            var src = voiceTemplate.AddComponent<AudioSource>();
            src.playOnAwake = false;
            voiceTemplate.AddComponent<SfxVoice>();
            flat = new AudioSource[Mathf.Max(1, flatSources)];
            for (int i = 0; i < flat.Length; i++)
            {
                var go = new GameObject($"Sfx 2D {i}");
                go.transform.SetParent(transform, false);
                flat[i] = go.AddComponent<AudioSource>();
                flat[i].playOnAwake = false; flat[i].spatialBlend = 0f;
            }
        }

        void OnDestroy() { if (instance == this) instance = null; }

        /// <summary>준비 단계에서 보이스를 미리 만들어 첫 전투의 끊김을 없앤다.</summary>
        public static void Prewarm(int count)
        {
            var p = Instance;
            PrefabPool.Instance.Prewarm(p.voiceTemplate, count);
        }

        public static SfxVoice Play(SfxCue cue, Vector3 position) => Instance.Start(cue, position, null, false);
        public static SfxVoice PlayAttached(SfxCue cue, Transform target) => target == null ? null : Instance.Start(cue, target.position, target, false);
        public static SfxVoice PlayLoop(SfxCue cue, Transform target) => target == null ? null : Instance.Start(cue, target.position, target, true);
        public static void Stop(SfxVoice voice) { if (voice != null && voice.Active) voice.StopLoop(); }

        /// <summary>2D 원샷. UI 클릭·화면 피격·스팅어.</summary>
        public static void Play2D(SfxCue cue, float volumeScale = 1f)
        {
            var p = Instance;
            if (!p.Admit(cue)) return;
            var clip = p.PickClip(cue);
            if (clip == null) return;
            var src = p.flat[p.flatNext++ % p.flat.Length];
            src.outputAudioMixerGroup = cue.mixerGroup;
            src.pitch = cue.RandomPitch();
            if (!p.muted) src.PlayOneShot(clip, cue.RandomVolume() * volumeScale);
            p.LastFlatClip = clip; p.FlatPlays++;
        }

        SfxVoice Start(SfxCue cue, Vector3 position, Transform follow, bool loop)
        {
            if (!loop && !Admit(cue)) return null;
            var clip = PickClip(cue);
            if (clip == null) return null;

            if (!active.TryGetValue(cue, out var list)) { list = new List<SfxVoice>(); active[cue] = list; }
            if (list.Count >= Mathf.Max(1, cue.maxVoices))
            {
                // 가장 오래된 보이스를 훔친다
                SfxVoice oldest = list[0];
                foreach (var v in list) if (v.StartedAt < oldest.StartedAt) oldest = v;
                list.Remove(oldest);
                if (oldest != null && oldest.Active) { oldest.OnReturn(); PrefabPool.Release(oldest.gameObject); }
            }

            var go = PrefabPool.Spawn(voiceTemplate, position, Quaternion.identity, follow != null ? follow : transform);
            if (go == null) return null;
            if (!go.activeSelf) go.SetActive(true); // 템플릿이 비활성이라 첫 생성 개체도 비활성으로 나온다
            var voice = go.GetComponent<SfxVoice>();
            if (follow != null) go.transform.localPosition = Vector3.zero;
            voice.Play(cue, clip, muted ? 0f : cue.RandomVolume(), cue.RandomPitch(), follow, loop);
            list.Add(voice);
            return voice;
        }

        /// <summary>같은 프레임 중복과 최소 간격을 거른다. 클립이 없으면 조용히 거부한다.</summary>
        bool Admit(SfxCue cue)
        {
            if (cue == null || !cue.HasClips) return false;
            int frame = Time.frameCount;
            if (lastFrame.TryGetValue(cue, out int f) && f == frame) return false;
            if (cue.minInterval > 0f && lastTime.TryGetValue(cue, out float t) && Time.unscaledTime - t < cue.minInterval && frame != f) return false;
            lastFrame[cue] = frame; lastTime[cue] = Time.unscaledTime;
            return true;
        }

        AudioClip PickClip(SfxCue cue)
        {
            lastClip.TryGetValue(cue, out int last);
            var clip = cue.Pick(ref last);
            lastClip[cue] = last;
            return clip;
        }

        readonly List<SfxVoice> finished = new List<SfxVoice>();

        /// <summary>보이스가 끝났다. 장부에서 지우고, 요청이 있으면 다음 LateUpdate에 풀로 돌려보낸다.</summary>
        internal static void NotifyVoiceEnded(SfxVoice voice, bool returnToPool = false)
        {
            if (instance == null || voice == null) return;
            if (voice.Cue != null && instance.active.TryGetValue(voice.Cue, out var list)) list.Remove(voice);
            if (returnToPool && !instance.finished.Contains(voice)) instance.finished.Add(voice);
        }

        /// <summary>미뤄 둔 반납. 활성/비활성 콜백 밖이라 부모를 바꿔도 안전하다.</summary>
        void LateUpdate()
        {
            if (finished.Count == 0) return;
            for (int i = 0; i < finished.Count; i++)
                if (finished[i] != null && !finished[i].Active) PrefabPool.Release(finished[i].gameObject);
            finished.Clear();
        }
    }
}

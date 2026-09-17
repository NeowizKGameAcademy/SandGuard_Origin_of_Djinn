using UnityEngine;

namespace SandGuard.Audio
{
    /// <summary>
    /// 루프 애니메이션이 한 바퀴 감길 때마다 큐를 낸다. 코어 범위 원의 핑(Ground.anim 2초 루프)처럼
    /// "애니메이션 주기에 맞춰 반복되는 소리"용. Animator의 normalizedTime 정수부가 오를 때 발화하므로
    /// 속도를 바꾸거나 일시정지해도 애니메이션과 어긋나지 않는다.
    /// </summary>
    public sealed class SfxAnimatorLoop : MonoBehaviour
    {
        public SfxCue Cue;
        [Tooltip("비우면 이 오브젝트(없으면 자식)의 Animator")] public Animator Animator;
        public int Layer;
        [Tooltip("비우면 어떤 상태든 감길 때마다")] public string StateName;
        [Range(0f, 0.99f), Tooltip("루프 안에서 소리를 낼 위상. 0 = 시작")] public float Phase;

        public int Fired { get; private set; }
        int lastCycle = int.MinValue;

        void OnEnable() { lastCycle = int.MinValue; }

        void Update()
        {
            if (Animator == null) Animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
            if (Animator == null || !Animator.isActiveAndEnabled || Animator.runtimeAnimatorController == null) return;
            var info = Animator.GetCurrentAnimatorStateInfo(Layer);
            if (!string.IsNullOrEmpty(StateName) && !info.IsName(StateName)) { lastCycle = int.MinValue; return; }
            float t = info.normalizedTime - Phase;
            int cycle = Mathf.FloorToInt(t);
            if (lastCycle == int.MinValue)
            {
                // 막 시작한 루프(위상 0.1 안)면 이번 바퀴도 울린다
                lastCycle = (t - cycle) < 0.1f ? cycle - 1 : cycle;
            }
            if (cycle > lastCycle)
            {
                lastCycle = cycle;
                Fired++;
                Play(Cue);
            }
        }

        void Play(SfxCue cue)
        {
            if (cue == null) return;
            if (cue.spatial) SfxPlayer.Play(cue, transform.position); else SfxPlayer.Play2D(cue);
        }
    }

    /// <summary>
    /// 특정 애니메이터 상태에 들어가면 EnterCue(+LoopCue 시작), 나오면 ExitCue(+LoopCue 정지).
    /// 낙하 바람(Falling 상태), 충전 웅크림처럼 "상태가 곧 소리"인 곳에 쓴다. 코드 이벤트가 없어도 애니메이션만 보고 동작한다.
    /// </summary>
    public sealed class SfxAnimatorState : MonoBehaviour
    {
        public string StateName = "Falling";
        public int Layer;
        [Tooltip("비우면 이 오브젝트(없으면 자식)의 Animator")] public Animator Animator;
        public SfxCue EnterCue, ExitCue, LoopCue;
        [Min(0f), Tooltip("이 시간보다 짧게 머문 상태는 무시 (전이 중 스침 방지)")] public float MinDwell = 0.05f;

        public bool Active { get; private set; }
        SfxVoice loop; float enteredAt; bool pendingEnter;

        void OnDisable() { Leave(false); pendingEnter = false; }

        void Update()
        {
            if (Animator == null) Animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
            if (Animator == null || !Animator.isActiveAndEnabled || Animator.runtimeAnimatorController == null) return;
            bool inState = Animator.GetCurrentAnimatorStateInfo(Layer).IsName(StateName);
            if (inState && !Active && !pendingEnter) { pendingEnter = true; enteredAt = Time.time; }
            if (inState && pendingEnter && Time.time - enteredAt >= MinDwell) { pendingEnter = false; Enter(); }
            if (!inState) { pendingEnter = false; if (Active) Leave(true); }
        }

        void Enter()
        {
            Active = true;
            PlayOnce(EnterCue);
            if (LoopCue != null) loop = LoopCue.spatial ? SfxPlayer.PlayLoop(LoopCue, transform) : SfxPlayer.PlayLoop(LoopCue, transform);
        }

        void Leave(bool playExit)
        {
            if (!Active) return;
            Active = false;
            if (loop != null) { SfxPlayer.Stop(loop); loop = null; }
            if (playExit) PlayOnce(ExitCue);
        }

        void PlayOnce(SfxCue cue)
        {
            if (cue == null) return;
            if (cue.spatial) SfxPlayer.Play(cue, transform.position); else SfxPlayer.Play2D(cue);
        }
    }
}

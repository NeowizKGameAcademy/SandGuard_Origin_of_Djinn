using UnityEngine;

namespace SandGuard.Audio
{
    /// <summary>
    /// 특정 애니메이터 상태에 들어가면 EnterCue(+LoopCue 시작), 나오면 ExitCue(+LoopCue 정지).
    /// 낙하 바람(Falling 상태), 충전 웅크림처럼 "상태가 곧 소리"인 곳에 쓴다. 코드 이벤트가 없어도 애니메이션만 보고 동작한다.
    /// (MonoBehaviour는 파일 이름과 클래스 이름이 같아야 프리팹에 저장된다. 그래서 SfxAnimatorLoop와 파일을 나눴다.)
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
            if (LoopCue != null) loop = SfxPlayer.PlayLoop(LoopCue, transform);
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

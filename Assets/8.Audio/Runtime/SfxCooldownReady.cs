using SandGuard.Player;
using UnityEngine;

namespace SandGuard.Audio
{
    /// <summary>
    /// 스킬 쿨다운이 끝나는 순간 짧은 '준비됨' 신호를 낸다. Q/E/R(PlayerSkillCaster), 관통탄 충전, 흔적 귀환.
    /// 대시는 쿨다운이 짧아 매번 울리면 시끄러우므로 넣지 않는다. MinCooldown보다 짧았던 쿨다운도 조용히 넘긴다.
    /// </summary>
    public sealed class SfxCooldownReady : MonoBehaviour
    {
        public SfxCue Cue;
        [Min(0f), Tooltip("이보다 짧았던 쿨다운은 끝나도 울리지 않는다")] public float MinCooldown = 1.5f;

        public int Fired { get; private set; }
        PlayerSkillCaster caster; PlayerPierceCharge pierce; PlayerRecall recall;
        readonly float[] last = new float[5];
        readonly float[] peak = new float[5];

        void Awake()
        {
            caster = GetComponentInChildren<PlayerSkillCaster>(true);
            pierce = GetComponentInChildren<PlayerPierceCharge>(true);
            recall = GetComponentInChildren<PlayerRecall>(true);
        }

        void OnEnable() { System.Array.Clear(last, 0, last.Length); System.Array.Clear(peak, 0, peak.Length); }

        void Update()
        {
            if (caster != null) for (int i = 0; i < 3; i++) Watch(i, caster.CooldownRemaining(i));
            if (pierce != null && pierce.isActiveAndEnabled) Watch(3, pierce.CooldownRemaining);
            if (recall != null && recall.isActiveAndEnabled) Watch(4, recall.CooldownRemaining);
        }

        void Watch(int i, float remaining)
        {
            if (remaining > last[i]) peak[i] = remaining; // 새 쿨다운 시작
            if (last[i] > 0f && remaining <= 0f && peak[i] >= MinCooldown)
            {
                Fired++;
                if (Cue != null) { if (Cue.spatial) SfxPlayer.Play(Cue, transform.position); else SfxPlayer.Play2D(Cue); }
                peak[i] = 0f;
            }
            last[i] = remaining;
        }
    }
}

using System;

namespace SandGuard.Player.Effects
{
    /// <summary>③ 상승 기류: Space를 누르고 있다가 놓으면 발사되듯 높이 도약하는 능력을 켠다. 떼면 진행 중이던 충전도 취소된다.</summary>
    public sealed class UpdraftEffect : PlayerEffect
    {
        public override string DisplayName => "상승 기류";
        protected override void OnApply(PlayerEffectContext context)
        {
            var updraft = context.Root.GetComponent<PlayerUpdraft>();
            if (updraft == null) throw new InvalidOperationException(DisplayName + ": 플레이어에 PlayerUpdraft가 없습니다.");
            bool was = updraft.unlocked;
            updraft.unlocked = true; updraft.RefreshDeferral();
            OnUndo(() => { if (updraft != null) { updraft.unlocked = was; updraft.Cancel(); updraft.RefreshDeferral(); } });
        }
    }
}

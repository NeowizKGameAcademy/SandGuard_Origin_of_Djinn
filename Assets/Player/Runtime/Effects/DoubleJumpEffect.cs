namespace SandGuard.Player.Effects
{
    /// <summary>① 더블 점프: 공중 추가 점프 횟수를 올린다. 기본값은 0이라 이 효과가 없으면 지상 점프만 된다.</summary>
    public sealed class DoubleJumpEffect : PlayerEffect
    {
        public int ExtraJumps { get; }
        public override string DisplayName => "더블 점프";
        public DoubleJumpEffect(int extraJumps = 1) { ExtraJumps = extraJumps; }
        protected override void OnApply(PlayerEffectContext context)
            => AddStat(context, PlayerStat.ExtraAirJumps, StatModifierKind.Flat, ExtraJumps, label: DisplayName);
    }
}

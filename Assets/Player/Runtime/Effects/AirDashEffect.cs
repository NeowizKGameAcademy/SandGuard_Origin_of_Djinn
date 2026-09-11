namespace SandGuard.Player.Effects
{
    /// <summary>④ 공중 대시: 한 번 뜬 동안 쓸 수 있는 대시 횟수를 올린다. 기본값은 0이라 이 효과가 없으면 지상에서만 대시한다.</summary>
    public sealed class AirDashEffect : PlayerEffect
    {
        public int Dashes { get; }
        public override string DisplayName => "공중 대시";
        public AirDashEffect(int dashes = 1) { Dashes = dashes; }
        protected override void OnApply(PlayerEffectContext context)
            => AddStat(context, PlayerStat.AirDashes, StatModifierKind.Flat, Dashes, label: DisplayName);
    }
}

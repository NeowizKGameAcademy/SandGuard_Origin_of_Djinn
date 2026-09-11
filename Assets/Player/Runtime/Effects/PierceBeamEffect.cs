namespace SandGuard.Player.Effects
{
    /// <summary>관통탄: 볼트 대신 즉발 빔을 쏜다. 빔은 선 위의 적을 전부 꿰뚫고 벽·시설에서 멈춘다. 사거리·굵기는 PlayerBasicAttack 인스펙터(beamRange·beamRadius)에서 조절한다.</summary>
    public sealed class PierceBeamEffect : PlayerEffect
    {
        public override string DisplayName => "관통탄";
        protected override void OnApply(PlayerEffectContext context)
            => AddStat(context, PlayerStat.PierceBeam, StatModifierKind.Flat, 1f, label: DisplayName);
    }
}

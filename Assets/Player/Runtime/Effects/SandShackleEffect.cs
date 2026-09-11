namespace SandGuard.Player.Effects
{
    /// <summary>⑫ 모래 족쇄(패시브): 모래 폭발이 터진 자리마다 주변 적을 잠시 묶는다(이동만 멈춤). Q 폭발과 폭발 관통탄의 폭발 모두에 붙는다. 반경·지속은 PlayerBasicAttack 인스펙터(shackleRadius·shackleDuration)에서 조절한다.</summary>
    public sealed class SandShackleEffect : PlayerEffect
    {
        public override string DisplayName => "모래 족쇄";
        protected override void OnApply(PlayerEffectContext context)
            => AddStat(context, PlayerStat.SandShackle, StatModifierKind.Flat, 1f, label: DisplayName);
    }
}

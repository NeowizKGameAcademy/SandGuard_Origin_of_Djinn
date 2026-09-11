namespace SandGuard.Player.Effects
{
    /// <summary>⑯ 사막 폭풍: R 시전을 해금한다. 조준 지점에 5초 폭풍을 일으켜 안의 적에게 지속 피해와 강한 둔화를 준다. 수치는 PlayerSkillCaster 인스펙터.</summary>
    public sealed class SandStormEffect : PlayerEffect
    {
        public override string DisplayName => "사막 폭풍";
        protected override void OnApply(PlayerEffectContext context)
            => AddStat(context, PlayerStat.SandStorm, StatModifierKind.Flat, 1f, label: DisplayName);
    }
}

namespace SandGuard.Player.Effects
{
    /// <summary>⑪ 모래 폭발: Q 시전을 해금한다. 볼트를 쏴서 닿은 자리(적·벽)에서 폭발해 반경 안의 적에게 볼트 피해의 일부를 준다. 반경·비율은 PlayerBasicAttack, 마나·쿨다운은 PlayerSkillCaster 인스펙터에서 조절한다.</summary>
    public sealed class SandBurstEffect : PlayerEffect
    {
        public override string DisplayName => "모래 폭발";
        protected override void OnApply(PlayerEffectContext context)
            => AddStat(context, PlayerStat.SandBurst, StatModifierKind.Flat, 1f, label: DisplayName);
    }
}

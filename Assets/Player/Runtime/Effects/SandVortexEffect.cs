namespace SandGuard.Player.Effects
{
    /// <summary>⑮ 모래 소용돌이: E 시전을 해금한다. 조준 지점에 소용돌이를 만들어 반경 안의 적을 중심으로 끌어당기고 끝날 때 잠시 묶는다. 수치는 PlayerSkillCaster 인스펙터.</summary>
    public sealed class SandVortexEffect : PlayerEffect
    {
        public override string DisplayName => "모래 소용돌이";
        protected override void OnApply(PlayerEffectContext context)
            => AddStat(context, PlayerStat.SandVortex, StatModifierKind.Flat, 1f, label: DisplayName);
    }
}

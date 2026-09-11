namespace SandGuard.Player.Effects
{
    /// <summary>⑬ 응축 마나탄: 볼트 피해가 오르고 볼트가 눈에 띄게 커진다. 수치는 생성자에서 바꾼다.</summary>
    public sealed class CondensedBoltEffect : PlayerEffect
    {
        public float DamageBonus { get; }
        public float ScaleBonus { get; }
        public override string DisplayName => "응축 마나탄";
        public CondensedBoltEffect(float damageBonus = 0.3f, float scaleBonus = 0.35f) { DamageBonus = damageBonus; ScaleBonus = scaleBonus; }
        protected override void OnApply(PlayerEffectContext context)
        {
            AddStat(context, PlayerStat.AttackDamage, StatModifierKind.PercentAdd, DamageBonus, label: DisplayName);
            AddStat(context, PlayerStat.BoltScale, StatModifierKind.PercentAdd, ScaleBonus, label: DisplayName);
        }
    }
}

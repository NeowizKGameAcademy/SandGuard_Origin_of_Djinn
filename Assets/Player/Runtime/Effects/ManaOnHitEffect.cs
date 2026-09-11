namespace SandGuard.Player.Effects
{
    /// <summary>⑩ 마나 순환: 볼트가 적에게 실제 피해를 줄 때마다 마나를 회복한다. 꿰뚫은 적마다 한 번씩 센다.</summary>
    public sealed class ManaOnHitEffect : PlayerEffect
    {
        public int ManaPerHit { get; }
        public override string DisplayName => "마나 순환";
        public ManaOnHitEffect(int manaPerHit = 3) { ManaPerHit = manaPerHit; }
        // 회복 자체는 PlayerBasicAttack이 ManaPerHit 스탯을 읽어 수행한다. 여기서는 그 수치만 붙였다 뗀다.
        protected override void OnApply(PlayerEffectContext context)
            => AddStat(context, PlayerStat.ManaPerHit, StatModifierKind.Flat, ManaPerHit, label: DisplayName);
    }
}

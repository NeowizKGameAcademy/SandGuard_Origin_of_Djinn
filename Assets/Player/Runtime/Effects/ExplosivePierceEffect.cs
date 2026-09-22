namespace SandGuard.Player.Effects
{
    /// <summary>
    /// ⑭ 폭발 관통탄: 기본 공격이 관통 빔이 되고, 빔이 꿰뚫은 적마다 폭발한다. 반경·피해·수평 밀어내기는 Q와 별도로 설정한다.
    /// 관통탄을 같이 켜 주므로 단독으로도 동작하고, 선행 노드가 이미 붙어 있어도 켜짐 개수만 늘 뿐 세기는 변하지 않는다. Q 시전 해금과는 별개다.
    /// </summary>
    public sealed class ExplosivePierceEffect : PlayerEffect
    {
        public override string DisplayName => "폭발 관통탄";
        protected override void OnApply(PlayerEffectContext context)
        {
            AddStat(context, PlayerStat.PierceBeam, StatModifierKind.Flat, 1f, label: DisplayName);
            AddStat(context, PlayerStat.BurstPerPierce, StatModifierKind.Flat, 1f, label: DisplayName);
        }
    }
}

namespace SandGuard.Enemy
{
    /// <summary>받는 쪽 피해 보정. <see cref="EnemyHealth"/>와 같은 오브젝트에 붙이면 피해를 적용하기 전에 불린다.</summary>
    /// <remarks>
    /// 방패처럼 특정 적만 가진 방어 규칙을 체력 코드 밖에 둔다. 여러 개면 붙은 순서대로 이어서 적용한다.
    /// 결과가 0이면 피해는 <see cref="DamageStatus.Protected"/>로 거부되고 피격 알림도 나가지 않는다.
    /// </remarks>
    public interface IDamageModifier
    {
        /// <summary>지금까지 보정된 피해량 amount를 받아 새 피해량을 돌려준다. 막으면 0을 돌려준다.</summary>
        float ModifyIncoming(DamageInfo damage, float amount);
    }
}

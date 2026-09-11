/// <summary>
/// 이동을 일정 시간 묶을 수 있는 개체(모래 족쇄 등). 공격·회전은 막지 않고 이동만 멈춘다.
/// 여러 번 걸리면 남은 시간이 더 긴 쪽이 남는다(누적하지 않는다).
/// </summary>
public interface IRestrainable
{
    bool IsRestrained { get; }
    /// <summary>지금부터 duration초 동안 묶는다. 이미 더 길게 묶여 있으면 그대로 둔다. 0 이하면 무시.</summary>
    void Restrain(float duration);
}

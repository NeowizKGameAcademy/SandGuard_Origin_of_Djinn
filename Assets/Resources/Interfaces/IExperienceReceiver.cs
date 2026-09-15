using UnityEngine;

/// <summary>경험치를 받아 누적하는 대상이다. 떨어진 경험치 입자가 날아갈 곳을 찾을 때 쓴다.</summary>
/// <remarks>
/// 누가 적을 처치했는지와 무관하게 입자는 받을 수 있는 가장 가까운 대상에게 간다.
/// 대상은 활성화될 때 <see cref="ExperienceReceivers"/>에 등록하고 비활성화될 때 해제한다.
/// </remarks>
public interface IExperienceReceiver
{
    /// <summary>입자가 닿아야 하는 월드 위치다. 보통 가슴 높이다.</summary>
    Vector3 CollectPosition { get; }
    /// <summary>지금 받을 수 있는지 알려 준다. 무력화 중이면 false이고, 입자는 받을 수 있을 때까지 기다린다.</summary>
    bool CanCollect { get; }
    /// <summary>경험치를 더한다. 최대 레벨에서 버려진 양을 뺀, 실제로 반영된 양을 돌려준다.</summary>
    int GainExperience(int amount);
}

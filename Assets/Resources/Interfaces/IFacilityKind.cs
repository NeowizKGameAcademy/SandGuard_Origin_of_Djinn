/// <summary>타워·시설이 어떤 종류인지 알려 준다. 종류마다 다르게 대응하는 적이 쓴다.</summary>
/// <remarks>
/// ID 규칙은 시설 카탈로그와 같다("tower.cobra", "tower.obelisk").
/// <see cref="IFacility"/>는 건설·수리·철거가 쓰는 전체 정보라 무겁다. 종류만 알면 되는 쪽은 이것만 구현한다.
/// 타워 구현이 Assembly-CSharp에 있어 적 어셈블리에서 직접 볼 수 없으므로, 종류는 이 인터페이스로만 건넨다.
/// </remarks>
public interface IFacilityKind
{
    /// <summary>이 시설이 어떤 종류인지 찾는 ID다. 비어 있으면 종류를 밝히지 않은 것이다.</summary>
    string DefinitionId { get; }
}

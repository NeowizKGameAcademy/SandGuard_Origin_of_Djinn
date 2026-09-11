/// <summary>이동 속도를 일정 시간 낮출 수 있는 개체(사막 폭풍 등). 여러 둔화가 겹치면 가장 강한 것 하나만 적용된다.</summary>
public interface ISlowable
{
    /// <summary>지금 적용 중인 둔화 비율(0 = 없음, 0.6 = 속도 40%).</summary>
    float SlowFactor { get; }
    /// <summary>duration초 동안 이동 속도를 (1 - factor)배로 낮춘다. 진행 중인 둔화보다 약하면 무시되고, 같은 세기면 시간을 늘린다.</summary>
    void Slow(float factor, float duration);
}

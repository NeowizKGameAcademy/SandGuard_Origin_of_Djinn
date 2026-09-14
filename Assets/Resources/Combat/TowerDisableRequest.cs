using System;
using UnityEngine;

/// <summary>타워 담당 시스템에 전달하는 정지 요청. 실제 상태와 VFX 수명은 수신자가 관리한다.</summary>
public readonly struct TowerDisableRequest
{
    public Guid TargetEntityId { get; }
    public float Duration { get; }
    public DamageInfo Cause { get; }
    public TowerDisableRequest(Guid targetEntityId, float duration, DamageInfo cause)
    {
        if (targetEntityId == Guid.Empty) throw new ArgumentException("A target is required.", nameof(targetEntityId));
        if (float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0) throw new ArgumentOutOfRangeException(nameof(duration));
        TargetEntityId = targetEntityId; Duration = duration; Cause = cause;
    }
}

/// <summary>전투 효과 요청 창구. 수신자가 없으면 요청만 발생하고 게임 상태는 바꾸지 않는다.</summary>
public static class CombatEffectSignals
{
    public static event Action<TowerDisableRequest> TowerDisableRequested;
    public static void RequestTowerDisable(TowerDisableRequest request) => TowerDisableRequested?.Invoke(request);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => TowerDisableRequested = null;
}

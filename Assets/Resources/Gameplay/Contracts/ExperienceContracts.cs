using System.Collections.Generic;
using UnityEngine;

/// <summary>씬에 있는 경험치 수신자 목록. 드롭 쪽은 플레이어 코드를 모르고 여기서 대상을 찾는다.</summary>
public static class ExperienceReceivers
{
    static readonly List<IExperienceReceiver> receivers = new List<IExperienceReceiver>();

    public static int Count => receivers.Count;
    public static void Register(IExperienceReceiver receiver) { if (receiver != null && !receivers.Contains(receiver)) receivers.Add(receiver); }
    public static void Unregister(IExperienceReceiver receiver) => receivers.Remove(receiver);

    /// <summary>지금 받을 수 있는 수신자 중 <paramref name="position"/>에서 가장 가까운 것을 찾는다.</summary>
    public static bool TryFindNearest(Vector3 position, out IExperienceReceiver nearest)
    {
        nearest = null;
        float best = float.PositiveInfinity;
        for (int i = receivers.Count - 1; i >= 0; i--)
        {
            var receiver = receivers[i];
            if (receiver == null || receiver is Object unityObject && unityObject == null) { receivers.RemoveAt(i); continue; }
            if (!receiver.CanCollect) continue;
            float distance = (receiver.CollectPosition - position).sqrMagnitude;
            if (distance < best) { best = distance; nearest = receiver; }
        }
        return nearest != null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => receivers.Clear();
}

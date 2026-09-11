using UnityEngine;

/// <summary>외부 힘(모래 소용돌이 등)에 밀려 움직일 수 있는 개체. 자기 이동은 그 프레임 동안 멈추고 주어진 변위만큼 옮겨진다.</summary>
public interface IDisplaceable
{
    /// <summary>이번 프레임에 delta(월드)만큼 옮긴다. 길 찾기 개체는 NavMesh 위에 남는다.</summary>
    void Displace(Vector3 delta);
}

using UnityEngine;

/// <summary>외부 힘(모래 소용돌이·아군 넉백 등)에 밀리거나 띄워질 수 있는 개체. 자기 이동은 그 동안 멈추고 주어진 힘대로 옮겨진다.</summary>
public interface IDisplaceable
{
    /// <summary>이번 프레임에 delta(월드)만큼 옮긴다. 길 찾기 개체는 NavMesh 위에 남는다.</summary>
    void Displace(Vector3 delta);
    /// <summary>방향과 세기(m/s)를 한 번만 준다. 감속하며 멈출 때까지 매 프레임 미는 일은 받는 쪽이 한다.</summary>
    /// <remarks>이미 밀리는 중이면 더 센 쪽이 덮어쓴다. 수직 성분은 쓰지 않는다.</remarks>
    void Knockback(Vector3 velocity);
    /// <summary>포물선으로 띄워 날린다. velocity의 y가 솟는 세기, xz가 날아가는 방향(m/s)이다. 띄우지 못했으면 false.</summary>
    /// <remarks>수직은 중력이, 낙하·낙사·복귀는 받는 쪽이 맡는다. 부르는 쪽은 한 번만 준다.</remarks>
    bool Launch(Vector3 velocity);
}

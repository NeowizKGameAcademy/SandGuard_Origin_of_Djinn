using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SandGuard.Enemy
{
    /// <summary>NavMesh 가장자리 한 구간. 밀려 넘어가면 어디에 떨어지는지 미리 본 것이다.</summary>
    public readonly struct NavMeshLedge
    {
        /// <summary>가장자리 중점(NavMesh 위).</summary>
        public readonly Vector3 Edge;
        /// <summary>바깥 방향(수평 단위 벡터).</summary>
        public readonly Vector3 Outward;
        /// <summary>가장자리에서 안쪽으로 조금 들어온 NavMesh 지점. 적을 세우는 자리다.</summary>
        public readonly Vector3 Inside;
        /// <summary>너머의 바닥. 없으면 NaN.</summary>
        public readonly Vector3 Landing;
        /// <summary>Edge.y - Landing.y. 바닥이 없으면 +Infinity.</summary>
        public readonly float Drop;
        /// <summary>너머의 바닥이 NavMesh 위인가(떨어진 뒤 바로 다시 붙을 수 있다).</summary>
        public readonly bool LandsOnNavMesh;
        public bool IsVoid => float.IsPositiveInfinity(Drop);

        public NavMeshLedge(Vector3 edge, Vector3 outward, Vector3 inside, Vector3 landing, float drop, bool landsOnNavMesh)
        { Edge = edge; Outward = outward; Inside = inside; Landing = landing; Drop = drop; LandsOnNavMesh = landsOnNavMesh; }
    }

    /// <summary>구워진 NavMesh의 바깥 가장자리 중 절벽인 곳을 찾는다. 낙사 테스트·데모가 적을 세울 자리를 고르는 데 쓴다.</summary>
    public static class NavMeshLedges
    {
        /// <param name="minDrop">이보다 얕은 단차는 절벽으로 치지 않는다.</param>
        /// <param name="probeDistance">가장자리에서 바깥으로 이만큼 나간 지점의 바닥을 본다. 굽기 반지름(0.5)만큼 안쪽으로 들어와 있으므로 그보다 커야 한다.</param>
        /// <param name="maxDepth">이보다 깊은 곳의 바닥은 없는 것으로 본다.</param>
        public static List<NavMeshLedge> Find(float minDrop = 1f, float probeDistance = 1f, float maxDepth = 80f, int areaMask = NavMesh.AllAreas, int groundMask = ~0)
        {
            var result = new List<NavMeshLedge>();
            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            if (tri.indices == null || tri.indices.Length < 3) return result;
            // 타일 경계에서 정점이 중복되므로 위치로 합쳐야 안쪽 이음새가 가장자리로 잡히지 않는다.
            var canonical = new int[tri.vertices.Length];
            var byPosition = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < tri.vertices.Length; i++)
            {
                Vector3 v = tri.vertices[i];
                var key = new Vector3Int(Mathf.RoundToInt(v.x * 100f), Mathf.RoundToInt(v.y * 100f), Mathf.RoundToInt(v.z * 100f));
                if (!byPosition.TryGetValue(key, out int id)) byPosition[key] = id = i;
                canonical[i] = id;
            }
            var edges = new Dictionary<(int, int), (int count, int third)>();
            void Add(int a, int b, int c)
            {
                var key = a < b ? (a, b) : (b, a);
                edges[key] = edges.TryGetValue(key, out var entry) ? (entry.count + 1, entry.third) : (1, c);
            }
            for (int t = 0; t + 2 < tri.indices.Length; t += 3)
            {
                int a = canonical[tri.indices[t]], b = canonical[tri.indices[t + 1]], c = canonical[tri.indices[t + 2]];
                if (a == b || b == c || a == c) continue;
                Add(a, b, c); Add(b, c, a); Add(c, a, b);
            }
            foreach (var pair in edges)
            {
                if (pair.Value.count != 1) continue;
                Vector3 a = tri.vertices[pair.Key.Item1], b = tri.vertices[pair.Key.Item2], c = tri.vertices[pair.Value.third];
                Vector3 along = b - a; along.y = 0f;
                if (along.magnitude < 0.4f) continue;
                Vector3 mid = (a + b) * 0.5f;
                Vector3 outward = new Vector3(-along.z, 0f, along.x).normalized;
                Vector3 toThird = c - mid; toThird.y = 0f;
                if (Vector3.Dot(outward, toThird) > 0f) outward = -outward;
                if (!NavMesh.SamplePosition(mid - outward * 0.6f, out NavMeshHit insideHit, 0.5f, areaMask)) continue;
                // 벽이면 절벽이 아니다.
                if (Physics.Raycast(mid + Vector3.up * 0.5f, outward, probeDistance + 0.5f, groundMask, QueryTriggerInteraction.Ignore)) continue;
                Vector3 probe = mid + outward * probeDistance;
                if (Physics.Raycast(probe + Vector3.up * 0.5f, Vector3.down, out RaycastHit ground, maxDepth + 0.5f, groundMask, QueryTriggerInteraction.Ignore))
                {
                    float drop = mid.y - ground.point.y;
                    if (drop < minDrop) continue;
                    bool onNavMesh = NavMesh.SamplePosition(ground.point, out NavMeshHit landingHit, 0.6f, areaMask)
                        && Mathf.Abs(landingHit.position.y - ground.point.y) <= 0.5f;
                    result.Add(new NavMeshLedge(mid, outward, insideHit.position, ground.point, drop, onNavMesh));
                }
                else result.Add(new NavMeshLedge(mid, outward, insideHit.position, new Vector3(float.NaN, float.NaN, float.NaN), float.PositiveInfinity, false));
            }
            return result;
        }
    }
}

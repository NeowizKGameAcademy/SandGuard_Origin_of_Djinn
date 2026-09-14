using System;
using System.Collections.Generic;
using DesertTower.Levels;
using UnityEngine;
using UnityEngine.AI;

namespace DesertTower.LevelIntegration
{
    public sealed class RouteGraph : MonoBehaviour
    {
        public LevelRoot level;
        public RouteNode[] Nodes => GetComponentsInChildren<RouteNode>(true);
        public RouteNode Find(LevelMarker marker)
        {
            foreach (var node in Nodes) if (node.marker == marker) return node;
            return null;
        }
        public static bool Enabled(RouteLink link) => link != null && link.available &&
            link.target && link.target.isActiveAndEnabled && link.weight > 0 &&
            !float.IsNaN(link.weight) && !float.IsInfinity(link.weight);

        public bool CanReach(RouteNode start, RouteNode goal)
        {
            var seen = new HashSet<RouteNode>();
            var stack = new Stack<RouteNode>();
            if (start) stack.Push(start);
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                if (!node || !node.isActiveAndEnabled || !seen.Add(node)) continue;
                if (node == goal) return true;
                foreach (var edge in node.outgoing) if (Enabled(edge)) stack.Push(edge.target);
            }
            return false;
        }

        // Called once on reaching a node; a blocked physical edge does not reroll every frame.
        public RouteNode Choose(RouteNode node, RouteNode goal, double sample)
        {
            var links = new List<RouteLink>();
            double total = 0;
            foreach (var edge in node.outgoing)
                if (Enabled(edge) && CanReach(edge.target, goal)) { links.Add(edge); total += edge.weight; }
            if (links.Count == 0) return null;
            double cursor = Math.Max(0, Math.Min(.999999999, sample)) * total;
            foreach (var edge in links) { cursor -= edge.weight; if (cursor < 0) return edge.target; }
            return links[links.Count - 1].target;
        }

        public List<string> ValidateGraph()
        {
            var errors = new List<string>();
            var nodes = new HashSet<RouteNode>(Nodes);
            var ids = new HashSet<string>();
            var markers = new HashSet<LevelMarker>();
            if (!level) errors.Add("LevelRoot가 없습니다.");
            if (nodes.Count == 0) errors.Add("노드가 없습니다.");
            foreach (var node in nodes)
            {
                if (string.IsNullOrWhiteSpace(node.id) || !ids.Add(node.id)) errors.Add(node.name + ": 노드 ID 누락/중복");
                if (node.arrivalRadius <= 0 || node.heightTolerance <= 0) errors.Add(node.name + ": 도착 허용값 오류");
                if (node.marker && (!markers.Add(node.marker) || node.marker.GetComponentInParent<LevelRoot>() != level))
                    errors.Add(node.name + ": 마커 중복 또는 다른 레벨 마커");
                if (node.marker && node.marker.kind != MarkerKind.Core && node.marker.kind != MarkerKind.EnemySpawn)
                    errors.Add(node.name + ": 코어/입구 마커만 지정하세요.");
                if (node.marker && !node.Contains(node.marker.transform.position)) errors.Add(node.name + ": 마커와 노드 위치가 떨어져 있습니다.");
                var targets = new HashSet<RouteNode>();
                foreach (var edge in node.outgoing)
                {
                    if (edge == null || !edge.target || !nodes.Contains(edge.target)) { errors.Add(node.name + ": 잘못된 연결"); continue; }
                    if (!targets.Add(edge.target)) errors.Add(node.name + ": 같은 목적지 중복 연결");
                    if (float.IsNaN(edge.weight) || float.IsInfinity(edge.weight) || edge.weight < 0) errors.Add(node.name + ": 분기 가중치 오류");
                }
                if (node.marker && node.marker.kind == MarkerKind.Core && node.outgoing.Exists(Enabled))
                    errors.Add(node.name + ": 코어 노드는 종점이어야 합니다.");
            }
            var visiting = new HashSet<RouteNode>(); var done = new HashSet<RouteNode>();
            foreach (var node in nodes) if (Cycle(node, visiting, done)) { errors.Add("활성 경로에 순환이 있습니다."); break; }
            CheckGround(nodes, errors);
            return errors;
        }

        /// <summary>
        /// 적은 NavMesh 바닥 위를 걷고 도착은 노드의 높이·반경 허용치로 판정하므로, 노드가 바닥에서 떠 있으면 영원히 도착하지 못한다.
        /// 어느 노드 근처에도 NavMesh가 없으면(굽기 전, 임시 검증 그래프) 이 검사는 건너뛴다.
        /// </summary>
        static void CheckGround(HashSet<RouteNode> nodes, List<string> errors)
        {
            var found = new Dictionary<RouteNode, NavMeshHit>();
            var missing = new List<(RouteNode node, float search)>();
            foreach (var node in nodes)
            {
                float search = Mathf.Max(3f, node.heightTolerance * 4f);
                if (NavMesh.SamplePosition(node.transform.position, out NavMeshHit hit, search, NavMesh.AllAreas)) found[node] = hit;
                else missing.Add((node, search));
            }
            if (found.Count == 0) return;
            foreach (var (node, search) in missing) errors.Add($"{node.name}: {search:0.#}m 안에 NavMesh 바닥이 없습니다.");
            foreach (var pair in found)
            {
                RouteNode node = pair.Key; Vector3 position = node.transform.position, ground = pair.Value.position;
                float rise = position.y - ground.y;
                float planar = Vector2.Distance(new Vector2(position.x, position.z), new Vector2(ground.x, ground.z));
                if (Mathf.Abs(rise) > node.heightTolerance)
                    errors.Add($"{node.name}: NavMesh 바닥과 높이가 {rise:+0.00;-0.00}m 어긋나 적이 도착하지 못합니다(높이 허용 {node.heightTolerance:0.##}m). Y를 {ground.y:0.00}로 옮기거나 허용치를 올리세요.");
                else if (planar > node.arrivalRadius)
                    errors.Add($"{node.name}: 가장 가까운 NavMesh 바닥이 {planar:0.00}m 옆이라 적이 도착하지 못합니다(도착 반경 {node.arrivalRadius:0.##}m).");
            }
        }
        static bool Cycle(RouteNode node, HashSet<RouteNode> visiting, HashSet<RouteNode> done)
        {
            if (done.Contains(node)) return false;
            if (!visiting.Add(node)) return true;
            foreach (var edge in node.outgoing) if (Enabled(edge) && Cycle(edge.target, visiting, done)) return true;
            visiting.Remove(node); done.Add(node); return false;
        }
    }
}

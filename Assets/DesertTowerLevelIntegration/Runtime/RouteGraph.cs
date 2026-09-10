using System;
using System.Collections.Generic;
using DesertTower.Levels;
using UnityEngine;

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
            return errors;
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

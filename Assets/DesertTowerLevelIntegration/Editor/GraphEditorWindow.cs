using System.Collections.Generic;
using DesertTower.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace DesertTower.LevelIntegration.Editor
{
    public sealed class GraphEditorWindow : EditorWindow
    {
        RouteGraph graph; RouteNode from, to;
        Vector2 scroll; string report = "";
        [MenuItem("Tools/Desert Tower/Graph & Wave Integration")]
        public static void Open() => GetWindow<GraphEditorWindow>("Graph & Waves");
        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("노드 경로 · 프리팹 · 웨이브", EditorStyles.boldLabel);
            graph = (RouteGraph)EditorGUILayout.ObjectField("Graph", graph, typeof(RouteGraph), true);
            if (GUILayout.Button("선택한 LevelRoot에 통합 구성 추가")) CreateSetup();
            if (graph)
            {
                if (GUILayout.Button("선택 위치에 노드 추가")) AddNode();
                from = (RouteNode)EditorGUILayout.ObjectField("출발 노드", from, typeof(RouteNode), true);
                to = (RouteNode)EditorGUILayout.ObjectField("도착 노드", to, typeof(RouteNode), true);
                using (new EditorGUI.DisabledScope(!from || !to || from == to))
                {
                    if (GUILayout.Button("단방향 연결 추가"))
                    {
                        if (from.GetComponentInParent<RouteGraph>() != graph || to.GetComponentInParent<RouteGraph>() != graph)
                            report = "두 노드가 선택한 Graph에 속해야 합니다.";
                        else if (!from.outgoing.Exists(e => e.target == to))
                        { Undo.RecordObject(from, "Connect nodes"); from.outgoing.Add(new RouteLink { target = to }); EditorUtility.SetDirty(from); }
                    }
                    if (GUILayout.Button("단방향 연결 제거")) { Undo.RecordObject(from, "Disconnect nodes"); from.outgoing.RemoveAll(e => e.target == to); EditorUtility.SetDirty(from); }
                }
                if (GUILayout.Button("선택한 기존 LevelRoute를 노드로 복사")) ImportRoute();
                if (GUILayout.Button("피라미드 분기 샘플 생성 (빈 Graph만)")) CreateSample();
                if (GUILayout.Button("데이터 및 NavMesh 경로 검사")) Validate();
                var director = graph.GetComponent<WaveDirector>();
                if (director)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("상태", director.State + " / Wave " + (director.WaveIndex + 1));
                    EditorGUILayout.LabelField("살아 있는 적", director.AliveCount.ToString());
                    if (GUILayout.Button("웨이브 진행기 선택")) Selection.activeObject = director;
                    using (new EditorGUI.DisabledScope(!Application.isPlaying))
                    {
                        if (GUILayout.Button("웨이브 시작")) director.Begin();
                        if (GUILayout.Button("웨이브 중지 및 생성 적 정리")) director.StopRun();
                    }
                }
            }
            EditorGUILayout.HelpBox("노드는 씬/프리팹에 저장됩니다. 기존 LevelLayout 내보내기는 이 추가 컴포넌트를 포함하지 않습니다. 분기는 weight 비율로 선택하며 목표까지 이어지는 활성 길만 사용합니다.", MessageType.Info);
            if (!string.IsNullOrEmpty(report)) EditorGUILayout.HelpBox(report, MessageType.None);
            EditorGUILayout.EndScrollView();
        }
        void CreateSetup()
        {
            var level = Selection.activeGameObject ? Selection.activeGameObject.GetComponentInParent<LevelRoot>() : null;
            if (!level) { report = "LevelRoot 또는 자식을 먼저 선택하세요."; return; }
            var existing = level.GetComponentInChildren<RouteGraph>();
            if (existing) { graph = existing; return; }
            var go = new GameObject("GraphIntegration"); Undo.RegisterCreatedObjectUndo(go, "Create integration");
            go.transform.SetParent(level.transform, false);
            graph = go.AddComponent<RouteGraph>(); graph.level = level;
            var director = go.AddComponent<WaveDirector>(); director.graph = graph;
            var bootstrap = go.AddComponent<LevelBootstrap>(); bootstrap.level = level; bootstrap.director = director;
            Selection.activeGameObject = go; EditorSceneManager.MarkSceneDirty(go.scene);
        }
        RouteNode NewNode(string name, Vector3 position)
        {
            var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Create node");
            go.transform.SetParent(graph.transform, false); go.transform.position = position;
            var node = go.AddComponent<RouteNode>(); node.label = name; return node;
        }
        void AddNode()
        {
            Vector3 position = Selection.activeTransform ? Selection.activeTransform.position : graph.transform.position;
            var node = NewNode("Node " + graph.Nodes.Length, position);
            Selection.activeGameObject = node.gameObject;
        }
        void ImportRoute()
        {
            var route = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<LevelRoute>() : null;
            if (!route || !route.spawn || !route.core) { report = "양 끝이 있는 LevelRoute를 선택하세요."; return; }
            if (route.GetComponentInParent<LevelRoot>() != graph.level) { report = "다른 레벨의 경로입니다."; return; }
            RouteNode start = graph.Find(route.spawn) ?? NewNode(route.spawn.label, route.spawn.transform.position);
            start.marker = route.spawn; var previous = start;
            foreach (var point in route.waypoints)
            {
                var node = NewNode(route.label + " guide", route.transform.TransformPoint(point));
                previous.outgoing.Add(new RouteLink { target = node }); previous = node;
            }
            var goal = graph.Find(route.core) ?? NewNode(route.core.label, route.core.transform.position);
            goal.marker = route.core;
            if (!previous.outgoing.Exists(e => e.target == goal)) previous.outgoing.Add(new RouteLink { target = goal });
            EditorSceneManager.MarkSceneDirty(graph.gameObject.scene);
            report = "복사 완료. WaveSet의 spawnId/targetId를 지정하고 routeId를 비우면 그래프를 사용합니다. 원본 경로는 보존했습니다.";
        }
        void CreateSample()
        {
            if (graph.Nodes.Length != 0) { report = "기존 노드가 있어 샘플을 추가하지 않았습니다."; return; }
            // World +Z is north. Coordinates are initial blockout values, not mesh generation.
            string[] names = { "NE Spawn", "SE Spawn", "SW Spawn", "NW Spawn", "T1", "T2", "T3", "T4", "T5", "T6", "NE L3", "SE L3", "SW L3", "NW L3", "T7", "T8", "Core" };
            Vector3[] p = { new Vector3(40,0,40),new Vector3(40,0,-40),new Vector3(-40,0,-40),new Vector3(-40,0,40),new Vector3(32,3,32),new Vector3(32,3,-32),new Vector3(-32,3,-32),new Vector3(-32,3,32),new Vector3(22,7,0),new Vector3(-22,7,0),new Vector3(14,11,18),new Vector3(14,11,-18),new Vector3(-14,11,-18),new Vector3(-14,11,18),new Vector3(0,15,-24),new Vector3(0,15,24),new Vector3(0,19,0) };
            var nodes = new RouteNode[names.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                nodes[i] = NewNode(names[i], graph.transform.TransformPoint(p[i]));
                nodes[i].floor = i < 4 ? 0 : i < 8 ? 1 : i < 10 ? 2 : i < 14 ? 3 : i < 16 ? 4 : 5;
                if (i < 4 || i == 16)
                {
                    var marker = nodes[i].gameObject.AddComponent<LevelMarker>();
                    marker.kind = i < 4 ? MarkerKind.EnemySpawn : MarkerKind.Core;
                    marker.label = names[i]; nodes[i].marker = marker;
                }
            }
            int[,] links = { {0,4},{1,5},{2,6},{3,7},{4,8},{5,8},{6,9},{7,9},{8,10},{8,11},{9,12},{9,13},{10,15},{11,14},{12,14},{13,15},{14,16},{15,16} };
            for (int i = 0; i < links.GetLength(0); i++) nodes[links[i,0]].outgoing.Add(new RouteLink { target = nodes[links[i,1]] });
            EditorSceneManager.MarkSceneDirty(graph.gameObject.scene);
            report = "17개 노드/18개 연결 생성. 실제 맵 바닥에 맞게 옮기고 경사로 경유 노드를 추가하세요. 지형과 NavMesh는 생성하지 않습니다.";
        }
        void Validate()
        {
            var errors = graph.ValidateGraph();
            foreach (var node in graph.Nodes)
                foreach (var link in node.outgoing)
                    if (RouteGraph.Enabled(link))
                    {
                        var path = new NavMeshPath();
                        if (!NavMesh.CalculatePath(node.transform.position, link.target.transform.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                            errors.Add(node.label + " → " + link.target.label + ": 완전한 NavMesh 경로 없음");
                    }
            var director = graph.GetComponent<WaveDirector>();
            if (director) errors.AddRange(director.ValidateSetup());
            report = errors.Count == 0 ? "데이터/경로 검사 통과" : string.Join("\n", errors);
        }
    }

    [CustomEditor(typeof(RouteNode))]
    public sealed class RouteNodeEditor : UnityEditor.Editor
    {
        void OnSceneGUI()
        {
            var node = (RouteNode)target;
            EditorGUI.BeginChangeCheck();
            var position = Handles.PositionHandle(node.transform.position, Quaternion.identity);
            if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(node.transform, "Move node"); node.transform.position = position; }
        }
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        static void Draw(RouteNode node, GizmoType type)
        {
            Handles.color = Color.cyan;
            Handles.Label(node.transform.position + Vector3.up, node.label + " [L" + node.floor + "]");
            Handles.DrawWireDisc(node.transform.position, Vector3.up, node.arrivalRadius);
            foreach (var edge in node.outgoing)
            {
                if (edge == null || !edge.target) continue;
                Handles.color = RouteGraph.Enabled(edge) ? Color.yellow : Color.gray;
                var a = node.transform.position; var b = edge.target.transform.position;
                Handles.DrawLine(a, b);
                if ((b-a).sqrMagnitude > .001f)
                    Handles.ConeHandleCap(0, Vector3.Lerp(a,b,.65f), Quaternion.LookRotation(b-a), .45f, EventType.Repaint);
            }
        }
    }
}

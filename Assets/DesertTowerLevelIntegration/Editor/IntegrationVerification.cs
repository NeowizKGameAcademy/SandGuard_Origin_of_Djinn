using System;
using System.IO;
using DesertTower.Levels;
using UnityEditor;
using UnityEngine;

namespace DesertTower.LevelIntegration.Editor
{
    /// <summary>씬을 바꾸지 않는 임시 객체 기반 그래프 검증. CI에서는 -executeMethod로 실행.</summary>
    public static class IntegrationVerification
    {
        [MenuItem("Tools/Desert Tower/Verify graph algorithms")]
        public static void Run()
        {
            var root = new GameObject("Graph verification");
            int passed = 0;
            try
            {
                var level = root.AddComponent<LevelRoot>();
                var graph = root.AddComponent<RouteGraph>(); graph.level = level;
                var a = Node(root, "Start", Vector3.zero);
                var b = Node(root, "Left", Vector3.left);
                var c = Node(root, "Right", Vector3.right);
                var end = Node(root, "End", Vector3.forward);
                var dead = Node(root, "DeadEnd", Vector3.back);
                a.outgoing.Add(new RouteLink { target = b, weight = 1 });
                a.outgoing.Add(new RouteLink { target = c, weight = 3 });
                a.outgoing.Add(new RouteLink { target = dead, weight = 100 });
                b.outgoing.Add(new RouteLink { target = end }); c.outgoing.Add(new RouteLink { target = end });
                Check(graph.CanReach(a,end), "branch merge reaches goal", ref passed);
                Check(!graph.CanReach(dead,end), "dead end rejected", ref passed);
                Check(graph.Choose(a,end,0)==b, "weighted lower interval", ref passed);
                Check(graph.Choose(a,end,.24)==b, "weight boundary before", ref passed);
                Check(graph.Choose(a,end,.25)==c, "weight boundary after", ref passed);
                Check(graph.Choose(a,end,.999)==c, "dead branch excluded from weights", ref passed);
                a.outgoing[1].available=false;
                Check(graph.Choose(a,end,.99)==b, "disabled branch excluded", ref passed);
                a.outgoing[0].weight=0;
                Check(graph.Choose(a,end,.5)==null, "no usable branch returns null", ref passed);
                a.outgoing[0].weight=1; a.outgoing[1].available=true;
                Check(graph.ValidateGraph().Count==0, "valid acyclic diamond", ref passed);
                end.outgoing.Add(new RouteLink { target=a });
                Check(graph.ValidateGraph().Exists(e=>e.Contains("순환")), "cycle detected", ref passed);
                end.outgoing.Clear();
                c.id=b.id;
                Check(graph.ValidateGraph().Exists(e=>e.Contains("ID")), "duplicate IDs detected", ref passed);
                c.id=Guid.NewGuid().ToString("N");
                a.transform.position=new Vector3(0,10,0);
                Check(!a.Contains(new Vector3(0,0,0)), "same XZ on wrong floor is not arrival", ref passed);
                Check(a.Contains(new Vector3(.1f,10.1f,.1f)), "arrival in 3D tolerance", ref passed);
                var spawn=a.gameObject.AddComponent<LevelMarker>(); spawn.id="spawn"; spawn.kind=MarkerKind.EnemySpawn; a.marker=spawn;
                var core=end.gameObject.AddComponent<LevelMarker>(); core.id="core"; core.kind=MarkerKind.Core; end.marker=core;
                var legacy=root.AddComponent<LevelRoute>(); legacy.spawn=spawn; legacy.core=core; legacy.id="legacy";
                var group=new SpawnGroup { routeId="legacy" };
                Check(level.TryResolveSpawnGroup(group,out var binding,out _) && binding.SuggestedRoute==legacy, "legacy route remains compatible", ref passed);
                group.routeId=null; group.spawnId=spawn.id; group.targetId=core.id;
                Check(level.TryResolveSpawnGroup(group,out binding,out _) && graph.Find(binding.Spawn)==a, "graph spawn binding", ref passed);
                Debug.Log("LEVEL_INTEGRATION_CHECKS_PASSED="+passed);
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static RouteNode Node(GameObject parent,string name,Vector3 position)
        {
            var go=new GameObject(name); go.transform.SetParent(parent.transform); go.transform.position=position;
            return go.AddComponent<RouteNode>();
        }
        static void Check(bool condition,string message,ref int count)
        { if(!condition) throw new Exception(message); count++; }
    }
}

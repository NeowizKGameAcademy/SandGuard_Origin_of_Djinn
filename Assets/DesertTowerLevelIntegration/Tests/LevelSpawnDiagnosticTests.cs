#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DesertTower.LevelIntegration.Tests
{
    /// <summary>
    /// 웨이브 스폰이 "Enemy 부품/베이크된 NavMesh를 확인하세요"로 실패할 때 원인을 로그로 보여 준다(단정 없음, [Diagnostic]).
    /// NavMesh가 로드됐는지, 각 경로 노드(스폰 마커) 아래에 NavMesh가 있는지, 카탈로그 적 프리팹을 스폰 지점에 세우면 부품·에이전트 상태가 어떤지 찍는다.
    /// </summary>
    [Category("Diagnostic")]
    public sealed class LevelSpawnDiagnosticTests
    {
        const string ScenePath = "Assets/1.Scene/Level.unity";

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            var empty = SceneManager.CreateScene("Empty " + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene != empty && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        [UnityTest, Timeout(300000)] public IEnumerator ReportWhySpawnFails()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) Assert.Ignore("Level.unity는 -nographics에서 재생할 수 없다. -batchmode만으로 실행하세요.");
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            yield return EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return new WaitForSecondsRealtime(2f);
            var report = new StringBuilder("[SpawnDiag]\n");

            // 1. NavMesh 로드 상태
            var tri = NavMesh.CalculateTriangulation();
            if (tri.vertices.Length > 0)
            {
                var min = tri.vertices[0]; var max = tri.vertices[0];
                foreach (var v in tri.vertices) { min = Vector3.Min(min, v); max = Vector3.Max(max, v); }
                report.AppendLine($"navmesh vertices={tri.vertices.Length} tris={tri.indices.Length / 3} bounds min={min} max={max}");
            }
            else report.AppendLine("navmesh vertices=0 (NavMesh가 로드되지 않았다)");
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mb == null || mb.GetType().Name != "NavMeshSurface") continue;
                var data = mb.GetType().GetProperty("navMeshData")?.GetValue(mb) as Object;
                var agentType = mb.GetType().GetProperty("agentTypeID")?.GetValue(mb);
                report.AppendLine($"NavMeshSurface '{mb.name}' active={mb.isActiveAndEnabled} data={(data ? data.name : "(none)")} agentTypeID={agentType} pos={mb.transform.position}");
            }

            // 2. 경로 노드마다 NavMesh 거리
            var nodes = Object.FindObjectsByType<RouteNode>(FindObjectsSortMode.None).OrderBy(n => n.label).ToArray();
            report.AppendLine($"route nodes={nodes.Length}");
            RouteNode[] spawns = nodes.Where(n => n.label.Contains("Spawn") || n.name.Contains("Spawn")).ToArray();
            foreach (var n in nodes)
            {
                bool hit = NavMesh.SamplePosition(n.transform.position, out var nav, 5f, NavMesh.AllAreas);
                report.AppendLine($"  node '{n.label}' ({n.name}) pos={n.transform.position} navmesh={(hit ? $"{Vector3.Distance(n.transform.position, nav.position):0.00}m at {nav.position}" : "NONE within 5m")}");
            }

            // 3. 카탈로그 적 프리팹을 스폰 지점에 세워 본다
            var director = Object.FindFirstObjectByType<WaveDirector>();
            report.AppendLine($"director={(director ? director.State.ToString() : "(none)")} lastError={(director ? director.LastError : "-")} catalog={(director && director.catalog ? director.catalog.name : "(none)")} factory={(director && director.actorFactory ? "yes" : "no")}");
            if (director != null && director.catalog != null)
            {
                foreach (var entry in director.catalog.entries.Where(e => e != null && e.role == PrefabRole.Enemy && e.prefab != null).Take(3))
                {
                    var at = spawns.Length > 0 ? spawns[0].transform.position : Vector3.zero;
                    var go = Object.Instantiate(entry.prefab, at, Quaternion.identity);
                    yield return null;
                    var comps = string.Join(",", go.GetComponents<MonoBehaviour>().Where(c => c != null).Select(c => c.GetType().Name + (c.enabled ? "" : "(off)")));
                    var agent = go.GetComponent<NavMeshAgent>();
                    string agentInfo = agent == null ? "no NavMeshAgent"
                        : $"agent enabled={agent.enabled} onNavMesh={agent.isOnNavMesh} typeID={agent.agentTypeID} radius={agent.radius} height={agent.height} baseOffset={agent.baseOffset}";
                    var bridge = go.GetComponent<ActorBridge>();
                    string prep = "no ActorBridge";
                    if (bridge != null) { bool ok = bridge.Prepare(out var err); prep = ok ? "Prepare OK" : "Prepare FAIL: " + err; }
                    report.AppendLine($"  prefab '{entry.key}' ({entry.prefab.name}) at {at}: {agentInfo}; {prep}; components=[{comps}]");
                    Object.Destroy(go);
                }
                var settings = NavMesh.GetSettingsCount();
                for (int i = 0; i < settings; i++) { var s = NavMesh.GetSettingsByIndex(i); report.AppendLine($"  navmesh agent settings[{i}] typeID={s.agentTypeID} radius={s.agentRadius} height={s.agentHeight}"); }
            }
            Debug.Log(report.ToString());
            Assert.Pass("진단 로그를 확인하세요.");
        }
    }
}
#endif

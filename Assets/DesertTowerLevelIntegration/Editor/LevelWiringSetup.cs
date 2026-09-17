using System.Collections.Generic;
using System.Linq;
using System.Text;
using DesertTower.Levels;
using SandGuard.Waves;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace DesertTower.LevelIntegration.Editor
{
    /// <summary>Level 씬의 웨이브 실행 배선을 코드로 맞춘다. 손으로 잇던 항목을 재현 가능하게 만들고
    /// 끝에 ValidateSetup으로 확인한다. 코어는 레벨 계층 안에 두고, 마커를 코어 위치에 맞춘다.
    /// 배치모드: -executeMethod DesertTower.LevelIntegration.Editor.LevelWiringSetup.InspectBatch|ApplyBatch</summary>
    public static class LevelWiringSetup
    {
        const string ScenePath = "Assets/1.Scene/Level.unity";
        const string CorePrefabPath = "Assets/2.Model/Prefabs/Core.prefab";
        const string CatalogPath = "Assets/2.Model/Prefabs/Level/GamePrefabCatalog.asset";
        /// <summary>ValidateSetup이 코어 마커와 수신 컴포넌트 거리를 1 이내로 요구한다.</summary>
        const float MarkerMatchDistance = 1f;
        /// <summary>플랫폼에 들어가는 원 반지름에 곱하는 비율. 원이 플랫폼 가장자리에 닿지 않게 살짝 줄인다.</summary>
        const float PlatformFill = 0.9f;
        /// <summary>같은 플랫폼으로 묶는 높이 폭. 도착 높이 허용치도 이 폭을 덮어야 원 끝에서 판정이 끊기지 않는다.</summary>
        const float PlatformBand = 0.4f;

        [MenuItem("Tools/Desert Tower/Wire Level Scene — 검사만", false, 20)]
        public static void Inspect() { Run(false); }

        [MenuItem("Tools/Desert Tower/Wire Level Scene — 적용", false, 21)]
        public static void Apply() { Run(true); }

        public static void InspectBatch() { Exit(Run(false)); }
        public static void ApplyBatch() { Exit(Run(true)); }

        // 배치모드에서 실패가 조용히 성공으로 넘어가지 않도록 종료 코드로 낸다.
        static void Exit(bool ok) { if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1); }

        static bool Run(bool apply)
        {
            var log = new StringBuilder();
            log.AppendLine(apply ? "=== Level 씬 배선: 적용 ===" : "=== Level 씬 배선: 검사만 ===");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid()) return Fail(log, ScenePath + " 를 열지 못했습니다.");

            var root = Object.FindFirstObjectByType<LevelRoot>(FindObjectsInactive.Include);
            var director = Object.FindFirstObjectByType<WaveDirector>(FindObjectsInactive.Include);
            if (!root) return Fail(log, "LevelRoot가 씬에 없습니다.");
            if (!director) return Fail(log, "WaveDirector가 씬에 없습니다.");

            var marker = root.Markers.FirstOrDefault(m => m.kind == MarkerKind.Core);
            if (!marker) return Fail(log, "Core 마커가 없습니다.");

            var core = FindCoreInstance();
            if (!core) return Fail(log, CorePrefabPath + " 인스턴스를 씬에서 찾지 못했습니다.");
            log.AppendLine("코어: " + Path(core.transform) + " @ " + core.transform.position);
            log.AppendLine("마커: " + Path(marker.transform) + " @ " + marker.transform.position);
            log.AppendLine("어긋난 거리: " + Vector3.Distance(core.transform.position, marker.transform.position).ToString("F3"));

            // 1) 코어를 레벨 계층 안으로. 월드 위치는 유지한다.
            bool reparent = core.transform.parent != root.transform;
            log.AppendLine(Step(apply, reparent, "부모 이동: "
                + (core.transform.parent ? Path(core.transform.parent) : "<씬 최상위>") + " → " + Path(root.transform)));
            if (apply && reparent) Undo.SetTransformParent(core.transform, root.transform, "Move core into level");

            // 2) 마커(= 같은 오브젝트의 RouteNode 도착 지점)를 코어 위치로.
            bool move = Vector3.Distance(core.transform.position, marker.transform.position) > 0.001f;
            log.AppendLine(Step(apply, move, "마커 이동: " + marker.transform.position + " → " + core.transform.position));
            if (apply && move)
            {
                Undo.RecordObject(marker.transform, "Align core marker");
                marker.transform.position = core.transform.position;
            }

            // 3) 수신 컴포넌트.
            var receiver = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(c => c is ILevelCoreReceiver);
            bool attach = !receiver;
            log.AppendLine(Step(apply, attach, "CoreReceiver 부착 → " + Path(core.transform)));
            if (attach && apply) receiver = Undo.AddComponent<CoreReceiver>(core);
            else if (!attach) log.AppendLine("  (기존 " + receiver.GetType().Name + " @ " + Path(receiver.transform) + " 사용)");

            // 4) 감독 배선.
            log.AppendLine(Step(apply, director.coreReceiver != (Object)receiver,
                "coreReceiver: " + Describe(director.coreReceiver) + " → " + Describe(receiver)));
            log.AppendLine(Step(apply, !director.startAutomatically, "startAutomatically: " + director.startAutomatically + " → True"));
            if (apply)
            {
                // 프리팹 인스턴스 오버라이드로 남도록 SerializedObject를 거친다.
                var serialized = new SerializedObject(director);
                serialized.FindProperty("coreReceiver").objectReferenceValue = receiver;
                serialized.FindProperty("startAutomatically").boolValue = true;
                serialized.ApplyModifiedProperties();
            }

            // 5) 웨이브 편집기가 적 종류를 이름으로 고를 수 있게 목록 공급자를 물린다.
            var catalog = AssetDatabase.LoadAssetAtPath<PrefabCatalog>(CatalogPath);
            if (!catalog) return Fail(log, CatalogPath + " 를 찾지 못했습니다.");
            bool needsCatalog = root.elementCatalog != catalog;
            log.AppendLine(Step(apply, needsCatalog, "LevelRoot.elementCatalog: "
                + Describe(root.elementCatalog) + " → " + Describe(catalog)));
            if (apply && needsCatalog)
            {
                Undo.RecordObject(root, "Assign element catalog");
                root.elementCatalog = catalog;
                EditorUtility.SetDirty(root);
            }

            // 6) 적 풀을 씬에 둔다. 없으면 런타임에 이름 없는 오브젝트로 생겨 인스펙터에서 안 보인다.
            var factory = Object.FindFirstObjectByType<EnemyPoolActorFactory>(FindObjectsInactive.Include);
            if (!factory) return Fail(log, "EnemyPoolActorFactory가 씬에 없습니다.");
            var pool = Object.FindFirstObjectByType<EnemyPool>(FindObjectsInactive.Include);
            log.AppendLine(Step(apply, !pool, "Enemy Pool 오브젝트 생성 → " + Path(root.transform) + "/Enemy Pool"));
            if (apply && !pool)
            {
                var host = new GameObject("Enemy Pool");
                Undo.RegisterCreatedObjectUndo(host, "Create enemy pool");
                Undo.SetTransformParent(host.transform, root.transform, "Create enemy pool");
                pool = Undo.AddComponent<EnemyPool>(host);
            }
            if (pool)
            {
                log.AppendLine(Step(apply, factory.pool != pool, "EnemyPoolActorFactory.pool: "
                    + Describe(factory.pool) + " → " + Describe(pool)));
                // 7) 미리 만들어 둘 수는 웨이브 데이터에서 뽑는다. 한 웨이브가 한 종류를 가장 많이 쓰는 값.
                int demand = Mathf.Min(PeakPerPrefabDemand(root, catalog), Mathf.Max(1, factory.maxActive));
                log.AppendLine(Step(apply, factory.prewarmPerPrefab != demand,
                    "prewarmPerPrefab: " + factory.prewarmPerPrefab + " → " + demand + " (웨이브 최대 동시 수요)"));
                if (apply)
                {
                    var serializedFactory = new SerializedObject(factory);
                    serializedFactory.FindProperty("pool").objectReferenceValue = pool;
                    serializedFactory.FindProperty("prewarmPerPrefab").intValue = demand;
                    serializedFactory.ApplyModifiedProperties();
                }
            }

            // 8) 노드 도착 반경을 발밑 플랫폼 크기에 맞춘다. 적이 많아지면 플랫폼이 거의 꽉 차므로,
            //    한 점이 아니라 플랫폼 전체를 도착 구역으로 본다.
            SizeRouteNodes(director.graph, catalog, log, apply);

            log.AppendLine("--- ValidateSetup ---");
            List<string> errors = director.ValidateSetup();
            if (errors.Count == 0) log.AppendLine("오류 없음.");
            else foreach (var e in errors) log.AppendLine("  · " + e);

            if (apply && errors.Count > 0)
                return Fail(log, "검증에 실패해 저장하지 않았습니다.");
            if (apply)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                log.AppendLine("씬 저장 완료: " + ScenePath);
            }
            Debug.Log(log.ToString());
            // 검사 모드는 아직 배선 전이라 오류가 남아 있는 게 정상이다.
            return true;
        }

        /// <summary>
        /// 노드의 도착 반경을 발밑 플랫폼 크기에 맞춘다. 노드 주변을 격자로 훑어 노드와 같은 높이로 이어진
        /// 걸을 수 있는 칸을 모으고(플랫폼), 노드에서 그 칸들까지 거리의 90% 지점에 PlatformFill을 곱한다.
        /// 노드가 플랫폼 가장자리나 NavMesh 경계에 찍혀 있어도 플랫폼 전체를 기준으로 잡힌다.
        /// 몸통이 가장 큰 적이 노드 근처까지는 들어올 수 있도록 하한을 두고, 같은 높이대의 다른 노드와는 겹치지 않게 묶는다.
        /// </summary>
        static void SizeRouteNodes(RouteGraph graph, PrefabCatalog catalog, StringBuilder log, bool apply)
        {
            if (!graph) { log.AppendLine("  [건너뜀] RouteGraph 없음"); return; }
            int agentType = 0; float widest = 0f;
            foreach (var e in catalog.entries)
            {
                var agent = e != null && e.role == PrefabRole.Enemy && e.prefab ? e.prefab.GetComponent<NavMeshAgent>() : null;
                if (!agent) continue;
                agentType = agent.agentTypeID;
                var scale = agent.transform.lossyScale;
                widest = Mathf.Max(widest, agent.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
            }
            var filter = new NavMeshQueryFilter { agentTypeID = agentType, areaMask = NavMesh.AllAreas };
            var nodes = graph.Nodes;
            int measured = 0;
            log.AppendLine("--- 노드 도착 반경 (플랫폼 기준, 가장 큰 적 반경 " + widest.ToString("0.00") + ") ---");
            foreach (var node in nodes.OrderBy(n => n.floor).ThenBy(n => n.label))
            {
                Vector3 p = node.transform.position;
                if (!NavMesh.SamplePosition(p, out var hit, 3f, filter)) { log.AppendLine("  [건너뜀] " + node.label + ": 발밑 3m 안에 NavMesh 없음"); continue; }
                float extent = PlatformExtent(p, hit.position, filter, out int cells);
                if (cells == 0) { log.AppendLine("  [건너뜀] " + node.label + ": 플랫폼 칸을 찾지 못함"); continue; }
                measured++;
                float offset = new Vector2(p.x - hit.position.x, p.z - hit.position.z).magnitude;
                float spacing = float.MaxValue;
                foreach (var other in nodes)
                {
                    if (other == node) continue;
                    Vector3 d = other.transform.position - p;
                    if (Mathf.Abs(d.y) < 3f) spacing = Mathf.Min(spacing, new Vector2(d.x, d.z).magnitude);
                }
                float target = extent * PlatformFill;
                target = Mathf.Max(target, offset + widest * 2f);
                if (spacing < float.MaxValue) target = Mathf.Min(target, spacing * 0.45f);
                target = Mathf.Round(target * 20f) / 20f;
                float dy = p.y - hit.position.y;
                // 플랫폼 칸은 발밑 점 기준 ±PlatformBand이고 노드는 거기서 dy만큼 떠 있다. 원 어디서든 판정되도록 덮는다. 줄이지는 않는다.
                float tolerance = Mathf.Max(node.heightTolerance, Mathf.Ceil((PlatformBand + Mathf.Abs(dy) + 0.15f) * 20f) / 20f);
                bool needs = Mathf.Abs(node.arrivalRadius - target) > 0.01f || Mathf.Abs(node.heightTolerance - tolerance) > 0.01f;
                log.AppendLine(Step(apply, needs, node.label + " (층 " + node.floor + "): 반경 " + node.arrivalRadius.ToString("0.##")
                    + " → " + target.ToString("0.##") + ", 높이 허용 " + node.heightTolerance.ToString("0.##") + " → " + tolerance.ToString("0.##")
                    + "  [플랫폼 " + cells + "칸, 90% 거리 " + extent.ToString("0.00")
                    + ", NavMesh 이탈 " + offset.ToString("0.00") + ", 높이차 " + dy.ToString("0.00") + "]"));
                if (apply && needs)
                {
                    var serialized = new SerializedObject(node);
                    serialized.FindProperty("arrivalRadius").floatValue = target;
                    serialized.FindProperty("heightTolerance").floatValue = tolerance;
                    serialized.ApplyModifiedProperties();
                }
            }
            if (measured == 0) log.AppendLine("  경고: 어떤 노드에서도 플랫폼을 재지 못했습니다. 씬의 NavMesh 데이터가 로드됐는지 확인하세요.");
        }

        /// <summary>
        /// surface(노드 발밑 NavMesh 점)와 같은 높이로 이어진 걸을 수 있는 격자 칸을 모아, 노드에서의 거리 90% 지점을 돌려준다.
        /// 경사로를 따라 높이가 변하면 거기서 끊기므로 층의 플랫폼만 잡힌다.
        /// </summary>
        static float PlatformExtent(Vector3 node, Vector3 surface, NavMeshQueryFilter filter, out int cells)
        {
            const float half = 7f, step = 0.5f, band = PlatformBand;
            int n = Mathf.RoundToInt(half * 2f / step) + 1;
            var walkable = new bool[n, n];
            float ox = node.x - half, oz = node.z - half;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    var g = new Vector3(ox + i * step, surface.y, oz + j * step);
                    if (!NavMesh.SamplePosition(g, out var h, 0.5f, filter)) continue;
                    if (Mathf.Abs(h.position.y - surface.y) > band) continue;
                    if (new Vector2(h.position.x - g.x, h.position.z - g.z).sqrMagnitude > 0.3f * 0.3f) continue;
                    walkable[i, j] = true;
                }
            int si = Mathf.Clamp(Mathf.RoundToInt((surface.x - ox) / step), 0, n - 1);
            int sj = Mathf.Clamp(Mathf.RoundToInt((surface.z - oz) / step), 0, n - 1);
            if (!walkable[si, sj])
            {
                bool found = false;
                for (int r = 1; r <= 2 && !found; r++)
                    for (int di = -r; di <= r && !found; di++)
                        for (int dj = -r; dj <= r && !found; dj++)
                        {
                            int ii = si + di, jj = sj + dj;
                            if (ii < 0 || jj < 0 || ii >= n || jj >= n || !walkable[ii, jj]) continue;
                            si = ii; sj = jj; found = true;
                        }
                if (!found) { cells = 0; return 0f; }
            }
            var seen = new bool[n, n];
            var queue = new Queue<Vector2Int>();
            var distances = new List<float>();
            queue.Enqueue(new Vector2Int(si, sj)); seen[si, sj] = true;
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                float x = ox + c.x * step - node.x, z = oz + c.y * step - node.z;
                distances.Add(Mathf.Sqrt(x * x + z * z));
                foreach (var d in new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) })
                {
                    int ii = c.x + d.x, jj = c.y + d.y;
                    if (ii < 0 || jj < 0 || ii >= n || jj >= n || seen[ii, jj] || !walkable[ii, jj]) continue;
                    seen[ii, jj] = true; queue.Enqueue(new Vector2Int(ii, jj));
                }
            }
            cells = distances.Count;
            distances.Sort();
            return distances[Mathf.Clamp(Mathf.FloorToInt(0.9f * (distances.Count - 1)), 0, distances.Count - 1)];
        }

        /// <summary>한 웨이브 안에서 같은 프리팹을 가장 많이 쓰는 수. 이만큼 미리 만들어 두면 전투 중 생성이 없다.</summary>
        static int PeakPerPrefabDemand(LevelRoot root, PrefabCatalog catalog)
        {
            if (!root.waves || !catalog) return 0;
            int peak = 0;
            var perPrefab = new Dictionary<GameObject, int>();
            foreach (var wave in root.waves.waves)
            {
                if (wave == null) continue;
                perPrefab.Clear();
                foreach (var group in wave.groups)
                {
                    if (group == null) continue;
                    var entry = catalog.Find(group.ResolvedKey);
                    if (entry == null || !entry.prefab) continue;
                    perPrefab.TryGetValue(entry.prefab, out int already);
                    perPrefab[entry.prefab] = already + Mathf.Max(0, group.count);
                }
                foreach (var count in perPrefab.Values) peak = Mathf.Max(peak, count);
            }
            return peak;
        }

        /// <summary>Core.prefab 인스턴스의 루트를 찾는다. 이름 규칙이 아니라 프리팹 출처로 판별한다.</summary>
        /// <summary>코어로 인정하는 프리팹. 2026-09-17 Level의 코어가 New Core.prefab으로 바뀌어 둘 다 받는다.</summary>
        static readonly string[] CorePrefabPaths = { CorePrefabPath, "Assets/2.Model/Prefabs/New Core.prefab" };

        static GameObject FindCoreInstance()
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(t.gameObject);
                if (!instanceRoot || instanceRoot != t.gameObject) continue;
                if (System.Array.IndexOf(CorePrefabPaths, PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instanceRoot)) >= 0) return instanceRoot;
            }
            // 프리팹 인스턴스가 아니어도 코어 수신 컴포넌트가 붙어 있으면 그것을 코어로 본다
            var receiver = Object.FindFirstObjectByType<CoreReceiver>(FindObjectsInactive.Include);
            return receiver ? receiver.gameObject : null;
        }

        static string Step(bool apply, bool needed, string what)
        {
            if (!needed) return "  [변경 없음] " + what;
            return (apply ? "  [적용] " : "  [예정] ") + what;
        }

        static bool Fail(StringBuilder log, string reason)
        {
            log.AppendLine("실패: " + reason);
            Debug.LogError(log.ToString());
            return false;
        }

        static string Describe(Object o) { return o ? o.GetType().Name + " (" + o.name + ")" : "<없음>"; }

        static string Path(Transform t)
        {
            var parts = new List<string>();
            for (var c = t; c; c = c.parent) parts.Insert(0, c.name);
            return string.Join("/", parts);
        }
    }
}

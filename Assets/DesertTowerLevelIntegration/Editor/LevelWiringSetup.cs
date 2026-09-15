using System.Collections.Generic;
using System.Linq;
using System.Text;
using DesertTower.Levels;
using SandGuard.Waves;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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
        static GameObject FindCoreInstance()
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(t.gameObject);
                if (!instanceRoot || instanceRoot != t.gameObject) continue;
                if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instanceRoot) == CorePrefabPath) return instanceRoot;
            }
            return null;
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

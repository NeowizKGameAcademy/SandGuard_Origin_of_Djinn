using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DesertTower.LevelIntegration.Editor
{
    /// <summary>적 기반 프리팹에 경로 러너 연결(ActorBridge)을 둔다. 변형 5종이 이를 상속하고
    /// 코어 피해량은 변형별로 조정한다. 기반에 생기면 변형에 따로 붙어 있던 중복은 걷어낸다.
    /// 배치모드: -executeMethod DesertTower.LevelIntegration.Editor.EnemyBridgeSetup.ApplyBatch</summary>
    public static class EnemyBridgeSetup
    {
        const string BasePrefab = "Assets/Enemy/Generated/Enemy.prefab";

        [MenuItem("Tools/Desert Tower/Enemy Bridge — 검사만", false, 30)]
        public static void Inspect() { Run(false); }

        [MenuItem("Tools/Desert Tower/Enemy Bridge — 적용", false, 31)]
        public static void Apply() { Run(true); }

        public static void InspectBatch() { Exit(Run(false)); }
        public static void ApplyBatch() { Exit(Run(true)); }

        static void Exit(bool ok) { if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1); }

        static bool Run(bool apply)
        {
            var log = new StringBuilder();
            log.AppendLine(apply ? "=== 적 브리지: 적용 ===" : "=== 적 브리지: 검사만 ===");

            // 1) 기반 프리팹에 브리지를 둔다.
            var root = PrefabUtility.LoadPrefabContents(BasePrefab);
            if (!root) { log.AppendLine("실패: " + BasePrefab + " 를 열지 못했습니다."); Debug.LogError(log); return false; }
            bool baseHas = root.GetComponent<ActorBridge>();
            log.AppendLine(Step(apply, !baseHas, BasePrefab + " 에 SandGuardActorBridge 부착"));
            if (apply && !baseHas)
            {
                root.AddComponent<SandGuardActorBridge>();
                PrefabUtility.SaveAsPrefabAsset(root, BasePrefab);
            }
            PrefabUtility.UnloadPrefabContents(root);
            // 변형을 읽기 전에 기반 변경을 반영시킨다. 그래야 중복 여부가 제대로 보인다.
            if (apply && !baseHas) { AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); }

            // 2) 기반을 쓰는 프리팹에서 중복 브리지를 걷어낸다.
            foreach (var path in Variants())
            {
                var go = PrefabUtility.LoadPrefabContents(path);
                if (!go) continue;
                var bridges = go.GetComponents<ActorBridge>();
                if (bridges.Length > 1)
                {
                    // 기반이 주는 것만 남기고, 변형에 따로 붙어 있던 것을 지운다.
                    var extra = bridges.FirstOrDefault(PrefabUtility.IsAddedComponentOverride) ?? bridges[bridges.Length - 1];
                    log.AppendLine(Step(apply, true, path + " 중복 브리지 " + (bridges.Length - 1) + "개 제거"));
                    if (apply) { Object.DestroyImmediate(extra, true); PrefabUtility.SaveAsPrefabAsset(go, path); }
                }
                else log.AppendLine("  [변경 없음] " + path + " 브리지 " + bridges.Length + "개"
                    + (bridges.Length == 1 ? " (코어 피해 " + bridges[0].coreDamage + ")" : ""));
                PrefabUtility.UnloadPrefabContents(go);
            }

            if (apply) AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
            return true;
        }

        /// <summary>기반 프리팹을 원본으로 삼는 프리팹들.</summary>
        static IEnumerable<string> Variants()
        {
            var baseAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefab);
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path == BasePrefab) continue;
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!asset) continue;
                var source = PrefabUtility.GetCorrespondingObjectFromSource(asset);
                if (source == baseAsset) yield return path;
            }
        }

        static string Step(bool apply, bool needed, string what)
        {
            return needed ? (apply ? "  [적용] " : "  [예정] ") + what : "  [변경 없음] " + what;
        }
    }
}

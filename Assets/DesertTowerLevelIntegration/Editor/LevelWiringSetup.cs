using System.Collections.Generic;
using System.Linq;
using System.Text;
using DesertTower.Levels;
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

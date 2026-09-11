using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SandGuard.Enemy.Editor
{
    /// <summary>
    /// 낙사 테스트 씬. 실제 레벨(Assets/1.Scene/Level.unity, DesertTemple + 구운 NavMesh)을 복사해 낙사 HUD(<see cref="EnemyFallDemoOverlay"/>)만 얹는다.
    /// 레벨을 그대로 쓰므로 NavMesh 가장자리·절벽 높이가 실제 플레이와 같다. 레벨이 바뀌면 다시 만든다.
    /// </summary>
    public static class EnemyFallSceneBuilder
    {
        public const string SourceScene = "Assets/1.Scene/Level.unity";
        public const string ScenePath = "Assets/Enemy/Generated/EnemyFallTest.unity";
        const string EnemyPrefab = "Assets/Enemy/Generated/Enemy.prefab";

        [MenuItem("SandGuard/Enemy/Create Fall Test Scene (Desert Temple)")]
        public static void CreateScene()
        {
            if (!File.Exists(SourceScene)) throw new FileNotFoundException("Level scene not found", SourceScene);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefab) == null) EnemySetupBuilder.CreateMissingAssets();
            EnemySetupBuilder.EnsureFall(EnemyPrefab);
            if (SceneManager.GetSceneByPath(ScenePath).isLoaded) throw new InvalidOperationException("Close the generated fall-test scene before rebuilding it.");
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            File.Copy(SourceScene, ScenePath, true); // 저장된 레벨을 복사한다. 열려 있는 씬의 저장 안 된 변경은 들어가지 않는다
            AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);
            Scene previous = SceneManager.GetActiveScene();
            // 저장되지 않은 빈 씬(배치 모드 등)에서는 추가 씬을 열 수 없으므로 단일 씬으로 연다.
            bool additive = previous.IsValid() && !string.IsNullOrEmpty(previous.path);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, additive ? OpenSceneMode.Additive : OpenSceneMode.Single);
            try
            {
                SceneManager.SetActiveScene(scene);
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var brain in root.GetComponentsInChildren<EnemyBrain>(true)) brain.gameObject.SetActive(false);
                Camera camera = null;
                foreach (var root in scene.GetRootGameObjects()) { camera = root.GetComponentInChildren<Camera>(true); if (camera != null) break; }
                if (camera == null)
                {
                    camera = new GameObject("Fall Test Camera").AddComponent<Camera>();
                    camera.transform.position = new Vector3(0f, 30f, -20f); camera.transform.rotation = Quaternion.Euler(50f, 0f, 0f);
                }
                camera.tag = "MainCamera";
                camera.gameObject.SetActive(true); camera.enabled = true;
                if (camera.GetComponent<AudioListener>() == null) camera.gameObject.AddComponent<AudioListener>();
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = listener.gameObject == camera.gameObject;
                var overlay = new GameObject("Fall Test HUD").AddComponent<EnemyFallDemoOverlay>();
                overlay.enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefab);
                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log("ENEMY_FALL_SCENE_COMPLETE: " + ScenePath + "\n" + Summary(overlay.enemyPrefab));
            }
            finally
            {
                if (additive) { EditorSceneManager.CloseScene(scene, true); if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>열린 씬의 NavMesh 가장자리를 종류별로 세어 로그로 남긴다. 테스트가 쓸 자리가 있는지 미리 본다.</summary>
        static string Summary(GameObject prefab)
        {
            var fall = prefab != null ? prefab.GetComponent<EnemyFall>() : null;
            float fatal = fall != null ? fall.fatalDropHeight : 5f, minDrop = fall != null ? fall.ledgeDrop : 0.8f;
            var ledges = NavMeshLedges.Find(minDrop);
            int fatalCount = 0, safeOn = 0, safeOff = 0, voidCount = 0;
            var samples = new StringBuilder();
            foreach (var ledge in ledges)
            {
                string kind;
                if (ledge.IsVoid) { voidCount++; kind = "void"; }
                else if (ledge.Drop >= fatal) { fatalCount++; kind = "fatal"; }
                else if (ledge.LandsOnNavMesh) { safeOn++; kind = "safe-on-navmesh"; }
                else { safeOff++; kind = "safe-off-navmesh"; }
                if (samples.Length < 4000)
                    samples.Append("  ").Append(kind).Append(" edge ").Append(ledge.Edge.ToString("F1")).Append(" drop ")
                        .Append(ledge.IsVoid ? "inf" : ledge.Drop.ToString("F2")).Append('\n');
            }
            return "NavMesh ledges (minDrop " + minDrop + ", fatal " + fatal + "): total " + ledges.Count + " | fatal " + fatalCount +
                " | safe→navmesh " + safeOn + " | safe→off " + safeOff + " | void " + voidCount + "\n" + samples;
        }
    }
}

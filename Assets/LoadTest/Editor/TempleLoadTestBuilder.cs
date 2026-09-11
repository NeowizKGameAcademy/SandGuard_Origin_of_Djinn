using System;
using System.IO;
using SandGuard.Enemy;
using SandGuard.Waves;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SandGuard.LoadTest.Editor
{
    public static class TempleLoadTestBuilder
    {
        const string ScenePath = "Assets/LoadTest/Generated/DesertTemple_LoadTest.unity";

        [MenuItem("SandGuard/Load Test/Create Desert Temple Test Scene")]
        public static void CreateScene()
        {
            Directory.CreateDirectory("Logs");
            Directory.CreateDirectory("Assets/LoadTest/Generated");
            AssetDatabase.Refresh();
            if (SceneManager.GetSceneByPath(ScenePath).isLoaded) throw new InvalidOperationException("Close the generated load-test scene before rebuilding it.");
            // Copy the saved source, not the user's open scene or unsaved changes.
            File.Copy("Assets/1.Scene/Level.unity", ScenePath, true);
            AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var wave in root.GetComponentsInChildren<WaveDirector>(true)) wave.enabled = false;
                    foreach (var enemy in root.GetComponentsInChildren<EnemyBrain>(true)) enemy.gameObject.SetActive(false);
                }
                var runner = new GameObject("Desert Temple Load Test").AddComponent<TempleLoadTest>();
                string[] names = { "Swordsman", "Assassin", "ShieldGuard", "HammerBrute", "Chief" };
                runner.enemyPrefabs = new GameObject[names.Length];
                for (int i = 0; i < names.Length; i++)
                {
                    runner.enemyPrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Generated/Enemy_" + names[i] + ".prefab");
                    if (runner.enemyPrefabs[i] == null) throw new InvalidOperationException("Missing enemy " + names[i]);
                }
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally { EditorSceneManager.CloseScene(scene, true); if (previous.IsValid()) SceneManager.SetActiveScene(previous); }
            File.WriteAllText("Logs/TempleLoadTest-scene-ready.txt", ScenePath);
        }

        [MenuItem("SandGuard/Load Test/Build Windows Benchmark")]
        public static void Build()
        {
            CreateScene();
            Directory.CreateDirectory("Builds/TempleLoadTest");
            bool timing = PlayerSettings.enableFrameTimingStats;
            bool autoGraphics = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64);
            var graphics = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64);
            try
            {
                PlayerSettings.enableFrameTimingStats = true;
                // Keep this benchmark off the DX12 device-loss path observed during count changes.
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { ScenePath }, locationPathName = "Builds/TempleLoadTest/TempleLoadTest.exe",
                    target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Benchmark build failed: " + report.summary.result);
                File.WriteAllText("Logs/TempleLoadTest-build-ready.txt", Path.GetFullPath("Builds/TempleLoadTest/TempleLoadTest.exe"));
            }
            finally
            {
                PlayerSettings.enableFrameTimingStats = timing;
                PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, graphics);
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, autoGraphics);
                AssetDatabase.SaveAssets();
            }
        }
    }
}

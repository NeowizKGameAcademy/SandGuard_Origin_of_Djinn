#if UNITY_EDITOR
using System;
using System.Collections;
using DesertTower.Levels;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DesertTower.LevelIntegration.Tests
{
    /// <summary>Level 씬이 실제로 웨이브를 돌리는지 확인한다. ValidateSetup은 정적 검증까지만 보므로
    /// 준비→전투 전환, 실제 스폰, HUD·건설이 읽는 상태 계약을 플레이로 확인한다.</summary>
    public sealed class LevelWavePlayModeTests
    {
        const string ScenePath = "Assets/1.Scene/Level.unity";

        /// <summary>timeScale을 올려 두므로 대기 한도는 실제 시간으로 잰다.</summary>
        static IEnumerator UntilRealtime(Func<bool> condition, float seconds, string message)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.True(condition(), message);
        }

        static IEnumerator LoadLevel(float timeScale)
        {
            Assert.True(System.IO.File.Exists(ScenePath), ScenePath + " 가 없습니다.");
            Time.timeScale = timeScale;
            yield return EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
        }

        static WaveDirector Director()
        {
            var director = UnityEngine.Object.FindFirstObjectByType<WaveDirector>();
            Assert.NotNull(director, "씬에 WaveDirector가 있어야 한다.");
            return director;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            var empty = SceneManager.CreateScene("Empty " + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene != empty && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        [UnityTest] public IEnumerator SceneEntersPreparationAndReportsWaveState()
        {
            yield return LoadLevel(1f);
            var director = Director();
            var root = UnityEngine.Object.FindFirstObjectByType<LevelRoot>();
            Assert.NotNull(root, "씬에 LevelRoot가 있어야 한다.");
            Assert.NotNull(root.waves, "LevelRoot에 WaveSet이 붙어 있어야 한다.");

            // startAutomatically가 켜져 있으므로 Start에서 첫 준비 단계로 들어간다.
            yield return UntilRealtime(() => director.State == RunState.Preparing, 5f,
                "준비 단계로 들어가지 않았다. State=" + director.State + " / " + director.LastError);

            IWaveStateReader wave = director;
            IGameStateReader game = director;
            Assert.AreEqual(GamePhase.Preparation, game.Phase, "준비 중에는 Preparation이어야 한다.");
            Assert.False(game.IsPaused, "timeScale이 1이면 일시정지가 아니다.");
            Assert.AreEqual(root.waves.waves.Count, wave.TotalWaves, "전체 웨이브 수가 WaveSet과 달라졌다.");
            Assert.AreEqual(1, wave.WaveNumber, "첫 준비 단계부터 1로 보여야 한다.");
            Assert.Greater(wave.PendingEnemyCount, 0, "준비 중에는 이번 웨이브의 등장 예정 수가 잡혀야 한다.");
            Assert.AreEqual(0, wave.AliveEnemyCount, "준비 중에는 살아 있는 적이 없다.");
            Assert.NotNull(wave.PreparationSecondsRemaining);
            Assert.Greater(wave.PreparationSecondsRemaining.Value, 0f, "준비 시간이 남아 있어야 한다.");
            Assert.IsNotEmpty(wave.NextRouteIds, "다음 진입로 ID가 비어 있다.");
        }

        [UnityTest] public IEnumerator PreparationEndsAndEnemiesSpawn()
        {
            yield return LoadLevel(10f);
            var director = Director();
            int changed = 0;
            ((IWaveStateReader)director).Changed += () => changed++;

            yield return UntilRealtime(() => director.State == RunState.Running, 30f,
                "전투 단계로 넘어가지 않았다. State=" + director.State + " / " + director.LastError);
            Assert.AreEqual(GamePhase.Combat, ((IGameStateReader)director).Phase);

            yield return UntilRealtime(() => director.AliveEnemyCount > 0, 30f,
                "적이 하나도 스폰되지 않았다. " + director.LastError);
            Assert.Greater(director.TotalSpawned, 0);
            Assert.AreNotEqual(RunState.Error, director.State, "실행 중 오류: " + director.LastError);
            Assert.Null(director.LastError);
            Assert.Greater(changed, 0, "상태가 바뀌었는데 Changed가 한 번도 오지 않았다.");
        }

        [UnityTest] public IEnumerator EnemiesClimbToCoreAndAreAbsorbed()
        {
            yield return LoadLevel(8f);
            var director = Director();
            yield return UntilRealtime(() => director.State == RunState.Running, 30f,
                "전투 단계로 넘어가지 않았다. " + director.LastError);

            // 0층 스폰에서 5층 코어까지 실제로 올라가는지 본다. 경로가 끊겨 있으면 여기서 걸린다.
            yield return UntilRealtime(() => director.Absorbed > 0 || director.State == RunState.Error, 180f,
                "적이 코어에 도달하지 못했다. Alive=" + director.AliveEnemyCount
                + " Spawned=" + director.TotalSpawned + " Killed=" + director.Killed + " / " + director.LastError);
            Assert.AreNotEqual(RunState.Error, director.State, "실행 중 오류: " + director.LastError);
            Assert.Greater(director.Absorbed, 0, "코어에 흡수된 적이 없다.");
        }
    }
}
#endif

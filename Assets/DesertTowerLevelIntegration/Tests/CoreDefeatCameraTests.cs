#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using DesertTower.VFX;
using NUnit.Framework;
using SandGuard.GameFlow;
using SandGuard.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DesertTower.LevelIntegration.Tests
{
    public sealed class CoreDefeatTestActor : ActorBridge
    {
        public override bool Alive => true;
        public override bool Prepare(out string error) { error = null; return true; }
        public override void Travel(Vector3 point) { }
        public override void Halt() { }
        public override void Remove() { }
    }

    public sealed class CoreDefeatCameraTests
    {
        static IEnumerator Until(Func<bool> condition, float seconds, string message)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.True(condition(), message);
        }

        static IEnumerator LoadLevel()
        {
            GameManager.Instance.ClearPauseRequests();
            Time.timeScale = 1f;
            yield return EditorSceneManager.LoadSceneInPlayMode("Assets/1.Scene/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var opening = Object.FindFirstObjectByType<LevelOpeningCinematic>();
            opening.Skip();
            yield return Until(() => !opening.IsPlaying, 4f, "Opening did not finish.");
        }

        [Test] public void OnlyTheBossOverridesCoreDamageToOneThousand()
        {
            var chief = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Generated/Enemy_Chief.prefab");
            var ordinary = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Generated/Enemy.prefab");
            Assert.AreEqual(1000f, chief.GetComponent<ActorBridge>().coreDamage);
            Assert.AreEqual(10f, ordinary.GetComponent<ActorBridge>().coreDamage);
        }

        [UnityTest] public IEnumerator FatalHitCutsToCoreBeforeBurstAndWaitsForDebrisBeforeResult()
        {
            yield return LoadLevel();
            var core = Object.FindFirstObjectByType<CoreReceiver>();
            var result = Object.FindFirstObjectByType<GameResultScreen>();
            var rig = Object.FindFirstObjectByType<PlayerCameraRig>();
            var camera = rig.GetComponent<Camera>();
            var director = Object.FindFirstObjectByType<WaveDirector>();
            var enemy = new GameObject("Core damage test enemy").AddComponent<CoreDefeatTestActor>();
            enemy.transform.position = core.transform.position;
            Assert.True(core.TryAbsorb(enemy, 10f));
            yield return null;
            Assert.AreEqual(90f, core.Current);
            Assert.IsNull(result.GetComponent<CoreDefeatCamera>(), "A non-fatal hit must leave gameplay camera alone.");

            // The player is on a lower floor, looking away when the core is destroyed.
            rig.owner.position = new Vector3(25f, 2f, -55f);
            rig.SetYaw(140f);
            yield return null; yield return null;
            Vector3 distantView = camera.transform.position;
            float bossDamage = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Generated/Enemy_Chief.prefab").GetComponent<ActorBridge>().coreDamage;
            Assert.True(core.TryAbsorb(enemy, bossDamage));
            yield return Until(() => result.GetComponent<CoreDefeatCamera>()?.IsActive ?? false, 1f, "The fatal hit did not focus the core.");
            var shot = result.GetComponent<CoreDefeatCamera>();
            var effect = Object.FindFirstObjectByType<VfxCoreDestruction>();
            Assert.AreEqual(RunState.Lost, director.State);
            Assert.Zero(core.Current);
            Assert.NotNull(effect);
            Assert.Less(effect.Age, effect.BurstTime, "The camera must arrive before the explosion, not fly in afterward.");
            Assert.Greater(Vector3.Distance(distantView, camera.transform.position), 30f);
            Assert.False(rig.input.GameplayEnabled);
            Assert.AreEqual(1f, Time.timeScale, "The destruction animation must continue to play.");
            Assert.False(result.IsShowing);
            var point = camera.WorldToViewportPoint(shot.FocusPoint);
            Assert.That(point.x, Is.EqualTo(.5f).Within(.001f));
            Assert.That(point.y, Is.EqualTo(.5f).Within(.001f));
            Assert.Greater(point.z, 0f);
            Object.FindFirstObjectByType<PauseMenuController>().Open();
            Assert.False(GameManager.Instance.IsPaused, "A pause popup must not cover the defeat shot.");

            Directory.CreateDirectory("Logs/CoreDefeatCaptures");
            LevelOpeningCinematicTests.Capture(camera, "Logs/CoreDefeatCaptures/01-overload.png");
            yield return new WaitForSeconds(1.1f);
            Assert.False(result.IsShowing, "The old one-second overlay delay must not obscure the debris.");
            Assert.Greater(effect.Age, effect.BurstTime);
            LevelOpeningCinematicTests.Capture(camera, "Logs/CoreDefeatCaptures/02-debris.png");
            yield return Until(() => result.IsShowing, 5f, "The result screen did not appear after the destruction shot.");
            Assert.True(shot.IsFinished);
            Assert.Zero(Time.timeScale);
            Assert.False(rig.input.GameplayEnabled);
            LevelOpeningCinematicTests.Capture(camera, "Logs/CoreDefeatCaptures/03-result.png");
        }

        [UnityTest] public IEnumerator InterruptedShotRestoresHudInputAndCameraWithoutChangingTimeScale()
        {
            yield return LoadLevel();
            var result = Object.FindFirstObjectByType<GameResultScreen>();
            var core = Object.FindFirstObjectByType<CoreReceiver>();
            var rig = Object.FindFirstObjectByType<PlayerCameraRig>();
            var hud = GameObject.Find("GameHUDCanvas").GetComponent<Canvas>();
            bool hudWasEnabled = hud.enabled;
            var shot = result.gameObject.AddComponent<CoreDefeatCamera>();
            Assert.True(shot.Begin(core));
            yield return null; yield return null;
            Assert.False(hud.enabled);
            Assert.False(rig.input.GameplayEnabled);
            shot.enabled = false;
            Assert.False(shot.IsActive);
            Assert.False(shot.Begin(core), "A disabled shot must not hold the result screen forever.");
            Assert.AreEqual(hudWasEnabled, hud.enabled);
            Assert.True(rig.input.GameplayEnabled);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.Less(Vector3.Distance(rig.UnshakenPosition, rig.transform.position), .001f);
            Assert.AreEqual(rig.BaseFieldOfView, rig.GetComponent<Camera>().fieldOfView, .001f);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            var empty = SceneManager.CreateScene("Core defeat cleanup " + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene != empty && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
            GameManager.Instance.ClearPauseRequests(); Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SandGuard.GameFlow;
using SandGuard.Player;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DesertTower.LevelIntegration.Tests
{
    public sealed class LevelOpeningCinematicTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        Keyboard keyboard;
        Mouse mouse;
        InputSettings originalSettings, testSettings;

        [SetUp] public void Setup()
        {
            GameManager.Instance.ClearPauseRequests();
            Time.timeScale = 1f;
            originalSettings = InputSystem.settings;
            testSettings = Object.Instantiate(originalSettings);
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testSettings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            InputSystem.settings = testSettings;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
        }

        GameObject Track(GameObject go) { objects.Add(go); return go; }

        LevelOpeningCinematic Create(out PlayerCameraRig rig, out Canvas hud, float seconds = .6f)
        {
            var target = Track(new GameObject("Opening test player")).transform;
            target.position = Vector3.up * 1.5f;
            var camera = Track(new GameObject("Opening test camera", typeof(Camera)));
            rig = camera.AddComponent<PlayerCameraRig>();
            rig.target = target; rig.owner = target;
            hud = Track(new GameObject("Opening test HUD", typeof(Canvas))).GetComponent<Canvas>();
            hud.renderMode = RenderMode.ScreenSpaceOverlay;
            var go = Track(new GameObject("Opening test")); go.SetActive(false);
            var opening = go.AddComponent<LevelOpeningCinematic>();
            opening.playerCamera = rig;
            opening.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/9.Font/Pretendard-Bold SDF.asset");
            opening.returnSeconds = .2f; opening.skipReturnSeconds = .1f;
            opening.shots = new[] { new LevelOpeningCinematic.Shot
            {
                title = "Temple", description = "Defend the core", seconds = seconds,
                from = new Vector3(8, 9, -12), to = new Vector3(10, 8, -11), lookAt = Vector3.zero
            }};
            go.SetActive(true);
            return opening;
        }

        static IEnumerator Until(Func<bool> condition, float timeout, string message)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.True(condition(), message);
        }

        [UnityTest] public IEnumerator NaturalFinishRestoresCameraHudAndPriorTimeScale()
        {
            Time.timeScale = 2f;
            var opening = Create(out var rig, out var hud);
            Assert.Zero(Time.timeScale, "Pause must be acquired in Awake, before wave Update.");
            yield return null; yield return null;
            Assert.True(opening.IsPlaying);
            Assert.False(hud.enabled);
            Assert.Greater(Vector3.Distance(rig.UnshakenPosition, rig.transform.position), 5f);
            yield return Until(() => opening.HasCompleted, 3f, "The unscaled opening did not finish while paused.");
            Assert.AreEqual(2f, Time.timeScale);
            Assert.True(hud.enabled);
            Assert.Less(Vector3.Distance(rig.UnshakenPosition, rig.transform.position), .001f);
            Assert.AreEqual(rig.BaseFieldOfView, rig.GetComponent<Camera>().fieldOfView, .001f);
        }

        [UnityTest] public IEnumerator SpaceSkipWaitsForReleaseAndPreservesAnotherPause()
        {
            var opening = Create(out _, out var hud, 10f);
            yield return new WaitForSecondsRealtime(.35f);
            var menu = Track(new GameObject("Another pause owner"));
            GameManager.Instance.RequestPause(menu);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return new WaitForSecondsRealtime(.3f);
            Assert.True(opening.IsPlaying, "Held skip must not become a gameplay jump.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Until(() => opening.HasCompleted, 2f, "SPACE did not skip the ten-second shot.");
            Assert.True(hud.enabled);
            Assert.Zero(Time.timeScale, "The opening must release only its own pause.");
            Assert.AreEqual(CursorLockMode.None, Cursor.lockState);
            GameManager.Instance.ReleasePause(menu);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest] public IEnumerator MouseClickSkipsAndDisableAlsoCleansUp()
        {
            var opening = Create(out _, out var hud, 10f);
            yield return new WaitForSecondsRealtime(.35f);
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return Until(() => opening.HasCompleted, 2f, "A mouse click did not skip the opening.");
            Assert.True(hud.enabled);
            var other = Create(out var rig, out var otherHud, 10f);
            yield return null; yield return null;
            other.enabled = false;
            Assert.False(other.IsPlaying);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.True(otherHud.enabled);
            Assert.Less(Vector3.Distance(rig.UnshakenPosition, rig.transform.position), .001f);
            yield return null;
            Assert.IsNull(GameObject.Find("Level Opening Overlay"));
        }

        [UnityTest] public IEnumerator LevelOpeningFreezesWavesAndHandsOffToGameplay()
        {
            yield return EditorSceneManager.LoadSceneInPlayMode("Assets/1.Scene/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var opening = Object.FindFirstObjectByType<LevelOpeningCinematic>();
            var director = Object.FindFirstObjectByType<WaveDirector>();
            Assert.NotNull(opening, "The shipped Level scene must contain the opening.");
            Assert.True(opening.IsPlaying);
            Assert.AreEqual(4, opening.shots.Length);
            float preparation = director.PreparationRemaining;
            var rig = Object.FindFirstObjectByType<PlayerCameraRig>();
            Vector3 playerPosition = rig.owner.position;
            Directory.CreateDirectory("Logs/OpeningCaptures");
            // Capture real game frames while also checking the countdown in every shot.
            for (int index = 0; index < opening.shots.Length; index++)
            {
                int expectedShot = index;
                yield return Until(() => opening.CurrentShotIndex == expectedShot, 5f, "An opening shot was skipped.");
                yield return new WaitForSecondsRealtime(.25f);
                Assert.True(opening.IsPlaying);
                Assert.Zero(Time.timeScale);
                Assert.AreEqual(preparation, director.PreparationRemaining, .001f);
                Assert.Zero(director.TotalSpawned);
                Assert.AreEqual(playerPosition, rig.owner.position);
                Capture(rig.GetComponent<Camera>(), $"Logs/OpeningCaptures/shot-{index + 1}.png");
            }
            yield return Until(() => opening.HasCompleted, 6f, "The actual Level opening never returned control.");
            Assert.False(GameManager.Instance.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.Less(Vector3.Distance(rig.UnshakenPosition, rig.transform.position), .001f);
            Capture(rig.GetComponent<Camera>(), "Logs/OpeningCaptures/gameplay.png");
            yield return new WaitForSecondsRealtime(.15f);
            Assert.Less(director.PreparationRemaining, preparation, "Countdown must resume after the opening.");
        }

        // ScreenCapture has no game-view repaint in batch mode. Render the live camera and
        // temporarily put overlay canvases in that camera so the saved image includes the UI.
        internal static void Capture(Camera camera, string path)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var canvases = new List<(Canvas canvas, Camera camera, float distance)>();
            var target = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            float previousAspect = camera.aspect;
            try
            {
                target.Create(); camera.targetTexture = target; camera.aspect = 1280f / 720f;
                foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    if (canvas.isActiveAndEnabled && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    {
                        canvases.Add((canvas, canvas.worldCamera, canvas.planeDistance));
                        canvas.renderMode = RenderMode.ScreenSpaceCamera;
                        canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + .1f;
                    }
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                foreach (var saved in canvases)
                {
                    saved.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    saved.canvas.worldCamera = saved.camera; saved.canvas.planeDistance = saved.distance;
                }
                camera.targetTexture = previousTarget; camera.aspect = previousAspect;
                RenderTexture.active = previousActive;
                target.Release(); Object.Destroy(target); Object.Destroy(pixels);
            }
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach (var go in objects) if (go) Object.Destroy(go);
            objects.Clear();
            var empty = SceneManager.CreateScene("Opening cleanup " + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene != empty && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
            GameManager.Instance.ClearPauseRequests(); Time.timeScale = 1f;
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
            InputSystem.settings = originalSettings; Object.Destroy(testSettings);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            yield return null;
        }
    }
}
#endif

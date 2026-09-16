#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SandGuard.Player.Tests
{
    /// <summary>점프 / 2단 점프 / 낙하 / 착지 상태 분리, 강한 착지, 조준점, 어깨 너머 카메라 검증.</summary>
    public sealed class PlayerAirborneTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        Keyboard keyboard;
        InputSettings originalSettings, testSettings;
        GameObject Track(GameObject value) { objects.Add(value); return value; }
        GameObject Player(Vector3 position)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Generated/Player.prefab");
            Assert.NotNull(asset, "Run SandGuard/Player/Create Missing Demo Assets first.");
            var result = Track(Object.Instantiate(asset, position, Quaternion.identity));
            result.GetComponent<PlayerInputReader>().captureCursor = false;
            return result;
        }
        GameObject Cube(Vector3 position, Vector3 scale)
        {
            var value = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            value.transform.position = position; value.transform.localScale = scale;
            Physics.SyncTransforms();
            return value;
        }
        void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        static AnimatorStateInfo State(Animator animator) => animator.GetCurrentAnimatorStateInfo(0);
        static void AssertClip(Animator animator, string name)
        {
            foreach (var info in animator.GetCurrentAnimatorClipInfo(0))
                if (info.clip.name == name && info.weight > .8f) return;
            Assert.Fail("Expected dominant clip " + name);
        }
        /// <summary>조건이 참이 될 때까지 프레임 단위로 기다린다. 기다리는 동안 본 상태 이름을 seen에 모은다.</summary>
        static IEnumerator Until(Func<bool> condition, float timeout, Animator animator, HashSet<string> seen, string message)
        {
            for (float t = 0f; t < timeout && !condition(); t += Time.deltaTime)
            {
                if (animator != null && seen != null)
                    foreach (var name in new[] { "Locomotion", "Jump", "Double Jump", "Falling", "Landing", "Hard Landing", "Dash", "Air Cast" })
                        if (State(animator).IsName(name)) seen.Add(name);
                yield return null;
            }
            Assert.True(condition(), message);
        }
        [SetUp] public void Setup()
        {
            originalSettings = InputSystem.settings;
            testSettings = Object.Instantiate(originalSettings);
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testSettings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            InputSystem.settings = testSettings;
            keyboard = InputSystem.AddDevice<Keyboard>(); Time.timeScale = 1f;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            foreach (var value in objects) if (value != null) Object.Destroy(value);
            objects.Clear();
            InputSystem.RemoveDevice(keyboard);
            InputSystem.settings = originalSettings;
            Object.Destroy(testSettings);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            yield return null;
        }

        [UnityTest] public IEnumerator AirJumpCostBlocksWithoutConsumingJumpAndZeroIsFree()
        {
            var player = Player(new Vector3(0, 20f, 0));
            var motor = player.GetComponent<PlayerMotor>();
            var mana = player.GetComponent<PlayerManaWallet>();
            motor.extraAirJumps = 2;
            motor.airJumpManaCost = 5;
            motor.Teleport(player.transform.position);
            mana.TrySpend(mana.CurrentMana - 5);
            int jumps = 0;
            motor.Jumped += () => jumps++;
            yield return null;
            Keys(Key.Space); yield return new WaitForSeconds(.05f);
            Assert.AreEqual(1, jumps);
            Assert.AreEqual(0, mana.CurrentMana);
            int remaining = motor.RemainingAirJumps;
            Keys(); yield return null;
            Keys(Key.Space); yield return new WaitForSeconds(.05f);
            Assert.AreEqual(1, jumps, "Insufficient mana blocks the extra jump.");
            Assert.AreEqual(remaining, motor.RemainingAirJumps);
            motor.airJumpManaCost = 0;
            Keys(); yield return null;
            Keys(Key.Space); yield return new WaitForSeconds(.05f);
            Assert.AreEqual(2, jumps, "Zero cost permits an extra jump without mana.");
            Assert.AreEqual(0, mana.CurrentMana);
        }

        [UnityTest] public IEnumerator JumpFlipFallAndLandUseSeparateStates()
        {
            Cube(new Vector3(0, -0.5f, 0), new Vector3(60, 1, 60));
            var player = Player(Vector3.zero);
            var motor = player.GetComponent<PlayerMotor>();
            var visuals = player.GetComponent<PlayerVisuals>();
            var mana = player.GetComponent<PlayerManaWallet>();
            motor.airJumpManaCost = 5;
            int initialMana = mana.CurrentMana;
            player.GetComponent<PlayerEffects>().Apply(new SandGuard.Player.Effects.DoubleJumpEffect()); // 기본값은 공중 점프 0
            float landedSpeed = -1f; int landings = 0, hardLandings = 0;
            motor.Landed += speed => { landedSpeed = speed; landings++; };
            visuals.onHardLanded.AddListener(() => hardLandings++);
            yield return new WaitForSeconds(0.4f);
            var animator = player.GetComponentInChildren<Animator>();
            Assert.True(motor.IsGrounded);
            Assert.True(State(animator).IsName("Locomotion"));
            landings = 0; // 생성 직후 바닥에 내려앉은 것은 세지 않는다

            Keys(Key.Space); yield return new WaitForSeconds(0.15f);
            Assert.False(motor.IsGrounded);
            Assert.False(motor.LastJumpWasAirJump);
            Assert.AreEqual(initialMana, mana.CurrentMana, "Ground jumps remain free.");
            Assert.Greater(motor.VerticalSpeed, 0f);
            Assert.True(State(animator).IsName("Jump"), "Ground jump uses the jump-up clip, not the flip.");
            AssertClip(animator, "Jump Up");
            CapturePose(player, "jump-up");

            Keys(); yield return null;
            Keys(Key.Space); yield return new WaitForSeconds(0.1f);
            Assert.True(motor.LastJumpWasAirJump);
            Assert.AreEqual(initialMana - 5, mana.CurrentMana, "Only the air jump spends mana.");
            Assert.True(State(animator).IsName("Double Jump"), "Air jump uses the flip.");
            AssertClip(animator, "Flip");
            yield return new WaitForSeconds(0.15f);
            CapturePose(player, "double-jump-flip");
            // 점프 버튼을 계속 눌러 최대 높이로 뛰어야 플립이 끝난 뒤 짧은 낙하 구간이 생긴다.
            var seen = new HashSet<string>();
            yield return Until(() => motor.IsGrounded, 2f, animator, seen, "Must touch down.");
            Assert.True(seen.Contains("Falling"), "After the flip finishes the falling pose plays until touchdown.");
            Assert.AreEqual(1, landings, "Landed fires exactly once per touchdown.");
            Assert.That(landedSpeed, Is.InRange(4f, visuals.hardLandingSpeed), "A normal jump lands below the hard-landing speed.");
            Assert.AreEqual(0, hardLandings);
            yield return Until(() => State(animator).IsName("Landing"), 0.3f, animator, seen, "Touchdown plays the landing clip.");
            AssertClip(animator, "Landing");
            CapturePose(player, "landing");
            Assert.False(seen.Contains("Hard Landing"));
            yield return Until(() => State(animator).IsName("Locomotion"), 1.5f, animator, seen, "Landing recovers into locomotion.");
            Keys();
            Assert.AreEqual(1, landings);
        }

        [UnityTest] public IEnumerator HighFallLocksMovementUntilHardLandingFinishes()
        {
            Cube(new Vector3(0, -0.5f, 0), new Vector3(60, 1, 60));
            var player = Player(Vector3.zero);
            var motor = player.GetComponent<PlayerMotor>();
            var visuals = player.GetComponent<PlayerVisuals>();
            float landedSpeed = -1f; int hardLandings = 0;
            motor.Landed += speed => landedSpeed = speed;
            visuals.onHardLanded.AddListener(() => hardLandings++);
            yield return new WaitForSeconds(0.4f);
            var animator = player.GetComponentInChildren<Animator>();
            motor.Teleport(new Vector3(0, 8f, 0));
            var seen = new HashSet<string>();
            yield return Until(() => State(animator).IsName("Falling"), 1f, animator, seen, "A long drop shows the falling pose.");
            CapturePose(player, "falling");
            yield return Until(() => motor.IsGrounded, 2.5f, animator, seen, "Must land after the drop.");
            Assert.True(seen.Contains("Falling"), "A long drop shows the falling pose.");
            Assert.False(seen.Contains("Jump") || seen.Contains("Double Jump"), "Dropping is not a jump.");
            Assert.GreaterOrEqual(landedSpeed, visuals.hardLandingSpeed);
            Assert.AreEqual(1, hardLandings);
            yield return Until(() => State(animator).IsName("Hard Landing"), 0.3f, animator, seen, "Fast impacts use the hard landing.");
            AssertClip(animator, "Hard Landing");
            yield return new WaitForSeconds(0.2f);
            CapturePose(player, "hard-landing");
            Assert.True(motor.HardLandingLocked);
            Vector3 planted = player.transform.position;
            Assert.False(motor.TryDash().Succeeded);
            Assert.False(motor.TryJump());
            Keys(Key.W);
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.True(motor.HardLandingLocked, "Pause must not consume the recovery.");
            Time.timeScale = 1f;
            float elapsed = 0f, lastNormalized = 0f;
            while (motor.HardLandingLocked && elapsed < 3f)
            {
                if (State(animator).IsName("Hard Landing")) lastNormalized = State(animator).normalizedTime;
                yield return null; elapsed += Time.deltaTime;
                if (motor.HardLandingLocked)
                    Assert.Less(Vector3.ProjectOnPlane(player.transform.position - planted, Vector3.up).magnitude, .001f, "Held movement must not slide during recovery.");
            }
            Assert.False(motor.HardLandingLocked);
            if (State(animator).IsName("Hard Landing")) lastNormalized = State(animator).normalizedTime;
            Assert.GreaterOrEqual(lastNormalized, 1f, "The complete landing clip must play before movement unlocks.");
            Assert.True(State(animator).IsName("Hard Landing") && animator.IsInTransition(0)
                && animator.GetNextAnimatorStateInfo(0).IsName("Locomotion"),
                "Unlock at clip completion, while the 0.15s recovery blend is still active.");
            Vector3 unlockedAt = player.transform.position;
            yield return new WaitForSeconds(.05f);
            Assert.Greater(Vector3.ProjectOnPlane(player.transform.position - unlockedAt, Vector3.up).magnitude, .001f,
                "Movement input must work during the visual recovery blend.");
            Assert.True(motor.TryDash().Succeeded, "Dash must be available before the recovery blend ends.");
            yield return new WaitForSeconds(.2f);
            Assert.Greater(Vector3.ProjectOnPlane(player.transform.position - planted, Vector3.up).magnitude, .05f, "Held movement resumes after recovery.");
            Keys();
        }

        [UnityTest] public IEnumerator WalkingOffALedgeFallsWithoutJumping()
        {
            Cube(new Vector3(0, -0.5f, 0), new Vector3(4, 1, 4));
            var player = Player(Vector3.zero);
            var motor = player.GetComponent<PlayerMotor>();
            int jumps = 0; motor.Jumped += () => jumps++;
            yield return new WaitForSeconds(0.4f);
            var animator = player.GetComponentInChildren<Animator>();
            Keys(Key.W);
            var seen = new HashSet<string>();
            yield return Until(() => !motor.IsGrounded && motor.VerticalSpeed < -PlayerVisuals.FallingSpeedThreshold, 2f, animator, seen, "Must walk off the edge.");
            yield return Until(() => State(animator).IsName("Falling"), 0.3f, animator, seen, "Leaving the ledge enters the falling state.");
            Keys();
            Assert.AreEqual(0, jumps);
            Assert.False(seen.Contains("Jump") || seen.Contains("Double Jump"), "Walking off a ledge never plays a jump.");
        }
        [UnityTest] public IEnumerator CrosshairSitsAtScreenCenterAndHidesWhenInputIsUnavailable()
        {
            Cube(new Vector3(0, -0.5f, 0), new Vector3(60, 1, 60));
            var player = Player(Vector3.zero);
            var input = player.GetComponent<PlayerInputReader>();
            var crosshair = player.GetComponent<PlayerCrosshair>();
            Assert.NotNull(crosshair, "Player.prefab carries a PlayerCrosshair.");
            yield return null; yield return null;
            var canvas = player.GetComponentInChildren<Canvas>();
            Assert.NotNull(canvas, "The crosshair builds its own canvas.");
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.AreEqual(Vector2.zero, crosshair.Root.anchoredPosition);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), crosshair.Root.anchorMin);
            Assert.True(crosshair.Visible);
            Assert.Greater(player.GetComponentsInChildren<UnityEngine.UI.Image>().Length, 3);
            input.GameplayEnabled = false;
            yield return null;
            Assert.False(crosshair.Visible, "No aiming while the cursor is released or gameplay is disabled.");
            input.GameplayEnabled = true;
            yield return null;
            Assert.True(crosshair.Visible);
            Assert.AreEqual(1, Object.FindObjectsByType<PlayerCrosshair>(FindObjectsSortMode.None).Length, "Exactly one crosshair per player.");
        }

        [UnityTest] public IEnumerator CameraLooksOverTheRightShoulder()
        {
            Cube(new Vector3(0, -0.5f, 0), new Vector3(60, 1, 60));
            var player = Player(Vector3.zero);
            var rig = player.GetComponentInChildren<PlayerCameraRig>();
            rig.pitch = 0f;
            yield return null; yield return null;
            Assert.GreaterOrEqual(rig.shoulderOffset, 0.8f, "Fortnite-like shoulder offset keeps the body out of the aim.");
            Assert.GreaterOrEqual(rig.distance, 3f, "The camera sits far enough back to see past the body.");
            Vector3 local = player.transform.InverseTransformPoint(rig.transform.position);
            Assert.Greater(local.x, 0.7f, "Camera is to the right of the character.");
            Assert.Less(local.z, -2.5f, "Camera is behind the character.");
            // 화면 중앙 광선이 캐릭터 몸을 지나지 않는다.
            var camera = rig.GetComponent<Camera>();
            Ray center = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            var body = player.GetComponent<CharacterController>();
            Assert.False(body.bounds.IntersectRay(center), "The body must not block the crosshair ray.");
            yield return CaptureView(player, camera, "camera-shoulder");
            rig.pitch = 25f; yield return null; yield return null;
            yield return CaptureView(player, camera, "camera-shoulder-down");
        }

        /// <summary>캐릭터 자세를 정면 비스듬히 저장한다. -nographics 배치에서는 건너뛴다.</summary>
        void CapturePose(GameObject player, string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var sun = Track(new GameObject("Pose Sun")).AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 2f; sun.transform.rotation = Quaternion.Euler(40f, -35f, 0f);
            var camera = Track(new GameObject("Pose Camera")).AddComponent<Camera>();
            camera.backgroundColor = new Color(.14f, .16f, .19f); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.orthographic = true; camera.orthographicSize = 1.5f; camera.nearClipPlane = .01f; camera.farClipPlane = 100f;
            Vector3 center = player.transform.position + Vector3.up * 1f;
            camera.transform.position = center + new Vector3(2f, .45f, 4f); camera.transform.LookAt(center);
            var rt = new RenderTexture(1000, 1000, 24); camera.targetTexture = rt;
            camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var texture = new Texture2D(1000, 1000, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1000, 1000), 0, 0); texture.Apply();
            System.IO.Directory.CreateDirectory("Logs/player-airborne-captures");
            System.IO.File.WriteAllBytes("Logs/player-airborne-captures/" + name + ".png", texture.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null; rt.Release();
            Object.Destroy(rt); Object.Destroy(texture); camera.enabled = false;
        }

        /// <summary>실제 플레이어 카메라 시점(조준점 포함)을 저장한다. -nographics 배치에서는 건너뛴다.</summary>
        IEnumerator CaptureView(GameObject player, Camera camera, string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) yield break;
            var sun = Track(new GameObject("Capture Sun")).AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 2f; sun.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            var canvas = player.GetComponentInChildren<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1f;
            yield return null;
            Canvas.ForceUpdateCanvases();
            var rt = new RenderTexture(1600, 900, 24); camera.targetTexture = rt;
            camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
            System.IO.Directory.CreateDirectory("Logs/player-airborne-captures");
            System.IO.File.WriteAllBytes("Logs/player-airborne-captures/" + name + ".png", texture.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null; rt.Release();
            Object.Destroy(rt); Object.Destroy(texture);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
    }
}
#endif

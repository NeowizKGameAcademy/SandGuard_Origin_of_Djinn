#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace SandGuard.Player.Tests
{
    /// <summary>조작감 항목(가감속 분리, 코요테·버퍼, 가변 점프, 대시 속도 유지, 이동 방향 회전) 검증.</summary>
    public sealed class PlayerFeelTests
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

        [UnityTest] public IEnumerator StopsAlmostImmediatelyAfterReleasingInput()
        {
            Cube(new Vector3(0, -0.5f, 0), new Vector3(30, 1, 30));
            var player = Player(Vector3.zero);
            yield return new WaitForSeconds(0.15f);
            Keys(Key.W); yield return new WaitForSeconds(0.4f);
            Keys();
            float releasedZ = player.transform.position.z;
            yield return new WaitForSeconds(0.3f);
            Assert.Less(player.transform.position.z - releasedZ, 0.35f, "Ground deceleration must stop the player within a few frames.");
        }

        [UnityTest] public IEnumerator JumpBufferFiresOnLandingAndCoyoteJumpKeepsAirJump()
        {
            Cube(new Vector3(0, -0.5f, 0), new Vector3(30, 1, 30));
            var player = Player(Vector3.zero);
            var motor = player.GetComponent<PlayerMotor>();
            yield return new WaitForSeconds(0.15f);
            // 착지 직전 입력이 버퍼에 남아 착지하자마자 다시 뛴다.
            motor.Teleport(new Vector3(0, 0.2f, 0));
            yield return null;
            Keys(Key.Space); yield return null; Keys();
            yield return new WaitForSeconds(0.2f);
            Assert.False(motor.IsGrounded, "Buffered jump must fire on landing.");
            Assert.AreEqual(motor.ExtraAirJumps, motor.RemainingAirJumps, "A buffered ground jump must not consume an air jump.");
            yield return new WaitForSeconds(1.2f);
            Assert.True(motor.IsGrounded);
            // 모서리에서 떨어진 직후의 점프는 지상 점프로 친다.
            var ledge = Cube(new Vector3(-10, 1.5f, 0), new Vector3(4, 1, 4));
            motor.Teleport(new Vector3(-8.4f, 2.1f, 0));
            yield return new WaitForSeconds(0.3f);
            Assert.True(motor.IsGrounded);
            Keys(Key.D); yield return new WaitForSeconds(0.2f);
            Keys(Key.D, Key.Space); yield return null; yield return null;
            Keys();
            Assert.False(motor.IsGrounded);
            Assert.AreEqual(motor.ExtraAirJumps, motor.RemainingAirJumps, "Coyote jump must count as a ground jump.");
        }

        [UnityTest] public IEnumerator HoldingJumpGoesHigherThanTapping()
        {
            Cube(new Vector3(0, -0.5f, 0), new Vector3(30, 1, 30));
            var player = Player(Vector3.zero);
            var motor = player.GetComponent<PlayerMotor>();
            yield return new WaitForSeconds(0.15f);
            Keys(Key.Space); yield return null; Keys();
            float tapPeak = 0f;
            for (float t = 0f; t < 1.2f; t += Time.deltaTime) { tapPeak = Mathf.Max(tapPeak, player.transform.position.y); yield return null; }
            Assert.True(motor.IsGrounded);
            Keys(Key.Space);
            float holdPeak = 0f;
            for (float t = 0f; t < 1.2f; t += Time.deltaTime) { holdPeak = Mathf.Max(holdPeak, player.transform.position.y); yield return null; }
            Keys();
            Assert.Greater(holdPeak, tapPeak + 0.3f, "Holding jump must reach clearly higher than a tap.");
            Assert.Greater(holdPeak, motor.jumpHeight * 0.85f);
        }

        /// <summary>대시는 사다리꼴이다. 첫 프레임들에 최고속에 붙어 거의 끝까지 유지하고 마지막에만 떨어진다. 총 거리는 그대로다.</summary>
        [UnityTest] public IEnumerator DashHoldsPeakSpeedAndDropsOnlyAtTheVeryEnd()
        {
            Cube(new Vector3(0, -0.5f, 0), new Vector3(60, 1, 60));
            var player = Player(Vector3.zero);
            var motor = player.GetComponent<PlayerMotor>();
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(0f, motor.DashProgress(0f), 0.0001f); Assert.AreEqual(1f, motor.DashProgress(1f), 0.0001f);
            Assert.Less(motor.DashProgress(0.05f), 0.05f, "첫 5%는 거리의 5%보다 덜 간다(짧은 가속).");
            Assert.That(motor.DashProgress(0.5f), Is.InRange(0.45f, 0.6f), "평평한 정점이라 중간에 거리의 약 절반을 간다.");
            Assert.Greater(motor.DashProgress(0.85f), 0.85f, "85%까지 감속이 없다.");
            Assert.Greater(1f - motor.DashProgress(0.9f), 0.05f, "마지막 10%에도 거리의 5% 이상을 간다. 떨어지는 건 맨 끝뿐이다.");
            Vector3 start = player.transform.position; Vector3 previous = start;
            var speeds = new List<float>();
            Assert.True(motor.TryDash().Succeeded);
            while (motor.IsDashing)
            {
                yield return null;
                speeds.Add(Vector3.Distance(player.transform.position, previous) / Time.deltaTime);
                previous = player.transform.position;
            }
            float travelled = Vector3.Distance(start, player.transform.position);
            Assert.That(travelled, Is.InRange(motor.DashDistance - 0.3f, motor.DashDistance + 0.3f), "The profile keeps the total distance.");
            Assert.GreaterOrEqual(speeds.Count, 4, "Enough frames to see the shape.");
            float peak = 0f;
            for (int i = 0; i < speeds.Count; i++) peak = Mathf.Max(peak, speeds[i]);
            Assert.Greater(peak, motor.DashDistance / motor.dashDuration * 1.03f, "정점은 평균(등속) 속도보다 높다. 사다리꼴이라 차이는 작다.");
            foreach (int quarter in new[] { 1, 2, 3 })
                Assert.Greater(speeds[speeds.Count * quarter / 4], peak * 0.9f, "25%·50%·75% 지점 모두 정점 속도다(평평한 정점).");
            Assert.Less(speeds[speeds.Count - 1], peak * 0.8f, "마지막 프레임은 떨어지는 중이다.");
        }

        /// <summary>질풍참 방식. 접지 상태에서 수평·아래 조준은 지면을 따라가고, 위를 보면 조준 방향으로 떠오른다. 공중 대시는 항상 조준을 따른다.</summary>
        [UnityTest] public IEnumerator DashFollowsTheAimUpwardButStaysFlatOnTheGroundWhenAimingDown()
        {
            Cube(new Vector3(0, -0.5f, 0), new Vector3(60, 1, 60));
            var player = Player(Vector3.zero);
            var motor = player.GetComponent<PlayerMotor>();
            var rig = player.GetComponentInChildren<PlayerCameraRig>();
            yield return new WaitForSeconds(0.3f);
            rig.pitch = 20f; yield return null; yield return null; // 아래를 본다
            float groundY = player.transform.position.y;
            Assert.True(motor.TryDash().Succeeded);
            Assert.AreEqual(0f, motor.DashDirection.y, 0.001f, "접지 + 아래 조준: 지면을 따라 수평으로 간다.");
            yield return new WaitUntil(() => !motor.IsDashing);
            Assert.AreEqual(groundY, player.transform.position.y, 0.05f);
            Assert.Greater(player.transform.position.z, motor.DashDistance * 0.9f);
            motor.ResetDashCooldown();
            rig.pitch = rig.pitchLimits.x; yield return null; yield return null; // 위를 본다(한계 -35도)
            float startY = player.transform.position.y;
            Assert.True(motor.TryDash().Succeeded);
            Assert.Greater(motor.DashDirection.y, 0.5f, "접지 + 위 조준(35도 > groundedDashLiftAngle): 조준 방향으로 떠오른다.");
            float peakY = startY;
            while (motor.IsDashing) { yield return null; peakY = Mathf.Max(peakY, player.transform.position.y); }
            Assert.Greater(peakY, startY + motor.DashDistance * 0.4f, "6m를 35도로 가면 3m 넘게 올라간다.");
            Assert.False(motor.IsGrounded);
            yield return new WaitForSeconds(2f);
            Assert.True(motor.IsGrounded, "올라간 만큼 떨어져 다시 착지한다.");
        }

        [UnityTest] public IEnumerator DashHandsOverRunSpeedAndStopsCleanlyWithoutInput()
        {
            Cube(new Vector3(0, -0.5f, 0), new Vector3(40, 1, 40));
            var player = Player(Vector3.zero);
            var motor = player.GetComponent<PlayerMotor>();
            yield return new WaitForSeconds(0.15f);
            Assert.That(motor.DashExitSpeed, Is.InRange(motor.moveSpeed * 0.7f, motor.moveSpeed * 1.5f), "곡선 끝 속도는 달리기 속도 근처다. 입력을 누르고 있으면 감속 없이 달리기로 이어진다.");
            Assert.True(motor.TryDash().Succeeded);
            yield return new WaitUntil(() => !motor.IsDashing);
            yield return null;
            float after = Vector3.ProjectOnPlane(motor.Velocity, Vector3.up).magnitude;
            Assert.LessOrEqual(after, motor.DashExitSpeed + 0.01f, "대시가 끝난 뒤 속도가 올라가지 않는다. 이동 속도로 덮어쓰지 않고 곡선의 끝 속도를 이어받는다.");
            Assert.Greater(after, motor.moveSpeed * 0.3f, "첫 프레임은 아직 넘겨받은 속도로 움직인다.");
            yield return new WaitForSeconds(0.15f);
            Assert.AreEqual(0f, Vector3.ProjectOnPlane(motor.Velocity, Vector3.up).magnitude, 0.01f, "입력이 없으면 dashExitDeceleration(60)으로 0.15초 안에 깔끔히 선다. 미끄러지지 않는다.");
            Assert.Greater(player.transform.position.z, motor.dashDistance * 0.9f);
        }

        [UnityTest] public IEnumerator FacesMovementDirectionUnlessAttacking()
        {
            Cube(new Vector3(0, -0.5f, 0), new Vector3(30, 1, 30));
            var player = Player(Vector3.zero);
            var motor = player.GetComponent<PlayerMotor>();
            yield return new WaitForSeconds(0.15f);
            Keys(Key.D); yield return new WaitForSeconds(0.4f);
            float yaw = player.transform.eulerAngles.y;
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(yaw, 90f)), 15f, "Strafing right without attacking must turn the body right.");
            motor.FaceCamera();
            yield return new WaitForSeconds(0.4f);
            yaw = player.transform.eulerAngles.y;
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(yaw, 0f)), 15f, "After attacking the body must face the camera forward.");
            Keys();
        }

        [UnityTest] public IEnumerator CameraFollowsWithBoundedLagAndCatchesUp()
        {
            Cube(new Vector3(0, -0.5f, 0), new Vector3(60, 1, 60));
            var player = Player(Vector3.zero);
            var rig = player.GetComponentInChildren<PlayerCameraRig>();
            yield return new WaitForSeconds(0.15f);
            Keys(Key.W);
            float maxLag = 0f, minLag = float.MaxValue;
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                float lag = Vector3.Distance(rig.Pivot, rig.target.position);
                if (t > 0.2f) { maxLag = Mathf.Max(maxLag, lag); minLag = Mathf.Min(minLag, lag); }
                yield return null;
            }
            Assert.Greater(minLag, 0.05f, "The pivot must trail the player while moving.");
            Assert.LessOrEqual(maxLag, rig.maxFollowLag + 0.01f, "The pivot must never fall further behind than maxFollowLag.");
            Keys();
            yield return new WaitForSeconds(0.6f);
            Assert.Less(Vector3.Distance(rig.Pivot, rig.target.position), 0.05f, "The pivot must settle on the player after stopping.");
        }

        [UnityTest] public IEnumerator DownhillSnapPreservesLocomotionVelocityAndResetsIt()
        {
            var slope = Cube(new Vector3(0, 0, 10), new Vector3(8, .2f, 30));
            slope.transform.rotation = Quaternion.Euler(35f, 0, 0);
            Physics.SyncTransforms();
            var player = Player(new Vector3(0, 8, 0));
            var motor = player.GetComponent<PlayerMotor>();
            var controller = player.GetComponent<CharacterController>();
            yield return new WaitForSeconds(.8f);
            Assert.True(motor.IsGrounded);
            motor.moveSpeed = 10f;
            // Force the explicit ground snap rather than the controller's automatic step-down.
            float originalStepOffset = controller.stepOffset;
            controller.stepOffset = 0f;
            motor.groundStickSpeed = 0f;
            motor.gravity = .01f;
            Keys(Key.W);
            yield return new WaitForSeconds(.3f); // Allow acceleration regardless of batch-mode frame rate.
            int correctedFrames = 0;
            for (int i = 0; i < 35; i++)
            {
                yield return null;
                if (!motor.IsGrounded) continue;
                Assert.Greater(motor.Velocity.z, 8f, "Ground snap must not erase horizontal locomotion speed.");
                if (controller.velocity.z < motor.Velocity.z * .5f) correctedFrames++;
            }
            Assert.Greater(correctedFrames, 0, "The test must exercise a second, downward Move call.");
            controller.stepOffset = originalStepOffset;
            motor.Teleport(new Vector3(0, 8, 0));
            Assert.AreEqual(Vector3.zero, motor.Velocity, "Teleport must not publish a velocity spike.");
            motor.MovementEnabled = false;
            yield return null;
            Assert.AreEqual(Vector3.zero, motor.Velocity);
            motor.MovementEnabled = true;
            Time.timeScale = 0f;
            yield return null;
            Assert.AreEqual(Vector3.zero, motor.Velocity, "Paused frames must have finite, zero velocity.");
            Time.timeScale = 1f;
            motor.enabled = false;
            Assert.AreEqual(Vector3.zero, motor.Velocity);
            Keys();
        }

        [UnityTest] public IEnumerator SnapsToGroundWhenWalkingDownASlope()
        {
            Cube(new Vector3(0, -0.5f, 0), new Vector3(60, 1, 60));
            var slope = Cube(new Vector3(0, 0, 6), new Vector3(6, 0.2f, 12));
            slope.transform.rotation = Quaternion.Euler(14f, 0, 0); // +Z로 갈수록 낮아지는 완만한 내리막
            Physics.SyncTransforms();
            var player = Player(new Vector3(0, 2.2f, 1.5f));
            var motor = player.GetComponent<PlayerMotor>();
            yield return new WaitForSeconds(0.4f);
            Assert.True(motor.IsGrounded);
            int airborneFrames = 0;
            Keys(Key.W);
            for (float t = 0f; t < 1.2f; t += Time.deltaTime) { if (!motor.IsGrounded) airborneFrames++; yield return null; }
            Keys();
            Assert.LessOrEqual(airborneFrames, 2, "Walking down a gentle slope must not flicker the grounded state.");
        }

        [UnityTest] public IEnumerator CaptureCrouchSprintDash()
        {
            Assert.AreNotEqual(UnityEngine.Rendering.GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType);
            float previousCapture = Time.captureDeltaTime;
            var floor = Cube(new Vector3(0, -.5f, 0), new Vector3(100, 1, 100));
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(.28f, .25f, .20f));
            floor.GetComponent<Renderer>().sharedMaterial = material;
            var light = Track(new GameObject("Dash capture light")).AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 2f;
            light.transform.rotation = Quaternion.Euler(40, -35, 0);
            var player = Player(Vector3.zero);
            var motor = player.GetComponent<PlayerMotor>();
            var cameras = new Camera[2];
            var offsets = new[] { new Vector3(5, 1, 0), new Vector3(4, 1.3f, 4) };
            var folders = new[] { "side", "three-quarter" };
            var rt = new RenderTexture(1280, 720, 24);
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var oldRT = RenderTexture.active;
            try
            {
                Time.captureDeltaTime = 1f / 60f;
                for (int j = 0; j < 2; j++)
                {
                    cameras[j] = Track(new GameObject("Dash capture " + folders[j])).AddComponent<Camera>();
                    cameras[j].enabled = false;
                    cameras[j].orthographic = true; cameras[j].orthographicSize = 1.55f;
                    cameras[j].nearClipPlane = .01f; cameras[j].farClipPlane = 100f;
                    cameras[j].clearFlags = CameraClearFlags.SolidColor;
                    cameras[j].backgroundColor = new Color(.16f, .18f, .21f);
                    System.IO.Directory.CreateDirectory("Logs/dash-crouch-review/" + folders[j]);
                }
                yield return new WaitForSeconds(.4f);
                Keys(Key.W); yield return new WaitForSeconds(.3f);
                var animator = player.GetComponentInChildren<Animator>();
                int layer = animator.GetLayerIndex("Dash Legs");
                Assert.GreaterOrEqual(layer, 0);
                bool observedClip = false;
                float dashStart = 0f;
                for (int i = 0; i < 96; i++)
                {
                    if (i == 24) { dashStart = player.transform.position.z; Assert.True(motor.TryDash().Succeeded); }
                    if (i == 70) Keys();
                    yield return null;
                    if (i >= 28 && i < 40)
                        foreach (var info in animator.GetCurrentAnimatorClipInfo(layer))
                            if (info.clip.name == "Crouch Sprint Dash" && info.weight > .5f) observedClip = true;
                    if (i == 43) Assert.Greater(player.transform.position.z - dashStart, motor.DashDistance * .85f);
                    for (int j = 0; j < 2; j++)
                    {
                        Vector3 center = player.transform.position + Vector3.up * .85f;
                        cameras[j].transform.position = center + offsets[j]; cameras[j].transform.LookAt(center);
                        cameras[j].targetTexture = rt; cameras[j].Render(); RenderTexture.active = rt;
                        texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
                        System.IO.File.WriteAllBytes($"Logs/dash-crouch-review/{folders[j]}/frame-{i:000}.png", texture.EncodeToPNG());
                    }
                }
                Assert.True(observedClip, "The actual dash layer must play the newly imported crop.");
                Assert.False(motor.IsDashing);
                Assert.Less(animator.GetLayerWeight(layer), .01f, "Dash must blend back out after release.");
                Assert.False(animator.applyRootMotion);
            }
            finally
            {
                Time.captureDeltaTime = previousCapture; RenderTexture.active = oldRT;
                foreach (var camera in cameras) if (camera != null) camera.targetTexture = null;
                rt.Release(); Object.Destroy(rt); Object.Destroy(texture); Object.Destroy(material); Keys();
            }
        }
    }
}
#endif

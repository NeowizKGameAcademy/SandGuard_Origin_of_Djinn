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
            Assert.AreEqual(motor.extraAirJumps, motor.RemainingAirJumps, "A buffered ground jump must not consume an air jump.");
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
            Assert.AreEqual(motor.extraAirJumps, motor.RemainingAirJumps, "Coyote jump must count as a ground jump.");
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

        [UnityTest] public IEnumerator DashKeepsMovingAfterItEndsInsteadOfStopping()
        {
            Cube(new Vector3(0, -0.5f, 0), new Vector3(40, 1, 40));
            var player = Player(Vector3.zero);
            var motor = player.GetComponent<PlayerMotor>();
            yield return new WaitForSeconds(0.15f);
            Assert.True(motor.TryDash().Succeeded);
            yield return new WaitUntil(() => !motor.IsDashing);
            yield return null;
            float speed = Vector3.ProjectOnPlane(motor.Velocity, Vector3.up).magnitude;
            Assert.Greater(speed, motor.moveSpeed * 0.5f, "Momentum must carry over when the dash ends.");
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
    }
}
#endif

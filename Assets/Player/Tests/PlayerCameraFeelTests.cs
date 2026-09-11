#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SandGuard.Player.Effects;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SandGuard.Player.Tests
{
    /// <summary>카메라 리그의 연출 입력(흔들림·킥·시야·거리)과 상승 기류 충전 연출(응축 → 해방 → 복귀).</summary>
    public sealed class PlayerCameraFeelTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        Keyboard keyboard;
        InputSettings originalSettings, testSettings;
        GameObject player; PlayerMotor motor; PlayerUpdraft updraft; PlayerCameraRig rig; Camera camera; PlayerUpdraftCameraFeel feel;
        GameObject Track(GameObject value) { objects.Add(value); return value; }
        void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        IEnumerator Until(System.Func<bool> condition, float timeout, string message)
        {
            for (float t = 0f; t < timeout && !condition(); t += Time.deltaTime) yield return null;
            Assert.True(condition(), message);
        }
        // 카메라는 플레이어의 자식이라 월드 위치는 플레이어 이동에 따라 흔들림 없이도 바뀐다. 리그가 더한 오프셋 자체를 본다.
        float Deviation => rig.LastShakeOffset.magnitude;

        [UnitySetUp] public IEnumerator Setup()
        {
            originalSettings = InputSystem.settings;
            testSettings = Object.Instantiate(originalSettings);
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testSettings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            InputSystem.settings = testSettings;
            keyboard = InputSystem.AddDevice<Keyboard>(); Time.timeScale = 1f;
            var floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            floor.transform.position = new Vector3(0, -0.5f, 0); floor.transform.localScale = new Vector3(60, 1, 60);
            Physics.SyncTransforms();
            player = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Generated/Player.prefab")));
            player.GetComponent<PlayerInputReader>().captureCursor = false;
            motor = player.GetComponent<PlayerMotor>(); updraft = player.GetComponent<PlayerUpdraft>();
            rig = player.GetComponentInChildren<PlayerCameraRig>(); camera = rig.GetComponent<Camera>();
            feel = player.GetComponent<PlayerUpdraftCameraFeel>();
            Assert.NotNull(feel, "Player.prefab carries a PlayerUpdraftCameraFeel.");
            Assert.AreSame(updraft, feel.updraft); Assert.AreSame(rig, feel.rig);
            yield return new WaitForSeconds(0.4f);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            foreach (var value in objects) if (value != null) Object.Destroy(value);
            objects.Clear();
            InputSystem.RemoveDevice(keyboard);
            InputSystem.settings = originalSettings; Object.Destroy(testSettings);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            yield return null;
        }

        [UnityTest] public IEnumerator RigAppliesShakeKickFovAndDistanceInputsAndClearsThem()
        {
            feel.enabled = false; // 연출 드라이버가 매 프레임 입력을 덮어쓰므로 리그 입력만 따로 본다
            yield return null;
            float baseFov = rig.BaseFieldOfView;
            Assert.AreEqual(baseFov, camera.fieldOfView, 0.001f);
            Assert.Less(Deviation, 0.0001f, "No shake at rest.");
            Vector3 restPosition = camera.transform.position;
            rig.SetShake(0.1f, 20f);
            float maxDeviation = 0f, maxRoll = 0f;
            for (int i = 0; i < 20; i++)
            {
                yield return null;
                maxDeviation = Mathf.Max(maxDeviation, Deviation);
                maxRoll = Mathf.Max(maxRoll, Mathf.Abs(Mathf.DeltaAngle(0f, camera.transform.eulerAngles.z)));
            }
            Assert.That(maxDeviation, Is.InRange(0.01f, 0.15f), "Continuous shake moves the camera within its amplitude.");
            Assert.Greater(maxRoll, 0.2f, "Shake also rolls the camera slightly.");
            rig.SetShake(0f, 20f); yield return null; yield return null;
            Assert.Less(Deviation, 0.0001f, "Zero amplitude stops the shake.");
            Assert.Less(Vector3.Distance(restPosition, camera.transform.position), 0.05f, "The rest pose is untouched by past shake.");
            rig.Kick(0.2f, 0.25f);
            yield return null; yield return null;
            Assert.Greater(rig.KickAmplitude, 0f); Assert.Greater(Deviation, 0.005f, "A kick shakes immediately.");
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(0f, rig.KickAmplitude); Assert.Less(Deviation, 0.0001f, "A kick decays to nothing.");
            float restDistance = Vector3.Distance(rig.UnshakenPosition, rig.Pivot);
            for (int i = 0; i < 2; i++) { rig.AddFov(-4f); rig.AddFov(-3f); rig.AddDistance(-0.45f); yield return null; } // 같은 프레임의 기여는 합산된다
            Assert.AreEqual(-7f, rig.FovOffset, 0.001f, "Contributions made in one frame add up.");
            Assert.AreEqual(baseFov - 7f, camera.fieldOfView, 0.001f);
            Assert.AreEqual(0.45f, restDistance - Vector3.Distance(rig.UnshakenPosition, rig.Pivot), 0.05f, "Distance offset pulls the camera in by that amount (pull-in is immediate).");
            yield return new WaitForSeconds(0.5f); // 더 넣지 않으면 다음 프레임부터 사라진다. 멀어지는 쪽은 충돌 복구 시간(0.12초)에 걸쳐 돌아온다
            Assert.AreEqual(0f, rig.FovOffset, 0.001f, "An offset lasts only the frame it was added in.");
            Assert.AreEqual(baseFov, camera.fieldOfView, 0.001f);
            Assert.AreEqual(restDistance, Vector3.Distance(rig.UnshakenPosition, rig.Pivot), 0.03f, "Distance returns once nothing adds to it.");
            rig.SetShake(0.1f, 20f); rig.AddFov(5f); rig.ClearFeel(); yield return null; yield return null;
            Assert.AreEqual(0f, rig.ShakeAmplitude); Assert.AreEqual(baseFov, camera.fieldOfView, 0.001f);
        }

        [UnityTest] public IEnumerator ChargeShakesHarderAndTightensThenLaunchPunchesAndEverythingSettles()
        {
            player.GetComponent<PlayerEffects>().Apply(new UpdraftEffect());
            float baseFov = rig.BaseFieldOfView;
            Assert.AreEqual(0f, feel.CurrentShake); Assert.AreEqual(baseFov, camera.fieldOfView, 0.001f);
            Keys(Key.Space);
            yield return Until(() => updraft.IsCharging, 0.5f, "Charging starts.");
            yield return Until(() => updraft.Charge >= 0.3f, 1f, "Charge builds.");
            float shakeLow = feel.CurrentShake, freqLow = rig.ShakeFrequency, fovLow = camera.fieldOfView, distLow = feel.CurrentDistanceOffset;
            Assert.Greater(shakeLow, 0f, "Charging shakes the camera.");
            Assert.Less(fovLow, baseFov, "Charging tightens the field of view.");
            Assert.Less(distLow, 0f, "Charging pulls the camera in.");
            yield return Until(() => updraft.Charge >= 1f, 1.5f, "Full charge.");
            yield return new WaitForSeconds(0.2f); // 조임·당김의 스무딩이 따라붙을 시간
            Assert.Greater(feel.CurrentShake, shakeLow * 1.5f, "Shake intensifies with charge (compression).");
            Assert.Greater(rig.ShakeFrequency, freqLow, "Shake gets faster with charge.");
            Assert.Less(camera.fieldOfView, fovLow, "Field of view keeps tightening.");
            Assert.AreEqual(feel.maxShake, feel.CurrentShake, 0.0001f);
            Assert.AreEqual(baseFov + feel.zoomInDegrees, camera.fieldOfView, 0.6f);
            Assert.Greater(Deviation, 0.005f);
            Keys(); // 발사
            yield return Until(() => !updraft.IsCharging, 0.2f, "Launched.");
            yield return null; yield return null;
            Assert.Greater(rig.KickAmplitude, 0f, "Launch kicks the camera.");
            Assert.Greater(camera.fieldOfView, baseFov + 2f, "Launch punches the field of view outward.");
            yield return new WaitForSeconds(0.9f);
            Assert.AreEqual(0f, feel.CurrentShake, 0.0001f); Assert.AreEqual(0f, rig.KickAmplitude);
            Assert.AreEqual(baseFov, camera.fieldOfView, 0.1f, "Field of view returns to base.");
            Assert.AreEqual(0f, feel.CurrentDistanceOffset, 0.02f, "Distance returns to base.");
            Assert.Less(Deviation, 0.0001f);
        }

        [UnityTest] public IEnumerator DashWidensTheViewOnTheSpeedCurveAndSettlesWithoutMovingTheAim()
        {
            var dash = player.GetComponent<PlayerDashCameraFeel>();
            Assert.NotNull(dash, "Player.prefab carries a PlayerDashCameraFeel.");
            Assert.AreSame(motor, dash.motor); Assert.AreSame(rig, dash.rig);
            float baseFov = rig.BaseFieldOfView;
            Assert.AreEqual(baseFov, camera.fieldOfView, 0.001f);
            Assert.True(motor.TryDash().Succeeded);
            float peak = 0f, peakTime = 0f, elapsed = 0f;
            while (motor.IsDashing)
            {
                yield return null;
                elapsed += Time.deltaTime;
                if (camera.fieldOfView > peak) { peak = camera.fieldOfView; peakTime = elapsed; }
                Ray centre = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                Assert.Less(Vector3.Angle(centre.direction, camera.transform.forward), 0.01f, "Widening the view never moves the centre aim ray.");
            }
            Assert.That(peak, Is.InRange(baseFov + dash.widenDegrees * 0.4f, baseFov + dash.widenDegrees + 0.01f), "The dash widens the field of view up to widenDegrees.");
            Assert.Less(peakTime, motor.dashDuration * 0.7f, "The widest point comes with the fast start of the dash, not the end.");
            Assert.Less(camera.fieldOfView, baseFov + dash.widenDegrees * 0.75f, "By the end of the dash the view has narrowed again with the deceleration.");
            for (float t = 0f; t < 0.4f; t += Time.deltaTime)
            {
                yield return null;
                Assert.GreaterOrEqual(camera.fieldOfView, baseFov - 0.001f, "The return never undershoots the base field of view (no recoil).");
            }
            Assert.AreEqual(baseFov, camera.fieldOfView, 0.05f, "Field of view returns to base.");
            Assert.AreEqual(0f, dash.CurrentFovOffset, 0.05f);
        }

        [UnityTest] public IEnumerator CancelledChargeReleasesTheCameraQuietly()
        {
            player.GetComponent<PlayerEffects>().Apply(new UpdraftEffect());
            float baseFov = rig.BaseFieldOfView;
            Keys(Key.Space);
            yield return Until(() => updraft.Charge >= 0.5f, 1.5f, "Half charge.");
            Assert.Greater(feel.CurrentShake, 0f);
            updraft.Cancel(); Keys();
            yield return null; yield return null;
            Assert.AreEqual(0f, rig.KickAmplitude, "Cancel is not a launch: no kick.");
            Assert.LessOrEqual(camera.fieldOfView, baseFov + 0.01f, "Cancel never punches outward.");
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(0f, feel.CurrentShake, 0.0001f);
            Assert.AreEqual(baseFov, camera.fieldOfView, 0.1f);
            Assert.AreEqual(0f, feel.CurrentDistanceOffset, 0.02f);
        }
    }
}
#endif

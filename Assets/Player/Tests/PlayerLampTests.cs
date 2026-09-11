#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SandGuard.Player.Tests
{
    public sealed class PlayerLampTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        float originalCaptureDeltaTime;
        InputSettings previousInputSettings, testInputSettings;
        Keyboard keyboard;
        [SetUp] public void Setup()
        {
            originalCaptureDeltaTime = Time.captureDeltaTime;
            previousInputSettings = InputSystem.settings;
            testInputSettings = Object.Instantiate(previousInputSettings);
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testInputSettings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            InputSystem.settings = testInputSettings;
            keyboard = InputSystem.AddDevice<Keyboard>();
        }
        GameObject Track(GameObject value) { objects.Add(value); return value; }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = originalCaptureDeltaTime;
            InputSystem.RemoveDevice(keyboard);
            InputSystem.settings = previousInputSettings;
            Object.Destroy(testInputSettings);
            foreach (var value in objects) if (value != null) Object.Destroy(value);
            objects.Clear(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            yield return null;
        }

        [UnityTest] public IEnumerator BeltAnchorManaPauseTeleportAndDeath()
        {
            Time.timeScale = 1f;
            var floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(50f, 1f, 50f);
            Physics.SyncTransforms();
            var sun = Track(new GameObject("Lamp verification sun")).AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.6f;
            sun.transform.rotation = Quaternion.Euler(45f, 150f, 0f);
            var player = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Generated/Player.prefab")));
            player.GetComponent<PlayerInputReader>().captureCursor = false;
            yield return new WaitForSeconds(0.5f);
            var lamp = player.GetComponentInChildren<PlayerLampEquipment>();
            var motor = player.GetComponent<PlayerMotor>();
            var mana = player.GetComponent<PlayerManaWallet>();
            var animator = player.GetComponentInChildren<Animator>();
            motor.enabled = false; animator.enabled = false;
            lamp.ResetSway();
            Assert.False(lamp.allowHandAttachment);
            lamp.SetHeld(true); lamp.SetHandSuppressed(true); lamp.SetHandSuppressed(false);
            Assert.False(lamp.IsHeld);
            Assert.AreSame(lamp.beltSocket, lamp.lamp.parent);
            Vector3 socket = player.transform.InverseTransformPoint(lamp.beltSocket.position);
            Assert.Less(socket.z, -0.08f, "Lamp belongs behind the belt.");
            Assert.Less(socket.x, 0f, "Lamp belongs beside the bag on the left.");
            Assert.Greater(lamp.Brightness, 0.98f);
            Assert.True(lamp.lampLight.enabled);
            Capture(player, "full", false); Capture(player, "full-detail", true);

            Assert.True(mana.TrySpend(mana.MaxMana / 2));
            yield return new WaitForSeconds(0.8f);
            Assert.That(lamp.Brightness, Is.InRange(0.24f, 0.27f));
            Capture(player, "half", true);
            float pausedBrightness = lamp.Brightness;
            Time.timeScale = 0f;
            Assert.True(mana.TrySpend(mana.CurrentMana));
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(pausedBrightness, lamp.Brightness, 0.00001f);
            Time.timeScale = 1f;
            yield return new WaitForSeconds(1f);
            Assert.Less(lamp.Brightness, 0.001f);
            Assert.False(lamp.lampLight.enabled);
            Capture(player, "empty", true);
            mana.Gain(mana.MaxMana);
            yield return new WaitForSeconds(0.8f);
            Assert.Greater(lamp.Brightness, 0.98f);

            float largest = 0f;
            for (float elapsed = 0f; elapsed < 0.5f; elapsed += Time.deltaTime)
            {
                player.transform.position += Vector3.forward * (4f * Time.deltaTime);
                yield return null;
                largest = Mathf.Max(largest, lamp.SwayAngles.magnitude);
                Assert.Less(Vector3.Distance(lamp.lamp.TransformPoint(lamp.beltAttachOffset), lamp.beltSocket.position), 0.0001f);
                Assert.LessOrEqual(Mathf.Abs(lamp.SwayAngles.x), lamp.swayDegrees + 0.001f);
                Assert.LessOrEqual(Mathf.Abs(lamp.SwayAngles.z), lamp.swayDegrees + 0.001f);
            }
            Assert.Greater(largest, 0.5f, "Actual attachment acceleration must move the lamp.");
            yield return new WaitForSeconds(2f);
            Assert.Less(lamp.SwayAngles.magnitude, 0.3f, "A stationary attachment must settle.");
            motor.Teleport(player.transform.position + Vector3.right * 0.1f);
            Assert.AreEqual(Vector3.zero, lamp.SwayAngles, "Even a small teleport resets the pendulum.");
            yield return null;
            Assert.Less(lamp.SwayAngles.magnitude, 0.01f);
            player.GetComponent<PlayerHealth>().TakeDamage(new DamageInfo(10000f, "Enemy"));
            Assert.AreEqual(Vector3.zero, lamp.SwayAngles);
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(LifeState.Alive, player.GetComponent<PlayerHealth>().State);
            Assert.False(lamp.IsHeld);
            lamp.SetGlowing(false);
            Assert.False(lamp.lampLight.enabled);
            lamp.SetGlowing(true);
            yield return new WaitForSeconds(0.8f);
            Assert.Greater(lamp.Brightness, 0.98f);
            var walletSettings = new SerializedObject(mana);
            walletSettings.FindProperty("maxMana").intValue = 0;
            walletSettings.ApplyModifiedPropertiesWithoutUndo();
            mana.ResetWallet();
            yield return new WaitForSeconds(1f);
            Assert.Less(lamp.Brightness, 0.001f, "A zero-capacity wallet must not produce NaN or light.");
        }

        [UnityTest] public IEnumerator AbsorptionPreviewIsBoundedPausesAndReturnsToManaBrightness()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = 1f / 60f; // Render each stage of the 0.3-second pulse deterministically.
            var floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            floor.transform.position = new Vector3(0, -0.5f, 0); floor.transform.localScale = new Vector3(50, 1, 50);
            Physics.SyncTransforms();
            var sun = Track(new GameObject("Absorption verification sun")).AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.6f; sun.transform.rotation = Quaternion.Euler(45, 150, 0);
            var player = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Generated/Player.prefab")));
            player.GetComponent<PlayerInputReader>().captureCursor = false;
            yield return new WaitForSeconds(0.4f);
            var lamp = player.GetComponentInChildren<PlayerLampEquipment>();
            var mana = player.GetComponent<PlayerManaWallet>();
            mana.TrySpend(50);
            yield return new WaitForSeconds(0.8f);
            Capture(player, "absorption-before", true);
            // First Camera.Render can compile shaders and stall the frame past the whole pulse.
            yield return new WaitForSeconds(0.2f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.H));
            yield return new WaitForSeconds(0.1f);
            Assert.Greater(lamp.AbsorptionPulse, 0.85f, $"enabled={lamp.isActiveAndEnabled}, glowing={lamp.IsGlowing}, life={player.GetComponent<PlayerHealth>().State}, dt={Time.deltaTime}, rise={lamp.absorptionRiseTime}, fade={lamp.absorptionFadeTime}");
            Capture(player, "absorption-peak", true);
            Capture(player, "absorption-peak-wide", false);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            float paused = lamp.AbsorptionPulse;
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(paused, lamp.AbsorptionPulse);
            Time.timeScale = 1;
            for (int i = 0; i < 100; i++) lamp.PreviewManaAbsorption();
            yield return new WaitForSeconds(0.1f);
            Assert.LessOrEqual(lamp.AbsorptionPulse, 1f);
            yield return new WaitForSeconds(lamp.absorptionRiseTime + lamp.absorptionFadeTime + 0.1f);
            Assert.AreEqual(0f, lamp.AbsorptionPulse);
            Assert.AreEqual(50, mana.CurrentMana, "A visual preview does not grant mana.");
            Assert.AreEqual(0.25f, lamp.Brightness, 0.005f);
            Capture(player, "absorption-after", true);
            lamp.SetGlowing(false); lamp.PreviewManaAbsorption();
            yield return new WaitForSeconds(0.1f);
            Assert.AreEqual(0f, lamp.AbsorptionPulse); Assert.False(lamp.lampLight.enabled);
            lamp.SetGlowing(true); lamp.PreviewManaAbsorption();
            yield return new WaitForSeconds(0.1f);
            lamp.enabled = false;
            Assert.AreEqual(0f, lamp.AbsorptionPulse);
        }

        [UnityTest] public IEnumerator SandRootCoversFeetAndReleases()
        {
            Time.timeScale = 1f; Time.captureDeltaTime = 1f / 60f;
            var floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            floor.transform.position = new Vector3(0, -.5f, 0); floor.transform.localScale = new Vector3(30, 1, 30);
            var sun = Track(new GameObject("Sand verification sun")).AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.6f; sun.transform.rotation = Quaternion.Euler(45, -35, 0);
            Physics.SyncTransforms();
            var player = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Generated/Player.prefab")));
            player.GetComponent<PlayerInputReader>().captureCursor = false;
            yield return new WaitForSeconds(.4f);
            player.GetComponent<PlayerMotor>().enabled = false;
            player.GetComponentInChildren<Animator>().enabled = false;
            var prefab = Resources.Load<GameObject>("VFX/Prefabs/VFX_Sand_Root");
            Assert.NotNull(prefab);
            var fx = Track(Object.Instantiate(prefab, player.transform.position, Quaternion.identity, player.transform));
            yield return new WaitForSeconds(.7f);
            Assert.NotNull(fx.transform.Find("BindingMass/LeftAnkle"));
            Capture(player, "sand-root-bound", false, true);
            fx.SendMessage("Release", SendMessageOptions.RequireReceiver);
            yield return new WaitForSeconds(.5f);
            Assert.Less(fx.transform.Find("BindingMass").localScale.y, .5f);
            Capture(player, "sand-root-releasing", false, true);
            yield return new WaitForSeconds(.6f);
            Assert.True(fx == null);
            Capture(player, "sand-root-cleared", false, true);
        }

        void Capture(GameObject player, string name, bool detail, bool front = false)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var camera = Track(new GameObject("Lamp verification camera")).AddComponent<Camera>();
            camera.enabled = false; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.14f, 0.17f);
            camera.orthographic = true; camera.orthographicSize = detail ? 0.42f : 1.05f;
            camera.nearClipPlane = 0.01f; camera.farClipPlane = 100f;
            Vector3 center = player.transform.position + new Vector3(detail ? -0.13f : 0f, 1f, 0f);
            camera.transform.position = center + (front ? new Vector3(1.4f, .5f, 3f) : new Vector3(-1.4f, 0.3f, -3f));
            camera.transform.LookAt(center);
            var rt = new RenderTexture(1200, 1000, 24);
            var previous = RenderTexture.active;
            var texture = new Texture2D(1200, 1000, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 1200, 1000), 0, 0); texture.Apply();
                Directory.CreateDirectory("Logs/lamp-captures");
                File.WriteAllBytes("Logs/lamp-captures/" + name + ".png", texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null;
                rt.Release(); Object.Destroy(rt); Object.Destroy(texture);
            }
        }
    }
}
#endif

#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SandGuard.Player.Tests
{
    public sealed class PlayerDashVfxTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        float previousCapture;
        Material groundMaterial;
        GameObject Track(GameObject go) { objects.Add(go); return go; }
        [SetUp] public void Setup() { previousCapture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 60f; Time.timeScale = 1; }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1; Time.captureDeltaTime = previousCapture;
            foreach (var go in objects) if (go != null) Object.Destroy(go);
            objects.Clear(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (groundMaterial != null) Object.Destroy(groundMaterial);
            yield return null;
        }

        [UnityTest] public IEnumerator DashLaunchPauseFinishAndDisable()
        {
            var ground = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            ground.transform.position = new Vector3(0, -.5f, 0); ground.transform.localScale = new Vector3(50, 1, 50);
            groundMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            groundMaterial.SetColor("_BaseColor", new Color(.27f, .21f, .14f));
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;
            var sun = Track(new GameObject("Sun")).AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.2f; sun.transform.rotation = Quaternion.Euler(45, -30, 0);
            Physics.SyncTransforms();
            var player = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Generated/Player.prefab")));
            player.GetComponent<PlayerInputReader>().captureCursor = false;
            yield return new WaitForSeconds(.4f);
            var motor = player.GetComponent<PlayerMotor>(); var vfx = player.GetComponent<PlayerDashVfx>();
            Assert.NotNull(vfx); Assert.NotNull(vfx.launchPrefab); Assert.NotNull(vfx.airflowPrefab);
            Assert.True(motor.TryDash().Succeeded);
            var ring = Track(vfx.LaunchRing); var flow = Track(vfx.ActiveAirflow);
            Assert.NotNull(ring); Assert.NotNull(flow);
            Vector3 origin = ring.transform.position;
            Assert.Greater(Vector3.Dot(flow.transform.forward, motor.DashDirection), .999f);
            yield return new WaitForSeconds(.08f);
            Capture(player.transform.position, "launch");
            Assert.AreEqual(origin, ring.transform.position);
            Assert.Less(Vector3.Distance(flow.transform.position, player.transform.position + Vector3.up * vfx.bodyHeight), .001f);
            Time.timeScale = 0;
            yield return null; // Let the already-started particle simulation frame finish.
            var ps = flow.GetComponentsInChildren<ParticleSystem>()[1]; float pausedTime = ps.time;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.AreEqual(pausedTime, ps.time);
            Time.timeScale = 1;
            yield return new WaitForSeconds(.07f);
            Capture(player.transform.position, "airflow");
            yield return new WaitForSeconds(.25f);
            Assert.IsNull(vfx.ActiveAirflow);
            yield return new WaitForSeconds(.4f);
            Assert.True(flow == null); Assert.True(ring == null);
            yield return new WaitForSeconds(motor.DashCooldownRemaining + .05f);
            Assert.True(motor.TryDash().Succeeded);
            var cancelled = Track(vfx.ActiveAirflow);
            vfx.enabled = false;
            yield return null;
            Assert.True(cancelled == null);
        }

        void Capture(Vector3 center, string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) Assert.Fail("Evidence capture needs a graphics device.");
            var camera = Track(new GameObject("Dash evidence camera")).AddComponent<Camera>();
            camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 1.55f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.19f, .22f, .27f);
            center += new Vector3(0, .85f, -.65f);
            camera.transform.position = center + new Vector3(4f, 1.2f, 2.5f); camera.transform.LookAt(center);
            var rt = new RenderTexture(1280, 800, 24); var tex = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            var old = RenderTexture.active;
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); tex.Apply();
                Directory.CreateDirectory("Logs/dash-vfx-captures"); File.WriteAllBytes("Logs/dash-vfx-captures/" + name + ".png", tex.EncodeToPNG());
            }
            finally { RenderTexture.active = old; camera.targetTexture = null; rt.Release(); Object.Destroy(rt); Object.Destroy(tex); }
        }
    }
}
#endif

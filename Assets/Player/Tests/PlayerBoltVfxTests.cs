#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SandGuard.Player.Tests
{
    public sealed class PlayerBoltVfxTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        GameObject Track(GameObject value) { objects.Add(value); return value; }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            foreach (var value in objects) if (value != null) Object.Destroy(value);
            objects.Clear();
            yield return null;
        }

        [UnityTest] public IEnumerator WiredBoltHitsOncePlaysImpactAndFinishesTrail()
        {
            Time.timeScale = 1f;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Generated/PlayerBolt.prefab");
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Generated/Player.prefab");
            Assert.AreSame(asset.GetComponent<PlayerProjectile>(), player.GetComponent<PlayerBasicAttack>().projectilePrefab);
            var targetObject = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            targetObject.transform.position = new Vector3(0f, 1f, 12f);
            var target = targetObject.AddComponent<PlayerTestTarget>();
            var sun = Track(new GameObject("Bolt verification sun")).AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.5f;
            sun.transform.rotation = Quaternion.Euler(45, -35, 0);
            Physics.SyncTransforms();
            var bolt = Track(Object.Instantiate(asset, Vector3.up, Quaternion.identity)).GetComponent<PlayerProjectile>();
            Assert.NotNull(bolt.impactPrefab);
            Assert.NotNull(bolt.visualRoot);
            Assert.IsNull(bolt.visualRoot.Find("BoltVisual"));
            Assert.Greater(bolt.visualRoot.GetComponentsInChildren<ParticleSystem>().Length, 2);
            Assert.AreEqual(45f, bolt.speed, "VFX wiring must preserve projectile tuning.");
            var tail = Track(bolt.visualRoot.gameObject);
            int hits = 0; bolt.Hit += _ => hits++;
            bolt.Launch(null, "Ally", 10f, Vector3.forward, Vector3.up);
            yield return new WaitForSeconds(0.08f);
            Assert.NotNull(bolt);
            Capture("flight", new Vector3(0, 1, 4));
            for (float elapsed = 0f; bolt != null && elapsed < 1f; elapsed += Time.deltaTime) yield return null;
            Assert.IsTrue(bolt == null);
            Assert.AreEqual(1, target.HitCount); Assert.AreEqual(1, hits);
            var impact = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "VFX_ManaBolt_Impact(Clone)");
            Assert.NotNull(impact); Track(impact.gameObject);
            Assert.Less(Vector3.Distance(impact.position, new Vector3(0, 1, 11.5f)), 0.2f);
            Assert.IsTrue(tail != null, "The tail should outlive the projectile.");
            Assert.IsNull(tail.transform.parent);
            Assert.IsTrue(tail.GetComponentsInChildren<Light>().All(l => !l.enabled));
            Assert.IsTrue(tail.GetComponentsInChildren<MeshRenderer>().All(r => !r.enabled));
            Assert.IsTrue(tail.GetComponentsInChildren<ParticleSystem>().All(p => !p.isEmitting));
            yield return new WaitForSeconds(0.06f);
            Capture("impact", new Vector3(0, 1, 11.5f));
            yield return new WaitForSeconds(0.8f);
            Assert.IsTrue(tail == null, "Detached particle effects must be cleaned up.");
        }

        void Capture(string name, Vector3 center)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var camera = Track(new GameObject("Bolt verification camera")).AddComponent<Camera>();
            camera.enabled = false; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.2f, .18f, .15f);
            camera.orthographic = true; camera.orthographicSize = 2f;
            camera.transform.position = center + new Vector3(3, 1.5f, -5);
            camera.transform.LookAt(center);
            var rt = new RenderTexture(1000, 700, 24);
            var previous = RenderTexture.active;
            var texture = new Texture2D(1000, 700, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 1000, 700), 0, 0); texture.Apply();
                Directory.CreateDirectory("Logs/bolt-vfx-captures");
                File.WriteAllBytes("Logs/bolt-vfx-captures/" + name + ".png", texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null; rt.Release();
                Object.Destroy(rt); Object.Destroy(texture);
            }
        }
    }
}
#endif

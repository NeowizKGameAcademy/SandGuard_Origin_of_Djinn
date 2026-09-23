#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DesertTower.VFX.Tests
{
    public sealed class BlueDestructionTests
    {
        GameObject source, effect;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (source) Object.Destroy(source);
            if (effect) Object.Destroy(effect);
            if (PrefabPool.Exists) Object.Destroy(PrefabPool.Instance.gameObject);
            yield return null;
        }

        static int Bursts(GameObject root)
        {
            int count = 0;
            foreach (Transform child in root.transform)
                if (child.name.StartsWith("Blue Pop") || child.name == "Final Blue Break") count++;
            return count;
        }

        [UnityTest]
        public IEnumerator CobraDeathKeepsItsProxyUntilBlueChainFinishes()
        {
            source = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/2.Model/Prefabs/Tower_Cobra.prefab"));
            var death = source.GetComponentInChildren<VfxDestructionOnDeath>();
            Assert.That(death, Is.Not.Null);
            Assert.That(death.GetComponent<IDamageable>().TakeDamage(new DamageInfo(10000000f, "Enemy")).WasKilled, Is.True);
            var ctrl = Object.FindFirstObjectByType<VfxCobraDestruction>();
            Assert.That(ctrl, Is.Not.Null);
            effect = ctrl.gameObject;
            Assert.That(Bursts(effect), Is.EqualTo(5));
            Assert.That(ctrl.BurstTime, Is.EqualTo(0.72f).Within(0.001f));
            Assert.That(ctrl.Proxy.activeSelf, Is.True);
            Object.Destroy(source);
            yield return new WaitForSeconds(0.8f);
            Assert.That(ctrl.Proxy.activeSelf, Is.False);
            Assert.That(ctrl.Pieces[0].gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator LevelCoreUsesAllMeshesAndCapturesAnimatedPose()
        {
            source = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/2.Model/Prefabs/New Core.prefab"));
            foreach (var animator in source.GetComponentsInChildren<Animator>()) animator.enabled = false;
            source.transform.SetPositionAndRotation(new Vector3(3f, 54f, -2f), Quaternion.Euler(0f, 37f, 0f));
            source.transform.localScale = Vector3.one * 0.6f;
            source.transform.Find("Ring Inner").localRotation = Quaternion.Euler(25f, 48f, 12f);
            source.transform.Find("Core Piece Big").localPosition += Vector3.up * 1.2f;
            var death = source.GetComponent<VfxDestructionOnDeath>();
            Assert.That(death, Is.Not.Null);
            effect = death.Play();
            var ctrl = effect.GetComponent<VfxCoreDestruction>();
            Assert.That(ctrl.WholeModel, Is.True);
            Assert.That(ctrl.MeshPaths.Length, Is.EqualTo(7), "Crystal, three floating pieces, three rings.");
            Assert.That(ctrl.Shards.Length, Is.GreaterThanOrEqualTo(16), "The firework burst needs small crystal fragments.");
            foreach (int meshIndex in ctrl.ShardMeshIndices)
                Assert.That(ctrl.MeshPaths[meshIndex], Is.EqualTo("Core Crystal/Mesh"));
            var sparks = effect.transform.Find("Crystal Firework Sparks").GetComponent<ParticleSystem>();
            Assert.That(sparks.main.startDelay.constant, Is.EqualTo(ctrl.BurstTime).Within(0.001f));
            Assert.That(Bursts(effect), Is.EqualTo(5));
            for (int i = 0; i < ctrl.MeshPaths.Length; i++)
            {
                var mesh = source.transform.Find(ctrl.MeshPaths[i]);
                Assert.That(Vector3.Distance(mesh.position, ctrl.ProxyMeshes[i].position), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(mesh.rotation, ctrl.ProxyMeshes[i].rotation), Is.LessThan(0.05f));
                Assert.That(ctrl.ProxyMeshes[i].GetComponent<MeshFilter>().sharedMesh,
                    Is.SameAs(mesh.GetComponent<MeshFilter>().sharedMesh));
            }
            foreach (var renderer in source.GetComponentsInChildren<Renderer>(true)) Assert.That(renderer.enabled, Is.False);
            foreach (var light in source.GetComponentsInChildren<Light>(true)) Assert.That(light.enabled, Is.False);
            yield return new WaitForSeconds(0.8f);
            Assert.That(ctrl.ProxyMeshes[0].gameObject.activeSelf, Is.False, "Only the crystal proxy disappears.");
            Assert.That(ctrl.Shards[0].gameObject.activeSelf, Is.True);
            ctrl.Tick(1f);
            for (int i = 0; i < ctrl.Shards.Length; i++)
                Assert.That(ctrl.Shards[i].gameObject.activeSelf,
                    Is.EqualTo(ctrl.MeshPaths[ctrl.ShardMeshIndices[i]] == "Core Crystal/Mesh"));
            for (int i = 1; i < ctrl.ProxyMeshes.Length; i++)
            {
                var part = ctrl.ProxyMeshes[i];
                Assert.That(part.gameObject.activeSelf, Is.True, "Surrounding pieces stay intact.");
                Assert.That(part.GetComponent<Renderer>().bounds.min.y,
                    Is.InRange(source.transform.position.y, source.transform.position.y + 0.3f),
                    "Intact debris settles on the core's ground plane.");
            }
            PrefabPool.Release(effect);
            foreach (var renderer in source.GetComponentsInChildren<Renderer>(true)) Assert.That(renderer.enabled, Is.False);
            ctrl.RestoreTarget();
            Assert.That(source.transform.Find("Core Crystal/Mesh").GetComponent<Renderer>().enabled, Is.True);
            ctrl.Restart();
            ctrl.BindTarget(source.transform);
            for (int i = 0; i < ctrl.ProxyMeshes.Length; i++)
            {
                Assert.That(ctrl.ProxyMeshes[i].gameObject.activeSelf, Is.True);
                Assert.That(Vector3.Distance(ctrl.ProxyMeshes[i].position,
                    source.transform.Find(ctrl.MeshPaths[i]).position), Is.LessThan(0.001f));
            }
        }
    }
}
#endif

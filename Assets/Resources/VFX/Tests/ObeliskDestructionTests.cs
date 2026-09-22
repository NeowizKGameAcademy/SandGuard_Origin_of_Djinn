#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DesertTower.VFX.Tests
{
    public sealed class ObeliskDestructionTests
    {
        GameObject tower;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (tower) Object.Destroy(tower);
            foreach (var effect in Object.FindObjectsByType<VfxObeliskDestruction>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(effect.gameObject);
            if (PrefabPool.Exists) Object.Destroy(PrefabPool.Instance.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DeathPlaysFiveBurstsAndSurvivesTowerRemoval()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/2.Model/Prefabs/Tower_Obelisk.prefab");
            tower = Object.Instantiate(prefab);
            tower.transform.SetPositionAndRotation(new Vector3(7f, 0f, -4f), Quaternion.Euler(0f, 35f, 0f));
            tower.transform.localScale = Vector3.one * 0.5f;
            yield return null;
            var listener = tower.GetComponentInChildren<VfxDestructionOnDeath>();
            Assert.That(listener, Is.Not.Null, "Current build-catalog prefab must have a death listener.");
            var damageable = listener.GetComponent<IDamageable>();
            Assert.That(damageable.TakeDamage(new DamageInfo(10000000f, "Enemy")).WasKilled, Is.True);
            var effect = Object.FindFirstObjectByType<VfxObeliskDestruction>();
            Assert.That(effect, Is.Not.Null);
            Assert.That(effect.transform.parent, Is.Null);
            Assert.That(Vector3.Distance(effect.transform.position, listener.transform.position), Is.LessThan(0.01f));
            Assert.That(Vector3.Distance(effect.transform.localScale, Vector3.one * 0.5f), Is.LessThan(0.001f));
            Assert.That(effect.Proxy.activeSelf, Is.True);
            Assert.That(effect.Pieces.Length, Is.GreaterThanOrEqualTo(6));
            int bursts = 0;
            foreach (Transform child in effect.transform)
                if (child.name.StartsWith("Blue Pop") || child.name == "Final Blue Break") bursts++;
            Assert.That(bursts, Is.EqualTo(5));
            foreach (var renderer in listener.Target.GetComponentsInChildren<Renderer>(true))
                Assert.That(renderer.enabled, Is.False, "Only the VFX proxy renders after death.");
            Object.Destroy(tower);
            yield return new WaitForSeconds(0.85f);
            Assert.That(effect, Is.Not.Null, "The VFX outlives removal of the gameplay object.");
            Assert.That(effect.Proxy.activeSelf, Is.False);
            Assert.That(effect.Pieces[0].gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator PooledReplayRestoresTheProxyAndReplaysParticles()
        {
            var prefab = Resources.Load<GameObject>("VFX/Prefabs/VFX_Obelisk_Destruction");
            var first = PrefabPool.Spawn(prefab, Vector3.zero, Quaternion.identity);
            var effect = first.GetComponent<VfxObeliskDestruction>();
            effect.Tick(3.9f);
            Assert.That(effect.Pieces[0].localScale, Is.EqualTo(Vector3.zero));
            PrefabPool.Release(first);
            yield return null;
            var replay = PrefabPool.Spawn(prefab, Vector3.zero, Quaternion.identity);
            Assert.That(replay, Is.SameAs(first));
            Assert.That(effect.Proxy.activeSelf, Is.True);
            Assert.That(effect.Pieces[0].gameObject.activeSelf, Is.False);
            foreach (var ps in replay.GetComponentsInChildren<ParticleSystem>()) Assert.That(ps.isPlaying, Is.True);
            effect.Tick(0.8f);
            Assert.That(effect.Pieces[0].localScale, Is.EqualTo(Vector3.one));
        }
    }
}
#endif

#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DesertTower.VFX.Tests
{
    public sealed class SummonerTowerDestructionTests
    {
        GameObject tower, effect;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (tower) Object.Destroy(tower);
            if (effect) Object.Destroy(effect);
            if (PrefabPool.Exists) Object.Destroy(PrefabPool.Instance.gameObject);
            yield return null;
        }

        [UnityTest] public IEnumerator AnubisDeathAndReplay() => Verify("Anubis");
        [UnityTest] public IEnumerator CoffinDeathAndReplay() => Verify("Coffin");

        IEnumerator Verify(string kind)
        {
            tower = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/2.Model/Prefabs/Tower_" + kind + ".prefab"));
            tower.transform.SetPositionAndRotation(new Vector3(5f, 0f, -3f), Quaternion.Euler(0f, 35f, 0f));
            tower.transform.localScale = Vector3.one * 0.6f;
            var death = tower.GetComponentInChildren<VfxDestructionOnDeath>();
            Assert.That(death, Is.Not.Null);
            var prefab = death.Prefab;
            Assert.That(prefab.name, Is.EqualTo("VFX_" + kind + "_Destruction"));
            Assert.That(death.GetComponent<IDamageable>().TakeDamage(new DamageInfo(10000000f, "Enemy")).WasKilled, Is.True);
            var ctrl = Object.FindFirstObjectByType<VfxObeliskDestruction>();
            Assert.That(ctrl, Is.Not.Null);
            effect = ctrl.gameObject;
            Assert.That(Vector3.Distance(effect.transform.position, death.transform.position), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(effect.transform.localScale, Vector3.one * 0.6f), Is.LessThan(0.001f));
            Assert.That(ctrl.Proxy.activeSelf, Is.True);
            Assert.That(ctrl.Pieces.Length, Is.GreaterThanOrEqualTo(4));
            int bursts = 0;
            foreach (Transform child in effect.transform)
                if (child.name.StartsWith("Blue Pop") || child.name == "Final Blue Break") bursts++;
            Assert.That(bursts, Is.EqualTo(5));
            foreach (var renderer in death.Target.GetComponentsInChildren<Renderer>(true)) Assert.That(renderer.enabled, Is.False);
            Object.Destroy(tower);
            yield return new WaitForSeconds(0.85f);
            Assert.That(ctrl.Proxy.activeSelf, Is.False, "The detached effect survives tower removal.");
            Assert.That(ctrl.Pieces[0].gameObject.activeSelf, Is.True);
            ctrl.Tick(4f);
            Assert.That(ctrl.Pieces[0].localScale, Is.EqualTo(Vector3.zero));
            PrefabPool.Release(effect);
            var replay = PrefabPool.Spawn(prefab, Vector3.zero, Quaternion.identity);
            Assert.That(replay, Is.SameAs(effect));
            Assert.That(ctrl.Proxy.activeSelf, Is.True);
            foreach (var ps in replay.GetComponentsInChildren<ParticleSystem>()) Assert.That(ps.isPlaying, Is.True);
            ctrl.Tick(0.8f);
            Assert.That(ctrl.Pieces[0].localScale, Is.EqualTo(Vector3.one));
        }
    }
}
#endif

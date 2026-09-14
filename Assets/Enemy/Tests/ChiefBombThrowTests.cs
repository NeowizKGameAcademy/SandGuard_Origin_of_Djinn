#if UNITY_EDITOR
using System;
using System.Collections;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SandGuard.Enemy.Tests
{
    public sealed class ChiefBombThrowTests
    {
        GameObject ground, chief, targetObject, friend;
        ChiefBombThrowSkill skill;
        EnemyBrain brain;
        EnemyTestTarget target;
        [SetUp] public void Setup()
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0, -.5f, 0); ground.transform.localScale = new Vector3(30, 1, 30);
            var nav = ground.AddComponent<NavMeshSurface>(); nav.collectObjects = CollectObjects.Children;
            nav.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders; nav.BuildNavMesh();
            chief = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Generated/Enemy_Chief.prefab"));
            brain = chief.GetComponent<EnemyBrain>(); brain.AIEnabled = false;
            skill = chief.GetComponent<ChiefBombThrowSkill>(); Assert.NotNull(skill);
            targetObject = GameObject.CreatePrimitive(PrimitiveType.Cube); targetObject.transform.position = new Vector3(0, 1, 5);
            target = targetObject.AddComponent<EnemyTestTarget>(); target.maxHealth = 10000;
            targetObject.AddComponent<SphereCollider>(); // 복수 충돌체에도 한 번만 피해
            Physics.SyncTransforms();
        }
        IEnumerator Until(Func<bool> condition, string message, float limit = 6)
        {
            float end = Time.time + limit;
            while (!condition() && Time.time < end) yield return null;
            Assert.True(condition(), message);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Object.Destroy(chief); Object.Destroy(targetObject); Object.Destroy(ground); if (friend) Object.Destroy(friend);
            foreach (var bomb in Object.FindObjectsByType<ChiefBombProp>(FindObjectsSortMode.None)) Object.Destroy(bomb.gameObject);
            yield return null;
        }
        [UnityTest] public IEnumerator AnimationReleasesBombAndExplosionHitsOnceAfterCasterDeath()
        {
            brain.AIEnabled = true;
            Assert.True(skill.TryUse(target));
            Assert.NotNull(skill.HeldBomb); Assert.False(skill.HeldBomb.hitCollider.enabled);
            Assert.False(chief.GetComponent<ChiefGoldenShieldSkill>().TryUse(target), "No simultaneous shield cast");
            Assert.False(skill.TryUse(target), "No duplicate throw");
            var prop = skill.HeldBomb;
            float width = prop.GetComponentInChildren<MeshRenderer>().bounds.size.magnitude;
            Assert.Greater(width, .05f, "Hand parenting preserves bomb size");
            yield return Until(() => skill.ThrowCount == 1, "Real Throw animation event releases bomb");
            CaptureThrow();
            Assert.Null(skill.HeldBomb); Assert.Null(prop.transform.parent); Assert.False(prop.body.isKinematic);
            Assert.True(prop.GetComponent<ChiefBombProjectile>().IsArmed);
            chief.GetComponent<EnemyHealth>().TakeDamage(new DamageInfo(100000, "World"));
            Assert.False(skill.IsCasting);
            yield return Until(() => target.HitCount > 0, "Released bomb survives caster death and explodes");
            Assert.AreEqual(1, target.HitCount); Assert.AreEqual(target.maxHealth - skill.damage, target.CurrentHealth, .001f);
        }
        [UnityTest] public IEnumerator CancelBeforeReleaseAndPoolReuseLeaveNoHeldBomb()
        {
            brain.AIEnabled = true; Assert.True(skill.TryUse(target));
            chief.SetActive(false);
            Assert.False(skill.IsCasting); Assert.Null(skill.HeldBomb);
            chief.SetActive(true); brain.ResetForReuse();
            Assert.AreEqual(0, skill.ThrowCount); Assert.AreEqual(0, skill.CooldownRemaining);
            Assert.True(skill.TryUse(target));
            skill.enabled = false;
            yield return null;
            Assert.Null(skill.HeldBomb); Assert.False(skill.IsCasting);
            Assert.AreEqual(0, Object.FindObjectsByType<ChiefBombProp>(FindObjectsSortMode.None).Length);
        }
        [UnityTest] public IEnumerator BrainUsesShieldThenBombAndExplosionIgnoresFriendlies()
        {
            var shield = chief.GetComponent<ChiefGoldenShieldSkill>(); shield.summonDuration = .1f;
            brain.AIEnabled = true; brain.Think();
            Assert.True(shield.IsCasting); Assert.False(skill.IsCasting);
            yield return Until(() => skill.IsCasting, "Brain chooses bomb after shield summon");
            yield return Until(() => skill.ThrowCount == 1, "AI throw releases through animation");
            friend = GameObject.CreatePrimitive(PrimitiveType.Cube); friend.transform.position = new Vector3(.8f, 1, 5);
            var ally = friend.AddComponent<EnemyTestTarget>(); ally.factionId = "Enemy";
            Physics.SyncTransforms();
            yield return Until(() => target.HitCount > 0, "AI bomb damages target");
            Assert.AreEqual(0, ally.HitCount);
            yield return Until(() => !skill.IsCasting, "Throw recovery finishes");
            Assert.Greater(skill.CooldownRemaining, 0); Assert.False(skill.TryUse(target));
        }
        void CaptureThrow()
        {
            var cameraObject = new GameObject("Throw test camera");
            var lightObject = new GameObject("Throw test light");
            var camera = cameraObject.AddComponent<Camera>();
            var bounds = new Bounds(chief.transform.position + Vector3.up, Vector3.one);
            foreach (var renderer in chief.GetComponentsInChildren<SkinnedMeshRenderer>()) bounds.Encapsulate(renderer.bounds);
            camera.transform.position = bounds.center + new Vector3(5, 2, 5);
            camera.transform.LookAt(bounds.center);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .1f, .14f);
            camera.orthographic = true; camera.orthographicSize = bounds.extents.y * 1.3f + .2f;
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 2;
            light.transform.rotation = Quaternion.Euler(35, -30, 0);
            var rt = new RenderTexture(1000, 800, 24); camera.targetTexture = rt;
            var previous = RenderTexture.active;
            var texture = new Texture2D(1000, 800, TextureFormat.RGB24, false);
            try
            {
                camera.Render(); RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, 1000, 800), 0, 0); texture.Apply();
                System.IO.Directory.CreateDirectory("Docs/vfx-preview/ChiefBombSkill");
                System.IO.File.WriteAllBytes("Docs/vfx-preview/ChiefBombSkill/Throw_Release.png", texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null; rt.Release();
                Object.Destroy(rt); Object.Destroy(texture); Object.Destroy(cameraObject); Object.Destroy(lightObject);
            }
        }
    }
}
#endif

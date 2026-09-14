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
    public sealed class ChiefGoldenShieldTests
    {
        GameObject ground, chief, targetObject;
        ChiefGoldenShieldSkill skill;
        EnemyHealth health;
        EnemyTestTarget target;
        EnemyBrain brain;
        [SetUp] public void Setup()
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0, -.5f, 0);
            ground.transform.localScale = new Vector3(30, 1, 30);
            var nav = ground.AddComponent<NavMeshSurface>();
            nav.collectObjects = CollectObjects.Children;
            nav.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            nav.BuildNavMesh();
            chief = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Generated/Enemy_Chief.prefab"));
            var bombSkill = chief.GetComponent<ChiefBombThrowSkill>();
            if (bombSkill) bombSkill.enabled = false; // 방패/근접 회귀 검증은 폭탄과 독립적으로 실행한다.
            skill = chief.GetComponent<ChiefGoldenShieldSkill>(); Assert.NotNull(skill);
            skill.summonDuration = .1f; skill.activeDuration = .5f; skill.cooldown = .2f;
            health = chief.GetComponent<EnemyHealth>(); brain = chief.GetComponent<EnemyBrain>();
            brain.AIEnabled = false; // 준비 프레임에서 먼저 자동 발동하지 않도록 한다.
            targetObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            targetObject.transform.position = new Vector3(0, 1, 2);
            target = targetObject.AddComponent<EnemyTestTarget>(); target.maxHealth = 10000;
            Physics.SyncTransforms();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Object.Destroy(chief); Object.Destroy(targetObject); Object.Destroy(ground);
            yield return null;
        }
        static DamageInfo Hit(Vector3 direction, float amount = 2)
            => new DamageInfo(amount, "Ally", hitDirection: direction);
        IEnumerator Until(Func<bool> condition, string message)
        {
            float deadline = Time.time + 4;
            while (!condition() && Time.time < deadline) yield return null;
            Assert.True(condition(), message);
        }
        [UnityTest] public IEnumerator BrainCastsBlocksThenResumesMeleeAndRecasts()
        {
            brain.AIEnabled = true;
            Assert.False(skill.shield.enabled);
            brain.Think();
            Assert.AreEqual(ChiefGoldenShieldSkill.Phase.Summoning, skill.State);
            Assert.False(chief.GetComponent<EnemyMeleeAttack>().IsAttacking);
            Assert.True(health.TakeDamage(Hit(Vector3.back)).WasApplied, "Windup is vulnerable");
            yield return Until(() => skill.shield.enabled, "Shield activates");
            float hp = health.CurrentHealth;
            Assert.AreEqual(DamageStatus.Protected, health.TakeDamage(Hit(Vector3.back)).Status);
            Assert.AreEqual(hp, health.CurrentHealth);
            Assert.True(health.TakeDamage(Hit(Vector3.left)).WasApplied);
            Assert.True(health.TakeDamage(Hit(Vector3.forward)).WasApplied);
            Assert.True(health.TakeDamage(new DamageInfo(1, "World")).WasApplied);
            Assert.True(skill.Visual.IsVisible);
            Assert.False(skill.TryUse(target), "Cannot overlap casts");
            yield return Until(() => skill.State == ChiefGoldenShieldSkill.Phase.Dismissing, "Shield expires");
            Assert.False(skill.shield.enabled);
            Assert.True(health.TakeDamage(Hit(Vector3.back)).WasApplied);
            yield return Until(() => target.HitCount > 0, "Melee resumes after summon");
            yield return Until(() => skill.CastCount >= 2, "AI casts again after cooldown");
        }
        [UnityTest] public IEnumerator DeathAndReuseClearShieldAndResetCooldown()
        {
            brain.AIEnabled = true;
            Assert.True(skill.TryUse(target));
            yield return Until(() => skill.shield.enabled, "Shield activates");
            health.ReleaseHandler = _ => chief.SetActive(false);
            health.TakeDamage(Hit(Vector3.forward, 100000));
            Assert.False(skill.shield.enabled);
            Assert.False(skill.Visual.gameObject.activeSelf);
            Assert.False(skill.TryUse(target));
            chief.SetActive(false);
            health.ResetForReuse(); brain.ResetForReuse(); chief.SetActive(true);
            Assert.AreEqual(ChiefGoldenShieldSkill.Phase.Ready, skill.State);
            Assert.AreEqual(0, skill.CastCount);
            Assert.True(skill.TryUse(target));
            Assert.AreEqual(1, chief.GetComponentsInChildren<DesertTower.VFX.VfxGoldenShield>(true).Length);
            brain.AIEnabled = false;
            yield return null;
            Assert.False(skill.shield.enabled);
            Assert.False(skill.Visual.gameObject.activeSelf);
        }
        [UnityTest] public IEnumerator InvalidTargetsAndDisabledSkillCannotCast()
        {
            brain.AIEnabled = true;
            Assert.False(skill.TryUse(null));
            target.factionId = health.FactionId;
            Assert.False(skill.TryUse(target));
            target.factionId = "Ally"; targetObject.transform.position = Vector3.forward * 20;
            Assert.False(skill.TryUse(target));
            targetObject.transform.position = new Vector3(0, 1, 2);
            Assert.True(skill.TryUse(target));
            skill.enabled = false;
            Assert.False(skill.shield.enabled); Assert.False(skill.Visual.gameObject.activeSelf);
            Assert.False(skill.TryUse(target));
            yield return null;
        }
    }
}
#endif

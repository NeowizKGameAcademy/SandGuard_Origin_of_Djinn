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
            skill.healthThreshold = 1f; // 방패 동작 자체는 시전 체력 조건과 떼어 검증한다. 조건은 전용 테스트가 본다.
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
        [UnityTest] public IEnumerator BrainCastsBlocksThenResumesMeleeAndIsNotRecast()
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
            // 한 생애에 한 번만 쓴다. 쿨다운(0.2초)이 몇 번 지나도 다시 소환하지 않는다.
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(1, skill.CastCount, "쿨다운 뒤에도 다시 쓰지 않는다");
            Assert.True(skill.IsSpent);
            Assert.False(skill.TryUse(target));
        }
        [Test] public void ChiefPrefabShieldLastsTwentySecondsAndBothSkillsAreUsedOnce()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Generated/Enemy_Chief.prefab");
            var shieldRules = prefab.GetComponent<ChiefGoldenShieldSkill>();
            var bombRules = prefab.GetComponent<ChiefBombThrowSkill>();
            Assert.AreEqual(20f, shieldRules.activeDuration, "황금 방패는 20초 유지된다");
            Assert.AreEqual(1, shieldRules.maxUses, "황금 방패는 한 번만 쓴다");
            Assert.AreEqual(0.7f, shieldRules.healthThreshold, "황금 방패는 체력이 70% 이하일 때 쓴다");
            Assert.AreEqual(1, bombRules.maxUses, "철거 폭탄은 한 번만 던진다");
        }

        [UnityTest] public IEnumerator ShieldWaitsUntilSeventyPercentHealth()
        {
            skill.healthThreshold = 0.7f;
            brain.AIEnabled = true;
            float max = health.MaxHealth;
            Assert.False(skill.TryUse(target), "체력이 가득하면 시전하지 않는다");
            brain.Think();
            Assert.AreEqual(ChiefGoldenShieldSkill.Phase.Ready, skill.State, "브레인도 체력이 충분하면 방패를 꺼내지 않는다");

            health.TakeDamage(new DamageInfo(max * 0.2f, "World"));
            Assert.Greater(health.CurrentHealth, max * 0.7f);
            Assert.False(skill.IsHealthLowEnough);
            Assert.False(skill.TryUse(target), "체력이 70%보다 높으면 아직 시전하지 않는다");

            health.TakeDamage(new DamageInfo(max * 0.15f, "World"));
            Assert.LessOrEqual(health.CurrentHealth, max * 0.7f);
            chief.GetComponent<EnemyMeleeAttack>().Cancel(); // 첫 판단에서 시작한 휘두르기는 시전을 막으므로 끊는다
            brain.Think();
            Assert.AreEqual(ChiefGoldenShieldSkill.Phase.Summoning, skill.State, "체력이 70% 이하가 되면 다음 판단에서 방패를 꺼낸다");
            yield return null;
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

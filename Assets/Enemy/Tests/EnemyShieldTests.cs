#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SandGuard.Enemy.Tests
{
    /// <summary>방패병 정면 방어: 받는 쪽 보정 훅(IDamageModifier)으로만 동작하고 EnemyHealth는 규칙을 모른다.</summary>
    public sealed class EnemyShieldTests
    {
        readonly List<GameObject> objects = new List<GameObject>();

        GameObject Enemy(string prefab, Vector3 position, float yaw)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Generated/" + prefab + ".prefab");
            Assert.NotNull(asset, "Run SandGuard/Enemy/Connect Combat Art first.");
            var value = Object.Instantiate(asset, position, Quaternion.Euler(0f, yaw, 0f));
            objects.Add(value);
            value.GetComponent<EnemyBrain>().AIEnabled = false; // 제자리에서 방향만 본다
            return value;
        }

        static DamageInfo From(Vector3 travel, float amount = 2f, Vector3? point = null)
            => new DamageInfo(amount, "Ally", causeId: "test", hitPosition: point ?? Vector3.up, hitDirection: travel);

        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach (var value in objects) if (value != null) Object.Destroy(value);
            objects.Clear();
            foreach (var clone in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                if (clone.transform.root.name.StartsWith("VFX_")) Object.Destroy(clone.transform.root.gameObject);
            yield return null;
        }

        [UnityTest] public IEnumerator ShieldGuardBlocksFrontalHitsButNotFlankOrDirectionlessDamage()
        {
            var enemy = Enemy("Enemy_ShieldGuard", Vector3.zero, 0f); // 정면 +Z
            yield return null;
            var shield = enemy.GetComponent<EnemyShield>();
            var health = enemy.GetComponent<EnemyHealth>();
            Assert.NotNull(shield, "Enemy_ShieldGuard carries EnemyShield (SandGuard/Enemy/Connect Shield Guard Front Block).");
            Assert.NotNull(shield.guardVfx, "The front guard VFX is assigned.");
            int damaged = 0; health.Damaged += _ => damaged++;
            float max = health.CurrentHealth;

            // 정면(+Z)에서 쏜 공격은 -Z로 날아온다.
            var front = health.TakeDamage(From(Vector3.back, point: new Vector3(0f, 1f, .4f)));
            Assert.AreEqual(DamageStatus.Protected, front.Status, "A frontal hit is blocked.");
            Assert.AreEqual(max, health.CurrentHealth);
            Assert.AreEqual(0, damaged, "A blocked hit raises no damage event (no hit flash).");
            Assert.AreEqual(1, shield.GuardCount);
            var vfx = GameObject.Find("VFX_Shield_Front_Guard(Clone)");
            Assert.NotNull(vfx, "Blocking plays the front guard VFX.");
            Assert.Greater(Vector3.Dot(vfx.transform.forward, Vector3.forward), .99f, "+Z of the VFX faces the attacker.");

            var diagonal = health.TakeDamage(From(Quaternion.Euler(0f, 50f, 0f) * Vector3.back));
            Assert.AreEqual(DamageStatus.Protected, diagonal.Status, "Inside the 60° half angle counts as frontal.");

            var flank = health.TakeDamage(From(Vector3.left));
            Assert.True(flank.WasApplied, "A side hit gets past the shield.");
            var back = health.TakeDamage(From(Vector3.forward));
            Assert.True(back.WasApplied, "A hit from behind gets past the shield.");
            var fall = health.TakeDamage(new DamageInfo(1f, "World", causeId: "fall"));
            Assert.True(fall.WasApplied, "Damage without a direction (fall) is never blocked.");
            Assert.AreEqual(max - 5f, health.CurrentHealth, .001f);
            Assert.AreEqual(3, damaged);

            shield.frontMultiplier = .5f;
            var half = health.TakeDamage(From(Vector3.back, 10f));
            Assert.True(half.WasApplied);
            Assert.AreEqual(5f, half.AppliedDamage, .001f, "A partial shield scales frontal damage.");

            enemy.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // 몸을 돌리면 정면도 돈다
            shield.frontMultiplier = 0f;
            Assert.True(health.TakeDamage(From(Vector3.back)).WasApplied, "After turning around the old front is the back.");
            Assert.AreEqual(DamageStatus.Protected, health.TakeDamage(From(Vector3.forward)).Status);

            shield.enabled = false;
            Assert.True(health.TakeDamage(From(Vector3.forward)).WasApplied, "A disabled modifier is skipped.");
        }

        [UnityTest] public IEnumerator EnemiesWithoutAShieldTakeFrontalHits()
        {
            var enemy = Enemy("Enemy", Vector3.zero, 0f);
            yield return null;
            Assert.True(enemy.GetComponent<EnemyShield>() == null);
            Assert.True(enemy.GetComponent<EnemyHealth>().TakeDamage(From(Vector3.back)).WasApplied);
        }
    }
}
#endif

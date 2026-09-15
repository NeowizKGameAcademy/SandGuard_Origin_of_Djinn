#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SandGuard.Player.Tests
{
    /// <summary>경험치·레벨: 여러 레벨 한꺼번에 오르기, 최대 레벨, 포인트, 레벨별 능력치, 회복 비율, 초기화, 프리팹 연결(레벨업 VFX).</summary>
    public sealed class PlayerProgressionTests
    {
        readonly List<Object> objects = new List<Object>();

        PlayerProgression Build(float healRatio = 0f, float manaRatio = 0f)
        {
            var table = ScriptableObject.CreateInstance<PlayerProgressionTable>();
            table.ApplyDefaults();
            table.healRatioOnLevelUp = healRatio; table.manaRatioOnLevelUp = manaRatio;
            var go = new GameObject("Progression Player");
            go.SetActive(false);
            go.AddComponent<PlayerStats>();
            go.AddComponent<PlayerHealth>();
            go.AddComponent<PlayerManaWallet>();
            var progression = go.AddComponent<PlayerProgression>();
            progression.table = table;
            go.SetActive(true);
            objects.Add(go); objects.Add(table);
            return progression;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach (var value in objects) if (value != null) Object.Destroy(value);
            objects.Clear();
            yield return null;
        }

        [Test] public void ExperienceCarriesOverSeveralLevelsGrantsPointsAndGrowsStatsWithoutHealing()
        {
            var progression = Build();
            var health = progression.GetComponent<PlayerHealth>();
            var mana = progression.GetComponent<PlayerManaWallet>();
            var stats = progression.GetComponent<PlayerStats>();
            var levels = new List<int>(); int unityEvents = 0, changed = 0;
            progression.LevelUp += levels.Add;
            progression.onLevelUp.AddListener(() => unityEvents++);
            progression.Changed += () => changed++;

            Assert.AreEqual(1, progression.Level);
            Assert.AreEqual(10, progression.MaxLevel);
            Assert.AreEqual(1, progression.SkillPoints, "Starting points");
            Assert.AreEqual(50, progression.ExperienceToNextLevel);

            Assert.AreEqual(30, progression.GainExperience(30));
            Assert.AreEqual(1, progression.Level); Assert.AreEqual(30, progression.ExperienceInLevel);
            Assert.AreEqual(0, unityEvents);

            // 20(→Lv.2) + 80(→Lv.3) + 120(→Lv.4) + 5
            Assert.AreEqual(225, progression.GainExperience(225));
            Assert.AreEqual(4, progression.Level);
            Assert.AreEqual(5, progression.ExperienceInLevel);
            Assert.AreEqual(170, progression.ExperienceToNextLevel);
            CollectionAssert.AreEqual(new[] { 2, 3, 4 }, levels);
            Assert.AreEqual(1, unityEvents, "The VFX hook fires once per gain even when several levels pass.");
            Assert.AreEqual(2, changed);
            Assert.AreEqual(1 + 2 + 2 + 2, progression.SkillPoints);

            Assert.AreEqual(130f, health.MaxHealth, 1e-4f, "+10 max HP per level");
            Assert.AreEqual(100f, health.CurrentHealth, 1e-4f, "Heal ratio 0 leaves current HP alone.");
            Assert.AreEqual(115, mana.MaxMana, "+5 max MP per level");
            Assert.AreEqual(100, mana.CurrentMana, "Mana ratio 0 leaves current MP alone.");
            Assert.AreEqual(10.9f, stats.Evaluate(PlayerStat.AttackDamage, 10f), 1e-3f, "+3% attack per level");
            Assert.AreEqual(3, stats.Count, "Growth is re-applied, not stacked per level.");
        }

        [Test] public void MaxLevelDiscardsOverflowAndReportsZeroExperience()
        {
            var progression = Build();
            Assert.AreEqual(2370, progression.GainExperience(100000));
            Assert.AreEqual(10, progression.Level);
            Assert.True(progression.IsMaxLevel);
            Assert.AreEqual(0, progression.ExperienceInLevel);
            Assert.AreEqual(0, progression.ExperienceToNextLevel);
            Assert.AreEqual(25, progression.SkillPoints, "Lv.10 totals 25 points.");
            Assert.AreEqual(0, progression.GainExperience(10));
            Assert.AreEqual(2370, progression.TotalExperience);
        }

        [Test] public void LevelUpRestoresConfiguredRatiosOfTheNewMaximum()
        {
            var progression = Build(healRatio: 0.2f, manaRatio: 0.2f);
            var health = progression.GetComponent<PlayerHealth>();
            var mana = progression.GetComponent<PlayerManaWallet>();
            health.TakeDamage(new DamageInfo(60f, "Enemy", causeId: "test"));
            Assert.True(mana.TrySpend(80));

            progression.GainExperience(50);

            Assert.AreEqual(40f + 110f * 0.2f, health.CurrentHealth, 1e-3f);
            Assert.AreEqual(20 + 21, mana.CurrentMana);
        }

        [Test] public void SpendingPointsAndResetRestoreTheStartingState()
        {
            var progression = Build();
            var health = progression.GetComponent<PlayerHealth>();
            progression.GainExperience(300);
            int points = progression.SkillPoints;
            Assert.False(progression.TrySpendSkillPoints(points + 1));
            Assert.True(progression.TrySpendSkillPoints(2));
            Assert.AreEqual(points - 2, progression.SkillPoints);

            progression.ResetProgression();
            Assert.AreEqual(1, progression.Level);
            Assert.AreEqual(0, progression.TotalExperience);
            Assert.AreEqual(1, progression.SkillPoints);
            Assert.AreEqual(100f, health.MaxHealth, 1e-4f);
            Assert.AreEqual(0, progression.GetComponent<PlayerStats>().Count);
        }

        [Test] public void ReceiverIsRegisteredOnlyWhileAlive()
        {
            var progression = Build();
            var health = progression.GetComponent<PlayerHealth>();
            Assert.True(ExperienceReceivers.TryFindNearest(Vector3.zero, out var found));
            Assert.AreSame(progression, found);
            health.TakeDamage(new DamageInfo(1000f, "Enemy", causeId: "test"));
            Assert.False(progression.CanCollect, "Incapacitated players do not collect.");
            Assert.False(ExperienceReceivers.TryFindNearest(Vector3.zero, out _));
        }

        [UnityTest] public IEnumerator PlayerPrefabLevelsUpAndPlaysTheLevelUpVfxAtItsFeet()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Generated/Player.prefab");
            Assert.NotNull(asset);
            var player = Object.Instantiate(asset, new Vector3(0f, 50f, 0f), Quaternion.identity);
            objects.Add(player);
            player.GetComponent<PlayerInputReader>().captureCursor = false;
            yield return null;
            var progression = player.GetComponent<PlayerProgression>();
            Assert.NotNull(progression, "Run SandGuard/Progression/Connect Experience and Level Up.");
            Assert.NotNull(progression.table);
            var anchor = player.transform.Find("LevelUpVfx");
            Assert.NotNull(anchor);

            progression.GainExperience(progression.ExperienceToNextLevel);
            Assert.AreEqual(2, progression.Level);
            bool spawned = false;
            foreach (Transform child in anchor) spawned |= child.name.StartsWith("VFX_LevelUp") && child.gameObject.activeSelf;
            Assert.True(spawned, "VFX_LevelUp follows the player under LevelUpVfx.");
        }
    }
}
#endif

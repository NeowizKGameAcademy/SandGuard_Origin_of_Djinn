#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SandGuard.Player.Effects;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SandGuard.Player.Tests
{
    /// <summary>공격 마법 스킬: ⑬ 응축 마나탄, 관통탄(빔), 모래 폭발, 모래 족쇄, 폭발 관통탄. 플레이어는 원점에서 +Z를 본다.</summary>
    public sealed class PlayerSkillShotTests
    {
        /// <summary>속박 대상 대역. 실제 적 구현(EnemyRestraint)과 독립적이다.</summary>
        sealed class RestrainableDummy : MonoBehaviour, IRestrainable
        {
            public float Duration; public int Calls;
            public bool IsRestrained => Duration > 0f;
            public void Restrain(float duration) { Duration = Mathf.Max(Duration, duration); Calls++; }
        }

        readonly List<GameObject> objects = new List<GameObject>();
        Keyboard keyboard; Mouse mouse;
        InputSettings originalSettings, testSettings;
        GameObject player; PlayerBasicAttack attack; PlayerStats stats; PlayerEffects effects; PlayerSkillCaster caster;
        GameObject Track(GameObject value) { objects.Add(value); return value; }
        void Fire(bool held) => InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left, held));

        [UnitySetUp] public IEnumerator Setup()
        {
            originalSettings = InputSystem.settings;
            testSettings = Object.Instantiate(originalSettings);
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testSettings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            InputSystem.settings = testSettings;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); Time.timeScale = 1f;
            var floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            floor.transform.position = new Vector3(0, -0.5f, 0); floor.transform.localScale = new Vector3(60, 1, 60);
            Physics.SyncTransforms();
            player = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Generated/Player.prefab")));
            player.GetComponent<PlayerInputReader>().captureCursor = false;
            attack = player.GetComponent<PlayerBasicAttack>(); stats = player.GetComponent<PlayerStats>(); effects = player.GetComponent<PlayerEffects>();
            caster = player.GetComponent<PlayerSkillCaster>();
            Assert.NotNull(caster, "Player.prefab carries a PlayerSkillCaster.");
            var aimCamera = Track(new GameObject("Aim Camera")).AddComponent<Camera>();
            aimCamera.enabled = false; aimCamera.transform.position = new Vector3(0, 1.5f, -3);
            attack.aimer.viewCamera = aimCamera;
            yield return new WaitForSeconds(0.3f);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            foreach (var value in objects) if (value != null) Object.Destroy(value);
            objects.Clear();
            foreach (var bolt in Object.FindObjectsByType<PlayerProjectile>(FindObjectsSortMode.None)) Object.Destroy(bolt.gameObject);
            InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard);
            InputSystem.settings = originalSettings; Object.Destroy(testSettings);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            yield return null;
        }

        /// <summary>정면 표적: 3×3×1 벽. 시야 중앙 광선이 정면 표적을 맞히므로 빔·볼트는 거의 +Z로 간다.</summary>
        PlayerTestTarget Slab(float z) => Cube(new Vector3(0, 1.5f, z), new Vector3(3, 3, 1)).AddComponent<PlayerTestTarget>();
        GameObject Cube(Vector3 position, Vector3 scale)
        {
            var cube = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            cube.transform.position = position; cube.transform.localScale = scale;
            Physics.SyncTransforms();
            return cube;
        }
        IEnumerator ShootOnce()
        {
            Fire(true); yield return new WaitForSeconds(0.2f); Fire(false);
            yield return new WaitForSeconds(0.4f);
        }
        /// <summary>Q 모래 폭발을 직접 시전하고 착탄까지 기다린다.</summary>
        IEnumerator CastBurst()
        {
            var result = caster.TryCast(PlayerSkillCaster.Burst);
            Assert.True(result.Succeeded, "Burst cast: " + result.Failure);
            yield return new WaitForSeconds(0.5f);
        }
        int Bolts => Object.FindObjectsByType<PlayerProjectile>(FindObjectsSortMode.None).Length;

        [Test] public void AttackSkillsAttachAndDetachSymmetrically()
        {
            var all = new IPlayerEffect[] { new CondensedBoltEffect(), new PierceBeamEffect(), new SandBurstEffect(), new SandShackleEffect(), new ExplosivePierceEffect(), new SandVortexEffect(), new SandStormEffect() };
            Assert.False(attack.PierceBeam); Assert.False(attack.SandBurst); Assert.False(attack.SandShackle); Assert.False(attack.BurstPerPierce);
            Assert.False(caster.VortexUnlocked); Assert.False(caster.StormUnlocked);
            Assert.AreEqual(1f, attack.BoltScale, 0.0001f); Assert.AreEqual(10f, attack.Damage, 0.0001f);
            foreach (var effect in all) Assert.True(effects.Apply(effect));
            Assert.True(attack.PierceBeam); Assert.True(attack.SandBurst); Assert.True(attack.SandShackle); Assert.True(attack.BurstPerPierce);
            Assert.True(caster.BurstUnlocked); Assert.True(caster.VortexUnlocked); Assert.True(caster.StormUnlocked);
            Assert.AreEqual(1.35f, attack.BoltScale, 0.0001f); Assert.AreEqual(13f, attack.Damage, 0.0001f);
            Assert.AreEqual(attack.burstRadius, attack.BurstRadius, 0.0001f, "Numbers stay at the inspector defaults; nodes only switch the behaviour on.");
            Assert.AreEqual(13f * attack.burstDamageRatio, attack.BurstDamage, 0.0001f);
            Assert.AreEqual(attack.shackleDuration, attack.ShackleDuration, 0.0001f);
            effects.Remove(all[1]); // 관통탄을 떼도 폭발 관통탄이 빔을 유지한다
            Assert.True(attack.PierceBeam, "Explosive pierce keeps the beam on by itself.");
            effects.RemoveAll();
            Assert.False(attack.PierceBeam); Assert.False(attack.SandBurst); Assert.False(attack.SandShackle); Assert.False(attack.BurstPerPierce);
            Assert.False(caster.VortexUnlocked); Assert.False(caster.StormUnlocked);
            Assert.AreEqual(1f, attack.BoltScale, 0.0001f); Assert.AreEqual(10f, attack.Damage, 0.0001f);
            Assert.AreEqual(0, stats.Count, "Detaching leaves no modifier behind.");
        }

        [UnityTest] public IEnumerator PierceBeamHitsEveryEnemyInLineAndStopsAtTheFirstWall()
        {
            var first = Slab(5f); var second = Slab(8f);
            Cube(new Vector3(0, 1.5f, 11f), new Vector3(3, 3, 1)); // 피해를 받지 않는 벽
            var behind = Slab(14f);
            var hits = new List<PlayerHitInfo>(); attack.Hit += hits.Add;
            int beams = 0; attack.BeamFired += _ => beams++;
            yield return ShootOnce();
            Assert.AreEqual(1, first.HitCount); Assert.AreEqual(0, second.HitCount, "Without the skill a bolt stops at the first target.");
            Assert.AreEqual(0, beams);

            effects.Apply(new PierceBeamEffect());
            yield return ShootOnce();
            Assert.AreEqual(0, Bolts, "The beam replaces the bolt; nothing is spawned as a projectile.");
            Assert.AreEqual(1, beams);
            Assert.AreEqual(2, first.HitCount); Assert.AreEqual(1, second.HitCount, "The beam pierces the first target and hits the second.");
            Assert.AreEqual(0, behind.HitCount, "The beam stops at the wall and never reaches the target behind it.");
            Assert.AreEqual(3, hits.Count); Assert.AreEqual("player.pierce", hits[2].CauseId); Assert.AreEqual(attack.Damage, hits[2].AppliedDamage, 0.0001f);
            var beam = attack.LastBeam;
            Assert.AreEqual(2, beam.EnemiesHit); Assert.True(beam.Landed);
            Assert.That(beam.Length, Is.InRange(8.5f, 11f), "The beam ends on the wall face (z≈10.5), not at full range.");
            Assert.Greater(Vector3.Dot(beam.Direction, Vector3.forward), 0.95f);
        }

        [UnityTest] public IEnumerator SandBurstCastExplodesAtTheImpactAndDamagesEnemiesInRadius()
        {
            var direct = Slab(5f);
            var near = Cube(new Vector3(2.0f, 1f, 5f), Vector3.one).AddComponent<PlayerTestTarget>();   // 착탄점에서 약 1.5m
            var far = Cube(new Vector3(6f, 1f, 5f), Vector3.one).AddComponent<PlayerTestTarget>();     // 반경(2.5m) 밖
            var bursts = new List<Vector3>(); attack.Burst += bursts.Add;
            Assert.AreEqual(ActionFailure.Locked, caster.TryCast(PlayerSkillCaster.Burst).Failure, "Q needs the node.");
            yield return ShootOnce();
            Assert.AreEqual(1, direct.HitCount); Assert.AreEqual(0, near.HitCount); Assert.AreEqual(0, bursts.Count, "The basic bolt never bursts.");

            effects.Apply(new SandBurstEffect());
            yield return ShootOnce();
            Assert.AreEqual(0, bursts.Count, "Unlocking Q does not change the basic bolt.");
            yield return CastBurst();
            Assert.AreEqual(1, bursts.Count, "One burst per Q impact.");
            Assert.Less(Vector3.Distance(bursts[0], new Vector3(0, 1.4f, 4.5f)), 1f, "The burst is centred on the impact point.");
            Assert.AreEqual(4, direct.HitCount, "Two basic bolts, then the Q bolt and its burst.");
            Assert.AreEqual(1, near.HitCount); Assert.AreEqual(50f - attack.BurstDamage, near.CurrentHealth, 0.001f, "Burst damage is bolt damage × ratio.");
            Assert.AreEqual(0, far.HitCount, "Outside the radius nothing happens.");
            Assert.AreEqual(5, attack.HitCount, "Burst hits count as hits (mana on hit applies).");
        }

        [UnityTest] public IEnumerator BurstRadiusModifierWidensBothTheHitAndTheVisual()
        {
            Slab(5f);
            var mid = Cube(new Vector3(4f, 1f, 5f), Vector3.one).AddComponent<PlayerTestTarget>(); // 착탄점에서 약 3.4m: 기본 2.5m 밖, 5m 안
            var template = Track(new GameObject("BurstVfx")); // 빈 오브젝트라 켜 둬도 무해하고, 복제본을 Find로 찾을 수 있다
            attack.burstPrefab = template;
            effects.Apply(new SandBurstEffect());
            stats.Add(PlayerStat.BurstRadius, StatModifierKind.PercentAdd, 1f, "wider"); // 2.5 → 5
            Assert.AreEqual(5f, attack.BurstRadius, 0.0001f);
            yield return CastBurst();
            Assert.AreEqual(1, mid.HitCount, "The wider radius reaches the target the default radius misses.");
            var instance = GameObject.Find("BurstVfx(Clone)");
            Assert.NotNull(instance, "The burst visual is spawned.");
            Assert.AreEqual(2f, instance.transform.localScale.x, 0.0001f, "The visual scales with radius ÷ burstVfxRadius.");
        }

        [UnityTest] public IEnumerator SandShackleRestrainsEnemiesAroundEveryBurst()
        {
            var direct = Slab(5f); var directRoot = direct.gameObject.AddComponent<RestrainableDummy>();
            var near = Cube(new Vector3(2.0f, 1f, 5f), Vector3.one).AddComponent<RestrainableDummy>();
            var far = Cube(new Vector3(6f, 1f, 5f), Vector3.one).AddComponent<RestrainableDummy>();
            int shackled = -1; attack.Shackled += (_, count) => shackled = count;
            effects.Apply(new SandShackleEffect()); effects.Apply(new SandBurstEffect());
            yield return ShootOnce();
            Assert.AreEqual(-1, shackled, "The passive only rides on bursts; a basic bolt shackles nothing.");
            yield return CastBurst();
            Assert.AreEqual(3, direct.HitCount, "Basic bolt, Q bolt and its burst all deal damage.");
            Assert.AreEqual(2, shackled);
            Assert.AreEqual(attack.ShackleDuration, directRoot.Duration, 0.0001f); Assert.AreEqual(1, directRoot.Calls);
            Assert.AreEqual(attack.ShackleDuration, near.Duration, 0.0001f);
            Assert.AreEqual(0f, far.Duration, "Outside the radius nothing is restrained.");
        }

        [UnityTest] public IEnumerator ExplosivePierceBurstsAtEveryPiercedEnemy()
        {
            var first = Slab(5f); var second = Slab(9f);
            var beside = Cube(new Vector3(2.0f, 1f, 9f), Vector3.one).AddComponent<PlayerTestTarget>(); // 두 번째 표적 옆, 첫 표적에서는 4m 이상
            var bursts = new List<Vector3>(); attack.Burst += bursts.Add;
            effects.Apply(new ExplosivePierceEffect());
            Assert.True(attack.PierceBeam); Assert.True(attack.BurstPerPierce);
            Assert.False(attack.SandBurst, "Explosive pierce does not unlock the Q cast by itself.");
            yield return ShootOnce();
            Assert.AreEqual(0, Bolts);
            Assert.AreEqual(2, bursts.Count, "One burst per pierced enemy.");
            Assert.AreEqual(2, first.HitCount, "Beam + its own burst.");
            Assert.AreEqual(2, second.HitCount, "Beam + its own burst.");
            Assert.AreEqual(1, beside.HitCount, "Only the second target's burst reaches the bystander.");
            Assert.AreEqual(attack.BurstDamage, 50f - beside.CurrentHealth, 0.001f);
        }

        [UnityTest] public IEnumerator PlainPierceBeamNeverBurstsEvenWithBurstUnlocked()
        {
            var first = Slab(5f);
            var beside = Cube(new Vector3(2.0f, 1f, 5f), Vector3.one).AddComponent<PlayerTestTarget>();
            var bursts = new List<Vector3>(); attack.Burst += bursts.Add;
            effects.Apply(new PierceBeamEffect()); effects.Apply(new SandBurstEffect());
            yield return ShootOnce();
            Assert.AreEqual(0, bursts.Count, "Without the explosive-pierce node the beam only pierces; bursts belong to Q.");
            Assert.AreEqual(1, first.HitCount); Assert.AreEqual(0, beside.HitCount);
        }

        [UnityTest] public IEnumerator CondensedBoltDealsMoreDamageAndFiresABiggerBolt()
        {
            var target = Slab(5f);
            effects.Apply(new CondensedBoltEffect());
            Assert.AreEqual(13f, attack.Damage, 0.0001f); Assert.AreEqual(1.35f, attack.BoltScale, 0.0001f);
            Fire(true);
            yield return new WaitForSeconds(0.2f);
            Fire(false);
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(1, target.HitCount); Assert.AreEqual(37f, target.CurrentHealth, 0.001f, "Damage +30%.");
        }
    }
}
#endif

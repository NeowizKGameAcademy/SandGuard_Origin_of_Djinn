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
    /// <summary>붙였다 뗄 수 있는 효과 계약과 ⑩ 마나 순환(타격 시 마나 +3) 검증.</summary>
    public sealed class PlayerEffectsTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        Keyboard keyboard; Mouse mouse;
        InputSettings originalSettings, testSettings;
        GameObject player; PlayerBasicAttack attack; PlayerStats stats; PlayerEffects effects; IManaWallet mana;
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
            attack = player.GetComponent<PlayerBasicAttack>(); stats = player.GetComponent<PlayerStats>();
            effects = player.GetComponent<PlayerEffects>(); mana = player.GetComponent<PlayerManaWallet>();
            Assert.NotNull(effects, "Player.prefab carries a PlayerEffects registry.");
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

        GameObject Wall(bool damageable)
        {
            var wall = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.transform.position = new Vector3(0, 1, 5); wall.transform.localScale = new Vector3(3, 2, 1);
            if (damageable) wall.AddComponent<PlayerTestTarget>();
            Physics.SyncTransforms();
            return wall;
        }
        /// <summary>한 발만 쏘고 명중까지 기다린다.</summary>
        IEnumerator ShootOnce()
        {
            int before = attack.HitCount;
            Fire(true); yield return new WaitForSeconds(0.2f); Fire(false);
            yield return new WaitForSeconds(0.4f);
            Assert.LessOrEqual(attack.HitCount, before + 1, "Exactly one bolt per tap.");
        }

        [Test] public void EffectsAttachAndDetachSymmetricallyAndAreCountedOnce()
        {
            var effect = new ManaOnHitEffect();
            var changes = new List<(IPlayerEffect, bool)>();
            effects.Changed += (e, on) => changes.Add((e, on));
            Assert.AreEqual(0, attack.ManaPerHit);
            Assert.True(effects.Apply(effect));
            Assert.False(effects.Apply(effect), "Applying twice is refused instead of stacking.");
            Assert.True(effect.IsApplied); Assert.True(effects.IsApplied(effect)); Assert.AreEqual(1, effects.Applied.Count);
            Assert.AreEqual(3, attack.ManaPerHit);
            Assert.True(stats.HasModifiers(PlayerStat.ManaPerHit));
            Assert.True(effects.Remove(effect));
            Assert.False(effects.Remove(effect), "Removing twice is a no-op.");
            Assert.False(effect.IsApplied); Assert.AreEqual(0, effects.Applied.Count);
            Assert.AreEqual(0, attack.ManaPerHit);
            Assert.False(stats.HasModifiers(PlayerStat.ManaPerHit), "Detaching leaves no modifier behind.");
            Assert.AreEqual(0, stats.Count);
            Assert.AreEqual(2, changes.Count); Assert.True(changes[0].Item2); Assert.False(changes[1].Item2);
            // 다시 붙이고 전부 떼기
            Assert.True(effects.Apply(effect)); effects.Apply(new ManaOnHitEffect(1));
            Assert.AreEqual(4, attack.ManaPerHit);
            effects.RemoveAll();
            Assert.AreEqual(0, attack.ManaPerHit); Assert.AreEqual(0, effects.Applied.Count); Assert.AreEqual(0, stats.Count);
        }

        [UnityTest] public IEnumerator ManaOnHitRestoresManaOnlyWhileAttachedAndOnlyForRealHits()
        {
            var target = Wall(true).GetComponent<PlayerTestTarget>();
            var effect = new ManaOnHitEffect(3);
            PlayerHitInfo? last = null; int hits = 0;
            attack.Hit += info => { hits++; last = info; };
            Assert.True(mana.TrySpend(20)); Assert.AreEqual(80, mana.CurrentMana);
            yield return ShootOnce();
            Assert.AreEqual(1, hits); Assert.AreEqual(1, target.HitCount);
            Assert.AreEqual(80, mana.CurrentMana, "Hits give nothing before the effect is attached.");
            Assert.AreEqual(attack.Damage, last.Value.AppliedDamage, 0.0001f); Assert.False(last.Value.WasKilled);
            Assert.AreEqual("player.basic", last.Value.CauseId); Assert.NotNull(last.Value.Receiver);

            effects.Apply(effect);
            yield return ShootOnce();
            Assert.AreEqual(2, hits);
            Assert.AreEqual(83, mana.CurrentMana, "Each real hit restores 3 mana while attached.");

            effects.Remove(effect);
            yield return ShootOnce();
            Assert.AreEqual(3, hits);
            Assert.AreEqual(83, mana.CurrentMana, "Detached: hits stop restoring mana.");

            effects.Apply(effect);
            Object.Destroy(target.gameObject); yield return null;
            Wall(false); // 피해를 받지 않는 벽
            yield return ShootOnce();
            Assert.AreEqual(3, hits, "Hitting a wall is not a hit on an enemy.");
            Assert.AreEqual(83, mana.CurrentMana, "Walls never restore mana.");

            mana.Gain(int.MaxValue);
            Object.Destroy(objects[objects.Count - 1]); yield return null;
            Wall(true);
            yield return ShootOnce();
            Assert.AreEqual(4, hits);
            Assert.AreEqual(mana.MaxMana, mana.CurrentMana, "Restoration is capped at max mana.");
        }

        [UnityTest] public IEnumerator ManaAbsorptionPulsesLampOnlyForActualRecovery()
        {
            Wall(true);
            effects.Apply(new ManaOnHitEffect(3));
            var lamp = player.GetComponentInChildren<PlayerLampEquipment>();
            int recovered = 0;
            attack.ManaAbsorbed += amount => recovered += amount;
            mana.TrySpend(2); // Partially full: report the actual 2, not the requested 3.
            float peak = 0f;
            Fire(true);
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                if (t >= 0.2f) Fire(false);
                peak = Mathf.Max(peak, lamp.AbsorptionPulse);
                yield return null;
            }
            Fire(false);
            Assert.AreEqual(2, recovered);
            Assert.AreEqual(mana.MaxMana, mana.CurrentMana);
            Assert.Greater(peak, 0.8f, "A real recovering hit must reach the lamp.");
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(0f, lamp.AbsorptionPulse);
            yield return ShootOnce();
            Assert.AreEqual(2, recovered, "Full mana must not emit another absorption event.");
            Assert.AreEqual(0f, lamp.AbsorptionPulse);
        }

        void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        IEnumerator UntilGrounded(PlayerMotor motor)
        {
            for (float t = 0f; t < 2f && !motor.IsGrounded; t += Time.deltaTime) yield return null;
            Assert.True(motor.IsGrounded);
        }

        [UnityTest] public IEnumerator DoubleJumpAndAirDashStayLockedUntilTheirEffectsAreAttached()
        {
            var motor = player.GetComponent<PlayerMotor>();
            int jumps = 0; motor.Jumped += () => jumps++;
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(0, motor.ExtraAirJumps); Assert.AreEqual(0, motor.AirDashes); Assert.AreEqual(0, motor.RemainingAirJumps);
            Keys(Key.Space); yield return new WaitForSeconds(0.1f); Keys(); yield return null;
            Keys(Key.Space); yield return new WaitForSeconds(0.1f); Keys();
            Assert.AreEqual(1, jumps, "Without the effect a second press in the air does nothing.");
            Assert.False(motor.IsGrounded);
            Assert.AreEqual(ActionFailure.Locked, motor.TryDash().Failure, "Without the effect there is no air dash.");
            yield return UntilGrounded(motor);

            effects.Apply(new DoubleJumpEffect()); effects.Apply(new AirDashEffect());
            yield return null;
            Assert.AreEqual(1, motor.RemainingAirJumps); Assert.AreEqual(1, motor.RemainingAirDashes);
            Keys(Key.Space); yield return new WaitForSeconds(0.1f); Keys(); yield return null;
            Keys(Key.Space); yield return new WaitForSeconds(0.1f); Keys();
            Assert.AreEqual(3, jumps, "Ground jump plus one air jump.");
            Assert.True(motor.TryDash().Succeeded, "One air dash per airtime.");
            Assert.AreEqual(0, motor.RemainingAirDashes);
            yield return new WaitForSeconds(motor.dashDuration + 0.05f);
            Assert.False(motor.IsGrounded);
            motor.ResetDashCooldown(); // 쿨다운(1초)이 먼저 걸리므로 걷어내고 잠김 여부만 본다
            Assert.AreEqual(ActionFailure.Locked, motor.TryDash().Failure, "The air dash does not recharge in the air.");
            yield return UntilGrounded(motor);
            Assert.AreEqual(1, motor.RemainingAirDashes, "Landing recharges the air dash.");

            effects.RemoveAll(); yield return null;
            Assert.AreEqual(0, motor.RemainingAirJumps); Assert.AreEqual(0, motor.AirDashes);
            Assert.True(motor.TryDash().Succeeded, "The ground dash is a base ability and stays available.");
        }

        [UnityTest] public IEnumerator StarterEffectsUnlockDemoAbilitiesAndCleanUpWhenDestroyed()
        {
            var motor = player.GetComponent<PlayerMotor>();
            var starter = Track(new GameObject("Starter")).AddComponent<PlayerStarterEffects>();
            yield return null; yield return null;
            Assert.AreEqual(3, starter.Applied.Count); Assert.AreEqual(3, effects.Applied.Count); // 더블 점프·공중 대시·상승 기류
            Assert.AreEqual(1, motor.ExtraAirJumps); Assert.AreEqual(1, motor.AirDashes);
            Object.Destroy(starter.gameObject); yield return null;
            Assert.AreEqual(0, effects.Applied.Count, "Removing the starter takes its unlocks with it.");
            Assert.AreEqual(0, motor.ExtraAirJumps); Assert.AreEqual(0, motor.AirDashes);
        }

        sealed class CountingEffect : PlayerEffect
        {
            public int Hits, Removed;
            protected override void OnApply(PlayerEffectContext context)
            {
                AddStat(context, PlayerStat.AttackDamage, StatModifierKind.PercentAdd, 0.5f);
                Subscribe(() => context.Attack.Hit += OnHit, () => context.Attack.Hit -= OnHit);
            }
            protected override void OnRemove(PlayerEffectContext context) => Removed++;
            void OnHit(PlayerHitInfo info) => Hits++;
        }

        [UnityTest] public IEnumerator BaseClassUndoesSubscriptionsAndStatsInOneRemove()
        {
            var target = Wall(true).GetComponent<PlayerTestTarget>();
            var effect = new CountingEffect();
            effects.Apply(effect);
            Assert.AreEqual(15f, attack.Damage, 0.0001f);
            yield return ShootOnce();
            Assert.AreEqual(1, effect.Hits);
            Assert.AreEqual(target.maxHealth - 15f, target.CurrentHealth, 0.0001f);
            effects.Remove(effect);
            Assert.AreEqual(1, effect.Removed);
            Assert.AreEqual(10f, attack.Damage, 0.0001f, "Stat undone.");
            yield return ShootOnce();
            Assert.AreEqual(1, effect.Hits, "Subscription undone: no more callbacks after Remove.");
            Assert.AreEqual(target.maxHealth - 25f, target.CurrentHealth, 0.0001f);
        }
    }
}
#endif

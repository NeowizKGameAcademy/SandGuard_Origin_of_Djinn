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
    /// <summary>Q/E/R 액티브 시전: 해금·마나·쿨다운, Q 모래 폭발(띄우기), E 모래 소용돌이(끌어당김·마무리 쳐올림), R 사막 폭풍(코어에서 퍼지는 링), 관통탄 충전. 플레이어는 원점에서 +Z를 본다.</summary>
    public sealed class PlayerSkillCastTests
    {
        /// <summary>적 대역: 밀림·둔화·속박을 기록하고, 밀리면 실제로 움직인다.</summary>
        sealed class StatusDummy : MonoBehaviour, IDisplaceable, ISlowable, IRestrainable
        {
            public Vector3 TotalDisplacement; public int DisplaceCalls;
            public float SlowFactor { get; private set; } public float SlowUntil = -1f; public float RestrainDuration;
            public bool IsRestrained => RestrainDuration > 0f;
            public void Displace(Vector3 delta) { transform.position += delta; TotalDisplacement += delta; DisplaceCalls++; }
            public Vector3 Knock; public int KnockCalls;
            public void Knockback(Vector3 velocity) { Knock = velocity; KnockCalls++; }
            public Vector3 Launched; public int LaunchCalls;
            public bool Launch(Vector3 velocity) { Launched = velocity; LaunchCalls++; return true; }
            public void Slow(float factor, float duration) { SlowFactor = Mathf.Max(SlowFactor, factor); SlowUntil = Time.time + duration; }
            public void Restrain(float duration) => RestrainDuration = Mathf.Max(RestrainDuration, duration);
            void Update() { if (SlowUntil >= 0f && Time.time >= SlowUntil) { SlowFactor = 0f; SlowUntil = -1f; } }
        }

        readonly List<GameObject> objects = new List<GameObject>();
        Keyboard keyboard; Mouse mouse;
        InputSettings originalSettings, testSettings;
        GameObject player; PlayerBasicAttack attack; PlayerStats stats; PlayerEffects effects; PlayerSkillCaster caster; IManaWallet mana;
        GameObject Track(GameObject value) { objects.Add(value); return value; }
        void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

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
            caster = player.GetComponent<PlayerSkillCaster>(); mana = player.GetComponent<PlayerManaWallet>();
            Assert.NotNull(caster, "Player.prefab carries a PlayerSkillCaster.");
            Assert.AreSame(attack, caster.attack); Assert.AreSame(stats, caster.stats); Assert.NotNull(caster.input); Assert.NotNull(caster.aimer);
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
            foreach (var zone in Object.FindObjectsByType<PlayerSandZone>(FindObjectsSortMode.None)) Object.Destroy(zone.gameObject);
            InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard);
            InputSystem.settings = originalSettings; Object.Destroy(testSettings);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            yield return null;
        }

        /// <summary>시야 중앙 광선이 맞는 정면 표적. 시전 지점은 그 앞면(z≈4.5)이 바닥에 붙은 곳이 된다.</summary>
        PlayerTestTarget Slab(float z) => Cube(new Vector3(0, 1.5f, z), new Vector3(3, 3, 1)).AddComponent<PlayerTestTarget>();
        GameObject Cube(Vector3 position, Vector3 scale)
        {
            var cube = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            cube.transform.position = position; cube.transform.localScale = scale;
            Physics.SyncTransforms();
            return cube;
        }
        StatusDummy Dummy(Vector3 position) => Cube(position, Vector3.one).AddComponent<StatusDummy>();

        [UnityTest] public IEnumerator CastsNeedTheNodeManaAndCooldownAndTheKeysWork()
        {
            Slab(6f);
            var casts = new List<(int, Vector3)>(); caster.Cast += (slot, point) => casts.Add((slot, point));
            Assert.AreEqual(ActionFailure.Locked, caster.TryCast(PlayerSkillCaster.Vortex).Failure);
            Assert.AreEqual(ActionFailure.Locked, caster.TryCast(PlayerSkillCaster.Storm).Failure);
            Assert.AreEqual(ActionFailure.InvalidRequest, caster.TryCast(7).Failure);
            effects.Apply(new SandBurstEffect()); effects.Apply(new SandVortexEffect()); effects.Apply(new SandStormEffect());
            int before = mana.CurrentMana;
            Assert.True(caster.TryCast(PlayerSkillCaster.Vortex).Succeeded);
            Assert.AreEqual(before - caster.vortexManaCost, mana.CurrentMana, "E costs mana.");
            Assert.AreEqual(ActionFailure.Cooldown, caster.TryCast(PlayerSkillCaster.Vortex).Failure, "E is on cooldown right after a cast.");
            Assert.That(caster.CooldownRemaining(PlayerSkillCaster.Vortex), Is.InRange(caster.vortexCooldown - 0.1f, caster.vortexCooldown));
            Assert.AreEqual(1, casts.Count); Assert.AreEqual(PlayerSkillCaster.Vortex, casts[0].Item1);
            Assert.Less(Vector3.Distance(casts[0].Item2, new Vector3(0, 0, 5.2f)), 1f, "The zone lands on the ground just in front of the aimed wall face (z 5.5), not on top of it.");
            Assert.Less(casts[0].Item2.y, 0.1f);
            Assert.NotNull(caster.LastVortex); Assert.AreEqual(caster.VortexRadius, caster.LastVortex.radius, 0.0001f);

            Assert.True(mana.TrySpend(mana.CurrentMana)); // 0
            Assert.AreEqual(ActionFailure.InsufficientMana, caster.TryCast(PlayerSkillCaster.Storm).Failure, "R needs mana.");
            mana.Gain(1000);
            Keys(Key.R); yield return null; yield return null; Keys();
            Assert.AreEqual(2, casts.Count); Assert.AreEqual(PlayerSkillCaster.Storm, casts[1].Item1);
            Assert.NotNull(caster.LastStorm);
            Keys(Key.Q); yield return null; yield return null; Keys();
            Assert.AreEqual(3, casts.Count); Assert.AreEqual(PlayerSkillCaster.Burst, casts[2].Item1);
            Assert.AreEqual(1, Object.FindObjectsByType<PlayerProjectile>(FindObjectsSortMode.None).Length, "Q fires a bolt.");
            caster.ResetCooldowns();
            Assert.AreEqual(0f, caster.CooldownRemaining(PlayerSkillCaster.Vortex));
        }

        [UnityTest] public IEnumerator VortexPullsEnemiesToItsCentreAndLaunchesThemAtTheEnd()
        {
            Slab(6f); // 시전 지점 ≈ (0, 0, 5.5)
            var near = Dummy(new Vector3(2.5f, 0.5f, 5.5f));     // 반경 3 안
            var far = Dummy(new Vector3(0f, 0.5f, 12f));        // 반경 밖
            effects.Apply(new SandVortexEffect());
            caster.vortexDuration = 1f; caster.vortexPullSpeed = 3f;
            Assert.True(caster.TryCast(PlayerSkillCaster.Vortex).Succeeded);
            var vortex = caster.LastVortex;
            Vector3 centre = vortex.transform.position;
            bool ended = false; vortex.Ended += () => ended = true;
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(near.DisplaceCalls, 5, "The vortex pulls every frame.");
            Assert.Less(Vector3.Distance(Flat(near.transform.position), Flat(centre)), 2.5f - 0.8f, "The near target has moved toward the centre.");
            Assert.AreEqual(0, far.DisplaceCalls, "Outside the radius nothing is pulled.");
            Assert.AreEqual(0.5f, near.transform.position.y, 0.001f, "Pulling never changes height.");
            yield return new WaitForSeconds(0.8f);
            Assert.True(ended); Assert.True(vortex == null || vortex.Finished);
            Assert.Less(Vector3.Distance(Flat(near.transform.position), Flat(centre)), 0.6f, "By the end the target sits at the centre (inner radius).");
            Assert.AreEqual(1, near.LaunchCalls, "The vortex launches what it gathered, once.");
            Assert.AreEqual(caster.vortexEndLaunch, near.Launched.y, 0.0001f); Assert.AreEqual(0f, Flat(near.Launched).magnitude, 0.0001f, "Straight up.");
            Assert.AreEqual(0, far.LaunchCalls); Assert.AreEqual(0f, near.RestrainDuration, "No shackle by default any more.");
        }

        [UnityTest] public IEnumerator BurstLaunchesEnemiesAroundTheImpact()
        {
            var slab = Slab(6f); // 볼트가 여기 맞고 앞면(z 4.5) 바닥에서 터진다
            var near = Dummy(new Vector3(1.5f, 0.5f, 4.5f));     // 폭발 반경 2.5 안
            var far = Dummy(new Vector3(6f, 0.5f, 4.5f));        // 밖
            effects.Apply(new SandBurstEffect());
            var launches = new List<(Vector3, int)>(); attack.Launched += (c, n) => launches.Add((c, n));
            Assert.True(caster.TryCast(PlayerSkillCaster.Burst).Succeeded);
            float deadline = Time.time + 2f;
            while (launches.Count == 0 && Time.time < deadline) yield return null;
            Assert.AreEqual(1, launches.Count, "The bolt detonated and launched something.");
            Assert.AreEqual(1, launches[0].Item2); Assert.AreEqual(1, attack.LastLaunchCount);
            Assert.AreEqual(1, near.LaunchCalls); Assert.AreEqual(0, far.LaunchCalls);
            Assert.AreEqual(attack.burstLaunchUp, near.Launched.y, 0.0001f, "Up at the configured speed.");
            Assert.Greater(near.Launched.x, 0f, "And away from the burst centre.");
            Assert.AreEqual(attack.burstLaunchOut, Flat(near.Launched).magnitude, 0.01f);
            Assert.Greater(slab.HitCount, 0, "The slab still takes the bolt/burst damage.");
        }

        [UnityTest] public IEnumerator PierceChargeTapFiresABasicBeamAndHoldingScalesItAndPushesPiercedEnemies()
        {
            var pierce = player.AddComponent<PlayerPierceCharge>();
            yield return null;
            var target = Dummy(new Vector3(0f, 1f, 5f)); var health = target.gameObject.AddComponent<PlayerTestTarget>();
            var wall = Cube(new Vector3(0f, 1.5f, 9f), new Vector3(3f, 3f, 1f));
            float baseDamage = attack.Damage;
            var fired = new List<float>(); pierce.Fired += fired.Add;
            bool started = false, cancelled = false; pierce.ChargeStarted += () => started = true; pierce.ChargeCancelled += () => cancelled = true;
            int before = mana.CurrentMana;
            // 탭: 누른 프레임에 바로 떼면 충전 0으로 기본 빔.
            Assert.True(pierce.BeginHold(0)); Assert.True(pierce.IsHolding);
            Assert.True(pierce.EndHold(0).Succeeded);
            Assert.False(pierce.IsHolding); Assert.False(started);
            Assert.AreEqual(before - pierce.manaCost, mana.CurrentMana, "A tap costs the base mana only.");
            Assert.AreEqual(1, fired.Count); Assert.AreEqual(0f, fired[0]); Assert.AreEqual(0f, attack.LastBeamCharge);
            Assert.AreEqual(1, health.HitCount); Assert.AreEqual(baseDamage, 50f - health.CurrentHealth, 0.0001f);
            Assert.AreEqual(0, target.KnockCalls, "A tap does not push.");
            Assert.AreEqual(ActionFailure.Cooldown, pierce.Fire(0f).Failure);
            Assert.False(pierce.BeginHold(0), "Holding during the cooldown does nothing.");
            pierce.ResetCooldown();
            // 홀드: holdToCharge 뒤 충전이 시작되고 chargeTime에 만충. 입력 리더 없이 검사하려고 떼어 둔다.
            pierce.input = null; pierce.holdToCharge = 0.1f; pierce.chargeTime = 0.4f;
            before = mana.CurrentMana;
            Assert.True(pierce.BeginHold(2));
            yield return new WaitForSeconds(0.05f);
            Assert.False(pierce.IsCharging, "Still inside the tap window.");
            yield return new WaitForSeconds(0.6f);
            Assert.True(started); Assert.True(pierce.IsCharging); Assert.AreEqual(1f, pierce.Charge, 0.0001f, "Fully charged.");
            Assert.AreEqual(ActionFailure.InvalidRequest, pierce.EndHold(0).Failure, "Releasing a different key is ignored.");
            Assert.True(pierce.EndHold(2).Succeeded);
            Assert.False(cancelled);
            Assert.AreEqual(before - pierce.manaCost - pierce.fullChargeExtraMana, mana.CurrentMana, "Full charge costs the extra mana.");
            Assert.AreEqual(2, fired.Count); Assert.AreEqual(1f, fired[1], 0.0001f); Assert.AreEqual(1f, attack.LastBeamCharge, 0.0001f);
            Assert.AreEqual(2, health.HitCount);
            Assert.AreEqual(baseDamage * attack.chargedDamageMultiplier, 50f - health.CurrentHealth - baseDamage, 0.001f, "Charged damage.");
            Assert.AreEqual(1, target.KnockCalls); Assert.Greater(target.Knock.z, 0f, "Pushed along the beam.");
            Assert.AreEqual(attack.chargedKnockback, target.Knock.magnitude, 0.01f);
            Assert.True(attack.LastBeam.Landed); Assert.Less(attack.LastBeam.End.z, 9f, "The charged beam still stops at the wall.");
            // 취소: 충전 중 못 쓰는 상태가 되면(여기서는 컴포넌트 비활성) 마나 없이 취소된다.
            pierce.ResetCooldown(); before = mana.CurrentMana;
            Assert.True(pierce.BeginHold(1));
            yield return new WaitForSeconds(0.3f);
            Assert.True(pierce.IsCharging);
            pierce.enabled = false;
            Assert.True(cancelled); Assert.False(pierce.IsHolding); Assert.AreEqual(before, mana.CurrentMana);
            Assert.AreEqual(2, fired.Count);
        }

        [UnityTest] public IEnumerator StormRingExpandsFromTheOriginStrikingEachEnemyOnceWithKnockbackAndSlow()
        {
            // 코어가 없는 씬이라 링은 시전자 위치에서 시작한다.
            var near = Dummy(new Vector3(0f, 0.5f, 4f)); var nearTarget = near.gameObject.AddComponent<PlayerTestTarget>();
            var far = Dummy(new Vector3(0f, 0.5f, 12f)); var farTarget = far.gameObject.AddComponent<PlayerTestTarget>();
            var hits = new List<PlayerHitInfo>(); attack.Hit += hits.Add;
            effects.Apply(new SandStormEffect());
            caster.stormRadius = 16f; caster.stormExpandSpeed = 8f; caster.stormRingThickness = 3f;
            Assert.Null(caster.Core);
            Assert.True(caster.TryCast(PlayerSkillCaster.Storm).Succeeded);
            var storm = caster.LastStorm;
            Assert.Less(Vector3.Distance(Flat(storm.transform.position), Flat(player.transform.position)), 0.5f, "Without a core the ring starts at the caster.");
            Assert.Less(storm.transform.position.y, 0.1f, "On the ground.");
            Assert.AreEqual(caster.StormDamage, storm.damage, 0.0001f); Assert.AreEqual(15f, storm.damage, 0.0001f); // 볼트 10 × 1.5
            Assert.AreEqual(caster.StormDuration, storm.duration, 0.0001f); Assert.AreEqual(19f / 8f, storm.duration, 0.0001f);
            Assert.AreEqual(caster.StormSlow, storm.slowFactor, 0.0001f);
            yield return new WaitForSeconds(0.9f); // 링 앞 ≈ 7.2 m: near(3.5 m)는 지났고 far(11.5 m)는 아직
            Assert.That(storm.Front, Is.InRange(6f, 8.5f));
            Assert.AreEqual(1, nearTarget.HitCount); Assert.AreEqual(50f - 15f, nearTarget.CurrentHealth, 0.001f);
            Assert.AreEqual(1, near.KnockCalls); Assert.Greater(near.Knock.z, 0f, "Pushed outward, away from the origin.");
            Assert.AreEqual(caster.stormKnockback, near.Knock.magnitude, 0.01f);
            Assert.AreEqual(caster.StormSlow, near.SlowFactor, 0.0001f, "Struck targets are slowed.");
            Assert.AreEqual(0, farTarget.HitCount); Assert.AreEqual(0, far.KnockCalls); Assert.AreEqual(0f, far.SlowFactor);
            Assert.AreEqual(1, hits.Count); Assert.AreEqual("player.storm", hits[0].CauseId); Assert.AreEqual(15f, hits[0].AppliedDamage, 0.0001f);
            yield return new WaitForSeconds(1.0f); // 링 앞 ≈ 15.2 m: far도 지나갔다
            Assert.AreEqual(1, farTarget.HitCount); Assert.AreEqual(1, far.KnockCalls);
            Assert.AreEqual(1, nearTarget.HitCount, "Each enemy is struck once, never again.");
            Assert.AreEqual(2, storm.TotalHits);
            yield return new WaitForSeconds(0.6f);
            Assert.That(storm == null || storm.Finished, "The ring ends once it has passed the max radius.");
            Assert.AreEqual(2, hits.Count, "Every ring hit reaches the attack's Hit event (mana on hit).");
        }

        [UnityTest] public IEnumerator PierceChargeRaisesTheTwoHandedLayerAndFireReleasesIt()
        {
            var pierce = player.AddComponent<PlayerPierceCharge>();
            pierce.input = null; pierce.holdToCharge = 0.1f; pierce.chargeTime = 0.4f;
            yield return null;
            var visuals = player.GetComponent<PlayerVisuals>();
            var animator = player.GetComponentInChildren<Animator>();
            Assert.NotNull(animator);
            int layer = animator.GetLayerIndex(visuals.pierceLayerName);
            Assert.GreaterOrEqual(layer, 0, "Protagonist.controller has the Pierce Casting layer (SandGuard > Player > Connect Pierce Charge Animation).");
            Assert.True(visuals.HasPierceAnimation);
            Assert.AreEqual(0f, animator.GetLayerWeight(layer), 0.001f);
            Slab(6f);
            Assert.True(pierce.BeginHold(0));
            yield return new WaitForSeconds(0.6f);
            Assert.True(pierce.IsCharging);
            Assert.True(visuals.PierceCharging);
            Assert.Greater(animator.GetLayerWeight(layer), 0.95f, "Charging raises the two-handed layer.");
            Assert.True(animator.GetCurrentAnimatorStateInfo(layer).IsName("Pierce Charge"), "Charging plays the gather pose.");
            var casting = visuals.Spellcasting;
            if (casting != null) Assert.True(casting.Suppressed, "The palm aim IK rests while both hands gather.");
            Assert.True(pierce.EndHold(0).Succeeded);
            Assert.False(visuals.PierceCharging);
            bool sawFire = false;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                var state = animator.GetCurrentAnimatorStateInfo(layer);
                if (state.IsName("Pierce Fire") || (animator.IsInTransition(layer) && animator.GetNextAnimatorStateInfo(layer).IsName("Pierce Fire"))) sawFire = true;
                yield return null;
            }
            Assert.True(sawFire, "Releasing plays the thrust.");
            yield return new WaitForSeconds(1.2f);
            Assert.True(animator.GetCurrentAnimatorStateInfo(layer).IsName("Empty"), "The thrust returns to Empty.");
            Assert.Less(animator.GetLayerWeight(layer), 0.05f, "The layer lowers after the thrust.");
            if (casting != null) Assert.False(casting.Suppressed);
            // 탭(충전 없이)도 내지르기 동작을 쓴다.
            pierce.ResetCooldown();
            Assert.True(pierce.Fire(0f).Succeeded);
            yield return null; yield return null;
            var tapState = animator.GetCurrentAnimatorStateInfo(layer);
            Assert.True(tapState.IsName("Pierce Fire") || (animator.IsInTransition(layer) && animator.GetNextAnimatorStateInfo(layer).IsName("Pierce Fire")), "A tap plays the thrust too.");
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
#endif

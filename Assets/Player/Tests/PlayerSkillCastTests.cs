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
    /// <summary>Q/E/R 액티브 시전: 해금·마나·쿨다운, E 모래 소용돌이(끌어당김·마무리 속박), R 사막 폭풍(틱 피해·둔화). 플레이어는 원점에서 +Z를 본다.</summary>
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

        [UnityTest] public IEnumerator VortexPullsEnemiesToItsCentreAndShacklesThemAtTheEnd()
        {
            Slab(6f); // 시전 지점 ≈ (0, 0, 5.5)
            var near = Dummy(new Vector3(2.5f, 0.5f, 5.5f));     // 반경 4 안
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
            Assert.AreEqual(caster.vortexEndShackle, near.RestrainDuration, 0.0001f, "The vortex shackles what it gathered.");
            Assert.AreEqual(0f, far.RestrainDuration);
        }

        [UnityTest] public IEnumerator StormTicksDamageAndSlowsWhileInsideThenClears()
        {
            var slab = Slab(6f); // 시전 지점(≈ z 5.2) 바로 뒤라 폭풍 안에 든다
            var inside = Dummy(new Vector3(2f, 0.5f, 6f)); var insideTarget = inside.gameObject.AddComponent<PlayerTestTarget>();
            var outside = Dummy(new Vector3(9f, 0.5f, 6f)); var outsideTarget = outside.gameObject.AddComponent<PlayerTestTarget>();
            var hits = new List<PlayerHitInfo>(); attack.Hit += hits.Add;
            effects.Apply(new SandStormEffect());
            caster.stormDuration = 2f; caster.stormTickInterval = 0.5f;
            Assert.True(caster.TryCast(PlayerSkillCaster.Storm).Succeeded);
            var storm = caster.LastStorm;
            Assert.AreEqual(caster.StormTickDamage, storm.tickDamage, 0.0001f); Assert.AreEqual(2.5f, storm.tickDamage, 0.0001f);
            yield return null; yield return null;
            Assert.AreEqual(1, storm.Ticks, "The first tick happens as the storm appears.");
            Assert.AreEqual(1, insideTarget.HitCount); Assert.AreEqual(50f - 2.5f, insideTarget.CurrentHealth, 0.001f);
            Assert.AreEqual(1, slab.HitCount, "Everything inside the radius takes the tick, once each.");
            Assert.AreEqual(caster.StormSlow, inside.SlowFactor, 0.0001f, "Inside the storm the target is slowed.");
            Assert.AreEqual(0, outsideTarget.HitCount); Assert.AreEqual(0f, outside.SlowFactor);
            Assert.AreEqual("player.storm", hits[0].CauseId); Assert.AreEqual(2.5f, hits[0].AppliedDamage, 0.0001f);
            yield return new WaitForSeconds(2.2f);
            Assert.That(storm == null || storm.Finished);
            Assert.That(insideTarget.HitCount, Is.InRange(4, 5), "About one tick per 0.5 s over 2 s.");
            Assert.AreEqual(insideTarget.HitCount + slab.HitCount, hits.Count, "Every tick hit reaches the attack's Hit event (mana on hit).");
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(0f, inside.SlowFactor, "The slow expires shortly after the storm ends.");
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
#endif

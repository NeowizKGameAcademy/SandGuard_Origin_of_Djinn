#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SandGuard.Player.Tests
{
    /// <summary>스탯 수정자 층: 계산 순서, 핸들·출처 제거, 시간 만료, 모터·공격·체력이 최종값을 쓰는지.</summary>
    public sealed class PlayerStatsTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        Keyboard keyboard; Mouse mouse;
        InputSettings originalSettings, testSettings;
        GameObject Track(GameObject value) { objects.Add(value); return value; }
        GameObject Player(Vector3 position)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Generated/Player.prefab");
            Assert.NotNull(asset, "Run SandGuard/Player/Create Missing Demo Assets first.");
            var result = Track(Object.Instantiate(asset, position, Quaternion.identity));
            result.GetComponent<PlayerInputReader>().captureCursor = false;
            return result;
        }
        void Floor()
        {
            var value = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            value.transform.position = new Vector3(0, -0.5f, 0); value.transform.localScale = new Vector3(60, 1, 60);
            Physics.SyncTransforms();
        }
        void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        void Fire(bool held) => InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left, held));
        [SetUp] public void Setup()
        {
            originalSettings = InputSystem.settings;
            testSettings = Object.Instantiate(originalSettings);
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testSettings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            InputSystem.settings = testSettings;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); Time.timeScale = 1f;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            foreach (var value in objects) if (value != null) Object.Destroy(value);
            objects.Clear();
            foreach (var bolt in Object.FindObjectsByType<PlayerProjectile>(FindObjectsSortMode.None)) Object.Destroy(bolt.gameObject);
            InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard);
            InputSystem.settings = originalSettings;
            Object.Destroy(testSettings);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            yield return null;
        }

        [Test] public void FlatThenPercentThenMultiplyAndRemovalByHandleOrSource()
        {
            var stats = Track(new GameObject("Stats")).AddComponent<PlayerStats>();
            var changes = new List<PlayerStat>();
            stats.Changed += changes.Add;
            Assert.AreEqual(4f, stats.Evaluate(PlayerStat.DashDistance, 4f), "No modifiers: base value.");
            var node = new object();
            var flat = stats.Add(PlayerStat.DashDistance, StatModifierKind.Flat, 2f, node);
            Assert.AreEqual(6f, stats.Evaluate(PlayerStat.DashDistance, 4f));
            var percent = stats.Add(PlayerStat.DashDistance, StatModifierKind.PercentAdd, 0.25f, node);
            stats.Add(PlayerStat.DashDistance, StatModifierKind.PercentAdd, 0.25f, "other");
            Assert.AreEqual(9f, stats.Evaluate(PlayerStat.DashDistance, 4f), 0.0001f, "(4+2) x (1+0.25+0.25): percentages add up.");
            var half = stats.Add(PlayerStat.DashDistance, StatModifierKind.Multiply, 0.5f, "buff");
            stats.Add(PlayerStat.DashDistance, StatModifierKind.Multiply, 0.5f, "buff");
            Assert.AreEqual(2.25f, stats.Evaluate(PlayerStat.DashDistance, 4f), 0.0001f, "Multipliers multiply: x0.25.");
            Assert.AreEqual(5f, stats.Evaluate(PlayerStat.MoveSpeed, 5f), "Other stats untouched.");
            Assert.True(stats.Remove(percent)); Assert.False(stats.Remove(percent), "A handle removes once.");
            Assert.AreEqual(6f * 1.25f * 0.25f, stats.Evaluate(PlayerStat.DashDistance, 4f), 0.0001f);
            Assert.AreEqual(2, stats.RemoveAll("buff"), "Source removal clears every modifier from that source.");
            Assert.AreEqual(7.5f, stats.Evaluate(PlayerStat.DashDistance, 4f), 0.0001f);
            Assert.AreEqual(1, stats.RemoveAll(node));
            Assert.AreEqual(5f, stats.Evaluate(PlayerStat.DashDistance, 4f), 0.0001f);
            stats.Add(PlayerStat.DamageTaken, StatModifierKind.PercentAdd, -2f);
            Assert.AreEqual(0f, stats.Evaluate(PlayerStat.DamageTaken, 1f), "Never below zero.");
            Assert.AreEqual(1, stats.EvaluateCount(PlayerStat.ExtraAirJumps, 1));
            stats.Add(PlayerStat.ExtraAirJumps, StatModifierKind.Flat, 1f);
            Assert.AreEqual(2, stats.EvaluateCount(PlayerStat.ExtraAirJumps, 1), "Count stats round to whole numbers.");
            Assert.True(changes.Count >= 8 && changes.All(s => s == PlayerStat.DashDistance || s == PlayerStat.DamageTaken || s == PlayerStat.ExtraAirJumps));
            Assert.True(stats.HasModifiers(PlayerStat.DamageTaken));
            var infos = new List<StatModifierInfo>(); stats.CopyModifiers(PlayerStat.DashDistance, infos);
            Assert.AreEqual(1, infos.Count); Assert.AreEqual("other", infos[0].Source); Assert.Less(infos[0].RemainingSeconds, 0f);
            stats.Clear();
            Assert.AreEqual(0, stats.Count); Assert.False(stats.HasModifiers(PlayerStat.DamageTaken));
        }

        [UnityTest] public IEnumerator TimedModifierExpiresOnItsOwn()
        {
            var stats = Track(new GameObject("Stats")).AddComponent<PlayerStats>();
            int changes = 0; stats.Changed += _ => changes++;
            stats.Add(PlayerStat.MoveSpeed, StatModifierKind.PercentAdd, 0.03f, "desert-master", duration: 0.2f);
            Assert.AreEqual(5.15f, stats.Evaluate(PlayerStat.MoveSpeed, 5f), 0.0001f);
            var infos = new List<StatModifierInfo>(); stats.CopyModifiers(PlayerStat.MoveSpeed, infos);
            Assert.That(infos[0].RemainingSeconds, Is.InRange(0.1f, 0.21f));
            yield return new WaitForSeconds(0.35f);
            Assert.AreEqual(5f, stats.Evaluate(PlayerStat.MoveSpeed, 5f), "Expired modifiers stop counting.");
            Assert.AreEqual(0, stats.Count, "Expired modifiers are dropped from the list.");
            Assert.AreEqual(2, changes, "Add and expiry each notify once.");
        }

        [UnityTest] public IEnumerator MotorUsesModifiedDashDistanceCooldownManaCostSpeedAndAirJumps()
        {
            Floor();
            var player = Player(Vector3.zero);
            var motor = player.GetComponent<PlayerMotor>();
            var stats = player.GetComponent<PlayerStats>();
            IManaWallet mana = player.GetComponent<PlayerManaWallet>();
            Assert.NotNull(stats, "Player.prefab carries a PlayerStats.");
            Assert.AreSame(stats, motor.stats);
            yield return new WaitForSeconds(0.4f);
            motor.dashCooldown = 1f;
            stats.Add(PlayerStat.DashDistance, StatModifierKind.PercentAdd, 0.25f, "long-stride");   // ② 긴 보폭
            stats.Add(PlayerStat.DashCooldown, StatModifierKind.PercentAdd, -0.2f, "quick-reset");   // ⑤ 빠른 재정비
            stats.Add(PlayerStat.DashManaCost, StatModifierKind.Flat, -5f, "test");
            stats.Add(PlayerStat.ExtraAirJumps, StatModifierKind.Flat, 1f, "double-jump");           // ① 더블 점프
            stats.Add(PlayerStat.MoveSpeed, StatModifierKind.PercentAdd, 0.03f, "desert-master");    // ⑧ 이속 +3%
            Assert.AreEqual(motor.dashDistance * 1.25f, motor.DashDistance, 0.0001f); Assert.AreEqual(0.8f, motor.DashCooldown, 0.0001f);
            Assert.AreEqual(5, motor.DashManaCost); Assert.AreEqual(1, motor.ExtraAirJumps); Assert.AreEqual(5.15f, motor.MoveSpeed, 0.0001f);
            yield return null; yield return null;
            Assert.AreEqual(1, motor.RemainingAirJumps, "Grounded players pick up the extra air jump right away (base is 0).");
            Assert.AreEqual(5, motor.State.DashManaCost);
            Vector3 start = player.transform.position;
            Assert.True(motor.TryDash().Succeeded);
            Assert.AreEqual(95, mana.CurrentMana, "Reduced mana cost is charged.");
            Assert.AreEqual(0.8f, motor.DashCooldownRemaining, 0.0001f, "Reduced cooldown is applied.");
            yield return new WaitForSeconds(motor.dashDuration + 0.05f);
            // 종료 뒤 0.05초는 넘겨받은 달리기 속도가 dashExitDeceleration으로 풀리며 0.2m쯤 더 간다
            Assert.That(Vector3.Distance(start, player.transform.position), Is.InRange(motor.DashDistance - 0.4f, motor.DashDistance + 0.4f), "Dash travels the modified distance.");
            Keys(Key.W); yield return new WaitForSeconds(0.7f);
            Assert.That(Vector3.ProjectOnPlane(motor.Velocity, Vector3.up).magnitude, Is.InRange(5.05f, 5.25f), "Run speed follows the modifier.");
            Keys();
            stats.RemoveAll("desert-master");
            yield return new WaitForSeconds(0.4f);
            Keys(Key.W); yield return new WaitForSeconds(0.5f);
            Assert.That(Vector3.ProjectOnPlane(motor.Velocity, Vector3.up).magnitude, Is.InRange(4.9f, 5.1f), "Removing the source restores the base speed.");
            Keys();
        }

        /// <summary>P1 회귀: 쿨다운 진행 중에 감소 수정자가 들어와도 상태 조회가 예외를 던지지 않고, 진행 중인 쿨다운은 발동 시점 길이를 유지한다.</summary>
        [UnityTest] public IEnumerator CooldownReductionDuringAnActiveCooldownKeepsStateValid()
        {
            Floor();
            var player = Player(Vector3.zero);
            var motor = player.GetComponent<PlayerMotor>();
            var stats = player.GetComponent<PlayerStats>();
            int published = 0; motor.Changed += _ => published++;
            yield return new WaitForSeconds(0.4f);
            motor.dashCooldown = 1f; motor.dashDuration = 0.3f; // 대시가 쿨다운보다 먼저 끝나야 두 번째 TryDash가 성공한다
            Assert.True(motor.TryDash().Succeeded);
            yield return new WaitForSeconds(0.05f);
            Assert.Greater(motor.DashCooldownRemaining, 0.8f);
            stats.Add(PlayerStat.DashCooldown, StatModifierKind.PercentAdd, -0.2f, "quick-reset");
            PlayerMobilityState state = default;
            Assert.DoesNotThrow(() => state = motor.State, "Remaining time above the new duration must not break the state contract.");
            Assert.AreEqual(1f, state.DashCooldownDuration, 0.0001f, "An in-progress cooldown keeps the length it started with.");
            Assert.LessOrEqual(state.DashCooldownRemaining, state.DashCooldownDuration);
            int before = published;
            yield return null; yield return null;
            Assert.Greater(published, before, "State publishing keeps working while the cooldown runs down.");
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(0f, motor.DashCooldownRemaining);
            Assert.AreEqual(0.8f, motor.State.DashCooldownDuration, 0.0001f, "Once idle, the state reports the modified length.");
            Assert.True(motor.TryDash().Succeeded);
            Assert.AreEqual(0.8f, motor.DashCooldownRemaining, 0.0001f); Assert.AreEqual(0.8f, motor.State.DashCooldownDuration, 0.0001f);
            stats.RemoveAll("quick-reset"); // 진행 중에 늘어나도 마찬가지로 발동 시점 길이를 쓴다
            Assert.DoesNotThrow(() => state = motor.State);
            Assert.AreEqual(0.8f, state.DashCooldownDuration, 0.0001f);
        }

        [UnityTest] public IEnumerator HealthAndAttackUseDamageTakenAttackDamageIntervalAndProjectileSpeed()
        {
            Floor();
            var player = Player(Vector3.zero);
            var health = player.GetComponent<PlayerHealth>();
            var attack = player.GetComponent<PlayerBasicAttack>();
            var stats = player.GetComponent<PlayerStats>();
            Assert.AreSame(stats, health.stats); Assert.AreSame(stats, attack.stats);
            stats.Add(PlayerStat.DamageTaken, StatModifierKind.PercentAdd, -0.3f, "desert-master");  // ⑧ 받는 피해 -30%
            stats.Add(PlayerStat.AttackDamage, StatModifierKind.PercentAdd, 0.3f, "condensed");       // ⑬ 마나탄 피해 +30%
            stats.Add(PlayerStat.ProjectileSpeed, StatModifierKind.PercentAdd, 0.2f, "condensed");
            stats.Add(PlayerStat.AttackInterval, StatModifierKind.Multiply, 0.5f, "test");
            Assert.AreEqual(0.7f, health.DamageTakenMultiplier, 0.0001f);
            Assert.AreEqual(13f, attack.Damage, 0.0001f); Assert.AreEqual(attack.attackInterval * 0.5f, attack.AttackInterval, 0.0001f);
            var result = health.TakeDamage(new DamageInfo(10f, "Enemy"));
            Assert.AreEqual(7f, result.AppliedDamage, 0.0001f); Assert.AreEqual(93f, health.CurrentHealth, 0.0001f);
            // 목표물 앞에서 한 발 쏜다: 조준 카메라가 +Z를 보고 그 앞에 표적이 있다.
            var aimCamera = Track(new GameObject("Aim Camera")).AddComponent<Camera>();
            aimCamera.enabled = false; aimCamera.transform.position = new Vector3(0, 1.5f, -3);
            attack.aimer.viewCamera = aimCamera;
            var targetObject = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            targetObject.transform.position = new Vector3(0, 1, 5); targetObject.transform.localScale = new Vector3(3, 2, 1);
            var target = targetObject.AddComponent<PlayerTestTarget>();
            Physics.SyncTransforms();
            yield return new WaitForSeconds(0.3f);
            float baseSpeed = attack.projectilePrefab.speed;
            int fired = 0; float boltSpeed = -1f;
            attack.visuals.onFired.AddListener(() =>
            {
                fired++;
                // 볼트는 표적에 닿는 즉시 사라지므로 발사 순간에 속도를 읽는다.
                var bolt = Object.FindObjectsByType<PlayerProjectile>(FindObjectsSortMode.None).FirstOrDefault();
                if (bolt != null) boltSpeed = bolt.speed;
            });
            // 준비 0.12초 뒤 첫 발, 이후 간격 0.15초(기본 0.3의 절반): 0.32초 동안 누르면 2발이 나간다.
            Fire(true); yield return new WaitForSeconds(0.32f); Fire(false);
            Assert.AreEqual(2, fired, "Halved attack interval fires twice within the hold.");
            Assert.AreEqual(baseSpeed * 1.2f, boltSpeed, 0.001f, "Projectile speed uses the modifier without touching the prefab.");
            Assert.AreEqual(baseSpeed, attack.projectilePrefab.speed, "The prefab keeps its base speed.");
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(2, target.HitCount);
            Assert.AreEqual(target.maxHealth - 26f, target.CurrentHealth, 0.0001f, "Modified damage reaches the target.");
        }
    }
}
#endif

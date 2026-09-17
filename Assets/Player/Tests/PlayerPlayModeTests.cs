#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace SandGuard.Player.Tests
{
    public sealed class PlayerPlayModeTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        Keyboard keyboard;
        InputSettings originalSettings, testSettings;
        GameObject Track(GameObject value) { objects.Add(value); return value; }
        GameObject Player()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Generated/Player.prefab");
            Assert.NotNull(asset, "Run SandGuard/Player/Create Missing Demo Assets first.");
            var result = Track(Object.Instantiate(asset));
            result.GetComponent<PlayerInputReader>().captureCursor = false;
            return result;
        }
        GameObject Cube(Vector3 position, Vector3 scale)
        {
            var value = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            value.transform.position = position; value.transform.localScale = scale;
            Physics.SyncTransforms();
            return value;
        }
        void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        [SetUp] public void Setup()
        {
            originalSettings = InputSystem.settings;
            testSettings = Object.Instantiate(originalSettings);
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testSettings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            InputSystem.settings = testSettings;
            keyboard = InputSystem.AddDevice<Keyboard>(); Time.timeScale = 1f;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            foreach (var value in objects) if (value != null) Object.Destroy(value);
            objects.Clear();
            foreach (var bolt in Object.FindObjectsByType<PlayerProjectile>(FindObjectsSortMode.None)) Object.Destroy(bolt.gameObject);
            InputSystem.RemoveDevice(keyboard);
            InputSystem.settings = originalSettings;
            Object.Destroy(testSettings);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            yield return null;
        }

        [UnityTest] public IEnumerator MovementStopsAtWallAndPauseBlocksMovement()
        {
            Cube(new Vector3(0, -0.5f, 0), new Vector3(20, 1, 20));
            Cube(new Vector3(0, 1.5f, 2), new Vector3(8, 3, 0.2f));
            var player = Player();
            yield return new WaitForSeconds(0.15f);
            Keys(Key.W);
            yield return new WaitForSeconds(0.7f);
            Assert.That(player.transform.position.z, Is.GreaterThan(0.5f));
            Assert.That(player.transform.position.z, Is.LessThan(1.7f), "Character must not cross the wall.");
            Vector3 before = player.transform.position;
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(Vector3.Distance(before, player.transform.position), Is.LessThan(0.001f));
        }

        [UnityTest] public IEnumerator OnlyOneAirJumpAndLandingRestoresIt()
        {
            Cube(new Vector3(0, -0.5f, 0), new Vector3(20, 1, 20));
            var motor = Player().GetComponent<PlayerMotor>();
            motor.GetComponent<PlayerEffects>().Apply(new SandGuard.Player.Effects.DoubleJumpEffect()); // 기본값은 공중 점프 0
            yield return new WaitForSeconds(0.15f);
            Assert.True(motor.IsGrounded);
            Keys(Key.Space); yield return null; yield return null;
            Keys(); yield return new WaitForSeconds(0.1f);
            Assert.False(motor.IsGrounded);
            Assert.AreEqual(1, motor.RemainingAirJumps);
            Keys(Key.Space); yield return null; yield return null;
            Assert.AreEqual(0, motor.RemainingAirJumps);
            Keys(); yield return new WaitForSeconds(0.1f);
            float velocity = motor.Velocity.y;
            Keys(Key.Space); yield return null; yield return null;
            Assert.LessOrEqual(motor.Velocity.y, velocity + 0.1f, "Third jump must not add upward velocity.");
            Keys(); yield return new WaitForSeconds(1.3f);
            Assert.True(motor.IsGrounded);
            Assert.AreEqual(1, motor.RemainingAirJumps);
        }

        [UnityTest] public IEnumerator ProjectileHitsNearestColliderOnlyOnce()
        {
            var targetObject = Cube(new Vector3(0, 1, 3), Vector3.one);
            var target = targetObject.AddComponent<PlayerTestTarget>();
            targetObject.AddComponent<SphereCollider>();
            var behind = Cube(new Vector3(0, 1, 5), Vector3.one).AddComponent<PlayerTestTarget>();
            var bolt = Track(new GameObject("Test Bolt")).AddComponent<PlayerProjectile>();
            bolt.transform.position = Vector3.up; bolt.speed = 500f;
            Physics.SyncTransforms();
            bolt.Launch(null, "Ally", 10, Vector3.forward, Vector3.up);
            yield return new WaitForSeconds(0.1f);
            Assert.AreEqual(1, target.HitCount);
            Assert.AreEqual(40f, target.CurrentHealth);
            Assert.AreEqual(0, behind.HitCount);
        }

        [UnityTest] public IEnumerator MuzzleBeyondWallCannotShootThroughIt()
        {
            Cube(new Vector3(0, 1, 1), new Vector3(4, 3, 0.1f));
            var target = Cube(new Vector3(0, 1, 4), Vector3.one).AddComponent<PlayerTestTarget>();
            var bolt = Track(new GameObject("Test Bolt")).AddComponent<PlayerProjectile>();
            bolt.transform.position = new Vector3(0, 1, 2);
            Physics.SyncTransforms();
            bolt.Launch(null, "Ally", 10, Vector3.forward, Vector3.up);
            Assert.False(bolt.IsLive, "The wall between the body and the muzzle stops the bolt at once.");
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(0, target.HitCount);
        }

        [UnityTest] public IEnumerator CameraRetractsBeforeRearWall()
        {
            Cube(new Vector3(0, -0.5f, 0), new Vector3(20, 1, 20));
            Cube(new Vector3(0, 2, -1.5f), new Vector3(10, 4, 0.2f));
            var player = Player();
            Physics.SyncTransforms();
            yield return null; yield return null;
            var camera = player.GetComponentInChildren<Camera>();
            Assert.Greater(camera.transform.position.z, -1.4f);
            Assert.Less(camera.transform.position.z, 0f);
        }

        [UnityTest] public IEnumerator VisualReplacementKeepsMotorAndUsesOptionalMuzzle()
        {
            var player = Player();
            var visuals = player.GetComponent<PlayerVisuals>();
            var motor = player.GetComponent<PlayerMotor>();
            Assert.AreEqual("RightPalmMuzzle", visuals.FirePoint.name);
            var model = Track(new GameObject("Replacement Model"));
            var bindings = model.AddComponent<PlayerVisualBindings>();
            var socket = new GameObject("Custom Muzzle").transform; socket.SetParent(model.transform);
            socket.localPosition = Vector3.up * 2f; bindings.firePoint = socket;
            visuals.visualPrefab = model; visuals.RebuildVisual();
            yield return null;
            Assert.AreSame(motor, player.GetComponent<PlayerMotor>());
            Assert.AreEqual("Custom Muzzle", visuals.FirePoint.name);
            Assert.True(visuals.FirePoint.IsChildOf(visuals.visualRoot));
            visuals.visualPrefab = null; visuals.RebuildVisual();
            yield return null;
            Assert.AreSame(visuals.fallbackFirePoint, visuals.FirePoint);
            Assert.AreEqual(0, visuals.visualRoot.childCount);
        }

        [UnityTest] public IEnumerator BasicAttackHonorsCooldownAndPause()
        {
            var attack = Player().GetComponent<PlayerBasicAttack>();
            int fired = 0;
            attack.visuals.onFired.AddListener(() => fired++);
            Assert.False(attack.TryFire(), "The first shot waits for the hand to rise.");
            yield return new WaitForSeconds(.2f);
            Assert.AreEqual(1, fired);
            Assert.False(attack.TryFire());
            yield return new WaitForSeconds(attack.attackInterval + 0.05f);
            Time.timeScale = 0f;
            Assert.False(attack.TryFire());
            Time.timeScale = 1f;
            attack.TryFire();
            yield return new WaitForSeconds(.2f);
            Assert.AreEqual(2, fired);
        }

        [Test] public void ManaReservationsDoNotCreateCapacityOrAllowDoubleSpending()
        {
            IManaWallet wallet = Track(new GameObject("Mana")).AddComponent<PlayerManaWallet>();
            int changes = 0; wallet.Changed += _ => changes++;
            Assert.True(wallet.TryReserve(95, out var reserved));
            Assert.AreEqual(100, wallet.CurrentMana);
            Assert.AreEqual(0, changes);
            Assert.AreEqual(0, wallet.Gain(int.MaxValue));
            Assert.False(wallet.TrySpend(6));
            Assert.True(wallet.TrySpend(5));
            Assert.True(reserved.TryCommit());
            Assert.False(reserved.TryCommit());
            reserved.Dispose(); reserved.Dispose();
            Assert.AreEqual(0, wallet.CurrentMana);
            Assert.AreEqual(2, changes);
            Assert.AreEqual(100, wallet.Gain(int.MaxValue));
            Assert.False(wallet.TrySpend(-1));
            Assert.False(wallet.TryReserve(-1, out _));
        }

        [Test] public void CancelledAndOldReservationsCannotSpendAfterReset()
        {
            var wallet = Track(new GameObject("Mana")).AddComponent<PlayerManaWallet>();
            Assert.True(wallet.TryReserve(100, out var cancelled));
            cancelled.Dispose(); cancelled.Dispose();
            Assert.False(cancelled.TryCommit());
            Assert.True(wallet.TryReserve(100, out var old));
            wallet.ResetWallet();
            Assert.False(old.TryCommit()); old.Dispose();
            Assert.True(wallet.TrySpend(100));
            Assert.AreEqual(0, wallet.CurrentMana);
        }

        [UnityTest] public IEnumerator DashChargesOnceAndCannotCrossWall()
        {
            Cube(new Vector3(0, -0.5f, 0), new Vector3(20, 1, 20));
            Cube(new Vector3(0, 1.5f, 2), new Vector3(8, 3, 0.2f));
            var player = Player();
            var motor = player.GetComponent<PlayerMotor>();
            IManaWallet mana = player.GetComponent<PlayerManaWallet>();
            bool observedCompleteState = false;
            mana.Changed += _ => observedCompleteState = motor.IsDashing && motor.DashCooldownRemaining == motor.dashCooldown;
            yield return new WaitForSeconds(0.1f);
            Keys(Key.LeftShift); yield return null; yield return null;
            Assert.AreEqual(90, mana.CurrentMana);
            Assert.True(observedCompleteState);
            Assert.AreEqual(ActionFailure.Cooldown, motor.TryDash().Failure);
            yield return new WaitForSeconds(motor.dashDuration + 0.05f);
            Assert.Greater(player.transform.position.z, 0.5f);
            Assert.Less(player.transform.position.z, 1.7f);
            Assert.False(motor.IsDashing);
            Assert.AreEqual(90, mana.CurrentMana);
        }

        [UnityTest] public IEnumerator DashAcrossLedgeHoldsHeightThenFallsAndLands()
        {
            Cube(new Vector3(0, -0.5f, -1), new Vector3(10, 1, 4)); // Edge at z=1.
            Cube(new Vector3(0, -0.75f, 6), new Vector3(10, 1, 10)); // Lower by 0.25m: inside normal snap distance.
            var player = Player();
            var motor = player.GetComponent<PlayerMotor>();
            yield return new WaitForSeconds(0.3f);
            Assert.True(motor.IsGrounded);
            Assert.AreEqual(0, motor.AirDashes);
            motor.dashDuration = 0.4f;
            float height = player.transform.position.y;
            float stepOffset = player.GetComponent<CharacterController>().stepOffset;
            int landings = 0; motor.Landed += _ => landings++;
            Assert.True(motor.TryDash().Succeeded);
            for (float t = 0f; motor.IsDashing && t < motor.dashDuration + 0.5f; t += Time.deltaTime)
            {
                yield return null;
                Assert.AreEqual(height, player.transform.position.y, 0.005f, "Neither gravity nor ground snapping may lower the dash, including its last frame.");
            }
            Assert.False(motor.IsDashing);
            Assert.Greater(player.transform.position.z, 3.5f);
            Assert.AreEqual(0f, motor.VerticalSpeed);
            Assert.AreEqual(0, landings);
            Assert.AreEqual(stepOffset, player.GetComponent<CharacterController>().stepOffset);
            yield return new WaitForSeconds(0.4f);
            Assert.Less(player.transform.position.y, height - 0.15f);
            Assert.True(motor.IsGrounded);
            Assert.AreEqual(1, landings);
        }

        [UnityTest] public IEnumerator FallingDashDiscardsFallSpeedAndResumesFromRest()
        {
            Cube(new Vector3(0, -0.5f, 0), new Vector3(30, 1, 30));
            var player = Player();
            var motor = player.GetComponent<PlayerMotor>();
            yield return new WaitForSeconds(0.3f);
            player.GetComponent<PlayerEffects>().Apply(new SandGuard.Player.Effects.AirDashEffect());
            player.GetComponentInChildren<PlayerCameraRig>().pitch = 0f; // 공중 대시는 조준을 따르므로 수평으로 보게 한다
            motor.Teleport(new Vector3(0, 5, 0));
            yield return new WaitForSeconds(0.2f);
            Assert.Less(motor.VerticalSpeed, -3f);
            float height = player.transform.position.y;
            Assert.True(motor.TryDash().Succeeded);
            Assert.AreEqual(0f, motor.VerticalSpeed);
            for (float t = 0f; motor.IsDashing && t < motor.dashDuration + 0.5f; t += Time.deltaTime)
            {
                yield return null;
                Assert.AreEqual(height, player.transform.position.y, 0.005f);
            }
            Assert.False(motor.IsDashing);
            Assert.AreEqual(0f, motor.VerticalSpeed);
            yield return null;
            Assert.Less(motor.VerticalSpeed, 0f);
            Assert.AreEqual(-motor.gravity * Time.deltaTime, motor.VerticalSpeed, 0.05f, "The old falling speed must not return after the dash.");
        }

        [UnityTest] public IEnumerator InsufficientAvailableManaLeavesDashUnchanged()
        {
            var player = Player();
            var motor = player.GetComponent<PlayerMotor>();
            player.GetComponent<PlayerEffects>().Apply(new SandGuard.Player.Effects.AirDashEffect()); // 바닥 없는 씬이라 공중 대시가 필요하다
            IManaWallet mana = player.GetComponent<PlayerManaWallet>();
            Assert.True(mana.TryReserve(95, out var reservation));
            Assert.AreEqual(ActionFailure.InsufficientMana, motor.TryDash().Failure);
            Assert.AreEqual(100, mana.CurrentMana);
            Assert.AreEqual(0f, motor.DashCooldownRemaining);
            Assert.False(motor.IsDashing);
            reservation.Dispose();
            Assert.True(motor.TryDash().Succeeded);
            yield return null;
        }

        /// <summary>스킬트리에서 대시를 배우면 더블 점프처럼 공중 대시 1회가 함께 열린다. 스킬트리가 없으면 기존대로 스탯 값이다.</summary>
        [UnityTest] public IEnumerator LearningTheDashOnTheSkillTreeAlsoGrantsOneAirDash()
        {
            var player = Player();
            var motor = player.GetComponent<PlayerMotor>();
            player.GetComponentInChildren<PlayerCameraRig>().pitch = 0f;
            Assert.AreEqual(0, motor.AirDashes, "스킬트리가 없으면 스탯 값(기본 0) 그대로다.");
            bool learned = false;
            motor.SkillTreeDashAllowed = () => learned;
            Assert.AreEqual(0, motor.AirDashes, "스킬트리는 있는데 대시를 안 배웠으면 0이다.");
            motor.Teleport(new Vector3(0, 4, 0)); yield return null; yield return null;
            Assert.AreEqual(ActionFailure.Locked, motor.TryDash().Failure);
            learned = true;
            Assert.AreEqual(1, motor.AirDashes, "대시를 배우면 공중 대시 1회가 함께 열린다.");
            Assert.AreEqual(1, motor.RemainingAirDashes);
            Assert.True(motor.TryDash().Succeeded, "공중에서 바로 대시할 수 있다.");
            Assert.AreEqual(0, motor.RemainingAirDashes);
            yield return new WaitForSeconds(motor.dashDuration + 0.05f);
            motor.ResetDashCooldown();
            Assert.AreEqual(ActionFailure.Locked, motor.TryDash().Failure, "한 번 뜬 동안 한 번뿐이다. 착지하면 다시 찬다.");
        }

        [UnityTest] public IEnumerator DashPauseFreezesMovementAndCooldown()
        {
            var player = Player();
            var motor = player.GetComponent<PlayerMotor>();
            player.GetComponent<PlayerEffects>().Apply(new SandGuard.Player.Effects.AirDashEffect()); // 바닥 없는 씬이라 공중 대시가 필요하다
            Assert.True(motor.TryDash().Succeeded);
            Time.timeScale = 0f;
            var position = player.transform.position;
            float cooldown = motor.DashCooldownRemaining;
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(position, player.transform.position);
            Assert.AreEqual(cooldown, motor.DashCooldownRemaining);
            Assert.AreEqual(ActionFailure.Paused, motor.TryDash().Failure);
        }

        [UnityTest] public IEnumerator AirDashFollowsTheAimNotTheStickAndPreservesAirJump()
        {
            var player = Player();
            var motor = player.GetComponent<PlayerMotor>();
            player.GetComponent<PlayerEffects>().Apply(new SandGuard.Player.Effects.AirDashEffect()); // 기본값은 공중 대시 0
            player.GetComponentInChildren<PlayerCameraRig>().pitch = 0f;
            motor.Teleport(new Vector3(0, 4, 0));
            Keys(Key.D); yield return null; yield return null; // 오른쪽을 밀고 있어도 대시는 크로스헤어(정면)로 간다
            int airJumps = motor.RemainingAirJumps;
            Assert.True(motor.TryDash().Succeeded);
            yield return new WaitForSeconds(motor.dashDuration + 0.05f);
            Assert.Greater(player.transform.position.z, 3f, "질풍참 방식: 조준 방향으로 간다.");
            Assert.Less(Mathf.Abs(player.transform.position.x), 0.5f, "이동 입력은 대시 방향에 영향을 주지 않는다.");
            Assert.AreEqual(airJumps, motor.RemainingAirJumps);
        }

        [UnityTest] public IEnumerator LethalDamageLocksActionsBeforeEventsAndOnlyDiesOnce()
        {
            var player = Player();
            var health = player.GetComponent<PlayerHealth>();
            var motor = player.GetComponent<PlayerMotor>();
            var attack = player.GetComponent<PlayerBasicAttack>();
            player.GetComponent<PlayerRespawner>().enabled = false; // 부활 없이 사망 상태의 잠금만 본다
            player.GetComponent<PlayerEffects>().Apply(new SandGuard.Player.Effects.AirDashEffect()); // 바닥 없는 씬이라 공중 대시가 필요하다
            int deaths = 0, hits = 0;
            health.Died += _ => deaths++;
            health.Damaged += _ => hits++;
            health.HealthChanged += _ =>
            {
                Assert.False(health.IsTargetable);
                Assert.False(attack.TryFire());
                Assert.AreEqual(ActionFailure.NotAlive, motor.TryDash().Failure);
            };
            Assert.True(motor.TryDash().Succeeded);
            var result = health.TakeDamage(new DamageInfo(200f, "Enemy"));
            Assert.True(result.WasKilled);
            Assert.AreEqual(100f, result.AppliedDamage);
            Assert.AreEqual(LifeState.Incapacitated, health.State);
            Assert.False(motor.IsDashing);
            Assert.AreEqual(DamageStatus.NotAlive, health.TakeDamage(new DamageInfo(10f, "Enemy")).Status);
            var position = player.transform.position;
            Keys(Key.W); yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(position, player.transform.position);
            Assert.AreEqual(1, deaths); Assert.AreEqual(1, hits);
        }

        [Test] public void HealthRejectsInvalidFriendlyAndPausedDamageWithoutEvents()
        {
            var health = Track(new GameObject("Health")).AddComponent<PlayerHealth>();
            int events = 0; health.Damaged += _ => events++;
            Assert.AreEqual(DamageStatus.InvalidRequest, health.TakeDamage(default).Status);
            Assert.AreEqual(DamageStatus.NonHostile, health.TakeDamage(new DamageInfo(25, "Ally")).Status);
            Time.timeScale = 0f;
            Assert.AreEqual(DamageStatus.Protected, health.TakeDamage(new DamageInfo(25, "Enemy")).Status);
            Assert.AreEqual(100f, health.CurrentHealth); Assert.AreEqual(0, events);
        }
    }
}
#endif

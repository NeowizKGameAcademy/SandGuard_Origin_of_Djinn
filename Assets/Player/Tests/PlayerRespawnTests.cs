#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using DesertTower.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SandGuard.Player.Tests
{
    /// <summary>사망 → 스폰 지점 부활, 레벨 마커(PlayerStart / Respawn) 사용, 지연·보호 옵션 검증.</summary>
    public sealed class PlayerRespawnTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        Keyboard keyboard;
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
        void Floor(float size)
        {
            var value = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            value.transform.position = new Vector3(0, -0.5f, 0); value.transform.localScale = new Vector3(size, 1, size);
            Physics.SyncTransforms();
        }
        static LevelMarker Marker(LevelRoot root, MarkerKind kind, Vector3 position, float yaw = 0f)
        {
            var marker = new GameObject(kind.ToString()).AddComponent<LevelMarker>();
            marker.transform.SetParent(root.transform, false);
            marker.transform.position = position; marker.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            marker.kind = kind; marker.id = kind + position.ToString(); marker.label = marker.id;
            return marker;
        }
        void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        static void Kill(PlayerHealth health)
        {
            var result = health.TakeDamage(new DamageInfo(1000f, "Enemy"));
            Assert.True(result.WasKilled);
            Assert.AreEqual(LifeState.Incapacitated, health.State);
        }
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
            InputSystem.RemoveDevice(keyboard);
            InputSystem.settings = originalSettings;
            Object.Destroy(testSettings);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            yield return null;
        }

        [UnityTest] public IEnumerator DiesForOneFrameThenRespawnsAtTheStartPositionWithFullControl()
        {
            Floor(60f);
            var player = Player(Vector3.zero);
            var health = player.GetComponent<PlayerHealth>();
            var motor = player.GetComponent<PlayerMotor>();
            var respawner = player.GetComponent<PlayerRespawner>();
            var crosshair = player.GetComponent<PlayerCrosshair>();
            Assert.NotNull(respawner, "Player.prefab carries a PlayerRespawner.");
            int respawns = 0; Vector3 from = Vector3.zero, to = Vector3.zero; int revived = 0;
            respawner.Respawned += (a, b) => { respawns++; from = a; to = b; };
            health.Revived += _ => revived++;
            yield return new WaitForSeconds(0.4f);
            var animator = player.GetComponentInChildren<Animator>();
            Keys(Key.W); yield return new WaitForSeconds(0.6f); Keys();
            Assert.Greater(player.transform.position.z, 1.5f);
            Vector3 deathPosition = player.transform.position;
            IManaWallet mana = player.GetComponent<PlayerManaWallet>();
            motor.dashCooldown = 1f; // 프리팹 값과 무관하게 쿨다운 회복을 검사한다
            Assert.True(motor.TryDash().Succeeded, "Spend mana and start the dash cooldown before dying.");
            Assert.Less(mana.CurrentMana, mana.MaxMana);
            Assert.Greater(motor.DashCooldownRemaining, 0f);
            Kill(health);
            Assert.True(respawner.IsRespawning);
            Assert.AreEqual(0, respawns, "The death frame keeps the player incapacitated.");
            yield return null; yield return null;
            Assert.AreEqual(LifeState.Alive, health.State);
            Assert.AreEqual(health.MaxHealth, health.CurrentHealth);
            Assert.AreEqual(1, respawns); Assert.AreEqual(1, revived);
            Assert.Less(Vector3.Distance(from, deathPosition), 0.05f);
            Assert.Less(Vector3.Distance(to, Vector3.zero), 0.05f, "Without level markers the player returns to where it started.");
            Assert.Less(Vector3.Distance(player.transform.position, Vector3.zero), 0.3f);
            Assert.True(player.GetComponent<CharacterController>().enabled, "Colliders disabled on death come back.");
            Assert.True(health.IsTargetable, "No protection by default.");
            Assert.AreEqual(mana.MaxMana, mana.CurrentMana, "Mana is fully restored on respawn.");
            Assert.AreEqual(0f, motor.DashCooldownRemaining, "Dash cooldown is cleared on respawn.");
            Assert.AreEqual(0f, player.GetComponent<PlayerBasicAttack>().CooldownRemaining);
            Assert.AreEqual(motor.extraAirJumps, motor.RemainingAirJumps);
            Assert.True(motor.TryDash().Succeeded, "Dash is available right after respawning.");
            Keys(Key.W); yield return new WaitForSeconds(0.4f); Keys();
            Assert.Greater(player.transform.position.z, 0.5f, "Movement works again after respawning.");
            for (float t = 0f; t < 0.6f && !animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"); t += Time.deltaTime) yield return null;
            Assert.True(animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"), "The death pose does not stick after reviving.");
            Assert.True(crosshair.Visible);
            Assert.AreEqual(1, respawner.RespawnCount);
        }

        [UnityTest] public IEnumerator StartsAtThePlayerStartMarkerAndRespawnsAtTheNearestRespawnMarker()
        {
            Floor(200f);
            var level = Track(new GameObject("Level")).AddComponent<LevelRoot>();
            Marker(level, MarkerKind.PlayerStart, new Vector3(10f, 0f, 10f), 90f);
            Marker(level, MarkerKind.Respawn, new Vector3(-8f, 0f, 0f));
            Marker(level, MarkerKind.Respawn, new Vector3(30f, 0f, 0f));
            var player = Player(Vector3.zero);
            var health = player.GetComponent<PlayerHealth>();
            var motor = player.GetComponent<PlayerMotor>();
            var rig = player.GetComponentInChildren<PlayerCameraRig>();
            yield return null; yield return null; yield return null;
            Assert.Less(Vector3.Distance(player.transform.position, new Vector3(10f, 0.1f, 10f)), 0.3f, "The designer's PlayerStart marker decides where the player begins.");
            Assert.Greater(player.transform.forward.x, 0.95f, "A rotated marker sets the facing.");
            Assert.Greater(Vector3.Dot(Vector3.ProjectOnPlane(rig.transform.forward, Vector3.up).normalized, Vector3.right), 0.9f, "The camera follows the marker facing.");
            yield return new WaitForSeconds(0.3f);
            motor.Teleport(new Vector3(25f, 0f, 0f));
            yield return new WaitForSeconds(0.3f);
            Kill(health);
            yield return null; yield return null;
            Assert.AreEqual(LifeState.Alive, health.State);
            Assert.Less(Vector3.Distance(player.transform.position, new Vector3(30f, 0.1f, 0f)), 0.3f, "Respawns at the Respawn marker nearest to the death position.");
            var respawner = player.GetComponent<PlayerRespawner>();
            respawner.preferRespawnMarkers = false;
            Kill(health);
            yield return null; yield return null;
            Assert.Less(Vector3.Distance(player.transform.position, new Vector3(10f, 0.1f, 10f)), 0.3f, "Without Respawn markers (or when disabled) the PlayerStart marker is used.");
        }

        [UnityTest] public IEnumerator RespawnDelayAndReviveProtectionAreHonored()
        {
            Floor(60f);
            var player = Player(Vector3.zero);
            var health = player.GetComponent<PlayerHealth>();
            var respawner = player.GetComponent<PlayerRespawner>();
            respawner.respawnDelay = 0.3f; health.reviveProtection = 0.5f;
            yield return new WaitForSeconds(0.3f);
            Kill(health);
            yield return new WaitForSeconds(0.15f);
            Assert.AreEqual(LifeState.Incapacitated, health.State, "Stays down until the delay elapses.");
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(LifeState.Alive, health.State);
            Assert.True(health.IsProtected); Assert.False(health.IsTargetable, "Protected players leave enemy target selection.");
            Assert.AreEqual(DamageStatus.Protected, health.TakeDamage(new DamageInfo(10f, "Enemy")).Status);
            yield return new WaitForSeconds(0.6f);
            Assert.False(health.IsProtected); Assert.True(health.IsTargetable);
            Assert.AreEqual(DamageStatus.Applied, health.TakeDamage(new DamageInfo(10f, "Enemy")).Status);
            Assert.AreEqual(90f, health.CurrentHealth);
        }
    }
}
#endif

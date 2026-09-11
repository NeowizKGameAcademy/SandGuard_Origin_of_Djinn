#if UNITY_EDITOR
using System;
using System.Collections;
using NUnit.Framework;
using SandGuard.Enemy;
using SandGuard.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SandGuard.Integration.Tests
{
    public sealed class PlayerAndEnemyPlayModeTests
    {
        const string ScenePath = "Assets/PlayerAndEnemy/Generated/PlayerAndEnemyTest.unity";

        IEnumerator Until(Func<bool> condition, float seconds, string message)
        {
            float deadline = Time.time + seconds;
            while (!condition() && Time.time < deadline) yield return null;
            Assert.True(condition(), message);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            var empty = SceneManager.CreateScene("Empty " + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene != empty && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        [UnityTest] public IEnumerator EnemiesSpawnOnNavMeshAndEngagePlayerOrCore()
        {
            Assert.True(System.IO.File.Exists(ScenePath), "Run SandGuard/Player And Enemy/Create Test Scene first.");
            Time.timeScale = 3f;
            yield return EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var motor = UnityEngine.Object.FindFirstObjectByType<PlayerMotor>();
            var director = UnityEngine.Object.FindFirstObjectByType<SandGuard.Waves.WaveDirector>();
            Assert.NotNull(motor); Assert.NotNull(director, "The scene must run waves through a WaveDirector.");
            director.SkipPreparation();
            var player = motor.gameObject;
            player.GetComponent<PlayerInputReader>().captureCursor = false;
            Assert.AreEqual(1, player.GetComponents<ICombatTarget>().Length, "The player must expose exactly one combat target.");
            var damageEvents = player.GetComponent<IDamageEvents>();
            Assert.NotNull(damageEvents, "The player must report damage so enemies and VFX can react.");
            EnemyTestTarget core = null;
            foreach (var target in UnityEngine.Object.FindObjectsByType<EnemyTestTarget>(FindObjectsSortMode.None)) if (target.kind == CombatTargetKind.Core) core = target;
            Assert.NotNull(core, "Scene must contain a core target.");
            int activeListeners = 0, activeCameras = 0;
            foreach (var listener in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) if (listener.isActiveAndEnabled) activeListeners++;
            foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) if (camera.isActiveAndEnabled) activeCameras++;
            Assert.AreEqual(1, activeListeners, "Only the player camera's listener may be active.");
            Assert.AreEqual(1, activeCameras, "Only the player camera may be active.");

            yield return Until(() => UnityEngine.Object.FindObjectsByType<EnemyMotor>(FindObjectsSortMode.None).Length >= 3, 40f, "Wave 1 enemies must spawn.");
            var motors = UnityEngine.Object.FindObjectsByType<EnemyMotor>(FindObjectsSortMode.None);
            foreach (var enemy in motors) Assert.True(enemy.IsOnNavMesh, enemy.name + " must stand on the level NavMesh.");

            bool playerHit = false;
            damageEvents.Damaged += _ => playerHit = true;
            yield return Until(() => playerHit || core.HitCount > 0, 120f, "Enemies must reach and attack the player or the core.");
        }

        [UnityTest] public IEnumerator FallbackCombatTargetIncapacitatesThenRevivesWithProtection()
        {
            var go = new GameObject("Fallback player");
            var player = go.AddComponent<PlayerCombatTarget>();
            player.autoReviveDelay = 0.2f; player.reviveProtection = 0.5f;
            int died = 0, revived = 0;
            player.Died += _ => died++; player.Revived += _ => revived++;
            yield return null;
            var result = player.TakeDamage(new DamageInfo(1000f, "Enemy"));
            Assert.True(result.WasKilled);
            Assert.AreEqual(LifeState.Incapacitated, player.State);
            Assert.False(player.IsTargetable);
            Assert.AreEqual(DamageStatus.NotAlive, player.TakeDamage(new DamageInfo(10f, "Enemy")).Status);
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(1, revived); Assert.AreEqual(1, died);
            Assert.AreEqual(LifeState.Alive, player.State);
            Assert.AreEqual(player.MaxHealth, player.CurrentHealth);
            Assert.AreEqual(DamageStatus.Protected, player.TakeDamage(new DamageInfo(10f, "Enemy")).Status, "Revive protection rejects damage.");
            yield return new WaitForSeconds(0.6f);
            Assert.True(player.TakeDamage(new DamageInfo(10f, "Enemy")).WasApplied, "Protection must expire.");
            UnityEngine.Object.Destroy(go);
        }
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SandGuard.Enemy.Tests
{
    /// <summary>
    /// 적 낙사(EnemyFall): 밀려서 절벽을 넘으면 떨어지고, 치명 높이면 죽고, 아니면 NavMesh로 돌아온다. 벽에 밀리면 그대로다.
    /// 앞부분은 만든 지형으로 각 갈래를 하나씩 고정해 검사하고, 뒷부분은 실제 레벨(DesertTemple) 복사 씬에서 같은 흐름을 확인한다.
    /// </summary>
    public sealed class EnemyFallTests
    {
        const string ScenePath = "Assets/Enemy/Generated/EnemyFallTest.unity";
        const float PushSpeed = 3f; // PlayerSandVortex.pullSpeed
        readonly List<GameObject> objects = new List<GameObject>();
        GameObject level;
        bool sceneLoaded;

        GameObject Track(GameObject value) { objects.Add(value); return value; }
        GameObject Cube(string name, Vector3 center, Vector3 size, Transform parent = null)
        {
            var value = GameObject.CreatePrimitive(PrimitiveType.Cube);
            value.name = name;
            if (parent != null) value.transform.SetParent(parent, false); else Track(value);
            value.transform.position = center; value.transform.localScale = size;
            return value;
        }
        /// <summary>자식 큐브들로 NavMesh를 굽는다. 이후 만드는 큐브는 NavMesh에 들어가지 않는다.</summary>
        void Bake(params (string name, Vector3 center, Vector3 size)[] cubes)
        {
            level = Track(new GameObject("Level"));
            foreach (var cube in cubes) Cube(cube.name, cube.center, cube.size, level.transform);
            var surface = level.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
        }
        GameObject Enemy(Vector3 position, Quaternion? rotation = null)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Generated/Enemy.prefab");
            Assert.NotNull(asset, "Run SandGuard/Enemy/Create Missing Demo Assets first.");
            var value = Track(UnityEngine.Object.Instantiate(asset, position, rotation ?? Quaternion.identity));
            value.name = "Enemy";
            Assert.NotNull(value.GetComponent<EnemyFall>(), "Enemy.prefab carries an EnemyFall (SandGuard/Enemy/Create Missing Demo Assets adds it).");
            value.GetComponent<EnemyBrain>().AIEnabled = false; // 제자리에 서서 밀림만 받는다
            return value;
        }
        IEnumerator Until(Func<bool> condition, float seconds, string message)
        {
            float deadline = Time.time + seconds;
            while (!condition() && Time.time < deadline) yield return null;
            Assert.True(condition(), message);
        }
        /// <summary>소용돌이처럼 매 프레임 direction으로 민다. stop이 참이 되거나 시간이 다하면 그친다.</summary>
        IEnumerator PushUntil(GameObject enemy, Vector3 direction, Func<bool> stop, float seconds)
        {
            var displaceable = enemy.GetComponent<IDisplaceable>();
            Assert.NotNull(displaceable, "Enemy exposes IDisplaceable (EnemyRestraint).");
            float deadline = Time.time + seconds;
            while (!stop() && Time.time < deadline && enemy != null)
            {
                displaceable.Displace(direction.normalized * PushSpeed * Time.deltaTime);
                yield return null;
            }
        }

        [SetUp] public void Setup() { Time.timeScale = 3f; sceneLoaded = false; }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            foreach (var value in objects) if (value != null) UnityEngine.Object.Destroy(value);
            objects.Clear();
            yield return null;
            if (!sceneLoaded) yield break;
            var empty = SceneManager.CreateScene("Empty " + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene != empty && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        [UnityTest] public IEnumerator PushedIntoAWallStaysOnTheNavMesh()
        {
            Bake(("Ground", new Vector3(0, -0.5f, 0), new Vector3(10, 1, 10)), ("Wall", new Vector3(0, 1.5f, 3), new Vector3(10, 3, 0.5f)));
            var enemy = Enemy(Vector3.zero);
            var fall = enemy.GetComponent<EnemyFall>(); var motor = enemy.GetComponent<EnemyMotor>();
            yield return null;
            Assert.True(motor.IsOnNavMesh);
            yield return PushUntil(enemy, Vector3.forward, () => false, 1.5f);
            Assert.AreEqual(EnemyFallState.OnNavMesh, fall.State, "A wall is not a ledge.");
            Assert.True(motor.IsOnNavMesh, "The agent stays on the NavMesh against a wall.");
            Assert.Less(enemy.transform.position.z, 2.8f, "The wall stops the push.");
            Assert.Greater(enemy.transform.position.z, 1.5f, "The push moved the enemy up to the wall.");
        }

        [UnityTest] public IEnumerator ShortDropLandsOnTheLowerNavMeshAndReattaches()
        {
            Bake(("Ground", new Vector3(0, -0.5f, 0), new Vector3(30, 1, 30)), ("Platform", new Vector3(0, 1.5f, 0), new Vector3(6, 3, 6)));
            var enemy = Enemy(new Vector3(0, 3, 0));
            var fall = enemy.GetComponent<EnemyFall>(); var motor = enemy.GetComponent<EnemyMotor>(); var health = enemy.GetComponent<EnemyHealth>();
            fall.fatalDropHeight = 5f;
            float landedDrop = -1f; bool landedFatal = true; int returned = 0; bool walked = false;
            fall.Landed += (drop, fatal) => { landedDrop = drop; landedFatal = fatal; };
            fall.Returned += w => { returned++; walked = w; };
            yield return null;
            Assert.True(motor.IsOnNavMesh); Assert.AreEqual(3f, enemy.transform.position.y, 0.2f);
            yield return PushUntil(enemy, Vector3.right, () => fall.State == EnemyFallState.Falling, 6f);
            Assert.AreEqual(EnemyFallState.Falling, fall.State, "Pushing over the platform edge makes the enemy fall.");
            Assert.False(motor.IsOnNavMesh); Assert.True(motor.IsDetached);
            Assert.AreEqual(1, fall.FallCount);
            yield return Until(() => fall.State == EnemyFallState.OnNavMesh, 6f, "The enemy lands and returns to the NavMesh.");
            Assert.True(health.IsAlive, "A 3 m drop is not fatal.");
            Assert.AreEqual(3f, landedDrop, 0.5f, "Landed reports the drop height."); Assert.False(landedFatal);
            Assert.AreEqual(1, returned); Assert.True(walked, "Landing on the NavMesh reattaches in place.");
            Assert.True(motor.IsOnNavMesh); Assert.False(motor.IsDetached);
            Assert.AreEqual(0f, enemy.transform.position.y, 0.3f, "The enemy stands on the lower ground.");
            Assert.True(motor.TrySetDestination(new Vector3(10, 0, 10)), "The agent can path again after reattaching.");
            Vector3 before = enemy.transform.position;
            yield return new WaitForSeconds(1f);
            Assert.Greater(EnemyMotor.Planar(before, enemy.transform.position), 0.5f, "The enemy walks again.");
        }

        [UnityTest] public IEnumerator FatalDropKillsOnLandingAndTheBodyStaysOnTheGround()
        {
            Bake(("Ground", new Vector3(0, -0.5f, 0), new Vector3(30, 1, 30)), ("Platform", new Vector3(0, 4, 0), new Vector3(6, 8, 6)));
            var enemy = Enemy(new Vector3(0, 8, 0));
            var fall = enemy.GetComponent<EnemyFall>(); var motor = enemy.GetComponent<EnemyMotor>(); var health = enemy.GetComponent<EnemyHealth>();
            fall.fatalDropHeight = 5f;
            float landedDrop = -1f; bool landedFatal = false; LifeState stateAtLanding = LifeState.Alive; DeathInfo? death = null;
            fall.Landed += (drop, fatal) => { landedDrop = drop; landedFatal = fatal; stateAtLanding = health.State; };
            health.Died += info => death = info;
            yield return null;
            yield return PushUntil(enemy, Vector3.right, () => fall.State == EnemyFallState.Falling, 6f);
            Assert.AreEqual(EnemyFallState.Falling, fall.State);
            yield return Until(() => landedDrop >= 0f, 6f, "The enemy lands.");
            Assert.AreEqual(8f, landedDrop, 0.5f); Assert.True(landedFatal, "An 8 m drop is fatal.");
            Assert.AreEqual(LifeState.Dying, stateAtLanding, "Listeners of Landed see the final state.");
            Assert.True(death.HasValue, "EnemyHealth announces the death.");
            Assert.AreEqual("fall", death.Value.KillingDamage.CauseId); Assert.AreEqual("World", death.Value.KillingDamage.SourceFactionId);
            Assert.AreEqual(0f, enemy.transform.position.y, 0.3f, "The body lies where it landed.");
            Assert.False(motor.IsOnNavMesh);
            yield return Until(() => enemy == null, 5f, "The body is removed after removeDelay.");
        }

        [UnityTest] public IEnumerator FallingIntoTheVoidDiesAndDespawnsWithoutABody()
        {
            Bake(("Platform", new Vector3(0, 9.5f, 0), new Vector3(6, 1, 6)));
            var enemy = Enemy(new Vector3(0, 10, 0));
            var fall = enemy.GetComponent<EnemyFall>(); var health = enemy.GetComponent<EnemyHealth>();
            fall.voidDepth = 5f;
            DeathInfo? death = null; bool landed = false;
            health.Died += info => death = info;
            fall.Landed += (_, __) => landed = true;
            yield return null;
            yield return PushUntil(enemy, Vector3.forward, () => fall.State == EnemyFallState.Falling, 6f);
            Assert.AreEqual(EnemyFallState.Falling, fall.State);
            yield return Until(() => enemy == null, 6f, "Below voidDepth the enemy dies and is removed at once.");
            Assert.True(death.HasValue, "The void death is still a death (kill credit).");
            Assert.AreEqual("fall", death.Value.KillingDamage.CauseId);
            Assert.False(landed, "No landing happens in the void.");
        }

        [UnityTest] public IEnumerator OffMeshLandingWalksBackToTheNavMesh()
        {
            Bake(("Ground", new Vector3(0, -0.5f, 0), new Vector3(10, 1, 10)));
            Cube("Unbaked ground", new Vector3(10, -0.5f, 0), new Vector3(10, 1, 10)); // 굽지 않은 바닥: 걸어서 돌아와야 한다
            var enemy = Enemy(Vector3.zero);
            var fall = enemy.GetComponent<EnemyFall>(); var motor = enemy.GetComponent<EnemyMotor>();
            int returned = 0; bool walked = false;
            fall.Returned += w => { returned++; walked = w; };
            yield return null;
            fall.LeaveNavMesh();
            fall.Push(new Vector3(9f, 0f, 0f));
            Assert.AreEqual(EnemyFallState.Pushed, fall.State); Assert.True(motor.IsDetached);
            float maxStep = 0f; Vector3 previous = enemy.transform.position;
            float deadline = Time.time + 8f;
            while (fall.State != EnemyFallState.OnNavMesh && Time.time < deadline)
            {
                yield return null;
                maxStep = Mathf.Max(maxStep, EnemyMotor.Planar(previous, enemy.transform.position));
                previous = enemy.transform.position;
            }
            Assert.AreEqual(EnemyFallState.OnNavMesh, fall.State, "The enemy walks back onto the NavMesh.");
            Assert.AreEqual(1, returned); Assert.True(walked, "It returned by walking, not by being moved.");
            Assert.Less(maxStep, 0.6f, "No teleport: every frame moved less than a stride.");
            Assert.True(motor.IsOnNavMesh);
            Assert.Less(enemy.transform.position.x, 5f, "It stands on the baked ground again.");
        }

        [UnityTest] public IEnumerator BlockedRecoveryMovesToTheNearestNavMeshPoint()
        {
            Bake(("Ground", new Vector3(0, -0.5f, 0), new Vector3(10, 1, 10)));
            Cube("Unbaked ground", new Vector3(10, -0.5f, 0), new Vector3(10, 1, 10));
            Cube("Unbaked wall", new Vector3(6.5f, 1.5f, 0), new Vector3(0.5f, 3, 10)); // 돌아가는 길을 막는다
            var enemy = Enemy(Vector3.zero);
            var fall = enemy.GetComponent<EnemyFall>(); var motor = enemy.GetComponent<EnemyMotor>();
            fall.stuckSeconds = 0.5f;
            bool? walked = null;
            fall.Returned += w => walked = w;
            yield return null;
            fall.LeaveNavMesh();
            fall.Push(new Vector3(9f, 0f, 0f));
            yield return Until(() => fall.State == EnemyFallState.OnNavMesh, 6f, "Stuck behind the wall, the enemy is moved back to the NavMesh.");
            Assert.True(walked.HasValue); Assert.False(walked.Value, "Returned reports that it was moved, not walked.");
            Assert.True(motor.IsOnNavMesh);
            Assert.Less(enemy.transform.position.x, 5.5f, "It stands on the baked side of the wall.");
        }

        // ---- 실제 레벨(DesertTemple 복사 씬) ----

        IEnumerator LoadTempleScene()
        {
            Assert.True(System.IO.File.Exists(ScenePath), "Run SandGuard/Enemy/Create Fall Test Scene (Desert Temple) first.");
            sceneLoaded = true;
            yield return EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            Assert.Greater(NavMesh.CalculateTriangulation().indices.Length, 0, "The copied level carries its baked NavMesh.");
        }

        /// <summary>조건에 맞는 가장자리를 차례로 시도해 실제로 떨어지는 자리를 찾는다. 장식물 등에 막힌 자리는 건너뛴다.</summary>
        IEnumerator SpawnAndPushOff(Func<NavMeshLedge, bool> accept, Action<GameObject> ready, Action<GameObject, NavMeshLedge> fell, int attempts = 6)
        {
            var prefabFall = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Generated/Enemy.prefab").GetComponent<EnemyFall>();
            var candidates = new List<NavMeshLedge>();
            foreach (var ledge in NavMeshLedges.Find(prefabFall.ledgeDrop)) if (accept(ledge)) candidates.Add(ledge);
            if (candidates.Count == 0) Assert.Ignore("Desert Temple has no NavMesh ledge matching this case.");
            for (int i = 0; i < Mathf.Min(attempts, candidates.Count); i++)
            {
                var ledge = candidates[i * candidates.Count / Mathf.Min(attempts, candidates.Count)];
                var enemy = Enemy(ledge.Inside, Quaternion.LookRotation(ledge.Outward));
                ready(enemy);
                var fall = enemy.GetComponent<EnemyFall>();
                yield return null;
                if (!enemy.GetComponent<EnemyMotor>().IsOnNavMesh) { UnityEngine.Object.Destroy(enemy); yield return null; continue; }
                yield return PushUntil(enemy, ledge.Outward, () => fall.State == EnemyFallState.Falling, 4f);
                if (fall.State != EnemyFallState.Falling) { UnityEngine.Object.Destroy(enemy); yield return null; continue; }
                fell(enemy, ledge);
                yield break;
            }
            Assert.Fail("None of the candidate ledges let the enemy fall when pushed.");
        }

        [UnityTest] public IEnumerator DesertTempleSceneHasTheFallHudAndLedges()
        {
            yield return LoadTempleScene();
            var overlay = UnityEngine.Object.FindFirstObjectByType<EnemyFallDemoOverlay>();
            Assert.NotNull(overlay, "The fall test scene carries the EnemyFallDemoOverlay HUD.");
            Assert.NotNull(overlay.enemyPrefab);
            Assert.NotNull(overlay.enemyPrefab.GetComponent<EnemyFall>());
            int cameras = 0, listeners = 0;
            foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) if (camera.isActiveAndEnabled) cameras++;
            foreach (var listener in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) if (listener.isActiveAndEnabled) listeners++;
            Assert.AreEqual(1, cameras, "Exactly one active camera follows the enemy.");
            Assert.AreEqual(1, listeners, "Exactly one active audio listener.");
            yield return null;
            int total = 0;
            foreach (EnemyFallDemoOverlay.Kind kind in Enum.GetValues(typeof(EnemyFallDemoOverlay.Kind))) total += overlay.Ledges(kind).Count;
            Debug.Log("Desert Temple ledges: fatal " + overlay.Ledges(EnemyFallDemoOverlay.Kind.Fatal).Count + ", safe→navmesh " + overlay.Ledges(EnemyFallDemoOverlay.Kind.SafeOntoNavMesh).Count
                + ", safe→off " + overlay.Ledges(EnemyFallDemoOverlay.Kind.SafeOffNavMesh).Count + ", void " + overlay.Ledges(EnemyFallDemoOverlay.Kind.Void).Count);
            Assert.Greater(total, 0, "The level NavMesh has ledges to test on.");
        }

        [UnityTest] public IEnumerator DesertTempleFatalLedgeKillsThePushedEnemy()
        {
            yield return LoadTempleScene();
            var prefabFall = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Generated/Enemy.prefab").GetComponent<EnemyFall>();
            float fatal = prefabFall.fatalDropHeight;
            GameObject enemy = null; NavMeshLedge used = default;
            yield return SpawnAndPushOff(l => !l.IsVoid && l.Drop >= fatal + 0.5f, _ => { }, (e, l) => { enemy = e; used = l; });
            var fall = enemy.GetComponent<EnemyFall>(); var health = enemy.GetComponent<EnemyHealth>();
            float landedDrop = -1f; bool landedFatal = false;
            fall.Landed += (drop, isFatal) => { landedDrop = drop; landedFatal = isFatal; };
            yield return Until(() => landedDrop >= 0f || enemy == null, 8f, "The enemy lands (or vanishes into the void).");
            if (enemy == null) { Assert.False(health == null ? false : health.IsAlive); yield break; }
            Assert.True(landedFatal, "Ledge drop " + used.Drop.ToString("F1") + " m ≥ fatal " + fatal + " m kills on landing (landed " + landedDrop.ToString("F1") + " m).");
            Assert.AreEqual(LifeState.Dying, health.State);
            Assert.False(enemy.GetComponent<EnemyMotor>().IsOnNavMesh);
        }

        [UnityTest] public IEnumerator DesertTempleSurvivableLedgeReturnsThePushedEnemyToTheNavMesh()
        {
            yield return LoadTempleScene();
            var prefabFall = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Generated/Enemy.prefab").GetComponent<EnemyFall>();
            float fatal = prefabFall.fatalDropHeight, minDrop = prefabFall.ledgeDrop;
            GameObject enemy = null; NavMeshLedge used = default;
            yield return SpawnAndPushOff(l => !l.IsVoid && l.Drop >= minDrop + 0.4f && l.Drop <= fatal - 1f, _ => { }, (e, l) => { enemy = e; used = l; });
            var fall = enemy.GetComponent<EnemyFall>(); var motor = enemy.GetComponent<EnemyMotor>(); var health = enemy.GetComponent<EnemyHealth>();
            int returned = 0;
            fall.Returned += _ => returned++;
            yield return Until(() => fall.State == EnemyFallState.OnNavMesh || !health.IsAlive, 15f, "The enemy lands and returns to the NavMesh (ledge drop " + used.Drop.ToString("F1") + " m).");
            Assert.True(health.IsAlive, "A drop below the fatal height (" + used.Drop.ToString("F1") + " m) is survived.");
            Assert.AreEqual(1, returned);
            Assert.True(motor.IsOnNavMesh, "Back on the level NavMesh.");
            enemy.GetComponent<EnemyBrain>().AIEnabled = true;
            yield return new WaitForSeconds(1f);
            Assert.AreNotEqual(EnemyBrainState.Dead, enemy.GetComponent<EnemyBrain>().State);
        }
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SandGuard.Enemy.Tests
{
    /// <summary>
    /// 담당자 타워 씬(Tower.unity)에 실제 적을 세워 통합 연결을 확인한다. 타워 스크립트는 Assembly-CSharp라 이름으로만 찾는다.
    /// 코브라: 실제 피해 요청, 정지 요청, 본체 파괴(받침은 남고 계속 막음). 오벨리스크: 둔화 요청.
    /// </summary>
    public sealed class TowerIntegrationTests
    {
        const string TowerScene = "Assets/1.Scene/Tower.unity";
        readonly List<GameObject> spawned = new List<GameObject>();

        static Type TowerType(string name) => Type.GetType(name + ", Assembly-CSharp");

        IEnumerator LoadTowerScene()
        {
            LogAssert.ignoreFailingMessages = false;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(TowerScene, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var capsule = GameObject.Find("Enemy Capsule"); // 담당자 임시 적. 이 테스트는 실제 적을 쓴다.
            if (capsule != null) capsule.SetActive(false);
            var plane = GameObject.Find("Plane");
            Assert.NotNull(plane, "Tower.unity keeps its ground plane.");
            var surface = plane.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            yield return new WaitForSeconds(.3f); // 받침 장애물이 NavMesh를 깎을 시간
        }

        static Transform Body(string kind)
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name != "Tower Base") continue;
                var body = root.transform.Find("Tower (" + kind + ")");
                if (body != null) return body;
            }
            Assert.Fail("Tower (" + kind + ") not found in Tower.unity");
            return null;
        }

        EnemyHealth Enemy(Vector3 position)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Generated/Enemy.prefab");
            var enemy = UnityEngine.Object.Instantiate(asset, position, Quaternion.identity);
            spawned.Add(enemy);
            enemy.GetComponent<EnemyBrain>().AIEnabled = false; // 제자리에 서서 타워의 요청만 받는다
            return enemy.GetComponent<EnemyHealth>();
        }

        static IEnumerator Until(Func<bool> condition, float seconds, string message)
        {
            float deadline = Time.time + seconds;
            while (!condition() && Time.time < deadline) yield return null;
            Assert.True(condition(), message);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach (var go in spawned) if (go != null) UnityEngine.Object.Destroy(go);
            spawned.Clear();
            yield return null;
        }

        [UnityTest] public IEnumerator CobraSendsRealDamageToARealEnemy()
        {
            yield return LoadTowerScene();
            var cobra = Body("Cobra");
            Assert.NotNull(cobra.GetComponent<IHealth>(), "Run SandGuard/Facility/Add Combat Health To Towers first.");
            var enemy = Enemy(new Vector3(5f, 0f, 7f));
            float start = enemy.CurrentHealth;
            DamageInfo? last = null;
            enemy.Damaged += info => last = info.Damage;
            yield return Until(() => enemy.CurrentHealth < start, 10f, "The cobra must find the untagged enemy and damage it through IDamageable.");
            Assert.True(last.Value.CauseId.StartsWith("tower."), "Tower damage is marked with a tower. cause: " + last.Value.CauseId);
            Assert.True(last.Value.HitDirection.HasValue, "Tower damage carries a direction for shields.");
            Assert.AreEqual(((ICombatTarget)cobra.GetComponent<IHealth>()).EntityId, last.Value.SourceEntityId, "The source is the tower body.");
        }

        [UnityTest] public IEnumerator ObeliskRequestsASlowThatWearsOffOutsideTheRange()
        {
            yield return LoadTowerScene();
            var obelisk = Body("Obelisk");
            var slowRange = obelisk.Find("Slow Range").GetComponent<SphereCollider>();
            // 담당자 씬의 Slow Range 반경은 0.5m(패치 문서의 확인 사항). 요청 경로만 보려고 테스트에서만 넓힌다.
            slowRange.radius = 6f;
            var enemy = Enemy(obelisk.position + new Vector3(4f, 0f, -1f));
            ISlowable slowable = enemy.GetComponent<ISlowable>();
            yield return Until(() => slowable.SlowFactor > .45f, 5f, "The obelisk must request a slow through ISlowable.");
            Assert.AreEqual(.5f, slowable.SlowFactor, .01f, "SlowRatio 0.5 (half speed) becomes slow amount 0.5.");
            enemy.GetComponent<EnemyMotor>().Enable(obelisk.position + new Vector3(40f, 0f, -20f)); // 범위 밖으로 옮긴다
            yield return Until(() => slowable.SlowFactor <= 0f, 3f, "Without refreshing requests the slow wears off by itself.");
        }

        [UnityTest] public IEnumerator DisableRequestStopsTheCobraWithVfxThenItResumes()
        {
            yield return LoadTowerScene();
            var cobra = Body("Cobra");
            var target = (ICombatTarget)cobra.GetComponent<IHealth>();
            var enemy = Enemy(new Vector3(5f, 0f, 7f));
            float start = enemy.CurrentHealth;
            yield return Until(() => enemy.CurrentHealth < start, 10f, "The cobra attacks before the request.");

            CombatEffectSignals.RequestTowerDisable(new TowerDisableRequest(target.EntityId, 1.5f, new DamageInfo(0f, "Enemy", causeId: "enemy.bomb")));
            var fire = (Behaviour)cobra.GetComponentInChildren(TowerType("RangeController"), true);
            Assert.False(fire.enabled, "The fire range stops while disabled.");
            Assert.NotNull(GameObject.Find("VFX_Facility_Disabled_Loop(Clone)"), "The disabled loop VFX plays on the cobra.");
            float frozen = enemy.CurrentHealth;
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(frozen, enemy.CurrentHealth, "No damage while disabled.");
            yield return new WaitForSeconds(.6f);
            Assert.True(fire.enabled, "The tower resumes after the duration.");
            Assert.Null(GameObject.Find("VFX_Facility_Disabled_Loop(Clone)"), "The loop VFX is returned when the tower resumes.");
            yield return Until(() => enemy.CurrentHealth < frozen, 5f, "Damage resumes after the disable ends.");
        }

        [UnityTest] public IEnumerator BreakingTheCobraKeepsTheBaseAndStopsItsFire()
        {
            yield return LoadTowerScene();
            var cobra = Body("Cobra");
            var baseRoot = cobra.parent;
            var enemy = Enemy(new Vector3(5f, 0f, 7f));
            float start = enemy.CurrentHealth;
            yield return Until(() => enemy.CurrentHealth < start, 10f, "The cobra attacks first.");

            Assert.True(((IDamageable)cobra.GetComponent<IHealth>()).TakeDamage(new DamageInfo(100000f, "Enemy")).WasKilled);
            Assert.NotNull(GameObject.Find("VFX_Cobra_Destruction(Clone)"), "Death plays the cobra destruction VFX.");
            yield return new WaitForSeconds(1f);
            Assert.False(cobra.gameObject.activeSelf, "Only the body breaks.");
            Assert.True(baseRoot.gameObject.activeInHierarchy && baseRoot.Find("Mesh").gameObject.activeInHierarchy, "The base stays.");
            var solid = baseRoot.GetComponent<BoxCollider>();
            Assert.True(solid != null && solid.enabled && !solid.isTrigger, "The base keeps blocking the player.");
            Assert.NotNull(baseRoot.GetComponent<UnityEngine.AI.NavMeshObstacle>(), "The base keeps blocking enemies.");
            float after = enemy.CurrentHealth;
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(after, enemy.CurrentHealth, "A broken tower stops attacking.");
        }

        [Test] public void TheBasePrefabBlocksPlayersAndEnemies()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/2.Model/Prefabs/Tower Base.prefab");
            var solid = prefab.GetComponent<BoxCollider>();
            Assert.True(solid != null && !solid.isTrigger && solid.size.y >= 2.2f, "Tower Base.prefab has a solid collider at least player height.");
            var obstacle = prefab.GetComponent<UnityEngine.AI.NavMeshObstacle>();
            Assert.True(obstacle != null && obstacle.carving, "Tower Base.prefab carves the NavMesh.");
        }
    }
}
#endif

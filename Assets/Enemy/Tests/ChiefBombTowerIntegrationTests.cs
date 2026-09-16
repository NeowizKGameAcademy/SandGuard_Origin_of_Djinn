#if UNITY_EDITOR
using System;
using System.Collections;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SandGuard.Enemy.Tests
{
    /// <summary>
    /// 우두머리가 스스로 타워를 보고 철거 폭탄을 던지면, Level에서 실제로 짓는 코브라 타워가 멈췄다가 다시 동작하는지 본다.
    /// 폭탄은 정지를 "요청"만 하고 멈추는 일은 타워의 TowerDisableReceiver가 하므로, 두 쪽이 실제 프리팹끼리 맞물리는지가 핵심이다.
    /// 타워 스크립트는 Assembly-CSharp라 이름으로 찾는다.
    /// </summary>
    public sealed class ChiefBombTowerIntegrationTests
    {
        const string CobraPrefab = "Assets/Facility/Generated/Towers/Tower_Cobra.prefab";
        GameObject ground, chief, tower;

        static Type TowerType(string name) => Type.GetType(name + ", Assembly-CSharp");

        IEnumerator Until(Func<bool> condition, float seconds, string message)
        {
            float end = Time.time + seconds;
            while (!condition() && Time.time < end) yield return null;
            Assert.True(condition(), message);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (chief) Object.Destroy(chief);
            if (tower) Object.Destroy(tower);
            if (ground) Object.Destroy(ground);
            foreach (var bomb in Object.FindObjectsByType<ChiefBombProp>(FindObjectsSortMode.None)) Object.Destroy(bomb.gameObject);
            yield return null;
        }

        [UnityTest] public IEnumerator ChiefThrowsBombAtBuiltCobraWhichStopsThenResumes()
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0, -.5f, 0); ground.transform.localScale = new Vector3(40, 1, 40);
            var nav = ground.AddComponent<NavMeshSurface>(); nav.collectObjects = CollectObjects.Children;
            nav.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders; nav.BuildNavMesh();

            tower = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(CobraPrefab), new Vector3(0, 0, 6), Quaternion.identity);
            var towerTarget = tower.GetComponent<ICombatTarget>();
            Assert.NotNull(towerTarget, "코브라 본체가 전투 대상이어야 폭탄이 찾는다");
            Assert.AreEqual(CombatTargetKind.Tower, towerTarget.Kind);
            var receiverType = TowerType("TowerDisableReceiver");
            Assert.NotNull(receiverType, "TowerDisableReceiver 타입을 찾지 못했다");
            var receiver = tower.GetComponent(receiverType);
            Assert.NotNull(receiver, "코브라에 정지 수신기가 붙어 있어야 한다");
            var isDisabled = receiverType.GetProperty("IsDisabled");
            var fire = (Behaviour)tower.GetComponentInChildren(TowerType("RangeController"), true);
            var aim = (Behaviour)tower.GetComponentInChildren(TowerType("FindEnemy"), true);
            bool Disabled() => (bool)isDisabled.GetValue(receiver);

            chief = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Generated/Enemy_Chief.prefab"),
                Vector3.zero, Quaternion.identity);
            var bomb = chief.GetComponent<ChiefBombThrowSkill>();
            bomb.towerDisableDuration = 1.5f; // 테스트 시간을 줄인다. 기본은 5초
            var brain = chief.GetComponent<EnemyBrain>();
            Physics.SyncTransforms();
            yield return null;
            Assert.False(Disabled(), "시작 시에는 동작 중이다");

            brain.AIEnabled = true;
            yield return Until(() => bomb.ThrowCount == 1, 15f, "우두머리가 타워를 보고 폭탄을 던져야 한다");
            Assert.AreSame(towerTarget, bomb.AimTarget, "폭탄은 코브라를 겨냥했다");
            brain.AIEnabled = false; // 던진 뒤에는 세워 둔다. 근접으로 타워를 부수면 정지·재개를 볼 수 없다

            yield return Until(Disabled, 6f, "폭탄이 터지면 코브라가 정지해야 한다");
            Assert.False(fire.enabled, "정지 중에는 공격 범위가 꺼진다");
            Assert.False(aim.enabled, "정지 중에는 조준이 꺼진다");
            Assert.NotNull(GameObject.Find("VFX_Facility_Disabled_Loop(Clone)"), "정지 연출이 뜬다");
            Assert.True(towerTarget.IsTargetable, "폭탄 피해로 부서지지 않았다");

            yield return Until(() => !Disabled(), 3f, "정지 시간이 끝나면 다시 동작해야 한다");
            Assert.True(fire.enabled && aim.enabled, "공격·조준이 다시 켜진다");
            Assert.True(bomb.IsSpent, "철거 폭탄은 한 번만 쓴다");
        }
    }
}
#endif

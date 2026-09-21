#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace SandGuard.Enemy.Tests
{
    /// <summary>
    /// 큰 밀림(사막 폭풍 넉백)에 에이전트가 NavMesh를 놓쳤을 때 스스로 돌아오는지.
    /// 돌아오지 못하면 <see cref="EnemyMotor.TrySetDestination"/>이 계속 실패해 두뇌가 영영 Idle로 남는다.
    /// </summary>
    public sealed class EnemyMotorRegroundTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        GameObject Track(GameObject value) { objects.Add(value); return value; }

        [SetUp] public void Setup() { Time.timeScale = 1f; }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach (var value in objects) if (value != null) Object.Destroy(value);
            objects.Clear();
            yield return null;
        }

        [UnityTest] public IEnumerator AnAgentThatLosesTheNavMeshComesBackOnItsOwn()
        {
            var level = Track(new GameObject("Level"));
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetParent(level.transform, false);
            ground.transform.position = new Vector3(0, -0.5f, 0); ground.transform.localScale = new Vector3(20, 1, 20);
            var surface = level.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children; surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders; surface.BuildNavMesh();

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Generated/Enemy.prefab");
            Assert.NotNull(asset, "Run SandGuard/Enemy/Create Missing Demo Assets first.");
            var enemy = Track(Object.Instantiate(asset, Vector3.zero, Quaternion.identity));
            var motor = enemy.GetComponent<EnemyMotor>();
            yield return null;
            Assert.True(motor.IsOnNavMesh, "시작은 NavMesh 위다.");

            // 폭풍 넉백이 걷기 영역 밖으로 밀어낸 상태를 그대로 만든다: 에이전트는 켜져 있는데 NavMesh만 놓쳤다.
            // (떼어 낸 것이 아니므로 EnemyFall은 이 이탈을 모른다.)
            motor.Agent.enabled = false;
            enemy.transform.position = new Vector3(300f, 0f, 300f);
            motor.Agent.enabled = true;
            Assert.False(motor.IsOnNavMesh, "NavMesh 밖으로 나갔다.");
            Assert.False(motor.IsDetached, "낙하로 떼어 낸 것이 아니라 그냥 놓친 상태다.");
            Assert.False(motor.TrySetDestination(Vector3.zero), "NavMesh 밖에서는 목적지를 잡지 못한다 — 두뇌가 멈추는 원인.");

            float deadline = Time.time + 2f;
            while (!motor.IsOnNavMesh && Time.time < deadline) yield return null;
            Assert.True(motor.IsOnNavMesh, "모터가 스스로 NavMesh로 돌아온다.");
            Assert.Less(EnemyMotor.Planar(enemy.transform.position, Vector3.zero), 12f, "마지막으로 서 있던 자리 근처로 돌아온다.");
            Assert.True(motor.TrySetDestination(new Vector3(5f, 0f, 5f)), "돌아온 뒤에는 다시 목적지를 잡는다.");
        }
    }
}
#endif

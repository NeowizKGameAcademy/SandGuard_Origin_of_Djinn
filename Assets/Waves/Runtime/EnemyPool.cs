using System.Collections.Generic;
using SandGuard.Enemy;
using UnityEngine;

namespace SandGuard.Waves
{
    /// <summary>적 프리팹별 오브젝트 풀. 제거된 적은 파괴 대신 비활성으로 보관했다가 다음 스폰에서 되살린다.</summary>
    /// <remarks>재사용 시 각 부품의 ResetForReuse를 불러 새 EntityId·체력·상태로 돌린다. 웨이브 집계는 새 ID로 다시 센다.</remarks>
    public sealed class EnemyPool : MonoBehaviour
    {
        readonly Dictionary<GameObject, Stack<GameObject>> idle = new Dictionary<GameObject, Stack<GameObject>>();
        readonly Dictionary<GameObject, GameObject> sourceOf = new Dictionary<GameObject, GameObject>();
        readonly HashSet<GameObject> rented = new HashSet<GameObject>();
        readonly HashSet<GameObject> used = new HashSet<GameObject>();
        Transform reserveRoot;
        public int ActiveCount { get { rented.RemoveWhere(go => go == null); return rented.Count; } }
        public int CreatedCount { get; private set; }
        public int ReusedCount { get; private set; }
        public int IdleCount { get { int n = 0; foreach (var stack in idle.Values) n += stack.Count; return n; } }

        /// <summary>비활성 예비 개체를 한 번에 하나 만든다. 준비 코루틴이 프레임별 생성량을 제한한다.</summary>
        public void PrewarmOne(GameObject prefab)
        {
            if (prefab == null) return;
            if (reserveRoot == null)
            {
                var reserve = new GameObject("Inactive Reserve");
                reserve.SetActive(false); reserve.transform.SetParent(transform, false); reserveRoot = reserve.transform;
            }
            if (!idle.TryGetValue(prefab, out var stack)) idle[prefab] = stack = new Stack<GameObject>();
            var instance = Instantiate(prefab, reserveRoot);
            instance.SetActive(false); sourceOf[instance] = prefab; stack.Push(instance); CreatedCount++;
        }

        public int AvailableCount(GameObject prefab)
        {
            if (!idle.TryGetValue(prefab, out var stack)) return 0;
            int count = 0; foreach (var go in stack) if (go != null) count++;
            return count;
        }

        public int OwnedCount(GameObject prefab)
        {
            int count = 0;
            foreach (var pair in sourceOf) if (pair.Key != null && pair.Value == prefab) count++;
            return count;
        }

        public GameObject Rent(GameObject prefab, Vector3 position, Quaternion rotation, int maxActive = 0)
        {
            if (prefab == null) return null;
            if (maxActive > 0 && ActiveCount >= maxActive) return null;
            if (!idle.TryGetValue(prefab, out var stack)) idle[prefab] = stack = new Stack<GameObject>();
            GameObject instance = null;
            while (stack.Count > 0 && instance == null) instance = stack.Pop();
            if (instance == null)
            {
                instance = Instantiate(prefab, position, rotation);
                sourceOf[instance] = prefab;
                rented.Add(instance); used.Add(instance);
                var health = instance.GetComponent<EnemyHealth>();
                if (health != null) health.ReleaseHandler = Release;
                CreatedCount++;
                return instance;
            }
            instance.transform.SetParent(null, false);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);
            var reusedHealth = instance.GetComponent<EnemyHealth>();
            if (reusedHealth != null) reusedHealth.ReleaseHandler = Release;
            rented.Add(instance);
            Revive(instance, position);
            if (!used.Add(instance)) ReusedCount++;
            return instance;
        }

        static void Revive(GameObject instance, Vector3 position)
        {
            instance.GetComponent<EnemyHealth>()?.ResetForReuse();
            instance.GetComponent<EnemyFall>()?.ResetForReuse();
            instance.GetComponent<EnemyMotor>()?.Enable(position);
            var brain = instance.GetComponent<EnemyBrain>();
            // 경로 러너(ActorBridge)가 AI를 멈추고 조종 중인 채로 반납할 수 있으므로 재사용 때 초기화한다.
            if (brain != null) { brain.enabled = true; brain.ResetForReuse(); }
            instance.GetComponent<EnemyMeleeAttack>()?.ResetForReuse();
            instance.GetComponent<EnemyVisuals>()?.ResetForReuse();
        }

        void Release(EnemyHealth health)
        {
            if (health == null) return;
            var instance = health.gameObject;
            if (!rented.Remove(instance)) return;
            if (!sourceOf.TryGetValue(instance, out var prefab) || this == null) { Destroy(instance); return; }
            instance.SetActive(false);
            instance.transform.SetParent(transform, false);
            idle[prefab].Push(instance);
        }

        public void Clear()
        {
            foreach (var stack in idle.Values) while (stack.Count > 0) { var go = stack.Pop(); if (go != null) Destroy(go); }
            // Active leases retain their return mapping when only idle storage is cleared.
            var remove = new List<GameObject>();
            foreach (var pair in sourceOf) if (pair.Key == null || !rented.Contains(pair.Key)) remove.Add(pair.Key);
            foreach (var go in remove) { sourceOf.Remove(go); used.Remove(go); }
        }
    }
}

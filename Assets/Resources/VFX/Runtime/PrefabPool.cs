using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DesertTower.VFX
{
    /// <summary>풀에서 꺼내고 되돌릴 때 알림을 받는다. 구독·수명 같은 재사용하면 안 되는 상태를 여기서 초기화한다.</summary>
    public interface IPoolable
    {
        void OnRent();
        void OnReturn();
    }

    /// <summary>풀이 만든 개체의 꼬리표. 원본 프리팹·원본 크기·대여 세대를 기억한다.</summary>
    [DisallowMultipleComponent]
    public sealed class PooledInstance : MonoBehaviour
    {
        public GameObject Source { get; internal set; }
        public Vector3 SourceScale { get; internal set; }
        /// <summary>대여할 때마다 오른다. 지연 반납은 세대가 그대로일 때만 실행되어 이미 다시 빌려 간 개체를 거두지 않는다.</summary>
        public int Generation { get; internal set; }
        public bool IsIdle { get; internal set; }
    }

    /// <summary>
    /// 짧게 살고 자주 생기는 프리팹(투사체·이펙트)용 범용 풀.
    /// <c>Destroy(Instantiate(prefab, p, r), t)</c>를 <c>PrefabPool.Release(PrefabPool.Spawn(prefab, p, r), t)</c>로 바꾸면 된다.
    /// 씬마다 하나가 필요할 때 만들어지고 씬과 함께 사라진다. 되돌릴 때 파티클을 멈춰 비활성으로 보관하고,
    /// 꺼낼 때 원본 크기로 되돌린 뒤 Play On Awake 파티클을 다시 튼다. 풀이 만들지 않은 개체를 Release하면 그냥 Destroy한다.
    /// </summary>
    public sealed class PrefabPool : MonoBehaviour
    {
        static PrefabPool instance;
        readonly Dictionary<GameObject, Stack<PooledInstance>> idle = new Dictionary<GameObject, Stack<PooledInstance>>();
        public int CreatedCount { get; private set; }
        public int ReusedCount { get; private set; }
        public int IdleCount { get { int n = 0; foreach (var stack in idle.Values) foreach (var pooled in stack) if (pooled != null) n++; return n; } }

        /// <summary>씬의 풀. 없으면 만든다.</summary>
        public static PrefabPool Instance
        {
            get
            {
                if (instance == null) instance = FindFirstObjectByType<PrefabPool>();
                if (instance == null) instance = new GameObject("Prefab Pool").AddComponent<PrefabPool>();
                return instance;
            }
        }

        /// <summary>풀이 이미 있는지. 종료·씬 전환 중에 새로 만들지 않으려고 본다.</summary>
        public static bool Exists => instance != null;

        void Awake() { if (instance == null) instance = this; }
        void OnDestroy() { if (instance == this) instance = null; }

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
            => prefab == null ? null : Instance.Rent(prefab, position, rotation, parent);

        public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
        {
            if (prefab == null) return null;
            GameObject instance = Spawn(prefab.gameObject, position, rotation, parent);
            return instance != null ? instance.GetComponent<T>() : null;
        }

        /// <summary>풀로 되돌린다. 풀이 만들지 않은 개체는 Destroy한다. null은 무시한다.</summary>
        public static void Release(GameObject go, float delay = 0f)
        {
            if (go == null) return;
            var pooled = go.GetComponent<PooledInstance>();
            if (pooled == null || pooled.Source == null || !Exists) { Destroy(go, Mathf.Max(0f, delay)); return; }
            if (delay <= 0f) instance.Return(pooled);
            else instance.StartCoroutine(instance.ReturnAfter(pooled, pooled.Generation, delay));
        }

        public static void Release(Component component, float delay = 0f) { if (component != null) Release(component.gameObject, delay); }

        /// <summary>이 개체가 풀에서 나왔는지.</summary>
        public static bool IsPooled(GameObject go) => go != null && go.GetComponent<PooledInstance>() != null;

        GameObject Rent(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
        {
            if (!idle.TryGetValue(prefab, out var stack)) idle[prefab] = stack = new Stack<PooledInstance>();
            PooledInstance pooled = null;
            while (stack.Count > 0 && pooled == null) pooled = stack.Pop(); // 부모와 함께 파괴된 개체는 건너뛴다
            if (pooled == null)
            {
                GameObject created = Instantiate(prefab, position, rotation, parent);
                pooled = created.AddComponent<PooledInstance>();
                pooled.Source = prefab;
                pooled.SourceScale = prefab.transform.localScale;
                CreatedCount++;
            }
            else
            {
                Transform t = pooled.transform;
                t.SetParent(parent, false);
                t.SetPositionAndRotation(position, rotation);
                t.localScale = pooled.SourceScale;
                pooled.IsIdle = false;
                pooled.gameObject.SetActive(true);
                foreach (var trail in pooled.GetComponentsInChildren<TrailRenderer>(true)) trail.Clear();
                foreach (var system in pooled.GetComponentsInChildren<ParticleSystem>(true))
                    if (system.main.playOnAwake && !system.isPlaying) system.Play(false);
                ReusedCount++;
            }
            pooled.Generation++;
            foreach (var poolable in pooled.GetComponentsInChildren<IPoolable>(true)) poolable.OnRent();
            return pooled.gameObject;
        }

        IEnumerator ReturnAfter(PooledInstance pooled, int generation, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (pooled != null && pooled.Generation == generation) Return(pooled);
        }

        void Return(PooledInstance pooled)
        {
            if (pooled == null || pooled.IsIdle) return;
            if (pooled.Source == null) { Destroy(pooled.gameObject); return; }
            foreach (var poolable in pooled.GetComponentsInChildren<IPoolable>(true)) poolable.OnReturn();
            foreach (var system in pooled.GetComponentsInChildren<ParticleSystem>(true))
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            pooled.IsIdle = true;
            pooled.gameObject.SetActive(false);
            pooled.transform.SetParent(transform, false);
            if (!idle.TryGetValue(pooled.Source, out var stack)) idle[pooled.Source] = stack = new Stack<PooledInstance>();
            stack.Push(pooled);
        }

        /// <summary>미리 만들어 둔다. 웨이브 준비 단계처럼 첫 사용의 끊김을 없애고 싶을 때 쓴다.</summary>
        public void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null) return;
            if (!idle.TryGetValue(prefab, out var stack)) idle[prefab] = stack = new Stack<PooledInstance>();
            // 대여를 거치지 않고 바로 보관해야 한 개체를 count번 빌렸다 돌려주는 꼴이 되지 않는다.
            for (int i = 0; i < count; i++)
            {
                GameObject created = Instantiate(prefab, transform);
                var pooled = created.AddComponent<PooledInstance>();
                pooled.Source = prefab;
                pooled.SourceScale = prefab.transform.localScale;
                pooled.IsIdle = true;
                created.SetActive(false);
                stack.Push(pooled);
                CreatedCount++;
            }
        }

        /// <summary>보관 중인 개체를 전부 파괴한다. 빌려 간 개체는 그대로 두며, 돌아오면 다시 보관한다.</summary>
        public void Clear()
        {
            foreach (var stack in idle.Values) while (stack.Count > 0) { var pooled = stack.Pop(); if (pooled != null) Destroy(pooled.gameObject); }
            idle.Clear();
        }
    }
}

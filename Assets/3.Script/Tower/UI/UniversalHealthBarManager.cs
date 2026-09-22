using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tower.UI
{
    public sealed class UniversalHealthBarManager : MonoBehaviour
    {
        [SerializeField] private UniversalHealthBar healthBarPrefab;
        [Min(1)] [SerializeField] private int poolSize = 32;
        [Min(0.1f)] [SerializeField] private float searchInterval = 0.5f;

        private readonly Queue<UniversalHealthBar> pool = new();
        private readonly Dictionary<Component, UniversalHealthBar> active = new();

        private void Awake()
        {
            for (int i = 0; i < poolSize; i++)
            {
                var bar = Instantiate(healthBarPrefab, transform);
                bar.gameObject.SetActive(false);
                pool.Enqueue(bar);
            }
        }

        private void Start() => StartCoroutine(TrackHealth());

        private IEnumerator TrackHealth()
        {
            var wait = new WaitForSeconds(searchInterval);
            while (true)
            {
                foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                {
                    if (behaviour is not IHealth health || !IsTowerOrSummon(behaviour)
                        || active.ContainsKey(behaviour) || pool.Count == 0) continue;
                    var bar = pool.Dequeue();
                    bar.SetTarget(behaviour, health);
                    bar.gameObject.SetActive(true);
                    active.Add(behaviour, bar);
                }

                foreach (var pair in new List<KeyValuePair<Component, UniversalHealthBar>>(active))
                {
                    if (pair.Key != null && pair.Key.gameObject.activeInHierarchy) continue;
                    pair.Value.ClearTarget();
                    pair.Value.gameObject.SetActive(false);
                    pool.Enqueue(pair.Value);
                    active.Remove(pair.Key);
                }
                yield return wait;
            }
        }

        private static bool IsTowerOrSummon(MonoBehaviour behaviour)
        {
            return behaviour is TowerHealth
                || behaviour.GetComponentInParent<TowerStatus>() != null;
        }
    }
}

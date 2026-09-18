using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TowerHPBarManager : MonoBehaviour
{
    [SerializeField] private TowerHPBar hpBarPrefab;
    [SerializeField] private int poolSize = 20;

    [Header("Search")]
    [SerializeField] private float searchTime = 0.5f;

    private Queue<TowerHPBar> pool = new Queue<TowerHPBar>();

    private Dictionary<TowerStatus, TowerHPBar> activeHPBars = new Dictionary<TowerStatus, TowerHPBar>();

    private void Awake()
    {
        CreatePool();
    }

    private void Start()
    {
        StartCoroutine(SearchTower());
    }

    private void CreatePool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            TowerHPBar hpBar = Instantiate(hpBarPrefab, transform);

            hpBar.gameObject.SetActive(false);

            pool.Enqueue(hpBar);
        }
    }

    private IEnumerator SearchTower()
    {
        while (true)
        {
            FindTowers();
            RemoveTowers();

            yield return new WaitForSeconds(searchTime);
        }
    }

    private void FindTowers()
    {
        TowerStatus[] towers = FindObjectsByType<TowerStatus>(
            FindObjectsSortMode.None
        );

        foreach (TowerStatus tower in towers)
        {
            // 이미 HPBar가 있으면 무시
            if (activeHPBars.ContainsKey(tower))
                continue;

            TowerHPBar hpBar = GetHPBar(tower);

            if (hpBar != null)
                activeHPBars.Add(tower, hpBar);
        }
    }

    private void RemoveTowers()
    {
        List<TowerStatus> removeTowers = new List<TowerStatus>();

        foreach (var pair in activeHPBars)
        {
            TowerStatus tower = pair.Key;

            if (tower == null || !tower.gameObject.activeInHierarchy)
            {
                ReturnHPBar(pair.Value);
                removeTowers.Add(tower);
            }
        }

        foreach (TowerStatus tower in removeTowers)
        {
            activeHPBars.Remove(tower);
        }
    }

    private TowerHPBar GetHPBar(TowerStatus tower)
    {
        if (pool.Count == 0)
            return null;

        TowerHPBar hpBar = pool.Dequeue();

        hpBar.SetTarget(tower);
        hpBar.gameObject.SetActive(true);

        return hpBar;
    }

    private void ReturnHPBar(TowerHPBar hpBar)
    {
        hpBar.ClearTarget();
        hpBar.gameObject.SetActive(false);

        pool.Enqueue(hpBar);
    }
}
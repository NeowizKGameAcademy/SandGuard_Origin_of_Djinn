using System.Collections;
using UnityEngine;

public class CoffinSummon : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private DetectRange Range;
    [SerializeField] private TowerStatus Status;
    [SerializeField] private ObjectPooling CreaturePool;
    [SerializeField] private float RespawnCoolDown = 5f;

    private Vector3[] summonPoints;
    private CreatureController[] summonedCreatures;

    private void Awake()
    {
        TryGetComponent(out Status);
        TryGetComponent(out Range);
    }

    private void OnEnable()
    {
        StartCoroutine(InitializeNextFrame());
    }

    private IEnumerator InitializeNextFrame()
    {
        yield return null;

        SetPoints();
        SummonAll();
    }

    private void OnDisable()
    {
        DespawnAll();
    }

    public void SetPoints()
    {
        if (Status.creatureNum <= 0)
            return;

        summonPoints = new Vector3[Status.creatureNum];
        summonedCreatures = new CreatureController[Status.creatureNum];

        float radius = Range.range * Status.summonDistance;
        float angleStep = 360f / Status.creatureNum;

        for (int i = 0; i < Status.creatureNum; i++)
        {
            float angle = angleStep * i;
            float radian = angle * Mathf.Deg2Rad;

            Vector3 offset = new Vector3(
                Mathf.Sin(radian),
                0.1f,
                Mathf.Cos(radian)
            );

            summonPoints[i] = transform.position + offset * radius;
        }
    }

    public void SummonAll()
    {
        for (int i = 0; i < Status.creatureNum; i++)
        {
            Summon(i);
        }
    }

    public void Summon(int index)
    {
        GameObject obj = CreaturePool.GetObject();

        obj.transform.position = summonPoints[index];

        CreatureController creature = obj.GetComponent<CreatureController>();

        creature.Initialize(this, index, summonPoints[index]);
        summonedCreatures[index] = creature;

        obj.SetActive(true);
    }

    private void DespawnAll()
    {
        if (summonedCreatures == null)
            return;

        for (int i = 0; i < summonedCreatures.Length; i++)
        {
            if (summonedCreatures[i] == null)
                continue;

            summonedCreatures[i].Despawn();
            summonedCreatures[i] = null;
        }
    }

    public void Respawn(int index)
    {
        summonedCreatures[index] = null;

        StartCoroutine(RespawnCoroutine(index));
    }

    private IEnumerator RespawnCoroutine(int index)
    {
        yield return new WaitForSeconds(RespawnCoolDown);

        Summon(index);
    }
}
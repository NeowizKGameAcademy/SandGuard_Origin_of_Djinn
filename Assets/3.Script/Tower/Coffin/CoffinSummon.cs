using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CoffinSummon : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private DetectRange Range;

    [Header("Summon")]
    [SerializeField] private int summonNum;
    [SerializeField] private ObjectPooling CreaturePool;

    private Vector3[] summonPoints;

    private void OnEnable()
    {
        TryGetComponent(out Range);

        SetPoints();
    }

    public void SetPoints()
    {
        if (summonNum <= 0)
            return;

        summonPoints = new Vector3[summonNum];

        float radius = Range.range * 0.5f;
        float angleStep = 360f / summonNum;

        for (int i = 0; i < summonNum; i++)
        {
            float angle = angleStep * i;
            float radian = angle * Mathf.Deg2Rad;

            Vector3 offset = new Vector3( Mathf.Sin(radian), 0f, Mathf.Cos(radian));

            summonPoints[i] = transform.position + offset * radius;
        }
    }

    public void Summon() { }
}

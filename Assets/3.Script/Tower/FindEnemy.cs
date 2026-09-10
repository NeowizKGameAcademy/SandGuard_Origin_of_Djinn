using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FindEnemy : MonoBehaviour
{
    [Header("Auto Aim")]
    [SerializeField] private float Auto_Aim_Range = 20f;
    [SerializeField] private float SearchTime = 0.2f;

    [SerializeField] private GameObject Range;

    private float Auto_Aim_Distance;
    private float SearchTimer;

    private Transform Target_Transform;

    //Properties
    public Transform target => Target_Transform;
    public float range => Auto_Aim_Range;

    private void Update()
    {
        Auto_Aim_Range = Mathf.Clamp(Auto_Aim_Range, 0f, 50f);

        Auto_Aim_Distance = Auto_Aim_Range * Auto_Aim_Range;

        if (Target_Transform != null)
        {
            if (!Target_Transform.gameObject.activeInHierarchy)
            {
                Target_Transform = null;
                return;
            }

            float Target_Distance = (Target_Transform.position - transform.position).sqrMagnitude;

            if (Target_Distance > Auto_Aim_Distance)
            {
                Target_Transform = null;
                return;
            }

            return;
        }

        SearchTimer -= Time.deltaTime;

        if (SearchTimer <= 0f)
        {
            FindClosestTarget();
            SearchTimer = SearchTime;
        }
    }

    private void FindClosestTarget()
    {
        float Closest_Distance = Auto_Aim_Distance;
        Transform Closest_Target = null;

        GameObject[] Enemies = GameObject.FindGameObjectsWithTag("Enemy");

        for (int i = 0; i < Enemies.Length; i++)
        {
            GameObject Enemy = Enemies[i];

            if (Enemy == null || !Enemy.activeInHierarchy)
                continue;

            float Distance = (Enemy.transform.position - transform.position).sqrMagnitude;

            if (Distance < Closest_Distance)
            {
                Closest_Distance = Distance;
                Closest_Target = Enemy.transform;
            }
        }

        Target_Transform = Closest_Target;
    }

    private void SetRange()
    {
        if (Range == null)
            return;

        Range.transform.localScale
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectPooling : MonoBehaviour
{
    [SerializeField] private GameObject Pool_Prefab;
    [SerializeField] private int Pool_Count = 100;

    private GameObject[] Objects;
    private int Pivot = 0;

    private void Awake()
    {
        Objects = new GameObject[Pool_Count];

        for (int i = 0; i < Objects.Length; i++)
        {
            Objects[i] = Instantiate(Pool_Prefab, transform);

            Objects[i].name = $"{Pool_Prefab.name}_{i}";

            Objects[i].SetActive(false);
        }
    }

    public GameObject GetObject()
    {
        for (int i = 0; i < Objects.Length; i++)
        {
            if (!Objects[Pivot].activeSelf)
            {
                GameObject Creature = Objects[Pivot];

                Pivot++;

                if (Pivot >= Objects.Length)
                    Pivot = 0;

                return Creature;
            }

            Pivot++;

            if (Pivot >= Objects.Length)
                Pivot = 0;
        }

        return null;
    }
}

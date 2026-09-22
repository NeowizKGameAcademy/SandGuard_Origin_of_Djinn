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
        if (Objects != null) return; // [통합 추가 2026-09-21] GetObject가 먼저 만들었으면 두 번 만들지 않는다.

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
        // [통합 추가 2026-09-21] 풀이 아직 없으면 여기서 만든다.
        //   Instantiate는 오브젝트 하나씩 Awake→OnEnable을 부르므로, 관 타워를 건설로 만들면
        //   부모의 SummonManager.OnEnable이 자식인 이 풀의 Awake보다 먼저 실행돼 Objects가 null이었다.
        //   (씬에 미리 놓았을 때는 Awake가 모두 먼저 돌아 드러나지 않던 문제다.)
        if (Objects == null) Awake();

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

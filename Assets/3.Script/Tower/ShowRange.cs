using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShowRange : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private FindEnemy findEnemy;

    private Vector3 Scale;

    private void Awake()
    {
        findEnemy = GetComponentInParent<FindEnemy>();
    }

    // Update is called once per frame
    void Update()
    {
        Scale = new Vector3(findEnemy.range * 2, findEnemy.range * 2, 0f);
        transform.localScale = Scale;
    }
}

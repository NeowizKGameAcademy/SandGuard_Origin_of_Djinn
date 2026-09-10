using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShowRange : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private FindEnemy findEnemy;
    [SerializeField] private Renderer renderer;

    private void Awake()
    {
        TryGetComponent(out renderer);
    }

    // Update is called once per frame
    void Update()
    {
        renderer.material.SetFloat("_Progress", findEnemy.range * 0.005f);
        renderer.material.SetFloat("_RingWidth", findEnemy.range * 0.005f);
    }
}

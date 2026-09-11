using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShowRange : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private DetectRange detectRange;
    [SerializeField] private Renderer renderer;

    private void Awake()
    {
        detectRange = transform.parent.GetComponentInChildren<DetectRange>();
        TryGetComponent(out renderer);
    }

    // Update is called once per frame
    void Update()
    {
        renderer.material.SetFloat("_Progress", detectRange.range * 0.01f);
        renderer.material.SetFloat("_RingWidth", detectRange.range * 0.01f);
    }
}

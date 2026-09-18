using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SandGuard.Player;

public class ShowRange : MonoBehaviour
{
    [SerializeField] private PlayerMotor Player;

    [Header("Components")]
    [SerializeField] private DetectRange detectRange;
    [SerializeField] private Renderer renderer;

    [Header("Fade")]
    [SerializeField] private float FadeSpeed = 10f;

    private float CurrentAlpha;

    private void Awake()
    {
        Player = FindAnyObjectByType<PlayerMotor>();
        detectRange = transform.parent.GetComponentInChildren<DetectRange>();
        TryGetComponent(out renderer);
    }

    // Update is called once per frame
    void Update()
    {
        renderer.material.SetFloat("_Progress", detectRange.range * 0.01f);
        renderer.material.SetFloat("_RingWidth", detectRange.range * 0.01f);

        OnOffRange();
    }

    private void OnOffRange()
    {
        float TargetAlpha = detectRange.Contains(Player.transform.position) ? 1f : 0f;

        CurrentAlpha = Mathf.Lerp(
            CurrentAlpha,
            TargetAlpha,
            FadeSpeed * Time.deltaTime
        );

        renderer.material.SetFloat("_Global_Alpha", CurrentAlpha);
    }
}

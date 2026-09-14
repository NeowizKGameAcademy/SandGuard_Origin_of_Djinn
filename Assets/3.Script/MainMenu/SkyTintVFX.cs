using UnityEngine;
using UnityEngine.UI;

public class SkyTintVFX : MonoBehaviour
{
    [SerializeField] private RawImage overlay;

    [SerializeField]
    private Color colorA =
        new Color(1f, 0.45f, 0.15f, 0.04f);

    [SerializeField]
    private Color colorB =
        new Color(1f, 0.7f, 0.35f, 0.14f);

    [SerializeField]
    private float speed = 0.08f;

    private void Update()
    {
        float t =
            (Mathf.Sin(Time.unscaledTime * speed) + 1f)
            * 0.5f;

        overlay.color =
            Color.Lerp(colorA, colorB, t);
    }
}
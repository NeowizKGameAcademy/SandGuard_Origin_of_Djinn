using UnityEngine;
using UnityEngine.UI;

public class GodRayVFX : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private Image image;

    [Header("Alpha")]
    [SerializeField] private float minAlpha = 0.12f;
    [SerializeField] private float maxAlpha = 0.32f;
    [SerializeField] private float alphaSpeed = 0.35f;

    [Header("Rotation")]
    [SerializeField] private float rotationAmount = 1.5f;
    [SerializeField] private float rotationSpeed = 0.15f;

    [Header("Scale")]
    [SerializeField] private bool useScalePulse = true;
    [SerializeField] private float minScale = 0.98f;
    [SerializeField] private float maxScale = 1.03f;
    [SerializeField] private float scaleSpeed = 0.2f;

    private RectTransform rectTransform;
    private Vector3 initialScale;
    private Quaternion initialRotation;

    private void Awake()
    {
        rectTransform = image.rectTransform;

        initialScale = rectTransform.localScale;
        initialRotation = rectTransform.localRotation;
    }

    private void Update()
    {
        AnimateAlpha();
        AnimateRotation();

        if (useScalePulse)
        {
            AnimateScale();
        }
    }

    private void AnimateAlpha()
    {
        float t =
            (Mathf.Sin(Time.unscaledTime * alphaSpeed) + 1f) * 0.5f;

        float alpha =
            Mathf.Lerp(minAlpha, maxAlpha, t);

        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    private void AnimateRotation()
    {
        float rotation =
            Mathf.Sin(Time.unscaledTime * rotationSpeed)
            * rotationAmount;

        rectTransform.localRotation =
            initialRotation * Quaternion.Euler(0f, 0f, rotation);
    }

    private void AnimateScale()
    {
        float t =
            (Mathf.Sin(Time.unscaledTime * scaleSpeed) + 1f) * 0.5f;

        float scale =
            Mathf.Lerp(minScale, maxScale, t);

        rectTransform.localScale =
            initialScale * scale;
    }
}
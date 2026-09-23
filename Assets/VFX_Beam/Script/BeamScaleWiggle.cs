using UnityEngine;

public class BeamScaleWiggle : MonoBehaviour
{
    [System.Serializable]
    public class HemisphereSetting
    {
        public Transform target;

        [Tooltip("X¿¡ ÃÖ¼Ú°ª, Y¿¡ ÃÖ´ñ°ª")]
        public Vector2 scaleRange = new Vector2(0.2f, 0.3f);

        [Min(0.01f)]
        public float wiggleSpeed = 1f;
    }

    [Header("Hemispheres")]
    [SerializeField] private HemisphereSetting[] hemispheres;
    [SerializeField] private float hemisphereYScale = 200f;
    [SerializeField] private float hemisphereCenterZ = 100f;

    [Header("Start Point Sphere")]
    [SerializeField] private Transform startPointSphere;

    [Tooltip("X¿¡ ÃÖ¼Ú°ª, Y¿¡ ÃÖ´ñ°ª")]
    [SerializeField] private Vector2 sphereScaleRange = new Vector2(0.3f, 0.5f);

    [Min(0.01f)]
    [SerializeField] private float sphereWiggleSpeed = 1.5f;

    private void Update()
    {
        UpdateHemispheres();
        UpdateStartPointSphere();
    }

    private void UpdateHemispheres()
    {
        for (int i = 0; i < hemispheres.Length; i++)
        {
            HemisphereSetting setting = hemispheres[i];

            if (setting.target == null)
                continue;

            float noise = Mathf.PerlinNoise(
                Time.time * setting.wiggleSpeed + i * 17.3f,
                0.23f
            );

            float scale = Mathf.Lerp(
                setting.scaleRange.x,
                setting.scaleRange.y,
                noise
            );

            setting.target.localPosition =
                new Vector3(0f, 0f, hemisphereCenterZ);

            setting.target.localScale =
                new Vector3(scale, hemisphereYScale, scale);
        }
    }

    private void UpdateStartPointSphere()
    {
        if (startPointSphere == null)
            return;

        float noise = Mathf.PerlinNoise(
            Time.time * sphereWiggleSpeed,
            5.37f
        );

        float scale = Mathf.Lerp(
            sphereScaleRange.x,
            sphereScaleRange.y,
            noise
        );

        startPointSphere.localScale = Vector3.one * scale;
    }
}
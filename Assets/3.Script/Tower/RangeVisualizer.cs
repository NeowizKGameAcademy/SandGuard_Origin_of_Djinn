using SandGuard.Player;
using UnityEngine;

namespace Tower
{
    public enum RangeType
    {
        Tower,
        Core
    }

    [DisallowMultipleComponent]
    public class RangeVisualizer : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private TargetDetector detector;
        [SerializeField] private Renderer rangeRenderer;
        [SerializeField] private PlayerMotor player;

        [Header("Visual")]
        [SerializeField] private RangeType visualType;

        [Header("Tower Fade")]
        [Min(0f)] [SerializeField] private float showDistance = 7.5f;
        [Min(0f)] [SerializeField] private float fadeSpeed = 10f;

        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int RingWidthId = Shader.PropertyToID("_RingWidth");
        private static readonly int AlphaId = Shader.PropertyToID("_Global_Alpha");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_Outline_Width");
        private static readonly int HexTilingId = Shader.PropertyToID("_Hex_Tiling");

        private MaterialPropertyBlock properties;
        private float currentAlpha;

        private void Awake()
        {
            if (rangeRenderer == null)
                TryGetComponent(out rangeRenderer);

            if (detector == null)
                detector = GetComponentInParent<TargetDetector>();

            if (detector == null && transform.parent != null)
                detector = transform.parent.GetComponentInChildren<TargetDetector>(true);

            properties = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            if (rangeRenderer == null)
                return;

            float range = detector != null && detector.isActiveAndEnabled ? detector.Range : 0f;
            rangeRenderer.GetPropertyBlock(properties);

            switch (visualType)
            {
                case RangeType.Tower:
                    ShowTowerRange(range);
                    break;

                case RangeType.Core:
                    ShowCoreRange(range);
                    break;
            }

            rangeRenderer.SetPropertyBlock(properties);
        }

        private void ShowTowerRange(float range)
        {
            if (player == null) ;
                player = FindAnyObjectByType<PlayerMotor>();

            bool isNear = range > 0f && player != null && player.isActiveAndEnabled
                && detector.Contains(player.transform.position, showDistance);

            currentAlpha = Mathf.Lerp(currentAlpha, isNear ? 1f : 0f, fadeSpeed * Time.deltaTime);

            properties.SetFloat(ProgressId, range * 0.01f);
            properties.SetFloat(RingWidthId, range * 0.01f);
            properties.SetFloat(AlphaId, currentAlpha);
        }

        private void ShowCoreRange(float range)
        {
            rangeRenderer.transform.localScale = Vector3.one * range * 2f;

            properties.SetFloat(OutlineWidthId, range > 0f ? 0.05f / range : 0f);
            properties.SetFloat(HexTilingId, range * 0.08f);
        }
    }
}

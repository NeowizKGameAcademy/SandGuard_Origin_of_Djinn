using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SandGuard.UI.HUD
{
    public sealed class CoreStatusHUD : MonoBehaviour
    {
        [SerializeField] private Image fill;
        [SerializeField] private TMP_Text valueText;
        public void SetStability(float current, float max)
        {
            float safeMax = Mathf.Max(0f, max);
            float safeCurrent = Mathf.Max(0f, current);
            fill.fillAmount = safeMax <= 0f ? 0f : Mathf.Clamp01(safeCurrent / safeMax);
            if (valueText != null) valueText.text = $"{Mathf.CeilToInt(safeCurrent):N0} / {Mathf.CeilToInt(safeMax):N0}";
        }
        public void SetVisible(bool visible) => gameObject.SetActive(visible);
    }
}

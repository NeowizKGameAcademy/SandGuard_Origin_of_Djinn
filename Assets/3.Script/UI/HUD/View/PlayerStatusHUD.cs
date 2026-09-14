using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SandGuard.UI.HUD
{
    public sealed class PlayerStatusHUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text levelValueText;
        [SerializeField] private Image hpFill;
        [SerializeField] private TMP_Text hpValueText;
        [SerializeField] private Image expFill;
        [SerializeField] private TMP_Text expValueText;
        [SerializeField] private Image manaFill;
        [SerializeField] private TMP_Text manaValueText;

        public void SetLevel(int level) => levelValueText.text = Mathf.Max(0, level).ToString();
        public void SetHealth(float current, float max) => SetBar(hpFill, hpValueText, current, max);
        public void SetExperience(float current, float required) => SetBar(expFill, expValueText, current, required);
        public void SetMana(float current, float max) => SetBar(manaFill, manaValueText, current, max);

        private static void SetBar(Image fill, TMP_Text text, float current, float max)
        {
            float safeCurrent = Mathf.Max(0f, current);
            float safeMax = Mathf.Max(0f, max);
            fill.fillAmount = safeMax <= 0f ? 0f : Mathf.Clamp01(safeCurrent / safeMax);
            text.text = $"{Mathf.CeilToInt(safeCurrent):N0} / {Mathf.CeilToInt(safeMax):N0}";
        }
    }
}

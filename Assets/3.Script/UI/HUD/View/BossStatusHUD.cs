using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SandGuard.UI.HUD
{
    public sealed class BossStatusHUD : MonoBehaviour
    {
        [SerializeField] private Image bossIcon;
        [SerializeField] private TMP_Text bossNameText;
        [SerializeField] private Image hpFill;
        [SerializeField] private TMP_Text hpValueText;
        public void ShowBoss(string bossName, Sprite icon, float maxHP)
        {
            gameObject.SetActive(true);
            bossNameText.text = string.IsNullOrWhiteSpace(bossName) ? "BOSS" : bossName;
            bossIcon.sprite = icon;
            bossIcon.enabled = icon != null;
            SetHealth(maxHP, maxHP);
        }
        public void SetHealth(float current, float max)
        {
            float safeMax = Mathf.Max(0f, max);
            float safeCurrent = Mathf.Max(0f, current);
            hpFill.fillAmount = safeMax <= 0f ? 0f : Mathf.Clamp01(safeCurrent / safeMax);
            if (hpValueText != null) hpValueText.text = $"{Mathf.CeilToInt(safeCurrent):N0} / {Mathf.CeilToInt(safeMax):N0}";
        }
        public void HideBoss() => gameObject.SetActive(false);
    }
}

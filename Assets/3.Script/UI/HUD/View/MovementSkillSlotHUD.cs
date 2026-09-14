using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SandGuard.UI.HUD
{
    public sealed class MovementSkillSlotHUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Image icon;
        [SerializeField] private Image cooldownRing;
        [SerializeField] private TMP_Text cooldownText;
        public void SetTitle(string title) => titleText.text = title ?? string.Empty;
        public void SetIcon(Sprite value) { icon.sprite = value; icon.enabled = value != null; }
        public void SetCooldown(float remaining, float total)
        {
            float ratio = total <= 0f ? 0f : Mathf.Clamp01(remaining / total);
            cooldownRing.fillAmount = ratio;
            cooldownText.gameObject.SetActive(ratio > 0f);
            if (ratio > 0f) cooldownText.text = Mathf.Max(0f, remaining).ToString("0.0");
        }
        public void SetReady(bool ready) { if (ready) SetCooldown(0f, 1f); }
    }
}

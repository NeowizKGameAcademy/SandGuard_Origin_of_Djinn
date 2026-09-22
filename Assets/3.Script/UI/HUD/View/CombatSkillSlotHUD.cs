using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SandGuard.UI.HUD
{
    public sealed class CombatSkillSlotHUD : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private Image cooldownOverlay;
        [SerializeField] private TMP_Text cooldownText;
        [SerializeField] private TMP_Text keyText;
        [SerializeField] private GameObject lockedOverlay;
        private void Awake() => CooldownTextStyle.Apply(cooldownText);
        public void SetIcon(Sprite value) { icon.sprite = value; icon.enabled = value != null; }
        public void SetCooldown(float remaining, float total)
        {
            float ratio = total <= 0f ? 0f : Mathf.Clamp01(remaining / total);
            cooldownOverlay.fillAmount = ratio;
            cooldownText.gameObject.SetActive(ratio > 0f);
            if (ratio > 0f) cooldownText.text = Mathf.Max(0f, remaining).ToString("0.0");
        }
        public void SetReady(bool ready) { if (ready) SetCooldown(0f, 1f); }
        public void SetKey(string key) => keyText.text = key ?? string.Empty;
        public void SetLocked(bool locked) => lockedOverlay.SetActive(locked);
        public void SetUnavailable(string reason)
        {
            cooldownOverlay.fillAmount=1f;
            cooldownText.gameObject.SetActive(true);
            cooldownText.text=reason;
        }
    }
}

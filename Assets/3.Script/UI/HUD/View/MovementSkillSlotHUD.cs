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
        [SerializeField] private TMP_Text keyText;
        private void Awake()
        {
            CooldownTextStyle.Apply(cooldownText);
            // 이름 칸은 프레임 위쪽 판자 너비(약 110px)에 갇혀 있다. "우클릭 · 흔적 귀환"처럼 긴 이름이
            // 판자 밖으로 번지지 않도록 들어갈 때까지만 줄인다.
            if (titleText != null)
            {
                float authored = titleText.fontSize;
                titleText.enableAutoSizing = true;
                titleText.fontSizeMin = 8f;
                titleText.fontSizeMax = authored;
            }
        }
        public void SetTitle(string title) => titleText.text = title ?? string.Empty;
        /// <summary>고리 아래 작은 키 이름(Shift · Space · 우클릭). 없는 프리팹에서는 아무 일도 하지 않는다.</summary>
        public void SetKey(string key) { if (keyText != null) keyText.text = key ?? string.Empty; }
        public void SetIcon(Sprite value) { icon.sprite = value; icon.enabled = value != null; }
        public void SetCooldown(float remaining, float total)
        {
            float ratio = total <= 0f ? 0f : Mathf.Clamp01(remaining / total);
            cooldownRing.fillAmount = ratio;
            cooldownText.gameObject.SetActive(ratio > 0f);
            if (ratio > 0f) cooldownText.text = Mathf.Max(0f, remaining).ToString("0.0");
        }
        public void SetReady(bool ready) { if (ready) SetCooldown(0f, 1f); }
        /// <summary>쿨타임 없이 지금 쓸 수 없는 상태(잠김, 공중 점프 소진). 링을 가득 덮고 숫자는 숨긴다.</summary>
        public void SetUnavailable() { cooldownRing.fillAmount = 1f; cooldownText.gameObject.SetActive(false); }
    }
}

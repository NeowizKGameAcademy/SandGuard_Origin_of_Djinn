using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SandGuard.UI.HUD
{
    public sealed class CoreStatusHUD : MonoBehaviour
    {
        [SerializeField] private Image fill;
        [SerializeField] private TMP_Text valueText;
        [SerializeField, Min(.5f)] private float attackWarningSeconds = 3f;
        CanvasGroup attackWarning;
        float warningRemaining;
        Color normalFillColor;
        bool fillColorCaptured;

        public void ShowAttackWarning()
        {
            if (!isActiveAndEnabled) return;
            if (attackWarning == null) CreateAttackWarning();
            if (!fillColorCaptured && fill != null)
            {
                normalFillColor = fill.color;
                fillColorCaptured = true;
            }
            warningRemaining = attackWarningSeconds;
            attackWarning.gameObject.SetActive(true);
            attackWarning.alpha = 1f;
        }

        void CreateAttackWarning()
        {
            // Keep the warning with the existing core HUD in every scene and prefab variant.
            var panel = new GameObject("CoreAttackWarning", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -8f);
            rect.sizeDelta = new Vector2(330f, 42f);
            panel.GetComponent<Image>().color = new Color(.25f, .025f, .025f, .94f);
            panel.GetComponent<Image>().raycastTarget = false;
            attackWarning = panel.GetComponent<CanvasGroup>();
            attackWarning.interactable = false;
            attackWarning.blocksRaycasts = false;

            var label = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
            var labelRect = (RectTransform)label.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 4f);
            labelRect.offsetMax = new Vector2(-8f, -4f);
            var text = label.GetComponent<TextMeshProUGUI>();
            if (valueText != null) text.font = valueText.font;
            text.text = "코어가 공격받고 있습니다!";
            text.fontSize = 21f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(1f, .8f, .7f);
            text.raycastTarget = false;
        }

        void Update()
        {
            if (warningRemaining <= 0f) return;
            warningRemaining = Mathf.Max(0f, warningRemaining - Time.unscaledDeltaTime);
            float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 7f);
            attackWarning.alpha = Mathf.Min(1f, warningRemaining / .5f) * Mathf.Lerp(.8f, 1f, pulse);
            if (fill != null) fill.color = Color.Lerp(normalFillColor, new Color(1f, .15f, .1f), .5f + .5f * pulse);
            if (warningRemaining <= 0f) HideAttackWarning();
        }

        public void HideAttackWarning()
        {
            warningRemaining = 0f;
            if (attackWarning != null) attackWarning.gameObject.SetActive(false);
            if (fillColorCaptured && fill != null) fill.color = normalFillColor;
        }

        void OnDisable() => HideAttackWarning();

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

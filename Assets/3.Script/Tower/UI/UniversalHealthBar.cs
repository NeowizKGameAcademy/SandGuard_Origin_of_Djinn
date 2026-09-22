using UnityEngine;
using UnityEngine.UI;

namespace Tower.UI
{
    public sealed class UniversalHealthBar : MonoBehaviour
    {
        [SerializeField] private Slider slider;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Vector3 offset = new Vector3(0f, 2.5f, 0f);

        private Component target;
        private IHealth health;

        private void Awake()
        {
            if (slider == null) TryGetComponent(out slider);
            if (canvasGroup == null) TryGetComponent(out canvasGroup);
        }

        public void SetTarget(Component value, IHealth valueHealth)
        {
            target = value;
            health = valueHealth;
            Refresh();
        }

        public void ClearTarget()
        {
            target = null;
            health = null;
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }

        private void LateUpdate()
        {
            if (target == null || health == null) return;
            var camera = Camera.main;
            if (camera == null) return;

            Vector3 screen = camera.WorldToScreenPoint(target.transform.position + offset);
            bool visible = screen.z > 0f && screen.x >= 0f && screen.x <= Screen.width && screen.y >= 0f && screen.y <= Screen.height;
            if (canvasGroup != null) canvasGroup.alpha = visible ? 1f : 0f;
            if (!visible) return;

            transform.position = screen;
            Refresh();
        }

        private void Refresh()
        {
            if (slider == null || health == null) return;
            slider.maxValue = Mathf.Max(1f, health.MaxHealth);
            slider.value = Mathf.Clamp(health.CurrentHealth, 0f, slider.maxValue);
        }
    }
}

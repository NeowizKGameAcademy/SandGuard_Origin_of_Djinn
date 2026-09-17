using System.Collections;
using TMPro;
using UnityEngine;

namespace SandGuard.UI.HUD
{
    public sealed class WaveAlertHUD : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text waveText;
        [SerializeField, Min(0f)] private float fadeInSeconds = .22f;
        [SerializeField, Min(0f)] private float holdSeconds = 1.65f;
        [SerializeField, Min(0f)] private float fadeOutSeconds = .45f;
        Coroutine animationRoutine;

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            HideImmediate();
        }

        public void ShowWave(int wave)
        {
            if (waveText != null) waveText.text = $"WAVE {Mathf.Max(1, wave)}";
            if (animationRoutine != null) StopCoroutine(animationRoutine);
            gameObject.SetActive(true);
            animationRoutine = StartCoroutine(Play());
        }

        public void HideImmediate()
        {
            if (animationRoutine != null) StopCoroutine(animationRoutine);
            animationRoutine = null;
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        IEnumerator Play()
        {
            yield return Fade(0f, 1f, fadeInSeconds);
            if (holdSeconds > 0f) yield return new WaitForSecondsRealtime(holdSeconds);
            yield return Fade(1f, 0f, fadeOutSeconds);
            animationRoutine = null;
            gameObject.SetActive(false);
        }

        IEnumerator Fade(float from, float to, float duration)
        {
            if (duration <= 0f) { canvasGroup.alpha = to; yield break; }
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            canvasGroup.alpha = to;
        }
    }
}

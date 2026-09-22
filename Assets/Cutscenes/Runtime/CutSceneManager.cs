using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SandGuard.Cutscenes
{
    /// <summary>한 씬 안에서 CutSceneSequence의 컷을 순서대로 재생합니다.</summary>
    public sealed class CutSceneManager : MonoBehaviour
    {
        [SerializeField] CutSceneSequence sequence;
        [SerializeField] Image background;
        [SerializeField] Image panel;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text bodyText;
        [SerializeField] TMP_Text progressText;
        [SerializeField] CanvasGroup visibleGroup;
        [SerializeField] Button nextButton;
        [SerializeField] Button skipButton;
        [SerializeField] AudioSource musicSource;

        int index;
        float elapsed;
        bool advancing;
        bool finished;
        bool replayPlayback;

        void Start()
        {
            if (!sequence || sequence.cuts == null || sequence.cuts.Length == 0)
            {
                Debug.LogError("CutSceneManager: 재생할 컷이 없습니다.", this);
                enabled = false;
                return;
            }

            replayPlayback = StoryProgress.ConsumeReplayRequest(sequence.storyId);
            if (sequence.playOnceAutomatically && StoryProgress.IsSeen(sequence.storyId) && !replayPlayback)
            {
                visibleGroup.alpha = 0f;
                StartCoroutine(LoadNextScene());
                return;
            }
            if (nextButton) nextButton.onClick.AddListener(Next);
            if (skipButton) skipButton.onClick.AddListener(Skip);
            PlayBackgroundMusic();
            ShowCut(0);
            StartCoroutine(Fade(0f, 1f));
        }

        void OnDestroy()
        {
            if (nextButton) nextButton.onClick.RemoveListener(Next);
            if (skipButton) skipButton.onClick.RemoveListener(Skip);
        }

        void Update()
        {
            if (finished || advancing) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { Skip(); return; }
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)) { Next(); return; }
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= Mathf.Max(1f, sequence.cuts[index].duration)) Next();
        }

        public void Next()
        {
            if (finished || advancing) return;
            StartCoroutine(Advance());
        }

        public void Skip()
        {
            if (finished) return;
            finished = true;
            StopAllCoroutines();
            StartCoroutine(Finish());
        }

        IEnumerator Advance()
        {
            advancing = true;
            yield return Fade(visibleGroup.alpha, 0f);
            if (index + 1 >= sequence.cuts.Length)
            {
                finished = true;
                StoryProgress.MarkSeen(sequence.storyId);
                yield return LoadNextScene();
                yield break;
            }
            ShowCut(index + 1);
            yield return Fade(0f, 1f);
            advancing = false;
        }

        IEnumerator Finish()
        {
            yield return Fade(visibleGroup.alpha, 0f);
            StoryProgress.MarkSeen(sequence.storyId);
            yield return LoadNextScene();
        }

        void ShowCut(int nextIndex)
        {
            index = nextIndex;
            elapsed = 0f;
            var cut = sequence.cuts[index];
            background.sprite = cut.image;
            titleText.text = cut.title;
            bodyText.text = cut.body;
            progressText.text = $"{index + 1:00} / {sequence.cuts.Length:00}";
            var rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(cut.panelX, cut.panelY);
            rect.anchoredPosition = Vector2.zero;
        }

        IEnumerator Fade(float from, float to)
        {
            float duration = Mathf.Max(0.01f, sequence.fadeDuration);
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                visibleGroup.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            visibleGroup.alpha = to;
        }

        IEnumerator LoadNextScene()
        {
            yield return FadeOutMusic();
            string targetScene = replayPlayback ? "MainScene" : sequence.nextScene;
            if (replayPlayback) StoryProgress.RequestStoryMenuReturn();
            if (!Application.CanStreamedLevelBeLoaded(targetScene))
            {
                Debug.LogError($"CutSceneManager: Build Settings에 '{targetScene}' 씬이 없습니다.", this);
                yield break;
            }
            var operation = SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Single);
            while (operation != null && !operation.isDone) yield return null;
        }

        void PlayBackgroundMusic()
        {
            if (!sequence.backgroundMusic) return;
            if (!musicSource) musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;
            musicSource.loop = sequence.loopMusic;
            musicSource.clip = sequence.backgroundMusic;
            musicSource.volume = sequence.musicVolume;
            musicSource.Play();
        }

        IEnumerator FadeOutMusic()
        {
            if (!musicSource || !musicSource.isPlaying) yield break;
            float startVolume = musicSource.volume;
            float duration = Mathf.Max(0.01f, sequence.fadeDuration);
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                musicSource.volume = Mathf.Lerp(startVolume, 0f, t / duration);
                yield return null;
            }
            musicSource.Stop();
        }
    }
}

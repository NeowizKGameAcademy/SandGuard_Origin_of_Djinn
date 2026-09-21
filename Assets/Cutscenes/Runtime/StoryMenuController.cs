using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SandGuard.Cutscenes
{
    public sealed class StoryMenuController : MonoBehaviour
    {
        [Serializable]
        public sealed class StoryEntry
        {
            public string storyId;
            public string sceneName;
            public bool available;
            public Button button;
            public TMP_Text stateText;
        }

        [SerializeField] Button storyButton;
        [SerializeField] Button closeButton;
        [SerializeField] GameObject popup;
        [SerializeField] StoryEntry[] stories = Array.Empty<StoryEntry>();

        void Awake()
        {
            if (!storyButton)
            {
                var storyObject = GameObject.Find("Story");
                if (storyObject) storyButton = storyObject.GetComponent<Button>();
            }
            if (storyButton) storyButton.onClick.AddListener(Open);
            if (closeButton) closeButton.onClick.AddListener(Close);
            foreach (var story in stories)
            {
                var captured = story;
                if (captured.button) captured.button.onClick.AddListener(() => Play(captured));
            }
            Close();
        }

        void OnDestroy()
        {
            if (storyButton) storyButton.onClick.RemoveListener(Open);
            if (closeButton) closeButton.onClick.RemoveListener(Close);
            foreach (var story in stories) if (story.button) story.button.onClick.RemoveAllListeners();
        }

        public void Open()
        {
            Refresh();
            if (popup) popup.SetActive(true);
        }

        public void Close()
        {
            if (popup) popup.SetActive(false);
        }

        void Refresh()
        {
            foreach (var story in stories)
            {
                bool unlocked = story.available && StoryProgress.IsSeen(story.storyId);
                if (story.button) story.button.interactable = unlocked;
                if (story.stateText) story.stateText.text = !story.available ? "준비 중" : unlocked ? "다시 보기" : "잠김";
            }
        }

        static void Play(StoryEntry story)
        {
            if (!story.available || !StoryProgress.IsSeen(story.storyId)) return;
            if (!Application.CanStreamedLevelBeLoaded(story.sceneName))
            {
                Debug.LogError($"StoryMenuController: Build Settings에 '{story.sceneName}' 씬이 없습니다.");
                return;
            }
            StoryProgress.RequestReplay(story.storyId);
            SceneManager.LoadScene(story.sceneName, LoadSceneMode.Single);
        }
    }
}

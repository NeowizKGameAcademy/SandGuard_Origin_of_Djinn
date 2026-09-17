using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DesertTower.LevelIntegration
{
    /// <summary>Reusable result prefab. Bind a WaveDirector explicitly or leave it empty for scene discovery.</summary>
    public sealed class GameResultScreen : MonoBehaviour
    {
        [SerializeField] WaveDirector director;
        [SerializeField] GameObject overlay;
        [SerializeField] RawImage cardArtwork;
        [SerializeField] TMP_Text title, subtitle, timeLabel, waveLabel, killsLabel;
        [SerializeField] TMP_Text[] statNames;
        [SerializeField] Button retryButton, mainMenuButton;
        [SerializeField] string mainMenuScene = "MainScene";
        [SerializeField] string retrySceneOverride;

        float previousTimeScale = 1f;
        CursorLockMode previousCursorLock;
        bool previousCursorVisible;
        bool showing;
        GameObject createdEventSystem;

        void Awake()
        {
            if (overlay) overlay.SetActive(false);
            if (retryButton) retryButton.onClick.AddListener(Retry);
            if (mainMenuButton) mainMenuButton.onClick.AddListener(MainMenu);
        }

        void Start()
        {
            if (!director) director = FindFirstObjectByType<WaveDirector>();
            if (!director) { Debug.LogWarning("GameResultScreen: WaveDirector not found.", this); return; }
            director.onStateChanged.AddListener(Refresh);
            Refresh();
        }

        void OnDestroy()
        {
            if (director) director.onStateChanged.RemoveListener(Refresh);
            if (retryButton) retryButton.onClick.RemoveListener(Retry);
            if (mainMenuButton) mainMenuButton.onClick.RemoveListener(MainMenu);
            RestoreState();
        }

        void Refresh()
        {
            if (!director || showing || (director.State != RunState.Won && director.State != RunState.Lost)) return;
            bool won = director.State == RunState.Won;
            if (title) { title.text = won ? "CLEAR" : "FAILED"; title.color = won ? new Color(1f,.84f,.47f) : new Color(1f,.34f,.27f); }
            if (subtitle) subtitle.text = won ? "사막의 평화가 다시 찾아왔습니다!" : "마석코어가 무너졌습니다...";
            if (timeLabel) { int seconds = Mathf.FloorToInt(director.RunElapsedSeconds); timeLabel.text = $"{seconds / 60:00}:{seconds % 60:00}"; }
            if (waveLabel) waveLabel.text = $"{director.WaveNumber} / {director.TotalWaves}";
            if (killsLabel) killsLabel.text = director.Killed.ToString("N0");
            var statColor = won ? new Color(.13f,.09f,.05f) : new Color(1f,.85f,.65f);
            if (statNames != null) foreach (var label in statNames) if (label) label.color = statColor;
            if (timeLabel) timeLabel.color = statColor;
            if (waveLabel) waveLabel.color = statColor;
            if (killsLabel) killsLabel.color = statColor;
            if (cardArtwork) cardArtwork.uvRect = won ? new Rect(0,0,.5f,1) : new Rect(.5f,0,.5f,1);
            previousTimeScale = Time.timeScale;
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Time.timeScale = 0;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (!EventSystem.current)
            {
                createdEventSystem = new GameObject("Result EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                createdEventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            showing = true;
            if (overlay) overlay.SetActive(true);
        }

        public void Retry()
        {
            RestoreState();
            SceneManager.LoadScene(string.IsNullOrEmpty(retrySceneOverride) ? SceneManager.GetActiveScene().name : retrySceneOverride);
        }

        public void MainMenu()
        {
            RestoreState();
            SceneManager.LoadScene(mainMenuScene);
        }

        void RestoreState()
        {
            if (!showing) return;
            Time.timeScale = previousTimeScale;
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
            showing = false;
            if (createdEventSystem) Destroy(createdEventSystem);
        }
    }
}

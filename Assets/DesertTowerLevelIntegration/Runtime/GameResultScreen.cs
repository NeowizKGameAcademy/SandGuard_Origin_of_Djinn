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
        [SerializeField] bool previewOnly;
        [SerializeField, Min(0f)] float overlayDelay = 1f;

        float previousTimeScale = 1f;
        CursorLockMode previousCursorLock;
        bool previousCursorVisible;
        bool showing;
        bool resultPending;
        float showAt;
        GameObject createdEventSystem;

        void Awake()
        {
            if (overlay && !previewOnly) overlay.SetActive(false);
            if (retryButton) retryButton.onClick.AddListener(Retry);
            if (mainMenuButton) mainMenuButton.onClick.AddListener(MainMenu);
        }

        void Start()
        {
            if (previewOnly) return;
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
            if (previewOnly || showing) return;
            if (!director || (director.State != RunState.Won && director.State != RunState.Lost))
            {
                resultPending = false;
                return;
            }
            if (resultPending) return;
            resultPending = true;
            showAt = Time.unscaledTime + Mathf.Max(0f, overlayDelay);
        }

        void Update()
        {
            if (!resultPending || Time.unscaledTime < showAt) return;
            resultPending = false;
            if (previewOnly || showing || !director || (director.State != RunState.Won && director.State != RunState.Lost)) return;
            ShowResult();
        }

        void ShowResult()
        {
            bool won = director.State == RunState.Won;
            if (title) { title.text = won ? "CLEAR" : "FAILED"; title.color = won ? new Color(1f,.84f,.47f) : new Color(1f,.34f,.27f); }
            if (subtitle) { subtitle.text = won ? "사막의 평화가 다시 찾아왔습니다!" : "마석코어가 무너졌습니다..."; subtitle.color = won ? new Color(.13f,.09f,.05f) : new Color(1f,.85f,.65f); }
            if (timeLabel) { int seconds = Mathf.FloorToInt(director.RunElapsedSeconds); timeLabel.text = $"{seconds / 60:00}:{seconds % 60:00}"; }
            if (waveLabel) waveLabel.text = $"{director.WaveNumber} / {director.TotalWaves}";
            if (killsLabel) killsLabel.text = director.Killed.ToString("N0");
            if (statNames != null && statNames.Length > 0 && statNames[0]) statNames[0].text = won ? "클리어 시간" : "경과 시간";
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

        /// <summary>Preview scene only: populate editable sample values without pausing gameplay.</summary>
        public void ShowPreview(bool failed)
        {
            if (!previewOnly) return;
            if (title) { title.text = failed ? "FAILED" : "CLEAR"; title.color = failed ? new Color(1f,.34f,.27f) : new Color(1f,.84f,.47f); }
            if (subtitle) { subtitle.text = failed ? "마석코어가 무너졌습니다..." : "사막의 평화가 다시 찾아왔습니다!"; subtitle.color = failed ? new Color(1f,.85f,.65f) : new Color(.13f,.09f,.05f); }
            if (timeLabel) timeLabel.text = failed ? "08:14" : "12:36";
            if (waveLabel) waveLabel.text = failed ? "3 / 5" : "5 / 5";
            if (killsLabel) killsLabel.text = failed ? "198" : "327";
            if (statNames != null && statNames.Length > 0 && statNames[0]) statNames[0].text = failed ? "경과 시간" : "클리어 시간";
            var color = failed ? new Color(1f,.85f,.65f) : new Color(.13f,.09f,.05f);
            if (statNames != null) foreach (var label in statNames) if (label) label.color = color;
            if (timeLabel) timeLabel.color = color;
            if (waveLabel) waveLabel.color = color;
            if (killsLabel) killsLabel.color = color;
            if (cardArtwork) cardArtwork.uvRect = failed ? new Rect(.5f,0,.5f,1) : new Rect(0,0,.5f,1);
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

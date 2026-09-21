using SandGuard.GameFlow;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DesertTower.LevelIntegration
{
    /// <summary>전투 씬의 일시정지 팝업과 입력을 관리합니다.</summary>
    public sealed class PauseMenuController : MonoBehaviour
    {
        [SerializeField] GameObject popupRoot;
        [SerializeField] Button pauseButton;
        [SerializeField] Button resumeButton;
        [SerializeField] Button mainButton;

        bool ownsPause;

        void Awake()
        {
            popupRoot.SetActive(false);
            pauseButton.onClick.AddListener(Open);
            resumeButton.onClick.AddListener(Resume);
            mainButton.onClick.AddListener(ReturnToMain);
        }

        void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
            if (ownsPause) Resume();
            else if (Time.timeScale > 0f && !GameManager.Instance.IsTransitioning) Open();
        }

        public void Open()
        {
            if (ownsPause || Time.timeScale <= 0f || GameManager.Instance.IsTransitioning) return;
            ownsPause = true;
            popupRoot.SetActive(true);
            GameManager.Instance.RequestPause(this);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            resumeButton.Select();
        }

        public void Resume()
        {
            if (!ownsPause) return;
            ownsPause = false;
            popupRoot.SetActive(false);
            GameManager.Instance.ReleasePause(this);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void ReturnToMain()
        {
            if (!ownsPause) return;
            ownsPause = false;
            GameManager.Instance.ReleasePause(this);
            GameManager.Instance.ReturnToMainMenu();
        }

        void OnDestroy()
        {
            if (ownsPause && GameManager.HasInstance) GameManager.Instance.ReleasePause(this);
        }
    }
}

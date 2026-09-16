using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SandGuard.GameFlow
{
    /// <summary>게임 시작, 씬 전환, 종료와 중첩 가능한 일시정지 요청을 관리합니다.</summary>
    [DefaultExecutionOrder(-2000)]
    public sealed class GameManager : MonoBehaviour
    {
        public const string DefaultMainMenuScene = "MainScene";
        public const string DefaultGameScene = "Level";

        static GameManager instance;
        readonly Dictionary<int,UnityEngine.Object> pauseOwners=new Dictionary<int,UnityEngine.Object>();
        bool manualPause;
        bool appliedPaused;
        float resumeTimeScale=1f;

        public static GameManager Instance
        {
            get
            {
                if(instance)return instance;
                var existing=FindFirstObjectByType<GameManager>();
                if(existing)return instance=existing;
                var go=new GameObject("GameManager");
                return instance=go.AddComponent<GameManager>();
            }
        }
        public static bool HasInstance=>instance;

        [field:SerializeField] public string MainMenuScene { get; private set; } = DefaultMainMenuScene;
        [field:SerializeField] public string GameScene { get; private set; } = DefaultGameScene;
        public bool IsPaused=>manualPause || pauseOwners.Count>0;
        public bool IsTransitioning { get; private set; }
        public event Action<bool> PauseChanged;
        public event Action<string> SceneLoadStarted;
        public event Action<string> SceneLoadFinished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()=>_ = Instance;

        void Awake()
        {
            if(instance && instance!=this){Destroy(gameObject);return;}
            instance=this;DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded+=OnSceneLoaded;
        }

        void OnDestroy()
        {
            if(instance!=this)return;
            SceneManager.sceneLoaded-=OnSceneLoaded;Time.timeScale=1f;instance=null;
        }

        void Update()
        {
            if(pauseOwners.Count==0)return;
            List<int> removed=null;
            foreach(var pair in pauseOwners)if(!pair.Value)(removed??=new List<int>()).Add(pair.Key);
            if(removed==null)return;
            foreach(int id in removed)pauseOwners.Remove(id);
            ApplyPauseState();
        }

        public void StartGame()=>LoadScene(GameScene);
        public void ReturnToMainMenu()=>LoadScene(MainMenuScene);
        public void RestartGame()=>LoadScene(SceneManager.GetActiveScene().name);

        public void LoadScene(string sceneName)
        {
            if(IsTransitioning || string.IsNullOrWhiteSpace(sceneName))return;
            if(!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"GameManager: Build Settings에 '{sceneName}' 씬이 없습니다.",this);return;
            }
            StartCoroutine(LoadSceneRoutine(sceneName));
        }

        IEnumerator LoadSceneRoutine(string sceneName)
        {
            IsTransitioning=true;ClearPauseRequests();SceneLoadStarted?.Invoke(sceneName);
            var operation=SceneManager.LoadSceneAsync(sceneName,LoadSceneMode.Single);
            if(operation==null){IsTransitioning=false;yield break;}
            while(!operation.isDone)yield return null;
            IsTransitioning=false;
        }

        public void QuitGame()
        {
            ClearPauseRequests();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }

        public void SetPaused(bool paused)
        {
            if(manualPause==paused)return;manualPause=paused;ApplyPauseState();
        }

        public void TogglePause()=>SetPaused(!manualPause);

        public void RequestPause(UnityEngine.Object owner)
        {
            if(!owner){SetPaused(true);return;}
            pauseOwners[owner.GetInstanceID()]=owner;ApplyPauseState();
        }

        public void ReleasePause(UnityEngine.Object owner)
        {
            if(!owner){SetPaused(false);return;}
            if(pauseOwners.Remove(owner.GetInstanceID()))ApplyPauseState();
        }

        public void ClearPauseRequests()
        {
            manualPause=false;pauseOwners.Clear();ApplyPauseState();
        }

        void ApplyPauseState()
        {
            bool paused=IsPaused;
            if(paused==appliedPaused)return;
            appliedPaused=paused;
            if(paused)
            {
                if(Time.timeScale>0f)resumeTimeScale=Time.timeScale;
                Time.timeScale=0f;
            }
            else Time.timeScale=resumeTimeScale>0f?resumeTimeScale:1f;
            PauseChanged?.Invoke(paused);
        }

        void OnSceneLoaded(Scene scene,LoadSceneMode mode)
        {
            if(scene.name==MainMenuScene)
            {
                Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
                BindButton(scene,"GameStart",StartGame);BindButton(scene,"Quit",QuitGame);
            }
            SceneLoadFinished?.Invoke(scene.name);
        }

        static void BindButton(Scene scene,string objectName,UnityEngine.Events.UnityAction action)
        {
            foreach(var root in scene.GetRootGameObjects())
            {
                var transforms=root.GetComponentsInChildren<Transform>(true);
                foreach(var item in transforms)
                {
                    if(item.name!=objectName || !item.TryGetComponent<Button>(out var button))continue;
                    button.onClick.AddListener(action);return;
                }
            }
        }
    }
}

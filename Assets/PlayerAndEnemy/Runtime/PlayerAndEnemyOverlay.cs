using SandGuard.Enemy;
using SandGuard.Waves;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SandGuard.Integration
{
    /// <summary>플레이어·적 통합 테스트 씬의 안내와 상태 표시. 실제 HUD가 붙으면 제거한다.</summary>
    public sealed class PlayerAndEnemyOverlay : MonoBehaviour
    {
        [Tooltip("플레이어의 IHealth/ILifeState 컴포넌트 (PlayerHealth 또는 PlayerCombatTarget)")]
        public MonoBehaviour playerLife;
        [Tooltip("비우면 씬에서 찾는다")]
        public WaveDirector waves;
        public EnemyTestTarget core;
        [Tooltip("씬에 PlayerCrosshair가 있으면 이 임시 + 표시는 그리지 않는다")]
        public bool showCrosshair = true;
        bool hasCrosshairComponent;

        void Awake() { if (waves == null) waves = FindFirstObjectByType<WaveDirector>(); }
        void Start() => hasCrosshairComponent = FindAnyObjectByType<SandGuard.Player.PlayerCrosshair>() != null;

        void OnGUI()
        {
            GUI.Box(new Rect(16, 16, 470, 150), "SandGuard | Player and Enemy Test");
            GUI.Label(new Rect(28, 40, 450, 22), "WASD 이동 | 마우스 시점 | Space 점프(2단) | Shift 대시 | 좌클릭 발사 | 숫자 키 건설 | Esc 커서 해제");
            var health = playerLife as IHealth;
            var life = playerLife as ILifeState;
            if (health != null)
            {
                string state = life == null || life.State == LifeState.Alive ? "생존" : "무력화";
                GUI.Label(new Rect(28, 62, 300, 22), "플레이어 HP " + Mathf.CeilToInt(health.CurrentHealth) + "/" + Mathf.CeilToInt(health.MaxHealth) + "  " + state);
            }
            if (GUI.Button(new Rect(340, 62, 130, 22), "씬 다시 시작")) Restart();
            if (core != null)
                GUI.Label(new Rect(28, 84, 450, 22), "코어 HP " + Mathf.CeilToInt(core.CurrentHealth) + "/" + Mathf.CeilToInt(core.maxHealth) + (core.State == LifeState.Alive ? "" : "  (파괴됨)"));
            if (waves != null)
            {
                string phase = waves.Phase == GamePhase.Preparation ? "준비 " + Mathf.CeilToInt(waves.PreparationSecondsRemaining ?? 0f) + "초"
                    : waves.Phase == GamePhase.Combat ? "전투" : waves.Phase.ToString();
                GUI.Label(new Rect(28, 106, 450, 22), "웨이브 " + waves.WaveNumber + "/" + waves.TotalWaves + "  " + phase
                    + "  |  남은 등장 " + waves.PendingEnemyCount + "  생존 " + waves.AliveEnemyCount + "  누적 " + waves.SpawnedTotal);
                if (waves.Phase == GamePhase.Preparation && GUI.Button(new Rect(28, 130, 130, 24), waves.Started ? "준비 건너뛰기" : "웨이브 시작")) waves.SkipPreparation();
            }
            if (showCrosshair && !hasCrosshairComponent) GUI.Label(new Rect(Screen.width / 2f - 5f, Screen.height / 2f - 10f, 20f, 24f), "+");
        }

        /// <summary>빌드 설정에 없는 생성 씬이므로 에디터에서는 경로로 다시 연다.</summary>
        static void Restart()
        {
            var scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
            if (scene.buildIndex < 0) { UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single)); return; }
#endif
            SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
        }
    }
}

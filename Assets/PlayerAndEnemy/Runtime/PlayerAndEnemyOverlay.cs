using SandGuard.Enemy;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SandGuard.Integration
{
    /// <summary>플레이어·적 통합 테스트 씬의 안내와 상태 표시. 실제 HUD가 붙으면 제거한다.</summary>
    public sealed class PlayerAndEnemyOverlay : MonoBehaviour
    {
        [Tooltip("플레이어의 IHealth/ILifeState 컴포넌트 (PlayerHealth 또는 PlayerCombatTarget)")]
        public MonoBehaviour playerLife;
        public EnemyStreamSpawner spawner;
        public EnemyTestTarget core;
        public bool showCrosshair = true;

        void OnGUI()
        {
            GUI.Box(new Rect(16, 16, 470, 150), "SandGuard | Player and Enemy Test");
            GUI.Label(new Rect(28, 40, 450, 22), "WASD 이동 | 마우스 시점 | Space 점프(2단) | Shift 대시 | 좌클릭 발사 | Esc 커서 해제");
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
            if (spawner != null)
            {
                GUI.Label(new Rect(28, 106, 450, 22), "적 생존 " + spawner.AliveCount + " / 최대 " + spawner.maxAlive + "  누적 " + spawner.TotalSpawned + (spawner.autoSpawn ? "  자동 공급 중" : "  자동 공급 꺼짐"));
                if (GUI.Button(new Rect(28, 130, 110, 24), "적 추가")) spawner.Spawn();
                if (GUI.Button(new Rect(144, 130, 110, 24), "적 전부 제거")) spawner.ClearAll();
                if (GUI.Button(new Rect(260, 130, 130, 24), spawner.autoSpawn ? "자동 공급 끄기" : "자동 공급 켜기")) spawner.autoSpawn = !spawner.autoSpawn;
            }
            if (showCrosshair) GUI.Label(new Rect(Screen.width / 2f - 5f, Screen.height / 2f - 10f, 20f, 24f), "+");
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

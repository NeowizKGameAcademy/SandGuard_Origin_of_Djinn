using System.Collections.Generic;
using System.IO;
using DesertTower.Levels;
using DesertTower.VFX;
using SandGuard.Enemy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace SandGuard.Integration.Editor
{
    /// <summary>
    /// 플레이어와 적을 같은 레벨에 놓은 통합 테스트 씬을 만든다. 숨겨진 협곡 레벨(HiddenPyramidCanyon)을 복사한 뒤
    /// PlayerStart 마커에 플레이어, Core 마커에 코어 표적, EnemySpawn 마커에 적 공급기를 놓는다.
    /// 레벨의 카메라·오디오 리스너는 끄고 플레이어 카메라를 쓴다. 이미 있으면 건드리지 않는다 (Rebuild 메뉴로 다시 만든다).
    /// </summary>
    public static class PlayerAndEnemySceneBuilder
    {
        const string Root = "Assets/PlayerAndEnemy/Generated";
        public const string ScenePath = Root + "/PlayerAndEnemyTest.unity";
        const string LevelScenePath = "Assets/Resources/DesertTowerLevels/HiddenCanyon/Scenes/HiddenPyramidCanyon.unity";
        const string PlayerPrefabPath = "Assets/Player/Generated/Player.prefab";
        const string EnemyPrefabPath = "Assets/Enemy/Generated/Enemy.prefab";
        const string CoreMaterialPath = "Assets/Enemy/Generated/Core.mat";
        const string WallHitPath = "Assets/Resources/VFX/Prefabs/VFX_Wall_Hit.prefab";
        const string WallDestroyPath = "Assets/Resources/VFX/Prefabs/VFX_Wall_Destroy.prefab";

        [MenuItem("SandGuard/Player And Enemy/Create Test Scene")]
        public static void CreateScene()
        {
            if (File.Exists(ScenePath)) { Debug.Log("PLAYER_ENEMY_SCENE_EXISTS: " + ScenePath + " (Rebuild 메뉴로 다시 만들 수 있습니다)"); return; }
            Build();
        }

        [MenuItem("SandGuard/Player And Enemy/Rebuild Test Scene")]
        public static void RebuildScene()
        {
            if (File.Exists(ScenePath)) AssetDatabase.DeleteAsset(ScenePath);
            Build();
        }

        static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null) SandGuard.Player.Editor.PlayerSetupBuilder.CreateMissingAssets();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath) == null) SandGuard.Enemy.Editor.EnemySetupBuilder.CreateMissingAssets();
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            if (playerPrefab == null || enemyPrefab == null) { Debug.LogError("플레이어/적 프리팹을 만들지 못했습니다."); return; }
            if (!File.Exists(LevelScenePath)) { Debug.LogError("레벨 씬이 없습니다: " + LevelScenePath); return; }

            Directory.CreateDirectory(Root);
            AssetDatabase.Refresh();
            if (!AssetDatabase.CopyAsset(LevelScenePath, ScenePath)) { Debug.LogError("레벨 씬 복사 실패"); return; }
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 레벨 카메라·리스너는 끈다. 플레이어 프리팹의 카메라가 MainCamera다.
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)) camera.enabled = false;
            foreach (var listener in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None)) listener.enabled = false;

            LevelRoot level = Object.FindFirstObjectByType<LevelRoot>();
            Vector3 playerStart = new Vector3(48, 0, 70), corePosition = new Vector3(22, 0, -40);
            var spawnPoints = new List<Transform>();
            bool startFound = false;
            if (level != null)
            {
                foreach (var marker in level.Markers)
                {
                    if (marker.kind == MarkerKind.PlayerStart && !startFound) { playerStart = marker.transform.position; startFound = true; }
                    else if (marker.kind == MarkerKind.Core) corePosition = marker.transform.position;
                    else if (marker.kind == MarkerKind.EnemySpawn) spawnPoints.Add(marker.transform);
                }
            }
            if (spawnPoints.Count == 0)
            {
                var fallback = new GameObject("Enemy Spawn (fallback)").transform;
                fallback.position = playerStart + new Vector3(0, 0, 12);
                spawnPoints.Add(fallback);
            }

            // 코어 표적: 적이 향하고 공격하는 대상
            var core = GameObject.CreatePrimitive(PrimitiveType.Cube);
            core.name = "Core (test target)";
            core.transform.position = corePosition + Vector3.up * 1f;
            core.transform.localScale = new Vector3(2, 2, 2);
            var coreMaterial = AssetDatabase.LoadAssetAtPath<Material>(CoreMaterialPath);
            if (coreMaterial != null) core.GetComponent<Renderer>().sharedMaterial = coreMaterial;
            var coreTarget = core.AddComponent<EnemyTestTarget>();
            coreTarget.kind = CombatTargetKind.Core; coreTarget.maxHealth = 500f; coreTarget.factionId = "Ally";
            core.AddComponent<EnemyObjective>();
            var obstacle = core.AddComponent<NavMeshObstacle>();
            obstacle.carving = true; obstacle.shape = NavMeshObstacleShape.Box; obstacle.size = Vector3.one;
            var coreVfx = core.AddComponent<VfxHitReaction>();
            coreVfx.HitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallHitPath);
            coreVfx.DeathPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallDestroyPath);
            coreVfx.FitDeathToBounds = true; coreVfx.CameraShake = 0.1f; coreVfx.DeathLifetime = 5f;

            // 플레이어: 시작 마커에서 코어 쪽을 본다
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.name = "Player";
            player.transform.position = playerStart + Vector3.up * 0.2f;
            Vector3 toCore = Vector3.ProjectOnPlane(corePosition - playerStart, Vector3.up);
            if (toCore.sqrMagnitude > 0.01f) player.transform.rotation = Quaternion.LookRotation(toCore);
            // 프리팹에 전투 개체(PlayerHealth 등)가 있으면 그대로 쓰고, 없을 때만 임시 PlayerCombatTarget을 붙인다.
            var playerLife = player.GetComponent<ICombatTarget>() as MonoBehaviour;
            if (playerLife == null) { var combat = player.AddComponent<PlayerCombatTarget>(); combat.maxHealth = 100f; playerLife = combat; }

            // 적 공급기
            var spawner = new GameObject("Enemy Spawner").AddComponent<EnemyStreamSpawner>();
            spawner.enemyPrefab = enemyPrefab;
            spawner.spawnPoints = spawnPoints.ToArray();
            spawner.objective = core.transform;
            spawner.initialCount = 3; spawner.interval = 8f; spawner.perWave = 2; spawner.maxAlive = 8;

            var overlay = new GameObject("Test HUD").AddComponent<PlayerAndEnemyOverlay>();
            overlay.playerLife = playerLife; overlay.spawner = spawner; overlay.core = coreTarget;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("PLAYER_ENEMY_SCENE_READY: " + ScenePath + " | player " + playerStart + " | core " + corePosition + " | spawns " + spawnPoints.Count);
        }
    }
}

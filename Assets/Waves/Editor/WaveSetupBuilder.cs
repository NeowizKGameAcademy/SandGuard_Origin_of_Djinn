using System.IO;
using System.Linq;
using DesertTower.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SandGuard.Waves.Editor
{
    /// <summary>적 5종의 레벨 요소 정의, 카탈로그, 협곡 웨이브 세트를 만들고 통합 씬에 웨이브 디렉터·풀을 넣는다.</summary>
    /// <remarks>웨이브 에셋은 gameKey만 들고, 프리팹은 카탈로그가 푼다. 원본 레벨의 동선 확인용 WaveSet은 건드리지 않고 씬 복사본의 LevelRoot.waves만 바꾼다.</remarks>
    public static class WaveSetupBuilder
    {
        const string Generated = "Assets/Waves/Generated";
        public const string CatalogPath = Generated + "/EnemyCatalog.asset";
        public const string WaveSetPath = Generated + "/CanyonBanditWaves.asset";
        const string PlayerAndEnemyScene = "Assets/PlayerAndEnemy/Generated/PlayerAndEnemyTest.unity";
        // HiddenPyramidCanyon의 스폰·코어·경로 마커 ID (CanyonPathPreviewWaves와 동일). 다른 레벨이면 WireIntoScene이 그 레벨의 첫 그룹 ID로 바꾼다.
        const string SpawnId = "669fd1761e5342dcb7985133c6d11a6d", TargetId = "c44756c477fe482ab68ed9a04d2372a6", RouteId = "c982609ee92c4acfbc8bbac7c8cfa504";

        static readonly (string key, string name, string prefab, Color color)[] Bandits =
        {
            ("bandit.swordsman", "검병", "Enemy_Swordsman", new Color(.85f, .30f, .25f)),
            ("bandit.assassin", "암살자", "Enemy_Assassin", new Color(.55f, .20f, .35f)),
            ("bandit.shieldguard", "방패병", "Enemy_ShieldGuard", new Color(.70f, .55f, .30f)),
            ("bandit.hammerbrute", "망치병", "Enemy_HammerBrute", new Color(.60f, .35f, .20f)),
            ("bandit.chief", "우두머리 자히르", "Enemy_Chief", new Color(.90f, .70f, .20f)),
        };

        [MenuItem("SandGuard/Waves/Create Missing Assets")]
        public static void Build()
        {
            Directory.CreateDirectory(Generated + "/Elements");
            AssetDatabase.Refresh();
            var catalog = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(CatalogPath);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<EnemyCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
            var elements = new System.Collections.Generic.Dictionary<string, LevelElementDefinition>();
            foreach (var (key, name, prefabName, color) in Bandits)
            {
                string path = Generated + "/Elements/" + prefabName.Replace("Enemy_", "Bandit_") + ".asset";
                var element = AssetDatabase.LoadAssetAtPath<LevelElementDefinition>(path);
                if (element == null) { element = ScriptableObject.CreateInstance<LevelElementDefinition>(); AssetDatabase.CreateAsset(element, path); }
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Generated/" + prefabName + ".prefab");
                element.displayName = name; element.gameKey = key; element.previewPrefab = prefab; element.previewColor = color;
                element.footprint = new Vector3(.7f, 2f, .7f);
                EditorUtility.SetDirty(element);
                elements[key] = element;
                var entry = catalog.entries.FirstOrDefault(e => e != null && e.gameKey == key);
                if (entry == null) { entry = new EnemyCatalog.Entry { gameKey = key }; catalog.entries.Add(entry); }
                entry.displayName = name; entry.prefab = prefab;
            }
            EditorUtility.SetDirty(catalog);

            var waves = AssetDatabase.LoadAssetAtPath<WaveSet>(WaveSetPath);
            if (waves == null) { waves = ScriptableObject.CreateInstance<WaveSet>(); AssetDatabase.CreateAsset(waves, WaveSetPath); }
            waves.waves.Clear();
            SpawnGroup G(string key, int count, float delay, float interval) => new SpawnGroup { spawnId = SpawnId, targetId = TargetId, routeId = RouteId, element = elements[key], count = count, delay = delay, interval = interval };
            waves.waves.Add(new Wave { label = "Wave 1 — 검병", preparationSeconds = 10, groups = { G("bandit.swordsman", 4, 0, 2f) } });
            waves.waves.Add(new Wave { label = "Wave 2 — 검병·암살자", preparationSeconds = 15, groups = { G("bandit.swordsman", 4, 0, 1.5f), G("bandit.assassin", 3, 4, 1.5f) } });
            waves.waves.Add(new Wave { label = "Wave 3 — 방패병 합류", preparationSeconds = 15, groups = { G("bandit.shieldguard", 3, 0, 2f), G("bandit.swordsman", 4, 2, 1.5f) } });
            waves.waves.Add(new Wave { label = "Wave 4 — 철거꾼", preparationSeconds = 15, groups = { G("bandit.hammerbrute", 2, 0, 3f), G("bandit.shieldguard", 3, 3, 2f), G("bandit.assassin", 4, 6, 1f) } });
            waves.waves.Add(new Wave { label = "Wave 5 — 우두머리", preparationSeconds = 20, groups = { G("bandit.chief", 1, 0, 1f), G("bandit.swordsman", 4, 2, 1.5f), G("bandit.hammerbrute", 2, 5, 3f) } });
            EditorUtility.SetDirty(waves);
            AssetDatabase.SaveAssets();
            Debug.Log("WAVE_ASSETS_READY " + WaveSetPath);
        }

        public static void EnsureAssets()
        {
            if (AssetDatabase.LoadAssetAtPath<WaveSet>(WaveSetPath) == null || AssetDatabase.LoadAssetAtPath<EnemyCatalog>(CatalogPath) == null) Build();
        }

        /// <summary>씬의 LevelRoot에 협곡 웨이브 세트를 물리고, 임시 스포너를 지우고, 웨이브 디렉터와 풀을 넣는다.</summary>
        public static bool WireIntoScene(Scene scene)
        {
            EnsureAssets();
            var level = Object.FindFirstObjectByType<LevelRoot>();
            if (level == null) { Debug.LogWarning("WaveSetupBuilder: LevelRoot가 없습니다."); return false; }
            var waves = AssetDatabase.LoadAssetAtPath<WaveSet>(WaveSetPath);
            // 다른 레벨이면 그 레벨의 기존 첫 그룹이 가리키는 마커 ID를 따른다.
            var previous = level.waves;
            if (previous != null && previous != waves && previous.waves.Count > 0 && previous.waves[0].groups.Count > 0)
            {
                var sample = previous.waves[0].groups[0];
                if (!string.IsNullOrWhiteSpace(sample.spawnId) && sample.spawnId != SpawnId)
                {
                    foreach (var wave in waves.waves) foreach (var group in wave.groups)
                    { group.spawnId = sample.spawnId; group.targetId = sample.targetId; group.routeId = sample.routeId; }
                    EditorUtility.SetDirty(waves);
                }
            }
            level.waves = waves; EditorUtility.SetDirty(level);
            foreach (var stale in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name == "Enemy Spawner" || t.name == "Enemy Spawn (fallback)").ToArray())
                Object.DestroyImmediate(stale.gameObject);
            var pool = Object.FindFirstObjectByType<EnemyPool>() ?? new GameObject("Enemy Pool").AddComponent<EnemyPool>();
            var director = Object.FindFirstObjectByType<WaveDirector>() ?? new GameObject("Wave Director").AddComponent<WaveDirector>();
            director.level = level; director.pool = pool; director.catalog = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(CatalogPath);
            EditorUtility.SetDirty(director);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("WAVES_WIRED " + waves.waves.Count + " waves");
            return true;
        }

        [MenuItem("SandGuard/Waves/Wire Into Player And Enemy Scene")]
        public static void WirePlayerAndEnemyScene()
        {
            if (!File.Exists(PlayerAndEnemyScene)) { Debug.LogError("씬이 없습니다: " + PlayerAndEnemyScene); return; }
            var scene = EditorSceneManager.OpenScene(PlayerAndEnemyScene, OpenSceneMode.Single);
            WireIntoScene(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        public static void BuildAndWire() { Build(); WirePlayerAndEnemyScene(); }
    }
}

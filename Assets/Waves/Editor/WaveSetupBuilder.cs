using System.IO;
using System.Linq;
using DesertTower.LevelIntegration;
using DesertTower.Levels;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SandGuard.Waves.Editor
{
    /// <summary>적 5종의 레벨 요소 정의, 카탈로그, 협곡 웨이브 세트를 없을 때만 만든다.</summary>
    /// <remarks>웨이브 에셋은 gameKey만 들고, 프리팹은 카탈로그가 푼다. 이미 있는 에셋은 기획 편집이 정본이므로 덮어쓰지 않는다. 되돌리려면 Reset 메뉴를 쓴다.</remarks>
    public static class WaveSetupBuilder
    {
        const string Generated = "Assets/Waves/Generated";
        /// <summary>게임이 실제로 보는 카탈로그. 적 등록은 여기 한 곳에만 쌓인다.</summary>
        public const string CatalogPath = "Assets/2.Model/Prefabs/Level/GamePrefabCatalog.asset";
        public const string WaveSetPath = Generated + "/CanyonBanditWaves.asset";
        // HiddenPyramidCanyon의 스폰·코어·경로 마커 ID (CanyonPathPreviewWaves와 동일) 기준.
        const string SpawnId = "669fd1761e5342dcb7985133c6d11a6d", TargetId = "c44756c477fe482ab68ed9a04d2372a6", RouteId = "c982609ee92c4acfbc8bbac7c8cfa504";

        static readonly (string key, string name, string prefab, Color color)[] Bandits =
        {
            ("bandit.swordsman", "검병", "Enemy_Swordsman", new Color(.85f, .30f, .25f)),
            ("bandit.assassin", "암살자", "Enemy_Assassin", new Color(.55f, .20f, .35f)),
            ("bandit.shieldguard", "방패병", "Enemy_ShieldGuard", new Color(.70f, .55f, .30f)),
            ("bandit.hammerbrute", "망치병", "Enemy_HammerBrute", new Color(.60f, .35f, .20f)),
            ("bandit.chief", "우두머리 자히르", "Enemy_Chief", new Color(.90f, .70f, .20f)),
        };

        /// <summary>없는 에셋만 만든다. 이미 있는 요소·카탈로그 항목·웨이브 구성은 손대지 않는다.
        /// 기획자가 편집한 내용이 정본이므로 재실행해도 덮어쓰지 않는다.</summary>
        [MenuItem("SandGuard/Waves/Create Missing Assets")]
        public static void Build() { Run(false); }

        /// <summary>웨이브 구성을 코드 기본값으로 되돌린다. 기획 편집 내용이 사라지므로 따로 확인을 받는다.</summary>
        [MenuItem("SandGuard/Waves/Reset Canyon Waves To Code Defaults")]
        public static void ResetWavesToDefaults()
        {
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog("협곡 웨이브 초기화",
                WaveSetPath + " 의 웨이브 구성을 코드 기본값(5웨이브)으로 되돌립니다. "
                + "기획자가 편집한 수량·간격·종류가 모두 사라집니다.",
                "되돌린다", "취소")) return;
            Run(true);
        }

        static void Run(bool resetWaves)
        {
            Directory.CreateDirectory(Generated + "/Elements");
            AssetDatabase.Refresh();
            int created = 0, kept = 0;

            var catalog = AssetDatabase.LoadAssetAtPath<PrefabCatalog>(CatalogPath);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<PrefabCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); created++; }

            var elements = new System.Collections.Generic.Dictionary<string, LevelElementDefinition>();
            foreach (var (key, name, prefabName, color) in Bandits)
            {
                string path = Generated + "/Elements/" + prefabName.Replace("Enemy_", "Bandit_") + ".asset";
                var element = AssetDatabase.LoadAssetAtPath<LevelElementDefinition>(path);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Generated/" + prefabName + ".prefab");
                if (element == null)
                {
                    element = ScriptableObject.CreateInstance<LevelElementDefinition>();
                    AssetDatabase.CreateAsset(element, path);
                    element.displayName = name; element.gameKey = key; element.previewPrefab = prefab; element.previewColor = color;
                    element.footprint = new Vector3(.7f, 2f, .7f);
                    EditorUtility.SetDirty(element); created++;
                }
                else kept++;
                elements[key] = element;

                var entry = catalog.Find(key);
                if (entry == null)
                {
                    entry = new PrefabEntry { key = key, displayName = name, role = PrefabRole.Enemy, prefab = prefab };
                    catalog.entries.Add(entry);
                    EditorUtility.SetDirty(catalog); created++;
                }
                else kept++;
            }

            var waves = AssetDatabase.LoadAssetAtPath<WaveSet>(WaveSetPath);
            bool fresh = waves == null;
            if (fresh) { waves = ScriptableObject.CreateInstance<WaveSet>(); AssetDatabase.CreateAsset(waves, WaveSetPath); created++; }
            if (fresh || resetWaves)
            {
                FillDefaultWaves(waves, elements);
                EditorUtility.SetDirty(waves);
            }
            else kept++;

            AssetDatabase.SaveAssets();
            Debug.Log("WAVE_ASSETS_READY " + WaveSetPath + "  생성 " + created + "개, 유지 " + kept + "개"
                + (resetWaves ? "  (웨이브 구성을 코드 기본값으로 되돌림)" : ""));
        }

        static void FillDefaultWaves(WaveSet waves, System.Collections.Generic.Dictionary<string, LevelElementDefinition> elements)
        {
            waves.waves.Clear();
            SpawnGroup G(string key, int count, float delay, float interval) => new SpawnGroup { spawnId = SpawnId, targetId = TargetId, routeId = RouteId, element = elements[key], enemyKey = key, count = count, delay = delay, interval = interval };
            waves.waves.Add(new Wave { label = "Wave 1 — 검병", preparationSeconds = 10, groups = { G("bandit.swordsman", 4, 0, 2f) } });
            waves.waves.Add(new Wave { label = "Wave 2 — 검병·암살자", preparationSeconds = 15, groups = { G("bandit.swordsman", 4, 0, 1.5f), G("bandit.assassin", 3, 4, 1.5f) } });
            waves.waves.Add(new Wave { label = "Wave 3 — 방패병 합류", preparationSeconds = 15, groups = { G("bandit.shieldguard", 3, 0, 2f), G("bandit.swordsman", 4, 2, 1.5f) } });
            waves.waves.Add(new Wave { label = "Wave 4 — 철거꾼", preparationSeconds = 15, groups = { G("bandit.hammerbrute", 2, 0, 3f), G("bandit.shieldguard", 3, 3, 2f), G("bandit.assassin", 4, 6, 1f) } });
            waves.waves.Add(new Wave { label = "Wave 5 — 우두머리", preparationSeconds = 20, groups = { G("bandit.chief", 1, 0, 1f), G("bandit.swordsman", 4, 2, 1.5f), G("bandit.hammerbrute", 2, 5, 3f) } });
        }

        public static void EnsureAssets()
        {
            if (AssetDatabase.LoadAssetAtPath<WaveSet>(WaveSetPath) == null || AssetDatabase.LoadAssetAtPath<PrefabCatalog>(CatalogPath) == null) Build();
        }

    }
}

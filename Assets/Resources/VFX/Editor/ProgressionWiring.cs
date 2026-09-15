using System.Collections.Generic;
using SandGuard.Enemy;
using SandGuard.Player;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;

namespace DesertTower.VFX.Editor
{
    /// <summary>
    /// 경험치·레벨 성장을 데모 프리팹에 꽂는다. 여러 번 실행해도 안전하다(이미 있는 수치는 덮지 않는다).
    /// 성장 표 에셋, 경험치 입자 프리팹(VFX_Experience_Mote 외형), 적 프리팹의 드롭, 플레이어 프리팹의 PlayerProgression과 레벨업 VFX.
    /// Batch: <c>-executeMethod DesertTower.VFX.Editor.ProgressionWiring.WireAll</c>
    /// </summary>
    public static class ProgressionWiring
    {
        public const string TablePath = "Assets/Player/Generated/PlayerProgressionTable.asset";
        public const string OrbPrefabPath = "Assets/Enemy/Generated/ExperienceOrb.prefab";
        const string EnemyPrefabPath = "Assets/Enemy/Generated/Enemy.prefab";
        const string PlayerPrefabPath = "Assets/Player/Generated/Player.prefab";

        // 처음 연결할 때만 넣는 종류별 처치 경험치. 임시 수치.
        static readonly Dictionary<string, int> VariantExperience = new Dictionary<string, int>
        {
            { "Assets/Enemy/Generated/Enemy_Swordsman.prefab", 10 },
            { "Assets/Enemy/Generated/Enemy_Assassin.prefab", 12 },
            { "Assets/Enemy/Generated/Enemy_ShieldGuard.prefab", 15 },
            { "Assets/Enemy/Generated/Enemy_HammerBrute.prefab", 20 },
            { "Assets/Enemy/Generated/Enemy_Chief.prefab", 60 },
        };

        [MenuItem("SandGuard/Progression/Connect Experience and Level Up")]
        public static void WireAll()
        {
            var table = EnsureTable();
            var orb = BuildOrbPrefab();
            WireEnemyPrefabs(orb);
            WirePlayerPrefab(table);
            AssetDatabase.SaveAssets();
            Debug.Log("[Progression] Experience drops, PlayerProgression and level-up VFX wired.");
        }

        static PlayerProgressionTable EnsureTable()
        {
            var table = AssetDatabase.LoadAssetAtPath<PlayerProgressionTable>(TablePath);
            if (table != null) return table;
            table = ScriptableObject.CreateInstance<PlayerProgressionTable>();
            table.ApplyDefaults();
            AssetDatabase.CreateAsset(table, TablePath);
            return table;
        }

        static ExperienceOrb BuildOrbPrefab()
        {
            var mote = AssetDatabase.LoadAssetAtPath<GameObject>(ExperienceMoteBuilder.PrefabPath);
            if (mote == null) mote = ExperienceMoteBuilder.Build();
            var root = new GameObject("ExperienceOrb");
            try
            {
                root.AddComponent<ExperienceOrb>();
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(mote, root.transform);
                visual.name = "Visual";
                // 루트는 큐브보다 0.35 아래에 있다. 루트를 입자 위치로 쓰므로 외형을 내려 큐브가 루트에 오게 한다.
                visual.transform.localPosition = new Vector3(0f, -0.35f, 0f);
                return PrefabUtility.SaveAsPrefabAsset(root, OrbPrefabPath).GetComponent<ExperienceOrb>();
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void WireEnemyPrefabs(ExperienceOrb orb)
        {
            bool firstTime;
            var root = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
            try
            {
                var drop = root.GetComponent<EnemyExperienceDrop>();
                firstTime = drop == null;
                if (firstTime) drop = root.AddComponent<EnemyExperienceDrop>();
                drop.orbPrefab = orb;
                PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            if (!firstTime) return;

            foreach (var pair in VariantExperience)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(pair.Key) == null) continue;
                var variant = PrefabUtility.LoadPrefabContents(pair.Key);
                try
                {
                    var drop = variant.GetComponent<EnemyExperienceDrop>();
                    if (drop == null) continue;
                    drop.experience = pair.Value;
                    PrefabUtility.SaveAsPrefabAsset(variant, pair.Key);
                }
                finally { PrefabUtility.UnloadPrefabContents(variant); }
            }
        }

        static void WirePlayerPrefab(PlayerProgressionTable table)
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var progression = root.GetComponent<PlayerProgression>();
                if (progression == null) progression = root.AddComponent<PlayerProgression>();
                if (progression.table == null) progression.table = table;
                progression.health = root.GetComponent<PlayerHealth>();
                progression.mana = root.GetComponent<PlayerManaWallet>();
                progression.stats = root.GetComponent<PlayerStats>();
                var wallet = root.GetComponent<PlayerManaWallet>();
                if (wallet != null) wallet.stats = progression.stats;

                var anchor = root.transform.Find("LevelUpVfx");
                if (anchor == null)
                {
                    anchor = new GameObject("LevelUpVfx").transform;
                    anchor.SetParent(root.transform, false);
                    anchor.localPosition = new Vector3(0f, 0.02f, 0f);
                }
                var oneShot = anchor.GetComponent<VfxOneShot>();
                if (oneShot == null) oneShot = anchor.gameObject.AddComponent<VfxOneShot>();
                oneShot.Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GoldVfxBuilder.LevelUpPath);
                if (oneShot.Prefab == null) oneShot.Prefab = GoldVfxBuilder.BuildLevelUp();
                oneShot.Anchor = anchor;
                oneShot.ParentToAnchor = true; // 달리면서 레벨업해도 링이 발밑을 따라온다
                oneShot.Lifetime = 2.5f;
                if (!HasListener(progression.onLevelUp, oneShot))
                    UnityEventTools.AddPersistentListener(progression.onLevelUp, new UnityAction(oneShot.Fire));
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static bool HasListener(UnityEventBase unityEvent, Object target)
        {
            for (int i = 0; i < unityEvent.GetPersistentEventCount(); i++)
                if (unityEvent.GetPersistentTarget(i) == target) return true;
            return false;
        }
    }
}

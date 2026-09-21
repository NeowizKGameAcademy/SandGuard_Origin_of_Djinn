using DesertTower.VFX;
using UnityEditor;
using UnityEngine;

namespace SandGuard.Enemy.Editor
{
    public static class ChiefSkillSetup
    {
        public const string PrefabPath = "Assets/Enemy/Generated/Enemy_Chief.prefab";
        public static void Ensure(GameObject root)
        {
            ChiefBombSkillSetup.Ensure(root);
            var shield = root.GetComponent<EnemyShield>() ?? root.AddComponent<EnemyShield>();
            shield.maxHealth = 100f;
            shield.enabled = false;
            var skill = root.GetComponent<ChiefGoldenShieldSkill>() ?? root.AddComponent<ChiefGoldenShieldSkill>();
            skill.shield = shield;
            skill.vfxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/VFX/Prefabs/VFX_Chief_Golden_Shield_Loop.prefab").GetComponent<VfxGoldenShield>();
        }
        [MenuItem("SandGuard/Enemy/Connect Chief Golden Shield Skill")]
        public static void Build()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try { Ensure(root); PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("CHIEF_SKILL_CONNECTED " + PrefabPath);
        }
    }
}

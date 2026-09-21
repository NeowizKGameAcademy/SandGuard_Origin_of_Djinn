using UnityEditor;
using UnityEngine;

namespace SandGuard.Enemy.Editor
{
    /// <summary>방패병 변형 프리팹에 정면 방어(<see cref="EnemyShield"/>)와 방어 연출을 붙인다. 전투 외형 빌더도 같은 설정을 쓴다.</summary>
    public static class EnemyShieldSetup
    {
        public const string ShieldGuardName = "ShieldGuard";
        const string GuardVfxPath = "Assets/Resources/VFX/Prefabs/VFX_Shield_Front_Guard.prefab";

        [MenuItem("SandGuard/Enemy/Connect Shield Guard Front Block")]
        public static void ConnectShieldGuard()
        {
            string path = EnemyCombatArtBuilder.VariantPath(ShieldGuardName);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Debug.LogError("방패병 프리팹이 없습니다: " + path); return; }
            var root = PrefabUtility.LoadPrefabContents(path);
            try { Ensure(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("ENEMY_SHIELD_READY " + path);
        }

        /// <summary>없으면 붙이고, 연출 프리팹이 비어 있으면 채운다. 배율·각도 조정값은 건드리지 않는다.</summary>
        public static EnemyShield Ensure(GameObject root)
        {
            var shield = root.GetComponent<EnemyShield>();
            if (shield == null)
            {
                shield = root.AddComponent<EnemyShield>();
                shield.maxHealth = 60f;
            }
            if (shield.guardVfx == null) shield.guardVfx = AssetDatabase.LoadAssetAtPath<GameObject>(GuardVfxPath);
            return shield;
        }
    }
}

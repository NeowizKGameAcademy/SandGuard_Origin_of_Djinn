using System;
using UnityEditor;
using UnityEngine;

namespace SandGuard.Player.Editor
{
    public static class PlayerBoltVfxBuilder
    {
        const string BoltPath = "Assets/Player/Generated/PlayerBolt.prefab";
        const string VfxPath = "Assets/Resources/VFX/Prefabs/VFX_ManaBolt_Projectile.prefab";
        const string ImpactPath = "Assets/Resources/VFX/Prefabs/VFX_ManaBolt_Impact.prefab";

        [MenuItem("SandGuard/Player/Connect Mana Bolt VFX")]
        public static void Apply()
        {
            var vfx = AssetDatabase.LoadAssetAtPath<GameObject>(VfxPath);
            var impact = AssetDatabase.LoadAssetAtPath<GameObject>(ImpactPath);
            if (vfx == null || impact == null) throw new InvalidOperationException("Mana bolt VFX prefabs are missing.");
            var root = PrefabUtility.LoadPrefabContents(BoltPath);
            try
            {
                var visual = root.transform.Find("VisualRoot");
                if (visual == null) throw new InvalidOperationException("PlayerBolt has no VisualRoot.");
                var placeholder = visual.Find("BoltVisual");
                if (placeholder != null) UnityEngine.Object.DestroyImmediate(placeholder.gameObject);
                var effect = visual.Find("VFX_ManaBolt_Projectile");
                if (effect == null)
                {
                    effect = ((GameObject)PrefabUtility.InstantiatePrefab(vfx)).transform;
                    effect.SetParent(visual, false);
                }
                var bolt = root.GetComponent<PlayerProjectile>();
                bolt.visualRoot = visual;
                bolt.visualTailLifetime = 0.6f;
                bolt.impactPrefab = impact;
                bolt.impactLifetime = 2f;
                PrefabUtility.SaveAsPrefabAsset(root, BoltPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }
    }
}

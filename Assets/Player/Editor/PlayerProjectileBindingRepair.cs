using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SandGuard.Player.Editor
{
    public static class PlayerProjectileBindingRepair
    {
        [MenuItem("SandGuard/Player/Repair Projectile Binding")]
        public static void Run()
        {
            const string boltPath = "Assets/Player/Generated/PlayerBolt.prefab";
            const string playerPath = "Assets/Player/Generated/Player.prefab";
            AssetDatabase.ImportAsset(boltPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var boltRoot = AssetDatabase.LoadAssetAtPath<GameObject>(boltPath);
            var bolt = boltRoot != null ? boltRoot.GetComponent<PlayerProjectile>() : null;
            if (bolt == null) throw new InvalidOperationException("PlayerBolt prefab has no PlayerProjectile component");
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(bolt, out string guid, out long id);
            var root = PrefabUtility.LoadPrefabContents(playerPath);
            string report;
            try
            {
                var attack = root.GetComponent<PlayerBasicAttack>();
                if (attack == null) throw new InvalidOperationException("Player has no PlayerBasicAttack");
                report = $"Before binding: {attack.projectilePrefab != null}; bolt={guid}/{id}\n";
                attack.projectilePrefab = bolt;
                PrefabUtility.SaveAsPrefabAsset(root, playerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPath);
            if (prefab.GetComponent<PlayerBasicAttack>().projectilePrefab != bolt)
                throw new InvalidOperationException("Projectile binding did not persist");
            File.WriteAllText("Logs/player-projectile-binding.txt", report + "After binding: valid\n");
            Debug.Log("PLAYER_PROJECTILE_BINDING_REPAIRED " + report);
        }
    }
}

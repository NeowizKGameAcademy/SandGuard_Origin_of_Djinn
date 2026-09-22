#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using DesertTower.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SandGuard.Facility.Editor
{
    public static class TowerIconCatalogBuilder
    {
        const string CatalogPath = "Assets/Facility/Generated/FacilityCatalog.asset";
        const string IconFolder = "Assets/4.Sprite/UI/Facility/TowerIcons";
        const string LevelScenePath = "Assets/1.Scene/Level.unity";
        static readonly string[] TowerIds = { "skeleton", "obelisk", "cobra", "anubis" };

        [MenuItem("SandGuard/Facility/Apply Tower Selection Icons")]
        public static void Build()
        {
            ConfigureIcons();
            var catalog = AssetDatabase.LoadAssetAtPath<FacilityCatalog>(CatalogPath);
            if (catalog == null) throw new System.InvalidOperationException("Facility catalog missing: " + CatalogPath);

            var existing = catalog.facilities.Where(x => x != null).ToDictionary(x => x.id, x => x);
            catalog.facilities = new List<FacilityDefinition>
            {
                Definition(existing, "skeleton", "스켈레톤 관", "tower.skeleton", "Assets/2.Model/Prefabs/Tower_Coffin.prefab", "TowerIcon_3_Skeleton.png", 70),
                Definition(existing, "obelisk", "오벨리스크", "tower.obelisk", "Assets/2.Model/Prefabs/Tower_Obelisk.prefab", "TowerIcon_2_Obelisk.png", 55),
                Definition(existing, "cobra", "코브라 타워", "tower.cobra", "Assets/2.Model/Prefabs/Tower_Cobra.prefab", "TowerIcon_1_Cobra.png", 100),
                Definition(existing, "anubis", "아누비스 석상", "tower.anubis", "Assets/2.Model/Prefabs/Tower_Anubis.prefab", "TowerIcon_4_Anubis.png", 85)
            };
            EditorUtility.SetDirty(catalog);
            UpdateLevelSlots();
            AssetDatabase.SaveAssets();
            Debug.Log("[Facility] Tower menu order/icons applied: 1 Skeleton, 2 Obelisk, 3 Cobra, 4 Anubis.");
        }

        static FacilityDefinition Definition(Dictionary<string, FacilityDefinition> existing, string id, string label,
            string skillId, string prefabPath, string iconFile, int cost)
        {
            if (!existing.TryGetValue(id, out var definition)) definition = new FacilityDefinition { id = id };
            definition.displayName = label;
            definition.requiredSkillId = skillId;
            definition.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            definition.icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + "/" + iconFile);
            definition.manaCost = cost;
            definition.maxHealth = Mathf.Max(150f, definition.maxHealth);
            if (definition.prefab == null) Debug.LogError("Tower prefab missing: " + prefabPath);
            if (definition.icon == null) Debug.LogError("Tower icon missing: " + iconFile);
            return definition;
        }

        static void ConfigureIcons()
        {
            foreach (string path in AssetDatabase.FindAssets("t:Texture2D", new[] { IconFolder }).Select(AssetDatabase.GUIDToAssetPath))
            {
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 512;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }

        static void UpdateLevelSlots()
        {
            Scene scene = EditorSceneManager.OpenScene(LevelScenePath, OpenSceneMode.Single);
            bool changed = false;
            foreach (var slot in Object.FindObjectsByType<LevelBuildSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (slot.allowedFacilityIds.Count > 0 && slot.allowedFacilityIds.Any(id => !TowerIds.Contains(id))) continue;
                if (slot.allowedFacilityIds.SequenceEqual(TowerIds)) continue;
                slot.allowedFacilityIds = new List<string>(TowerIds);
                EditorUtility.SetDirty(slot);
                changed = true;
            }
            if (changed) EditorSceneManager.SaveScene(scene);
        }
    }
}
#endif

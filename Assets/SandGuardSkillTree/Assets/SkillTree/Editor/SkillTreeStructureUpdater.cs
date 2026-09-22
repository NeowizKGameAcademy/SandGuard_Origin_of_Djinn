#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using SandGuard.Skills.Unity;
using UnityEditor;
using UnityEngine;

namespace SandGuard.Skills.Editor
{
    public static class SkillTreeStructureUpdater
    {
        const string DataPath = "Assets/SandGuardSkillTree/Assets/SkillTree/Generated/SkillTreeData.asset";

        [MenuItem("SandGuard/Skills/Apply Attack And Tower Structure")]
        public static void Apply()
        {
            var data = AssetDatabase.LoadAssetAtPath<SkillTreeAsset>(DataPath);
            if (!data) throw new InvalidOperationException("SkillTreeData asset is missing.");

            Set(data, "attack.pierce", 1);
            Set(data, "attack.burst", 2, "attack.pierce");
            Set(data, "attack.vortex", 3, "attack.pierce");
            Set(data, "attack.storm", 3, "attack.burst", "attack.vortex");

            Set(data, "tower.skeleton", 1);
            Set(data, "tower.cobra", 2, "tower.skeleton");
            Set(data, "tower.obelisk", 2, "tower.skeleton");
            Set(data, "tower.anubis", 3, "tower.cobra");

            Reorder(data, new[]
            {
                "move.dash", "move.jump", "move.updraft", "move.recall",
                "attack.pierce", "attack.burst", "attack.vortex", "attack.storm",
                "tower.skeleton", "tower.cobra", "tower.obelisk", "tower.anubis"
            });

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            ValidateData(data);
            Debug.Log("SKILL_TREE_STRUCTURE_UPDATE_PASS");
        }

        [MenuItem("SandGuard/Skills/Validate Attack And Tower Structure")]
        public static void Validate()
        {
            var data = AssetDatabase.LoadAssetAtPath<SkillTreeAsset>(DataPath);
            if (!data) throw new InvalidOperationException("SkillTreeData asset is missing.");
            ValidateData(data);
            Debug.Log("SKILL_TREE_STRUCTURE_VALIDATION_PASS");
        }

        static void Set(SkillTreeAsset data, string id, int cost, params string[] prerequisites)
        {
            var node = data.nodes.FirstOrDefault(x => x != null && x.id == id);
            if (node == null) throw new InvalidOperationException($"Skill node is missing: {id}");
            node.cost = cost;
            node.prerequisites = new List<string>(prerequisites);
        }

        static void Reorder(SkillTreeAsset data, IEnumerable<string> ids)
        {
            var order = ids.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
            data.nodes = data.nodes.OrderBy(x => x != null && order.TryGetValue(x.id, out int index) ? index : int.MaxValue).ToList();
        }

        static void ValidateData(SkillTreeAsset data)
        {
            Expect(data, "attack.pierce", 1);
            Expect(data, "attack.burst", 2, "attack.pierce");
            Expect(data, "attack.vortex", 3, "attack.pierce");
            Expect(data, "attack.storm", 3, "attack.burst", "attack.vortex");
            Expect(data, "tower.skeleton", 1);
            Expect(data, "tower.cobra", 2, "tower.skeleton");
            Expect(data, "tower.obelisk", 2, "tower.skeleton");
            Expect(data, "tower.anubis", 3, "tower.cobra");
            data.Build();
        }

        static void Expect(SkillTreeAsset data, string id, int cost, params string[] prerequisites)
        {
            var node = data.nodes.FirstOrDefault(x => x != null && x.id == id);
            if (node == null || node.cost != cost || !node.prerequisites.SequenceEqual(prerequisites))
                throw new InvalidOperationException($"Unexpected skill structure: {id}");
        }
    }
}
#endif

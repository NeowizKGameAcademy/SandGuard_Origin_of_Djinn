using UnityEditor;
using UnityEngine;

namespace DesertTower.VFX.Editor
{
    public static class BlueDestructionBursts
    {
        public const float BreakTime = 0.72f;

        public static void Add(GameObject root, Bounds bounds)
        {
            var burst = AssetDatabase.LoadAssetAtPath<GameObject>(ObeliskDestructionBuilder.BurstPath);
            if (!burst) throw new System.InvalidOperationException("Build the blue partial explosion prefab first.");
            var points = new[] {
                new Vector3(-0.32f, 0.32f, -0.2f), new Vector3(0.32f, 0.62f, 0.15f),
                new Vector3(-0.22f, 0.82f, 0.08f), new Vector3(0.24f, 0.44f, -0.3f),
                new Vector3(0f, 0.55f, 0f)
            };
            float[] times = { 0.04f, 0.21f, 0.38f, 0.55f, BreakTime };
            for (int i = 0; i < points.Length; i++)
            {
                var pop = (GameObject)PrefabUtility.InstantiatePrefab(burst, root.transform);
                pop.name = i == 4 ? "Final Blue Break" : "Blue Pop " + (i + 1);
                pop.transform.localPosition = new Vector3(bounds.center.x + points[i].x * bounds.size.x,
                    bounds.min.y + points[i].y * bounds.size.y, bounds.center.z + points[i].z * bounds.size.z);
                float size = Mathf.Max(0.4f, bounds.size.y * (i == 4 ? 0.42f : 0.2f));
                pop.transform.localScale = Vector3.one * size;
                foreach (var ps in pop.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.startDelay = main.startDelay.constant + times[i];
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }
            }
        }

        public static Bounds MeshBounds(GameObject root)
        {
            Bounds result = default; bool found = false;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.name != "Mesh" || !filter.sharedMesh) continue;
                var bounds = CoreDestructionBuilder.WorldBounds(filter);
                if (!found) { result = bounds; found = true; } else result.Encapsulate(bounds);
            }
            if (!found) throw new System.InvalidOperationException("No model meshes found: " + root.name);
            return result;
        }
    }
}

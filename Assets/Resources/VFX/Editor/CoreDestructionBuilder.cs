using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static DesertTower.VFX.Editor.VfxBuildKit;

namespace DesertTower.VFX.Editor
{
    public static class CoreDestructionBuilder
    {
        public const string ReferencePath = "Assets/2.Model/Prefabs/New Core.prefab";
        public const string PrefabPath = PrefabDir + "/VFX_Core_Destruction.prefab";

        [MenuItem("DesertTower/VFX/Build Core Destruction (Level Model)")]
        public static void Build()
        {
            var reference = PrefabUtility.LoadPrefabContents(ReferencePath);
            var root = new GameObject("VFX_Core_Destruction");
            try
            {
                var ctrl = root.AddComponent<VfxCoreDestruction>();
                ctrl.WholeModel = true;
                ctrl.BurstTime = BlueDestructionBursts.BreakTime;
                ctrl.FlightTime = 0.65f;
                var proxy = Child(root, "Overload Level Core");
                ctrl.Overload = proxy.transform;
                var shards = new List<Transform>(); var starts = new List<Vector3>();
                var ends = new List<Vector3>(); var rotations = new List<Quaternion>();
                var paths = new List<string>(); var proxies = new List<Transform>();
                var matrices = new List<Matrix4x4>(); var groups = new List<int>();
                var bounds = BlueDestructionBursts.MeshBounds(reference);
                ctrl.Center = bounds.center; ctrl.Lift = bounds.size.y * 0.16f;
                int number = 0;
                foreach (var filter in reference.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    // Fracture the crystal, floating pieces and rings, excluding the gameplay range disc.
                    if (!renderer || !renderer.enabled || !filter.sharedMesh || filter.name != "Mesh") continue;
                    var part = Child(proxy, filter.transform.parent.name);
                    part.transform.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
                    part.transform.localScale = filter.transform.lossyScale;
                    part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    part.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
                    paths.Add(AnimationUtility.CalculateTransformPath(filter.transform, reference.transform));
                    proxies.Add(part.transform);
                    matrices.Add(filter.transform.localToWorldMatrix);
                    int first = shards.Count;
                    CobraDestructionBuilder.Fracture(root, filter, renderer, number, shards, starts, ends, rotations,
                        "LevelCoreDestruction", 2);
                    for (int i = first; i < shards.Count; i++)
                    {
                        groups.Add(number);
                        float angle = i * 2.4f;
                        float radius = Mathf.Max(bounds.extents.x, bounds.extents.z) * (0.7f + (i % 3) * 0.15f);
                        ends[i] = new Vector3(bounds.center.x + Mathf.Cos(angle) * radius, ends[i].y,
                            bounds.center.z + Mathf.Sin(angle) * radius);
                    }
                    number++;
                }
                ctrl.Shards = shards.ToArray(); ctrl.Starts = starts.ToArray();
                ctrl.Landings = ends.ToArray(); ctrl.LandingRotations = rotations.ToArray();
                ctrl.MeshPaths = paths.ToArray(); ctrl.ProxyMeshes = proxies.ToArray();
                ctrl.ReferenceMatrices = matrices.ToArray(); ctrl.ShardMeshIndices = groups.ToArray();
                BlueDestructionBursts.Add(root, bounds);
                ctrl.Restart();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[VFX] Level core: {number} original meshes, {shards.Count} textured fragments, 5 blue bursts.");
            }
            finally { Object.DestroyImmediate(root); PrefabUtility.UnloadPrefabContents(reference); }
        }

        public static Bounds WorldBounds(MeshFilter filter)
        {
            var b = filter.sharedMesh.bounds; var result = new Bounds(filter.transform.TransformPoint(b.center), Vector3.zero);
            for (int i = 0; i < 8; i++) result.Encapsulate(filter.transform.TransformPoint(new Vector3(
                (i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z)));
            return result;
        }
    }
}

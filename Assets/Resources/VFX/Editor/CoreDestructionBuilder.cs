using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static DesertTower.VFX.Editor.VfxBuildKit;

namespace DesertTower.VFX.Editor
{
    public static class CoreDestructionBuilder
    {
        public const string ReferencePath = "Assets/2.Model/Prefabs/Core Base.prefab";
        public const string PrefabPath = PrefabDir + "/VFX_Core_Destruction.prefab";
        const string MeshDir = RootDir + "/Meshes";

        [MenuItem("DesertTower/VFX/Build Core Destruction (Core Base)")]
        public static void Build()
        {
            var reference = AssetDatabase.LoadAssetAtPath<GameObject>(ReferencePath);
            var filter = reference.transform.Find("Core/Mesh").GetComponent<MeshFilter>();
            var pedestal = reference.transform.Find("Mesh").GetComponent<MeshFilter>();
            var bounds = WorldBounds(filter); var baseBounds = WorldBounds(pedestal);
            Debug.Log($"[VFX] Core Base crystal: center={bounds.center:F3}, size={bounds.size:F3}; pedestal={baseBounds.size:F3}");
            var spec = new ImpactSpec {
                PrefabName = "VFX_Core_Destruction", BodyHeight = bounds.center.y,
                FlashSize = bounds.size.y * 1.2f, FlashColor = new Color(0.8f, 1f, 1f),
                CoreCount = 3, CoreSize = bounds.size.y * 0.08f, CoreA = Color.white, CoreB = Teal, CoreC = TealDark,
                ShockwaveSize = Mathf.Max(baseBounds.size.x, baseBounds.size.z) * 2.2f, ShockwaveColor = Teal,
                DebrisCount = 24, DebrisSize = new Vector2(0.04f, 0.09f), DebrisSpeed = new Vector2(1.5f, 4f),
                DebrisLife = new Vector2(0.4f, 0.8f), DebrisA = Color.white, DebrisB = Teal, DebrisC = TealDark,
                EmberCount = 12, EmberCubes = true, EmberColor = Teal, EmberLife = new Vector2(0.7f, 1.5f),
                EmberSpeed = new Vector2(0.3f, 0.65f), DustCount = 8, DustSize = new Vector2(0.25f, 0.5f),
                DustLife = new Vector2(0.65f, 1.2f), LightIntensity = 4f, LightRange = bounds.size.y * 2f
            };
            ImpactVfxBuilder.Build(spec, PrefabPath);
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var shared = GetShared();
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>()) { var main = ps.main; main.startDelay = 0.2f; }
                var ctrl = root.AddComponent<VfxCoreDestruction>(); ctrl.Center = bounds.center; ctrl.Lift = bounds.size.y * 0.4f;
                var proxy = Child(root, "Overload Crystal");
                proxy.transform.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
                proxy.transform.localScale = filter.transform.lossyScale;
                proxy.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                ctrl.OverloadRenderer = proxy.AddComponent<MeshRenderer>(); ctrl.OverloadRenderer.sharedMaterial = shared.CubeWhite;
                ctrl.Overload = proxy.transform;
                ctrl.Cracks = Cracks(root, filter, bounds, shared);
                BuildShards(root, filter, bounds, baseBounds, ctrl, shared);
                ctrl.Restart();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        public static Bounds WorldBounds(MeshFilter filter)
        {
            var b = filter.sharedMesh.bounds; var result = new Bounds(filter.transform.TransformPoint(b.center), Vector3.zero);
            for (int i = 0; i < 8; i++) result.Encapsulate(filter.transform.TransformPoint(new Vector3(
                (i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z)));
            return result;
        }

        static LineRenderer[] Cracks(GameObject root, MeshFilter source, Bounds b, Shared shared)
        {
            Vector3[] surface;
            using (var data = MeshUtility.AcquireReadOnlyMeshData(source.sharedMesh))
            using (var vertices = new NativeArray<Vector3>(data[0].vertexCount, Allocator.Temp))
            {
                data[0].GetVertices(vertices); surface = new Vector3[vertices.Length];
                for (int i = 0; i < surface.Length; i++) surface[i] = source.transform.TransformPoint(vertices[i]);
            }
            var lines = new LineRenderer[3];
            for (int a = 0; a < lines.Length; a++)
            {
                var line = Child(root, "Overload Fracture " + a).AddComponent<LineRenderer>();
                line.useWorldSpace = false; line.positionCount = 9; line.widthMultiplier = b.size.y * 0.007f;
                line.sharedMaterial = shared.MeshAdditive; line.shadowCastingMode = ShadowCastingMode.Off;
                for (int i = 0; i < 9; i++)
                {
                    float y = i / 8f;
                    float x = (a - 1) * 0.3f + (i % 2 == 0 ? -0.07f : 0.07f);
                    var desired = b.center + new Vector3(x * b.size.x, (y - 0.5f) * b.size.y * 0.7f, -b.extents.z);
                    float best = float.MaxValue; var point = desired;
                    foreach (var vertex in surface)
                    {
                        if (vertex.z > b.center.z) continue;
                        var delta = vertex - desired;
                        float score = delta.x * delta.x + delta.y * delta.y + delta.z * delta.z * 0.035f;
                        if (score < best) { best = score; point = vertex; }
                    }
                    line.SetPosition(i, point + Vector3.back * 0.025f);
                }
                lines[a] = line;
            }
            return lines;
        }

        static void BuildShards(GameObject root, MeshFilter source, Bounds b, Bounds pedestal, VfxCoreDestruction ctrl, Shared shared)
        {
            if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder(RootDir, "Meshes");
            var groups = new List<Vector3>[8]; for (int i = 0; i < 8; i++) groups[i] = new List<Vector3>();
            using (var data = MeshUtility.AcquireReadOnlyMeshData(source.sharedMesh))
            using (var vertices = new NativeArray<Vector3>(data[0].vertexCount, Allocator.Temp))
            {
                data[0].GetVertices(vertices);
                for (int sub = 0; sub < data[0].subMeshCount; sub++)
                {
                    using (var indices = new NativeArray<int>(data[0].GetSubMesh(sub).indexCount, Allocator.Temp))
                    {
                        data[0].GetIndices(indices, sub);
                        for (int i = 0; i < indices.Length; i += 3)
                        {
                            Vector3 a = source.transform.TransformPoint(vertices[indices[i]]);
                            Vector3 c = source.transform.TransformPoint(vertices[indices[i + 1]]);
                            Vector3 d = source.transform.TransformPoint(vertices[indices[i + 2]]);
                            Vector3 middle = (a + c + d) / 3f - b.center;
                            int group = (middle.x >= 0f ? 1 : 0) | (middle.y >= 0f ? 2 : 0) | (middle.z >= 0f ? 4 : 0);
                            groups[group].Add(a); groups[group].Add(c); groups[group].Add(d);
                        }
                    }
                }
            }
            var shards = new List<Transform>(); var starts = new List<Vector3>();
            var landings = new List<Vector3>(); var rotations = new List<Quaternion>();
            for (int i = 0; i < groups.Length; i++)
            {
                var points = groups[i]; if (points.Count == 0) continue;
                var bounds = new Bounds(points[0], Vector3.zero); foreach (var v in points) bounds.Encapsulate(v);
                var pivot = bounds.center; var indices = new int[points.Count];
                for (int j = 0; j < points.Count; j++) { points[j] -= pivot; indices[j] = j; }
                string path = MeshDir + "/CoreDestruction_Shard_" + i + ".asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
                else mesh.Clear();
                mesh.name = "Core shard " + i; mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(points); mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                EditorUtility.SetDirty(mesh);
                var go = Child(root, "Crystal Shard " + i); go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = shared.CubeWhite;
                var rotation = Quaternion.Euler(50f + i * 41f, i * 73f, 20f + i * 23f);
                float minY = float.MaxValue; foreach (var point in points) minY = Mathf.Min(minY, (rotation * point).y);
                float angle = i * Mathf.PI * 0.25f;
                float radius = Mathf.Max(pedestal.extents.x, pedestal.extents.z) + b.size.y * (0.6f + (i % 3) * 0.12f);
                shards.Add(go.transform); starts.Add(pivot); rotations.Add(rotation);
                landings.Add(new Vector3(b.center.x + Mathf.Cos(angle) * radius, pedestal.min.y - minY + 0.03f,
                    b.center.z + Mathf.Sin(angle) * radius));
            }
            ctrl.Shards = shards.ToArray(); ctrl.Starts = starts.ToArray(); ctrl.Landings = landings.ToArray(); ctrl.LandingRotations = rotations.ToArray();
        }
    }
}

using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static DesertTower.VFX.Editor.VfxBuildKit;

namespace DesertTower.VFX.Editor
{
    public static class CobraDestructionBuilder
    {
        public const string PrefabPath = PrefabDir + "/VFX_Cobra_Destruction.prefab";
        public const string ReferencePath = RequestedVfxBuilder.CobraPath;

        [MenuItem("DesertTower/VFX/Build Cobra Destruction")]
        public static void Build()
        {
            var reference = ObeliskDestructionBuilder.FindHealth(AssetDatabase.LoadAssetAtPath<GameObject>(ReferencePath)).gameObject;
            var bounds = BlueDestructionBursts.MeshBounds(reference);
            ImpactVfxBuilder.Build(new ImpactSpec {
                PrefabName = "VFX_Cobra_Destruction", BodyHeight = bounds.center.y, FlashSize = 1.6f,
                FlashColor = new Color(0.65f, 0.88f, 1f), CoreCount = 3, CoreSize = 0.18f,
                CoreA = Color.white, CoreB = new Color(0.08f, 0.38f, 1f), CoreC = new Color(0.03f, 0.1f, 0.35f),
                ShockwaveSize = 4f, ShockwaveColor = new Color(0.35f, 0.7f, 1f), DebrisCount = 22,
                DebrisSize = new Vector2(0.04f, 0.12f), DebrisSpeed = new Vector2(1f, 3f),
                DebrisLife = new Vector2(0.5f, 0.9f), DebrisA = Beige, DebrisB = BeigeDark, DebrisC = BeigeDark,
                EmberCount = 10, EmberCubes = true, EmberColor = new Color(0.08f, 0.38f, 1f), EmberLife = new Vector2(0.3f, 0.7f),
                DustCount = 16, DustColor = new Color(0.5f, 0.38f, 0.23f, 0.65f),
                DustSize = new Vector2(0.6f, 1.2f), DustLife = new Vector2(0.8f, 1.5f),
                DustSpeed = new Vector2(0.3f, 0.7f), LightIntensity = 2.5f, LightRange = 4f
            }, PrefabPath);
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>()) { var main = ps.main; main.startDelay = BlueDestructionBursts.BreakTime; }
                var ctrl = root.AddComponent<VfxCobraDestruction>();
                ctrl.BurstTime = BlueDestructionBursts.BreakTime;
                ctrl.Proxy = Child(root, "Intact Cobra");
                var pieces = new List<Transform>(); var starts = new List<Vector3>();
                var ends = new List<Vector3>(); var rotations = new List<Quaternion>();
                int sourceNumber = 0;
                foreach (var filter in reference.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (renderer == null || !renderer.enabled || filter.sharedMesh == null || filter.name != "Mesh") continue;
                    var proxy = Child(ctrl.Proxy, "Original Mesh " + sourceNumber);
                    proxy.transform.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
                    proxy.transform.localScale = filter.transform.lossyScale;
                    proxy.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    proxy.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
                    Fracture(root, filter, renderer, sourceNumber++, pieces, starts, ends, rotations);
                }
                ctrl.Pieces = pieces.ToArray(); ctrl.Starts = starts.ToArray(); ctrl.Landings = ends.ToArray(); ctrl.Rotations = rotations.ToArray();
                BlueDestructionBursts.Add(root, bounds);
                ctrl.Restart(); PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[VFX] Cobra destruction: " + pieces.Count + " textured source-mesh fragments.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        struct CutVertex { public Vector3 Position; public Vector2 UV; }

        static List<CutVertex> Clip(List<CutVertex> input, int axis, float plane, bool positive)
        {
            var output = new List<CutVertex>();
            if (input.Count == 0) return output;
            var previous = input[input.Count - 1];
            float previousDistance = (previous.Position[axis] - plane) * (positive ? 1f : -1f);
            foreach (var current in input)
            {
                float distance = (current.Position[axis] - plane) * (positive ? 1f : -1f);
                if ((distance >= 0f) != (previousDistance >= 0f))
                {
                    float t = previousDistance / (previousDistance - distance);
                    output.Add(new CutVertex { Position = Vector3.Lerp(previous.Position, current.Position, t),
                        UV = Vector2.Lerp(previous.UV, current.UV, t) });
                }
                if (distance >= 0f) output.Add(current);
                previous = current; previousDistance = distance;
            }
            return output;
        }

        public static void Fracture(GameObject root, MeshFilter source, MeshRenderer renderer, int number,
            List<Transform> pieces, List<Vector3> starts, List<Vector3> ends, List<Quaternion> rotations,
            string assetPrefix = "CobraDestruction", int heightBands = 2)
        {
            string folder = RootDir + "/Meshes";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(RootDir, "Meshes");
            var b = CoreDestructionBuilder.WorldBounds(source);
            using (var data = MeshUtility.AcquireReadOnlyMeshData(source.sharedMesh))
            using (var vertices = new NativeArray<Vector3>(data[0].vertexCount, Allocator.Temp))
            using (var uv = new NativeArray<Vector2>(data[0].vertexCount, Allocator.Temp))
            {
                data[0].GetVertices(vertices);
                if (data[0].HasVertexAttribute(VertexAttribute.TexCoord0)) data[0].GetUVs(0, uv);
                for (int sub = 0; sub < data[0].subMeshCount; sub++)
                using (var indices = new NativeArray<int>(data[0].GetSubMesh(sub).indexCount, Allocator.Temp))
                {
                    data[0].GetIndices(indices, sub);
                    int groupCount = heightBands * 2;
                    var groups = new List<Vector3>[groupCount]; var coords = new List<Vector2>[groupCount];
                    for (int g = 0; g < groupCount; g++) { groups[g] = new List<Vector3>(); coords[g] = new List<Vector2>(); }
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        for (int g = 0; g < groupCount; g++)
                        {
                            var polygon = new List<CutVertex>();
                            for (int j = 0; j < 3; j++) polygon.Add(new CutVertex {
                                Position = source.transform.TransformPoint(vertices[indices[i + j]]), UV = uv[indices[i + j]] });
                            polygon = Clip(polygon, 0, b.center.x, (g & 1) != 0);
                            int band = g / 2;
                            if (band > 0) polygon = Clip(polygon, 1, b.min.y + b.size.y * band / heightBands, true);
                            if (band + 1 < heightBands) polygon = Clip(polygon, 1, b.min.y + b.size.y * (band + 1) / heightBands, false);
                            for (int j = 1; j + 1 < polygon.Count; j++)
                                foreach (int k in new[] { 0, j, j + 1 }) { groups[g].Add(polygon[k].Position); coords[g].Add(polygon[k].UV); }
                        }
                    }
                    for (int g = 0; g < groupCount; g++)
                    {
                        var points = groups[g]; if (points.Count == 0) continue;
                        var box = new Bounds(points[0], Vector3.zero); foreach (var p in points) box.Encapsulate(p);
                        var pivot = box.center; var triangles = new int[points.Count];
                        for (int i = 0; i < points.Count; i++) { points[i] -= pivot; triangles[i] = i; }
                        string path = folder + $"/{assetPrefix}_{number}_{sub}_{g}.asset";
                        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                        if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); } else mesh.Clear();
                        mesh.name = $"{assetPrefix} fragment {number}.{sub}.{g}"; mesh.indexFormat = IndexFormat.UInt32;
                        mesh.SetVertices(points); mesh.SetUVs(0, coords[g]); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                        EditorUtility.SetDirty(mesh);
                        var piece = Child(root, mesh.name); piece.AddComponent<MeshFilter>().sharedMesh = mesh;
                        piece.AddComponent<MeshRenderer>().sharedMaterial = renderer.sharedMaterials[sub];
                        int index = pieces.Count; float angle = index * 2.4f;
                        var rotation = Quaternion.Euler(45f + index * 31f, index * 67f, 30f + index * 23f);
                        float minY = float.MaxValue; foreach (var p in points) minY = Mathf.Min(minY, (rotation * p).y);
                        pieces.Add(piece.transform); starts.Add(pivot); rotations.Add(rotation);
                        float radius = 2.4f + index % 3 * 0.3f;
                        ends.Add(new Vector3(Mathf.Cos(angle) * radius, -minY + 0.03f, Mathf.Sin(angle) * radius));
                    }
                }
            }
        }
    }
}

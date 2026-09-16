using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class TempleStairGapRepair
{
    const string Root = "Assets/TempleArt/ArchitectureV2";
    const string Output = "Docs/LevelArt/TempleArchitecture/V2/stair-gaps";
    const float Overlap = .02f;

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run with -batchmode -executeMethod TempleStairGapRepair.Run");
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene(Root + "/Level_TempleCourtyard.unity");
        if (!File.Exists(Output + "/before-entry.png")) Capture("before");
        var report = new List<string>();
        var prefab = PrefabUtility.LoadPrefabContents(Root + "/DesertTemple_Courtyard.prefab");
        try
        {
            Apply(prefab.transform, report);
            PrefabUtility.SaveAsPrefabAsset(prefab, Root + "/DesertTemple_Courtyard.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        var scene = EditorSceneManager.OpenScene(Root + "/Level_TempleCourtyard.unity");
        var temple = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Single(t => t.Find("Source Level walking collision"));
        Apply(temple, report);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Capture("after");
        File.WriteAllLines(Output + "/verification.txt", report);
        TempleRailCollisionVerification.Repair();
    }

    public static void Apply(Transform temple, List<string> report = null)
    {
        string folder = Root + "/StairSeams";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Root, "StairSeams");
        var filters = temple.GetComponentsInChildren<MeshFilter>(true);
        // The mirrored FBX retains its Blender-side route names. Match its support
        // meshes by position, not by the NE/NW labels used by the Unity treads.
        var originalDress = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Temple_Courtyard_v2.fbx")
            .GetComponentsInChildren<MeshFilter>(true).ToDictionary(f => f.name, f => f.sharedMesh);
        foreach (var f in filters.Where(f => f.name.StartsWith("V_ClosedRisers_") || f.name.StartsWith("S_Flight_")))
            f.sharedMesh = originalDress[f.name];
        int routes = 0, visuals = 0;
        foreach (var part in TempleCourtyardSurfaces.Reference().parts.Where(p => p.name.StartsWith("TREADS_FLUSH_")))
        {
            var rail = filters.Single(f => f.name == part.name.Replace("TREADS_FLUSH_", "RAILS_REBUILT_"));
            var left = rail.transform.Find("Left").GetComponent<MeshFilter>();
            var right = rail.transform.Find("Right").GetComponent<MeshFilter>();
            var across = Vector3.ProjectOnPlane(right.GetComponent<Renderer>().bounds.center - left.GetComponent<Renderer>().bounds.center, Vector3.up).normalized;
            float innerLeft = WorldVertices(left).Max(v => Vector3.Dot(v, across));
            float innerRight = WorldVertices(right).Min(v => Vector3.Dot(v, across));
            float targetMin = innerLeft - Overlap, targetMax = innerRight + Overlap;
            var tread = filters.Single(f => f.name == part.name && f.GetComponent<Renderer>() && f.GetComponent<Renderer>().enabled);
            var surfaces = new List<MeshFilter> { tread };
            if (part.normal.y < .999f)
                foreach (string prefix in new[] { "V_ClosedRisers_", "S_Flight_" })
                {
                    var support = filters.Where(f => f.name.StartsWith(prefix)).OrderBy(f => Vector3.Distance(f.GetComponent<Renderer>().bounds.center, tread.GetComponent<Renderer>().bounds.center)).First();
                    if (Vector3.Distance(support.GetComponent<Renderer>().bounds.center, tread.GetComponent<Renderer>().bounds.center) > 2)
                        throw new Exception("Cannot match stair support: " + part.name);
                    surfaces.Add(support);
                }
            foreach (var surface in surfaces)
            {
                var world = WorldVertices(surface);
                float min = world.Min(v => Vector3.Dot(v, across)), max = world.Max(v => Vector3.Dot(v, across));
                if (Mathf.Abs(min-innerLeft) > .5f || Mathf.Abs(innerRight-max) > .5f)
                    throw new Exception("Unexpected stair width: " + surface.name + " for " + part.name);
                var source = surface.sharedMesh;
                var mesh = new Mesh { indexFormat = source.indexFormat };
                var positions = world.Select(v => Stretch(v, across, min, max, targetMin, targetMax, Vector3.up)).Select(surface.transform.InverseTransformPoint).ToArray();
                mesh.vertices = positions;
                mesh.normals = source.normals; mesh.tangents = source.tangents;
                mesh.uv = source.uv; mesh.uv2 = source.uv2; mesh.colors = source.colors;
                mesh.subMeshCount = source.subMeshCount;
                for (int sub = 0; sub < source.subMeshCount; sub++) mesh.SetIndices(source.GetIndices(sub), source.GetTopology(sub), sub);
                mesh.RecalculateBounds();
                if (!mesh.triangles.SequenceEqual(source.triangles)) throw new Exception("Changed stair topology: " + surface.name);
                surface.sharedMesh = Save(folder + "/" + surface.name + ".asset", mesh);
                PrefabUtility.RecordPrefabInstancePropertyModifications(surface);
                visuals++;
                var fitted = WorldVertices(surface);
                float leftGap = fitted.Min(v => Vector3.Dot(v, across)) - innerLeft;
                float rightGap = innerRight - fitted.Max(v => Vector3.Dot(v, across));
                if (leftGap > -.015f || rightGap > -.015f) throw new Exception("Unsealed stair edge: " + surface.name);
                report?.Add(surface.name + " original side gaps=" + (min-innerLeft).ToString("F4") + "," + (innerRight-max).ToString("F4") + "m; sealed overlap=" + (-leftGap).ToString("F4") + "," + (-rightGap).ToString("F4") + "m");
            }
            var floor = temple.Find("Source Level walking collision/" + part.name).GetComponent<MeshCollider>();
            float oldMin = part.vertices.Min(v => Vector3.Dot(v, across)), oldMax = part.vertices.Max(v => Vector3.Dot(v, across));
            var floorMesh = Object.Instantiate(floor.sharedMesh);
            // Keep the original slope and triangulation; only extend into the rail.
            floorMesh.vertices = part.vertices.Select(v => Stretch(v, across, oldMin, oldMax, targetMin, targetMax, part.normal)).Select(floor.transform.InverseTransformPoint).ToArray();
            floorMesh.RecalculateBounds();
            var saved = Save(AssetDatabase.GetAssetPath(floor.sharedMesh), floorMesh);
            floor.sharedMesh = null; floor.sharedMesh = saved;
            routes++;
        }
        if (routes != 22 || visuals != 58) throw new Exception("Unexpected stair coverage: " + routes + " routes, " + visuals + " meshes");
        report?.Add("PASS: 22 routes, 58 visual meshes, both sides overlap the rail by at least 15mm.");
        Physics.SyncTransforms();
    }

    static Vector3 Stretch(Vector3 v, Vector3 across, float min, float max, float targetMin, float targetMax, Vector3 normal)
    {
        // Never shrink an already wider support. Horizontal treads retain their height.
        targetMin = Mathf.Min(min, targetMin); targetMax = Mathf.Max(max, targetMax);
        float p = Vector3.Dot(v, across);
        var delta = across * (Mathf.Lerp(targetMin, targetMax, (p-min)/(max-min)) - p);
        delta.y = -(normal.x*delta.x + normal.z*delta.z)/normal.y;
        return v + delta;
    }

    static Vector3[] WorldVertices(MeshFilter f)
    {
        using (var data = Mesh.AcquireReadOnlyMeshData(f.sharedMesh))
        using (var vertices = new Unity.Collections.NativeArray<Vector3>(data[0].vertexCount, Unity.Collections.Allocator.Temp))
        {
            data[0].GetVertices(vertices);
            return vertices.ToArray().Select(f.transform.TransformPoint).ToArray();
        }
    }

    static Mesh Save(string path, Mesh mesh)
    {
        mesh.name = Path.GetFileNameWithoutExtension(path) + " sealed edges";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (!existing) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing); return existing;
    }

    static void Capture(string stage)
    {
        var camera = new GameObject("Stair seam verification camera").AddComponent<Camera>();
        camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        camera.nearClipPlane = .05f; camera.farClipPlane = 1000; camera.fieldOfView = 65;
        foreach (var shot in new[] {
            ("entry", new Vector3(-73, 2.4f, -76), new Vector3(-69, 1.6f, -69)),
            ("stairs", new Vector3(-59, 5, -28), new Vector3(-52, 7, -18)) })
        {
            camera.transform.position = shot.Item2; camera.transform.LookAt(shot.Item3);
            var rt = new RenderTexture(1200, 800, 24); rt.Create();
            var old = RenderTexture.active; camera.targetTexture = rt; camera.aspect = 1.5f;
            camera.Render(); camera.Render(); RenderTexture.active = rt;
            var texture = new Texture2D(1200, 800, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1200, 800), 0, 0); texture.Apply();
            File.WriteAllBytes(Output + "/" + stage + "-" + shot.Item1 + ".png", texture.EncodeToPNG());
            camera.targetTexture = null; RenderTexture.active = old; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(texture);
        }
        Object.DestroyImmediate(camera.gameObject);
    }
}

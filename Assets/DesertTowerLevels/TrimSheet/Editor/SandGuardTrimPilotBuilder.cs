using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SandGuard.LevelArt.Editor
{
    /// <summary>Explicit, repeatable art pilot. Source model, gameplay scene and collision are preserved.</summary>
    public static class SandGuardTrimPilotBuilder
    {
        const string Root = "Assets/DesertTowerLevels/TrimSheet";
        const string Source = "Assets/2.Model/Prefabs/Level/DesertTemple_V6.prefab";
        const string Model = Root + "/Models/DesertTemple_TrimPilot_v1.fbx";
        const string Texture = Root + "/Textures/SandGuard_Sandstone_Trim_v1.png";
        const string Prefab = Root + "/Prefabs/DesertTemple_TrimPilot_v1.prefab";
        const string Scene = Root + "/Scenes/SandGuard_Trim_Comparison.unity";
        const string Docs = "Docs/LevelArt/TrimSheet";

        [MenuItem("SandGuard/Level Art/Build Trim Sheet Comparison")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var originalSetup = EditorSceneManager.GetSceneManagerSetup();
            foreach (var folder in new[] {"Materials", "Meshes", "Prefabs", "Scenes"}) Directory.CreateDirectory(Root + "/" + folder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(Texture);
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true; ti.mipmapEnabled = true;
            ti.wrapModeU = TextureWrapMode.Mirror; ti.wrapModeV = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Trilinear; ti.anisoLevel = 8;
            ti.maxTextureSize = 2048; ti.npotScale = TextureImporterNPOTScale.ToLarger;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
            var mi = (ModelImporter)AssetImporter.GetAtPath(Model);
            mi.importAnimation = false; mi.importCameras = false; mi.importLights = false;
            mi.isReadable = true; mi.addCollider = false; mi.SaveAndReimport();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader || !shader.isSupported) throw new Exception("URP Lit unavailable");
            var material = SaveMaterial("SandGuard_Sandstone_Trim_v1", shader, Color.white);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Texture));
            material.SetFloat("_Smoothness", .14f); material.SetFloat("_Metallic", 0);
            EditorUtility.SetDirty(material);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            try
            {
                var before = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Source), scene);
                var after = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Source), scene);
                var imported = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model), scene);
                before.name = "BEFORE - Original V6 (toggle to compare)";
                after.name = "AFTER - Trim pilot (15 meshes)";
                // The FBX materials can use legacy shaders; preserve their diffuse colors in URP.
                ConvertLegacyMaterials(before, shader); ConvertLegacyMaterials(after, shader);
                var targets = after.GetComponentsInChildren<MeshFilter>(true).ToDictionary(f => f.name);
                var sources = imported.GetComponentsInChildren<MeshFilter>(true);
                var pilotNames = sources.Select(f => f.name).ToArray();
                if (sources.Length != 15) throw new Exception("Expected exactly 15 pilot meshes");
                foreach (var f in sources) if (!targets.ContainsKey(f.name)) throw new Exception("Missing target " + f.name);
                // Derive FBX axis conversion from all fifteen spatially distinct bounds, not one guessed axis.
                var mapping = FindMapping(sources, targets);
                float maxBoundsError = 0;
                foreach (var f in sources)
                {
                    var target = targets[f.name];
                    var transform = target.transform.worldToLocalMatrix * mapping * f.transform.localToWorldMatrix;
                    var mesh = Object.Instantiate(f.sharedMesh); mesh.name = f.name;
                    var expected = target.sharedMesh.bounds;
                    mesh.vertices = mesh.vertices.Select(v => transform.MultiplyPoint3x4(v)).ToArray();
                    var normalTransform = transform.inverse.transpose;
                    mesh.normals = mesh.normals.Select(n => normalTransform.MultiplyVector(n).normalized).ToArray();
                    if (transform.determinant < 0)
                        for (int sub = 0; sub < mesh.subMeshCount; sub++)
                        {
                            var tris = mesh.GetTriangles(sub);
                            for (int i = 0; i < tris.Length; i += 3) { var t = tris[i]; tris[i] = tris[i + 1]; tris[i + 1] = t; }
                            mesh.SetTriangles(tris, sub);
                        }
                    mesh.RecalculateBounds(); mesh.RecalculateTangents();
                    float error = (mesh.bounds.center - expected.center).magnitude + (mesh.bounds.size - expected.size).magnitude;
                    maxBoundsError = Mathf.Max(maxBoundsError, error);
                    if (error > .005f) throw new Exception("Geometry moved: " + f.name + " error=" + error);
                    if (mesh.uv.Length != mesh.vertexCount || mesh.uv.Any(uv => !float.IsFinite(uv.x) || !float.IsFinite(uv.y) || uv.y < 0 || uv.y > 1))
                        throw new Exception("Invalid trim UV: " + f.name);
                    string path = Root + "/Meshes/" + f.name + ".asset";
                    var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (existing) { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; }
                    else AssetDatabase.CreateAsset(mesh, path);
                    mesh.UploadMeshData(false);
                    EditorUtility.SetDirty(mesh);
                    target.sharedMesh = mesh;
                    target.GetComponent<Renderer>().sharedMaterials = new[] {material};
                }
                Object.DestroyImmediate(imported);
                AssetDatabase.SaveAssets();
                var oldColliders = before.GetComponentsInChildren<MeshCollider>(true);
                var newColliders = after.GetComponentsInChildren<MeshCollider>(true);
                if (oldColliders.Length != 1 || newColliders.Length != 1 || oldColliders[0].sharedMesh != newColliders[0].sharedMesh)
                    throw new Exception("Collision asset changed");
                PrefabUtility.SaveAsPrefabAsset(after, Prefab);
                var camera = new GameObject("Comparison Camera").AddComponent<Camera>();
                camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.20f,.23f,.26f);
                camera.nearClipPlane = .1f; camera.farClipPlane = 500; camera.orthographic = true;
                camera.allowHDR = true; camera.tag = "MainCamera";
                var light = new GameObject("Soft key light").AddComponent<Light>();
                light.type = LightType.Directional; light.intensity = 1.3f; light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(48,-35,0);
                RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.55f,.58f,.62f);
                RenderSettings.skybox = null;
                var close = WorldBounds(targets["T1_CLEAN_MOLDINGS"]);
                close.Encapsulate(WorldBounds(targets["STAIR_FITTED_ENTRY_NE_T1"]));
                var pilotBounds = WorldBounds(targets[pilotNames[0]]);
                foreach (var name in pilotNames) pilotBounds.Encapsulate(WorldBounds(targets[name]));
                // Use the outward horizontal direction of the selected quadrant.
                var outward = new Vector3(close.center.x, 0, close.center.z).normalized;
                foreach (var shot in new[] {"Close", "Rail_Detail", "Overview"})
                {
                    var bounds = shot == "Overview" ? pilotBounds : close;
                    camera.transform.position = bounds.center + outward * 85 + Vector3.up * 65;
                    if (shot == "Rail_Detail")
                    {
                        bounds = WorldBounds(targets["RAILS_REBUILT_ENTRY_NE_T1"]);
                        camera.transform.position = bounds.center + Quaternion.Euler(0,35,0) * outward * 60 + Vector3.up * 24;
                    }
                    camera.transform.LookAt(bounds.center);
                    camera.orthographicSize = Mathf.Max(bounds.size.y, Mathf.Max(bounds.size.x, bounds.size.z)) * (shot == "Close" ? .65f : .60f);
                    before.SetActive(true); after.SetActive(false); Render(camera, Docs + "/Before_" + shot + ".png");
                    before.SetActive(false); after.SetActive(true); Render(camera, Docs + "/After_" + shot + ".png");
                }
                EditorSceneManager.SaveScene(scene, Scene);
                AssetDatabase.SaveAssets();
                var saved = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
                if (!saved || saved.GetComponentsInChildren<MeshFilter>(true).Count(f => AssetDatabase.GetAssetPath(f.sharedMesh).StartsWith(Root + "/Meshes/")) != 15)
                    throw new Exception("Saved prefab did not preserve fifteen trim overrides");
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Texture);
                File.WriteAllText(Docs + "/unity-validation.txt", $"PASS\nUnity: {Application.unityVersion}\nPilot meshes: 15 (including 3 paired rail meshes)\nImported texture: {tex.width} x {tex.height}\nMaximum local bounds error: {maxBoundsError:R} m\nCollision: original shared mesh, exactly one MeshCollider\nSaved prefab overrides verified: 15\nBefore/after renders: 6\nSource prefab and gameplay scene: unchanged\n");
                Debug.Log("TRIM_PILOT_PASS " + Prefab);
            }
            finally
            {
                if (!Application.isBatchMode && originalSetup.Length > 0 && originalSetup.All(s => !string.IsNullOrEmpty(s.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }

        [MenuItem("SandGuard/Level Art/Open Trim Sheet Comparison")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            Selection.activeGameObject = scene.GetRootGameObjects().FirstOrDefault(o => o.name.StartsWith("AFTER"));
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        static Material SaveMaterial(string name, Shader shader, Color color)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader; m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", .14f);
            EditorUtility.SetDirty(m); return m;
        }

        static void ConvertLegacyMaterials(GameObject root, Shader shader)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = r.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var m = materials[i];
                    if (!m) throw new Exception("Missing source material " + r.name);
                    if (m.shader.name.StartsWith("Universal Render Pipeline/")) continue;
                    var color = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                    materials[i] = SaveMaterial("Original_" + m.name, shader, color);
                }
                r.sharedMaterials = materials;
            }
        }

        static Bounds WorldBounds(MeshFilter f) => TransformBounds(f.sharedMesh.bounds, f.transform.localToWorldMatrix);
        static Bounds TransformBounds(Bounds b, Matrix4x4 m)
        {
            var result = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
            for (int i = 0; i < 8; i++) result.Encapsulate(m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return result;
        }

        static Matrix4x4 FindMapping(MeshFilter[] sources, Dictionary<string, MeshFilter> targets)
        {
            float bestError = float.PositiveInfinity; var best = Matrix4x4.identity;
            int[][] axes = {new[]{0,1,2},new[]{0,2,1},new[]{1,0,2},new[]{1,2,0},new[]{2,0,1},new[]{2,1,0}};
            foreach (var a in axes) for (int s = 0; s < 8; s++)
            {
                var m = Matrix4x4.zero; m[3,3] = 1;
                for (int i = 0; i < 3; i++) m[i,a[i]] = (s & (1 << i)) == 0 ? 1 : -1;
                float error = 0;
                foreach (var f in sources)
                {
                    var b = TransformBounds(f.sharedMesh.bounds, m * f.transform.localToWorldMatrix);
                    var t = WorldBounds(targets[f.name]);
                    error += (b.center-t.center).sqrMagnitude+(b.size-t.size).sqrMagnitude;
                }
                if (error < bestError) { bestError=error; best=m; }
            }
            if (bestError > .001f) throw new Exception("FBX coordinate mapping failed: " + bestError);
            return best;
        }

        static void Render(Camera camera, string path)
        {
            var rt = new RenderTexture(1600,1000,24); var pixels = new Texture2D(1600,1000,TextureFormat.RGB24,false);
            var previous = RenderTexture.active;
            try { camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt; pixels.ReadPixels(new Rect(0,0,1600,1000),0,0); pixels.Apply(); File.WriteAllBytes(path,pixels.EncodeToPNG()); }
            finally { camera.targetTexture=null; RenderTexture.active=previous; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(pixels); }
        }
    }
}

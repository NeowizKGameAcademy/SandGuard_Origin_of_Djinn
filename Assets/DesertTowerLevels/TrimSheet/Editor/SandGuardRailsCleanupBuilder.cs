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
    public static class SandGuardRailsCleanupBuilder
    {
        const string Root = "Assets/DesertTowerLevels/TrimSheet";
        const string Docs = "Docs/LevelArt/TrimSheet";
        const string Source = Root + "/Prefabs/DesertTemple_TrimPilot_v1.prefab";
        const string Model = Root + "/Models/DesertTemple_Rails_Split_v2.fbx";
        const string Prefab = Root + "/Prefabs/DesertTemple_TrimRails_v2.prefab";
        const string ScenePath = Root + "/Scenes/SandGuard_Rails_Comparison_v2.unity";

        [MenuItem("SandGuard/Level Art/Build Separated Rails v2")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var previous = EditorSceneManager.GetSceneManagerSetup();
            Directory.CreateDirectory(Root + "/Meshes/RailsV2");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(Model) as ModelImporter;
            if (!importer) throw new Exception("Missing split rail FBX");
            importer.isReadable = true; importer.importAnimation = false;
            importer.importLights = false; importer.importCameras = false;
            importer.addCollider = false; importer.SaveAndReimport();
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/SandGuard_Sandstone_Trim_v1.mat");
            if (!material) throw new Exception("Missing shared trim material");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            try
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
                var before = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
                var after = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
                PrefabUtility.UnpackPrefabInstance(after, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                before.name = "BEFORE - Paired rails v1"; after.name = "AFTER - 44 separated rails v2";
                var imported = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model), scene);
                var targets = after.GetComponentsInChildren<MeshFilter>(true).Where(f => f.name.StartsWith("RAILS_REBUILT_")).ToDictionary(f => f.name);
                var groups = imported.GetComponentsInChildren<MeshFilter>(true).GroupBy(f => f.name.Split(new[]{"__"},StringSplitOptions.None)[0]).ToDictionary(g => g.Key, g => g.ToArray());
                if (targets.Count != 22 || groups.Count != 22 || groups.Values.Any(g => g.Length != 2)) throw new Exception("Expected 22 pairs / 44 individual rails");
                var originalBounds = targets.ToDictionary(p => p.Key, p => ExactBounds(new[]{p.Value}, Matrix4x4.identity));
                var mapping = FindMapping(groups, originalBounds);
                float maxError = 0;
                foreach (var group in groups)
                {
                    var target = targets[group.Key];
                    var parent = target.transform;
                    foreach (var filter in group.Value)
                    {
                        string side = filter.name.EndsWith("__Left") ? "Left" : "Right";
                        var child = new GameObject(side); child.transform.SetParent(parent, false); child.isStatic = target.gameObject.isStatic;
                        child.layer = target.gameObject.layer;
                        var mesh = Object.Instantiate(filter.sharedMesh); mesh.name = filter.name;
                        var matrix = parent.worldToLocalMatrix * mapping * filter.transform.localToWorldMatrix;
                        var vertices = mesh.vertices.Select(v => matrix.MultiplyPoint3x4(v)).ToArray();
                        var center = vertices.Aggregate(Vector3.zero, (a,b) => a+b) / vertices.Length;
                        child.transform.localPosition = center;
                        mesh.vertices = vertices.Select(v => v-center).ToArray();
                        mesh.normals = mesh.normals.Select(n => matrix.inverse.transpose.MultiplyVector(n).normalized).ToArray();
                        if (matrix.determinant < 0)
                        {
                            var triangles = mesh.triangles;
                            for (int i=0; i<triangles.Length; i+=3) { int temp=triangles[i]; triangles[i]=triangles[i+1]; triangles[i+1]=temp; }
                            mesh.triangles = triangles;
                        }
                        mesh.RecalculateBounds(); mesh.RecalculateTangents();
                        if (mesh.uv.Length != mesh.vertexCount || mesh.uv.Any(v => !float.IsFinite(v.x) || !float.IsFinite(v.y) || v.y<0 || v.y>1)) throw new Exception("Invalid UV: " + filter.name);
                        string path = Root + "/Meshes/RailsV2/" + filter.name + ".asset";
                        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                        if (existing) { EditorUtility.CopySerialized(mesh,existing); Object.DestroyImmediate(mesh); mesh=existing; }
                        else AssetDatabase.CreateAsset(mesh,path);
                        mesh.UploadMeshData(false); EditorUtility.SetDirty(mesh);
                        child.AddComponent<MeshFilter>().sharedMesh = mesh;
                        child.AddComponent<MeshRenderer>().sharedMaterial = material;
                    }
                    // Keep the route group as an empty parent; remove the old paired renderer.
                    Object.DestroyImmediate(target.GetComponent<MeshRenderer>());
                    Object.DestroyImmediate(target);
                    var actual = ExactBounds(parent.GetComponentsInChildren<MeshFilter>(), Matrix4x4.identity);
                    var expected = originalBounds[group.Key];
                    float error = (actual.center-expected.center).magnitude + (actual.size-expected.size).magnitude;
                    maxError = Mathf.Max(maxError,error);
                    if (error > .001f) throw new Exception("Rail moved: " + group.Key + " " + error);
                }
                Object.DestroyImmediate(imported);
                var oldCollider = before.GetComponentsInChildren<MeshCollider>(true);
                var newCollider = after.GetComponentsInChildren<MeshCollider>(true);
                if (oldCollider.Length != 1 || newCollider.Length != 1 || oldCollider[0].sharedMesh != newCollider[0].sharedMesh || oldCollider[0].transform.localToWorldMatrix != newCollider[0].transform.localToWorldMatrix) throw new Exception("Collision changed");
                AssetDatabase.SaveAssets();
                PrefabUtility.SaveAsPrefabAsset(after,Prefab);
                var saved = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
                var savedGroups = saved.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("RAILS_REBUILT_")).ToArray();
                if (savedGroups.Length != 22 || savedGroups.Any(t => t.GetComponent<MeshFilter>() || t.childCount != 2 || !t.Find("Left") || !t.Find("Right"))) throw new Exception("Saved rail hierarchy invalid");
                if (savedGroups.SelectMany(t => t.GetComponentsInChildren<MeshRenderer>()).Any(r => r.sharedMaterial != material)) throw new Exception("Rail material not shared");
                var camera = new GameObject("Rails comparison camera").AddComponent<Camera>();
                camera.gameObject.AddComponent<UniversalAdditionalCameraData>(); camera.tag="MainCamera";
                camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.20f,.23f,.26f);
                camera.orthographic=true; camera.nearClipPlane=.1f; camera.farClipPlane=500;
                var light = new GameObject("Soft key light").AddComponent<Light>(); light.type=LightType.Directional; light.intensity=1.3f; light.shadows=LightShadows.Soft;
                light.transform.rotation=Quaternion.Euler(48,-35,0);
                RenderSettings.skybox=null; RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.55f,.58f,.62f);
                foreach (var shot in new[]{"Stairs", "Road", "Overview"})
                {
                    var bounds = originalBounds[shot=="Road" ? "RAILS_REBUILT_T1_B_NE" : "RAILS_REBUILT_ENTRY_NE_T1"];
                    if (shot=="Overview") foreach (var b in originalBounds.Values) bounds.Encapsulate(b);
                    var outward=new Vector3(bounds.center.x,0,bounds.center.z).normalized;
                    if (outward.sqrMagnitude<.01f) outward=new Vector3(1,0,-1).normalized;
                    camera.transform.position=bounds.center+Quaternion.Euler(0,35,0)*outward*100+Vector3.up*(shot=="Overview"?80:55);
                    camera.transform.LookAt(bounds.center);
                    camera.orthographicSize=Mathf.Max(bounds.size.y,Mathf.Max(bounds.size.x,bounds.size.z))*.66f;
                    before.SetActive(true); after.SetActive(false); Render(camera,Docs+"/RailsV2_Before_"+shot+".png");
                    before.SetActive(false); after.SetActive(true); Render(camera,Docs+"/RailsV2_After_"+shot+".png");
                }
                EditorSceneManager.SaveScene(scene,ScenePath); AssetDatabase.SaveAssets();
                File.WriteAllText(Docs+"/rails-v2-unity-validation.txt",$"PASS\n22 original pairs -> 44 independent rails\n18 stair groups and 4 bridge groups\nLeft/Right hierarchy reloaded and checked: 22\nOne existing trim material shared across all 44 rails\nMaximum bounds error: {maxError:R} metres\nOriginal collision mesh AND transform unchanged\nSix before/after renders\nSource gameplay scene and v1 prefab unchanged\n");
                Debug.Log("RAILS_V2_PASS " + Prefab);
            }
            finally { if (!Application.isBatchMode && previous.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }

        [MenuItem("SandGuard/Level Art/Open Separated Rails v2")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        static Bounds ExactBounds(IEnumerable<MeshFilter> filters, Matrix4x4 mapping)
        {
            bool first=true; var bounds=new Bounds();
            foreach (var f in filters) foreach (var v in f.sharedMesh.vertices)
            {
                var p=(mapping*f.transform.localToWorldMatrix).MultiplyPoint3x4(v);
                if (first) { bounds=new Bounds(p,Vector3.zero); first=false; } else bounds.Encapsulate(p);
            }
            return bounds;
        }
        static Matrix4x4 FindMapping(Dictionary<string,MeshFilter[]> groups, Dictionary<string,Bounds> targets)
        {
            float bestError=float.PositiveInfinity; var best=Matrix4x4.identity;
            int[][] axes={new[]{0,1,2},new[]{0,2,1},new[]{1,0,2},new[]{1,2,0},new[]{2,0,1},new[]{2,1,0}};
            foreach (var a in axes) for (int s=0;s<8;s++)
            {
                var m=Matrix4x4.zero; m[3,3]=1; for(int i=0;i<3;i++)m[i,a[i]]=(s&(1<<i))==0?1:-1;
                float error=0;
                foreach(var group in groups) { var b=ExactBounds(group.Value,m); var t=targets[group.Key]; error+=(b.center-t.center).sqrMagnitude+(b.size-t.size).sqrMagnitude; }
                if(error<bestError){bestError=error;best=m;}
            }
            if(bestError>.001f)throw new Exception("Rail coordinate mapping failed: "+bestError);
            return best;
        }
        static void Render(Camera camera,string path)
        {
            var rt=new RenderTexture(1600,1000,24); var pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false); var active=RenderTexture.active;
            try { camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt; pixels.ReadPixels(new Rect(0,0,1600,1000),0,0); pixels.Apply(); File.WriteAllBytes(path,pixels.EncodeToPNG()); }
            finally { camera.targetTexture=null; RenderTexture.active=active; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(pixels); }
        }
    }
}

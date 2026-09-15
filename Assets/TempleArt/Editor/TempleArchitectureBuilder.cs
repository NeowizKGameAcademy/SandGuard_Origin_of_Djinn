using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

public static class TempleArchitectureBuilder
{
    const string Root="Assets/TempleArt/Architecture";
    const string Output="Docs/LevelArt/TempleArchitecture";
    const string SourceScene="Assets/TempleArt/Scenes/Level_TempleArt.unity";
    [Serializable] public class Part {public string name;public Vector3 min,max;public Vector3[] vertices;public int[] triangles;public string meshPath;}
    [Serializable] public class Survey {public Part[] parts;}
    static Transform FindTemple()=>Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).First(t=>t.name=="DesertTemple_V7"||t.name=="DesertTemple_V6");
    const string ScenePath=Root+"/Level_TempleArchitecture.unity";
    static Camera overview,detail,top;
    static int ticks;
    static string[] protectedBefore,collidersBefore;
    static bool Protected(string n)=>n.EndsWith("_deck")||n.StartsWith("TREADS_FLUSH")||n.StartsWith("STAIR_FITTED")||n.StartsWith("RAILS_REBUILT");
    static string Matrix(Transform t)=>t.localToWorldMatrix.ToString("F5");
    static string[] ProtectedSnapshot(Transform temple)=>temple.GetComponentsInChildren<MeshFilter>(true).Where(f=>Protected(f.name)).Select(f=>f.name+"|"+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(f.sharedMesh))+"|"+f.sharedMesh.name+"|"+f.sharedMesh.vertexCount+"|"+Matrix(f.transform)).OrderBy(s=>s).ToArray();
    static string[] CollisionSnapshot(Transform temple)=>temple.GetComponentsInChildren<Collider>(true).Select(c=>c.name+"|"+c.GetType().Name+"|"+Matrix(c.transform)+"|"+EditorJsonUtility.ToJson(c)).OrderBy(s=>s).ToArray();
    static Material Stone(string name,Color color,float masonry=1){
        var m=new Material(Shader.Find("SandGuard/Architecture/Weathered Stone")){name=name};m.SetColor("_BaseColor",color);m.SetColor("_JointColor",color*.65f);m.SetFloat("_Masonry",masonry);m.SetFloat("_Variation",.09f);
        AssetDatabase.CreateAsset(m,Root+"/"+name+".mat");return m;
    }
    static Material Lit(string name,Color color,float metallic){
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.25f);m.SetFloat("_Metallic",metallic);AssetDatabase.CreateAsset(m,Root+"/"+name+".mat");return m;
    }
    public static void Build(){
        try{
            if(File.Exists(ScenePath))throw new Exception("Architecture preview exists; refusing to overwrite edits.");
            Directory.CreateDirectory(Root);Directory.CreateDirectory(Output);AssetDatabase.Refresh();
            if(!AssetDatabase.CopyAsset(SourceScene,ScenePath))throw new Exception("Cannot create architecture scene");
            var scene=EditorSceneManager.OpenScene(ScenePath);var temple=FindTemple();
            protectedBefore=ProtectedSnapshot(temple);collidersBefore=CollisionSnapshot(temple);
            var sandstone=Stone("Facade_Sandstone",new Color(.66f,.51f,.33f));
            var limestone=Stone("Facade_Limestone",new Color(.77f,.64f,.45f),.3f);
            var shadow=Stone("Facade_Shadow",new Color(.30f,.235f,.16f),.1f);
            var relief=Stone("Facade_Relief",new Color(.64f,.49f,.31f),0);
            var bronze=Lit("Facade_Bronze",new Color(.43f,.30f,.15f),.6f);
            var teal=Lit("Facade_Turquoise",new Color(.075f,.29f,.27f),.15f);
            var palette=new[]{sandstone,limestone,shadow,relief,bronze,teal}.ToDictionary(m=>m.name);
            int hidden=0;
            foreach(var r in temple.GetComponentsInChildren<MeshRenderer>()){
                if(!r.enabled)continue;
                // Replace only the small rough brick overlay; its filled structural mesh stays visible.
                if(r.name=="PYRAMID_EXPOSED_ROUGH_BRICKS"){r.enabled=false;hidden++;PrefabUtility.RecordPrefabInstancePropertyModifications(r);continue;}
                Material material=r.name.Contains("RECESSED")?shadow:r.name.Contains("MOLDING")||r.name.Contains("CAPITAL")||Protected(r.name)?limestone:sandstone;
                r.sharedMaterials=r.sharedMaterials.Select(_=>material).ToArray();PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            }
            var importer=(ModelImporter)AssetImporter.GetAtPath(Root+"/Temple_Architecture_v1.fbx");
            importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;importer.isReadable=true;importer.SaveAndReimport();
            var dress=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Temple_Architecture_v1.fbx"));
            dress.name="Concept stonework - below gameplay surfaces";
            // Export is in surveyed world coordinates, independent of the source FBX's authoring axes.
            dress.transform.SetParent(temple,true);dress.transform.position=Vector3.zero;dress.transform.rotation=Quaternion.identity;dress.transform.localScale=Vector3.one;
            foreach(var r in dress.GetComponentsInChildren<MeshRenderer>()){
                r.sharedMaterials=r.sharedMaterials.Select(m=>palette.TryGetValue(m.name,out var found)?found:sandstone).ToArray();PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                r.shadowCastingMode=ShadowCastingMode.On;
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(dress.transform);
            if(!protectedBefore.SequenceEqual(ProtectedSnapshot(temple)))throw new Exception("Gameplay platform/stair geometry changed");
            if(!collidersBefore.SequenceEqual(CollisionSnapshot(temple)))throw new Exception("Source collision changed");
            PrefabUtility.SaveAsPrefabAsset(temple.gameObject,Root+"/DesertTemple_ConceptFacade.prefab");
            foreach(var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))c.enabled=false;
            overview=Cam("Architecture - Overview",new Vector3(148,145,-185),new Vector3(0,22,0),105);
            detail=Cam("Architecture - Masonry",new Vector3(91,67,-104),new Vector3(28,20,-45),44);
            top=Cam("Architecture - Top",new Vector3(0,280,0),Vector3.zero,85);top.transform.rotation=Quaternion.Euler(90,0,0);
            overview.enabled=true;
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            File.WriteAllText(Output+"/validation.txt","Preserved "+protectedBefore.Length+" platform, stair and rail mesh references and exact world matrices. All "+collidersBefore.Length+" source collider serializations unchanged. Hidden rough-brick visual overlays: "+hidden+". Decoration has no colliders. Blender rejected candidate decoration overlapping protected deck/tread headroom. Full gameplay playthrough/performance profiling not performed.\n");
            ticks=0;EditorApplication.update+=Capture;
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static Camera Cam(string name,Vector3 position,Vector3 target,float size){
        var c=new GameObject(name).AddComponent<Camera>();c.transform.position=position;c.transform.LookAt(target);c.orthographic=true;c.orthographicSize=size;c.nearClipPlane=.1f;c.farClipPlane=2000;c.enabled=false;c.GetUniversalAdditionalCameraData().renderPostProcessing=false;return c;
    }
    public static void Refresh(){
        try{
            EditorSceneManager.OpenScene(SourceScene);
            var sourceProtected=ProtectedSnapshot(FindTemple());
            var scene=EditorSceneManager.OpenScene(ScenePath);var temple=FindTemple();
            var before=ProtectedSnapshot(temple);
            if(!sourceProtected.SequenceEqual(before))throw new Exception("Preview gameplay geometry differs from the source scene");
            var dress=temple.Find("Concept stonework - below gameplay surfaces");
            foreach(var r in temple.GetComponentsInChildren<MeshRenderer>().Where(r=>!r.transform.IsChildOf(dress))){
                if(r.name=="PYRAMID_FILLED_TERRACES"||r.name=="PYRAMID_EXPOSED_ROUGH_BRICKS"||r.name.EndsWith("_masonry")||r.name.Contains("STONE_COURSES")){
                    r.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                }
            }
            foreach(var name in new[]{"Facade_Sandstone","Facade_Limestone","Facade_Shadow","Facade_Relief"}){
                var mat=AssetDatabase.LoadAssetAtPath<Material>(Root+"/"+name+".mat");
                Color color=name=="Facade_Sandstone"?new Color(.72f,.64f,.51f):name=="Facade_Limestone"?new Color(.82f,.75f,.63f):name=="Facade_Shadow"?new Color(.35f,.30f,.23f):new Color(.67f,.57f,.42f);
                mat.SetColor("_BaseColor",color);mat.SetColor("_JointColor",color*.79f);mat.SetVector("_BlockSize",new Vector4(4.8f,2.2f,0,0));mat.SetFloat("_JointWidth",.018f);mat.SetFloat("_Variation",.11f);EditorUtility.SetDirty(mat);
            }
            foreach(var r in dress.GetComponentsInChildren<MeshRenderer>()){
                r.sharedMaterials=r.sharedMaterials.Select(m=>AssetDatabase.LoadAssetAtPath<Material>(Root+"/"+m.name+".mat")??AssetDatabase.LoadAssetAtPath<Material>(Root+"/Facade_Sandstone.mat")).ToArray();PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            }
            foreach(var f in dress.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.StartsWith("Podium_structural_shell"))){
                var col=f.GetComponent<MeshCollider>();if(!col)col=f.gameObject.AddComponent<MeshCollider>();col.sharedMesh=f.sharedMesh;
            }
            Physics.SyncTransforms();
            var surfaces=JsonUtility.FromJson<Survey>(File.ReadAllText(Output+"/structure-reference.json"));
            int samples=0,obstructions=0;var issues=new List<string>();
            var bodyColliders=dress.GetComponentsInChildren<MeshCollider>();
            foreach(var part in surfaces.parts){
                for(int j=0;j<part.triangles.Length;j+=3){
                    var a=part.vertices[part.triangles[j]];var b=part.vertices[part.triangles[j+1]];var c=part.vertices[part.triangles[j+2]];
                    if(Vector3.Cross(b-a,c-a).y<=.000001f)continue;
                    var p=(a+b+c)/3;samples++;
                    foreach(var col in bodyColliders)if(col.Raycast(new Ray(new Vector3(p.x,65,p.z),Vector3.down),out var hit,66)&&hit.point.y>p.y+.12f){obstructions++;if(issues.Count<25)issues.Add(part.name+": "+p+" -> "+hit.point);break;}
                }
            }
            File.WriteAllText(Output+"/route-clearance.txt","Triangle-centroid surface probes: "+samples+"; new masonry higher than preserved walking surface: "+obstructions+"\n"+string.Join("\n",issues));
            if(obstructions>0)throw new Exception("New masonry overlaps "+obstructions+" route probes");
            if(!before.SequenceEqual(ProtectedSnapshot(temple)))throw new Exception("Protected geometry changed");
            PrefabUtility.SaveAsPrefabAsset(temple.gameObject,Root+"/DesertTemple_ConceptFacade.prefab");
            overview=GameObject.Find("Architecture - Overview").GetComponent<Camera>();detail=GameObject.Find("Architecture - Masonry").GetComponent<Camera>();top=GameObject.Find("Architecture - Top").GetComponent<Camera>();
            overview.transform.position=new Vector3(130,125,-165);overview.transform.LookAt(new Vector3(0,16,0));overview.orthographicSize=88;
            if(!GameObject.Find("Architecture - Facade Detail"))Cam("Architecture - Facade Detail",new Vector3(34,10,-85),new Vector3(25,4,-71),8);
            foreach(var key in Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l=>l.type==LightType.Directional&&l.name!="Dunes - Sky Fill")){
                key.transform.rotation=Quaternion.Euler(40,-35,0);key.intensity=1.3f;key.color=new Color(1,.94f,.84f);
            }
            var fill=GameObject.Find("Dunes - Sky Fill");if(fill)fill.GetComponent<Light>().intensity=.22f;
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            File.AppendAllText(Output+"/validation.txt","Refinement: broad structural masonry added with its own MeshCollider, below protected deck/tread surfaces; original source collider retained. Protected mesh references/matrices rechecked unchanged.\n");
            ticks=0;EditorApplication.update+=Capture;
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Capture(){
        if(++ticks<35)return;EditorApplication.update-=Capture;
        var capturePipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        var previousDistance=capturePipeline.shadowDistance;var previousResolution=capturePipeline.mainLightShadowmapResolution;var previousBias=capturePipeline.shadowNormalBias;
        int exitCode=0;
        try{
            // The whole-temple orthographic camera is beyond the game's 50m shadow distance.
            // Borrow in-memory shadow values and restore before Exit; never reassign QualitySettings.
            capturePipeline.shadowDistance=600;capturePipeline.mainLightShadowmapResolution=4096;capturePipeline.shadowNormalBias=.15f;
            var storms=Object.FindObjectsByType<DesertTower.VFX.TempleSandstorm>(FindObjectsSortMode.None);
            var renderers=storms.SelectMany(s=>s.GetComponentsInChildren<Renderer>()).Distinct().ToArray();var enabled=renderers.Select(r=>r.enabled).ToArray();
            // Isolated architecture study captures; actual saved scene keeps its sealed storm.
            foreach(var r in renderers)r.enabled=false;
            Shot(overview,"architecture-overview",1920,1500);Shot(detail,"architecture-detail",1920,1200);Shot(top,"architecture-top",1800,1800);
            var facadeCamera=GameObject.Find("Architecture - Facade Detail");if(facadeCamera)Shot(facadeCamera.GetComponent<Camera>(),"architecture-facade",1500,1200);
            for(int i=0;i<renderers.Length;i++)renderers[i].enabled=enabled[i];
            Shot(overview,"architecture-in-level",1920,1500);
            if(ShaderUtil.ShaderHasError(Shader.Find("SandGuard/Architecture/Weathered Stone")))throw new Exception("Stone shader errors");
            Debug.Log("TEMPLE_ARCHITECTURE_COMPLETE");
        }catch(Exception e){Debug.LogException(e);exitCode=1;}
        finally{capturePipeline.shadowDistance=previousDistance;capturePipeline.mainLightShadowmapResolution=previousResolution;capturePipeline.shadowNormalBias=previousBias;}
        EditorApplication.Exit(exitCode);
    }
    static void Shot(Camera c,string name,int w,int h){
        var rt=new RenderTexture(w,h,24);rt.Create();c.targetTexture=rt;c.aspect=w/(float)h;c.Render();c.Render();var prev=RenderTexture.active;RenderTexture.active=rt;
        var tex=new Texture2D(w,h,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();File.WriteAllBytes(Output+"/"+name+".png",tex.EncodeToPNG());RenderTexture.active=prev;c.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);
    }
    public static void Inspect(){
        try{
            Directory.CreateDirectory(Output);EditorSceneManager.OpenScene(SourceScene);
            var temple=FindTemple();var parts=new List<Part>();
            foreach(var f in temple.GetComponentsInChildren<MeshFilter>()){
                var r=f.GetComponent<Renderer>();if(!r||!r.enabled||!f.sharedMesh||f.sharedMesh.vertexCount==0||!f.sharedMesh.HasVertexAttribute(VertexAttribute.Position))continue;
                // Read-only MeshData avoids altering importer settings on shared source meshes.
                using(var md=Mesh.AcquireReadOnlyMeshData(f.sharedMesh)){
                    var v=new Unity.Collections.NativeArray<Vector3>(md[0].vertexCount,Unity.Collections.Allocator.Temp);
                    md[0].GetVertices(v);var vertices=v.ToArray().Select(p=>f.transform.TransformPoint(p)).ToArray();v.Dispose();
                    var tris=new List<int>();for(int s=0;s<md[0].subMeshCount;s++){
                        var indices=new Unity.Collections.NativeArray<int>(md[0].GetSubMesh(s).indexCount,Unity.Collections.Allocator.Temp);md[0].GetIndices(indices,s);tris.AddRange(indices.ToArray());indices.Dispose();
                    }
                    parts.Add(new Part{name=f.name,min=r.bounds.min,max=r.bounds.max,vertices=vertices,triangles=tris.ToArray(),meshPath=AssetDatabase.GetAssetPath(f.sharedMesh)});
                }
            }
            File.WriteAllText(Output+"/survey.json",JsonUtility.ToJson(new Survey{parts=parts.ToArray()}));
            File.WriteAllLines(Output+"/parts.txt",parts.Select(p=>p.name+" | "+p.min+" | "+p.max));
            Debug.Log("ARCHITECTURE_SURVEY_COMPLETE "+parts.Count);EditorApplication.Exit(0);
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}

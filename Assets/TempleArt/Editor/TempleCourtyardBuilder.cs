using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Art iteration tool. The original Level, source FBX and source prefab are never saved here.
public static class TempleCourtyardBuilder
{
    const string Root="Assets/TempleArt/ArchitectureV2";
    const string Output="Docs/LevelArt/TempleArchitecture/V2";
    const string Source="Assets/TempleArt/Scenes/Level_TempleArt.unity";
    const string ScenePath=Root+"/Level_TempleCourtyard.unity";
    const string DressName="Blender courtyard architecture";
    static Camera overview,detail,top,facade;
    static int ticks;
    static string stage;
    static bool Protected(string n)=>n.EndsWith("_deck")||n.StartsWith("TREADS_FLUSH")||n.StartsWith("STAIR_FITTED")||n.StartsWith("RAILS_REBUILT");
    static bool Walking(string n)=>n.EndsWith("_deck")||n.StartsWith("TREADS_FLUSH")||n.StartsWith("RAILS_REBUILT");
    static Transform Temple()=>Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).First(t=>t.name=="DesertTemple_V7"||t.name=="DesertTemple_V6");
    static string[] Snapshot(Transform root)=>root.GetComponentsInChildren<MeshFilter>(true).Where(f=>Protected(f.name)).Select(f=>f.name+"|"+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(f.sharedMesh))+"|"+f.sharedMesh.name+"|"+f.sharedMesh.vertexCount+"|"+f.transform.localToWorldMatrix.ToString("F6")).OrderBy(s=>s).ToArray();
    static Camera Cam(string name,Vector3 position,Vector3 target,float size){
        var go=GameObject.Find(name);if(!go)go=new GameObject(name);var c=go.GetComponent<Camera>();if(!c)c=go.AddComponent<Camera>();
        c.transform.position=position;c.transform.LookAt(target);c.orthographic=true;c.orthographicSize=size;c.nearClipPlane=.1f;c.farClipPlane=2000;c.enabled=false;
        c.GetUniversalAdditionalCameraData().renderPostProcessing=false;return c;
    }
    static Material Mat(string name,Color color,float masonry=1){
        string path=Root+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        bool glaze=name.Contains("Turquoise")||name.Contains("Bronze");
        var shader=Shader.Find("SandGuard/Architecture/Courtyard Stone")??Shader.Find("SandGuard/Architecture/Weathered Stone");
        if(!m){m=new Material(shader){name=name};AssetDatabase.CreateAsset(m,path);}else m.shader=shader;
        m.SetColor("_BaseColor",color);
        m.SetFloat("_Smoothness",glaze?.17f:.10f);m.SetFloat("_Metallic",name.Contains("Bronze")?.45f:0);
        m.SetColor("_JointColor",color*.79f);m.SetFloat("_Masonry",glaze?0:masonry);m.SetFloat("_Variation",.22f);m.SetVector("_BlockSize",name.Contains("Paving")?new Vector4(3.8f,2.1f,0,0):new Vector4(2.8f,1.25f,0,0));m.SetFloat("_JointWidth",.023f);
        m.SetTexture("_WeatherMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/StoneWeather.png"));m.SetFloat("_BumpStrength",.65f);
        if(name=="Temple_Sand"){m.SetFloat("_IsSand",1);m.SetTexture("_SurfaceMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TempleArt/Terrain/Textures/Sand_BaseColor.png"));}
        EditorUtility.SetDirty(m);return m;
    }
    static Mesh CombineWalking(Transform temple,bool rails=false){
        var vertices=new List<Vector3>();var indices=new List<int>();
        foreach(var f in temple.GetComponentsInChildren<MeshFilter>(true).Where(f=>Walking(f.name)&&f.name.StartsWith("RAILS_REBUILT")==rails)){
            using(var data=Mesh.AcquireReadOnlyMeshData(f.sharedMesh)){
                int offset=vertices.Count;
                var v=new Unity.Collections.NativeArray<Vector3>(data[0].vertexCount,Unity.Collections.Allocator.Temp);data[0].GetVertices(v);
                vertices.AddRange(v.ToArray().Select(p=>f.transform.TransformPoint(p)));v.Dispose();
                for(int s=0;s<data[0].subMeshCount;s++){
                    var ids=new Unity.Collections.NativeArray<int>(data[0].GetSubMesh(s).indexCount,Unity.Collections.Allocator.Temp);data[0].GetIndices(ids,s);
                    indices.AddRange(ids.ToArray().Select(i=>i+offset));ids.Dispose();
                }
            }
        }
        var mesh=new Mesh{name="Original deck tread and rail collision",indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
        string path=Root+(rails?"/OriginalRailCollision.asset":"/OriginalWalkingCollision.asset");
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;EditorUtility.SetDirty(mesh);}else AssetDatabase.CreateAsset(mesh,path);
        return mesh;
    }
    [MenuItem("Tools/Temple Art/Refresh Courtyard Architecture")]
    public static void Build(){
        try{
            var args=Environment.GetCommandLineArgs();var si=Array.IndexOf(args,"-courtyardStage");stage=si>=0&&si+1<args.Length?args[si+1]:"latest";
            Directory.CreateDirectory(Root);Directory.CreateDirectory(Output);AssetDatabase.Refresh();
            var weatherImporter=AssetImporter.GetAtPath(Root+"/StoneWeather.png") as TextureImporter;
            if(weatherImporter){weatherImporter.sRGBTexture=false;weatherImporter.wrapMode=TextureWrapMode.Repeat;weatherImporter.mipmapEnabled=true;weatherImporter.anisoLevel=8;weatherImporter.maxTextureSize=2048;weatherImporter.SaveAndReimport();}
            EditorSceneManager.OpenScene(Source);var sourceSnapshot=Snapshot(Temple());
            if(!File.Exists(ScenePath)&&!AssetDatabase.CopyAsset(Source,ScenePath))throw new Exception("Cannot copy art scene");
            var scene=EditorSceneManager.OpenScene(ScenePath);var temple=Temple();
            if(!sourceSnapshot.SequenceEqual(Snapshot(temple)))throw new Exception("Source gameplay structure differs before build");
            if(PrefabUtility.IsPartOfPrefabInstance(temple))PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(temple),PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            var old=temple.Find(DressName);if(old)Object.DestroyImmediate(old.gameObject);
            var oldRoutes=temple.Find("Original walking collision");if(oldRoutes)Object.DestroyImmediate(oldRoutes.gameObject);
            var palette=new[]{Mat("Temple_Sandstone",new Color(.70f,.595f,.445f)),Mat("Temple_Limestone",new Color(.82f,.74f,.59f),.65f),Mat("Temple_Recess",new Color(.31f,.24f,.16f),.1f),Mat("Temple_Paving",new Color(.68f,.57f,.42f),.7f),Mat("Temple_Turquoise",new Color(.10f,.29f,.265f)),Mat("Temple_Bronze",new Color(.41f,.285f,.13f)),Mat("Temple_Sand",Color.white,0)}.ToDictionary(m=>m.name);
            int hidden=0;
            foreach(var r in temple.GetComponentsInChildren<MeshRenderer>(true)){
                r.enabled=Walking(r.name);if(!r.enabled)hidden++;
                if(r.enabled)r.sharedMaterials=r.sharedMaterials.Select(_=>palette["Temple_Limestone"]).ToArray();
            }
            // The old single collider includes the removed full pyramid. Keeping it would leave invisible walls.
            foreach(var col in temple.GetComponentsInChildren<Collider>(true))col.enabled=false;
            var routeMesh=CombineWalking(temple);var routeGo=new GameObject("Original walking collision");routeGo.transform.SetParent(temple,true);
            routeGo.transform.position=Vector3.zero;routeGo.transform.rotation=Quaternion.identity;routeGo.transform.localScale=Vector3.one;
            var routeCollider=routeGo.AddComponent<MeshCollider>();routeCollider.sharedMesh=routeMesh;
            var railGo=new GameObject("Preserved rail collision");railGo.transform.SetParent(routeGo.transform,false);railGo.AddComponent<MeshCollider>().sharedMesh=CombineWalking(temple,true);
            var importer=(ModelImporter)AssetImporter.GetAtPath(Root+"/Temple_Courtyard_v2.fbx");importer.importAnimation=false;importer.isReadable=true;importer.SaveAndReimport();
            var dress=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Temple_Courtyard_v2.fbx"));dress.name=DressName;
            dress.transform.SetParent(temple,true);dress.transform.position=Vector3.zero;dress.transform.rotation=Quaternion.identity;dress.transform.localScale=Vector3.one;
            foreach(var r in dress.GetComponentsInChildren<MeshRenderer>()){
                r.sharedMaterials=r.sharedMaterials.Select(m=>palette.TryGetValue(m.name,out var found)?found:palette["Temple_Sandstone"]).ToArray();r.shadowCastingMode=ShadowCastingMode.On;
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                if(r.name.StartsWith("S_"))r.gameObject.AddComponent<MeshCollider>().sharedMesh=r.GetComponent<MeshFilter>().sharedMesh;
            }
            Physics.SyncTransforms();
            Validate(temple,dress.transform,routeCollider,sourceSnapshot,hidden);
            PrefabUtility.SaveAsPrefabAsset(temple.gameObject,Root+"/DesertTemple_Courtyard.prefab");
            foreach(var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))c.enabled=false;
            overview=Cam("Courtyard - Overview",new Vector3(130,125,-165),new Vector3(0,16,0),88);
            detail=Cam("Courtyard - Interior",new Vector3(59,41,-61),new Vector3(15,15,-21),32);
            top=Cam("Courtyard - Top",new Vector3(0,280,0),Vector3.zero,83);top.transform.rotation=Quaternion.Euler(90,0,0);
            facade=Cam("Courtyard - Gate",new Vector3(24,17,-96),new Vector3(9,6,-71),11);
            overview.enabled=true;
            foreach(var key in Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l=>l.type==LightType.Directional&&l.name!="Dunes - Sky Fill")){
                key.transform.rotation=Quaternion.Euler(40,-35,0);key.intensity=1.3f;key.color=new Color(1,.94f,.84f);
            }
            var fill=GameObject.Find("Dunes - Sky Fill");if(fill)fill.GetComponent<Light>().intensity=.22f;
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();ticks=0;EditorApplication.update+=Capture;
        }catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);}
    }
    static void Validate(Transform temple,Transform dress,MeshCollider route,string[] original,int hidden){
        if(!original.SequenceEqual(Snapshot(temple)))throw new Exception("Protected mesh references or world transforms changed");
        var survey=JsonUtility.FromJson<TempleArchitectureBuilder.Survey>(File.ReadAllText("Docs/LevelArt/TempleArchitecture/structure-reference.json"));
        var solid=dress.GetComponentsInChildren<MeshCollider>();int probes=0,blocked=0,missing=0;var issues=new List<string>();
        foreach(var part in survey.parts)for(int j=0;j<part.triangles.Length;j+=3){
            var a=part.vertices[part.triangles[j]];var b=part.vertices[part.triangles[j+1]];var c=part.vertices[part.triangles[j+2]];
            if(Vector3.Cross(b-a,c-a).y<=1e-6f)continue;var p=(a+b+c)/3;probes++;
            if(!route.Raycast(new Ray(p+Vector3.up*.3f,Vector3.down),out var floor,.6f)||Mathf.Abs(floor.point.y-p.y)>.025f){missing++;if(issues.Count<30)issues.Add("ROUTE "+part.name+" "+p+" hit "+floor.point+" area "+Vector3.Cross(b-a,c-a).magnitude*.5f);}
            foreach(var col in solid)if(col.Raycast(new Ray(new Vector3(p.x,80,p.z),Vector3.down),out var hit,82)&&hit.point.y>p.y+.08f){
                blocked++;if(issues.Count<30)issues.Add(part.name+" "+p+" -> "+col.name+" "+hit.point);break;
            }
        }
        int closedCourts=0;
        foreach(float x in new[]{-45f,45f})foreach(float z in new[]{-43f,43f}){
            if(!Physics.Raycast(new Ray(new Vector3(x,70,z),Vector3.down),out var hit,72,~0,QueryTriggerInteraction.Ignore)||hit.point.y>2){closedCourts++;issues.Add("Courtyard not clear at "+new Vector2(x,z)+" hit "+hit.point);}
        }
        string report=$"Protected source mesh references / matrices unchanged: {original.Length}\nWalking triangle probes: {probes}\nMissing/misaligned route collision: {missing}\nNew architecture above route: {blocked}\nBlocked ground-level courtyard probes (of 4): {closedCourts}\nHidden original bulk/decorative renderers: {hidden}\nOld whole-pyramid collider disabled in this copy. Exact deck/tread geometry supplies the route collider, and original rails have a separate collider. New structural meshes have separate colliders.\n"+string.Join("\n",issues);
        File.WriteAllText(Output+"/validation-"+stage+".txt",report);
        if(blocked>0||missing>0||closedCourts>0)throw new Exception("Courtyard clearance failed: "+blocked+" blocked, "+missing+" missing, "+closedCourts+" closed courts");
    }
    static void Shot(Camera c,string name,int w,int h){
        var rt=new RenderTexture(w,h,24);rt.Create();var old=RenderTexture.active;var tex=new Texture2D(w,h,TextureFormat.RGB24,false);
        try{c.targetTexture=rt;c.aspect=w/(float)h;c.Render();c.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();File.WriteAllBytes(Output+"/"+stage+"-"+name+".png",tex.EncodeToPNG());}
        finally{RenderTexture.active=old;c.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);}
    }
    static void Capture(){
        if(++ticks<35)return;EditorApplication.update-=Capture;
        var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;var distance=pipeline.shadowDistance;var resolution=pipeline.mainLightShadowmapResolution;var bias=pipeline.shadowNormalBias;
        var storms=Object.FindObjectsByType<DesertTower.VFX.TempleSandstorm>(FindObjectsSortMode.None).SelectMany(s=>s.GetComponentsInChildren<Renderer>()).Distinct().ToArray();var enabled=storms.Select(r=>r.enabled).ToArray();int exitCode=0;
        try{
            if(ShaderUtil.ShaderHasError(Shader.Find("SandGuard/Architecture/Courtyard Stone")))throw new Exception("Courtyard stone shader compilation failed");
            pipeline.shadowDistance=600;pipeline.mainLightShadowmapResolution=4096;pipeline.shadowNormalBias=.15f;
            foreach(var r in storms)r.enabled=false;
            Shot(overview,"overview",1920,1500);Shot(top,"top",1800,1800);Shot(detail,"interior",1920,1400);Shot(facade,"gate",1600,1400);
            for(int i=0;i<storms.Length;i++)storms[i].enabled=enabled[i];Shot(overview,"in-level",1920,1500);
            Debug.Log("TEMPLE_COURTYARD_COMPLETE "+stage);
        }catch(Exception e){Debug.LogException(e);exitCode=1;}
        finally{for(int i=0;i<storms.Length;i++)storms[i].enabled=enabled[i];pipeline.shadowDistance=distance;pipeline.mainLightShadowmapResolution=resolution;pipeline.shadowNormalBias=bias;}
        if(Application.isBatchMode)EditorApplication.Exit(exitCode);
    }
}

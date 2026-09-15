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
        m.SetColor("_JointColor",color*.83f);m.SetFloat("_Masonry",glaze?0:masonry);m.SetFloat("_Variation",glaze?.06f:(name.Contains("Paving")?.22f:.18f));m.SetVector("_BlockSize",name.Contains("Paving")?new Vector4(3.8f,2.1f,0,0):new Vector4(2.8f,1.25f,0,0));m.SetFloat("_JointWidth",.018f);
        m.SetTexture("_WeatherMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/StoneWeather.png"));m.SetFloat("_BumpStrength",.28f);
        if(name=="Temple_Sand"){m.SetFloat("_IsSand",1);m.SetTexture("_SurfaceMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TempleArt/Terrain/Textures/Sand_BaseColor.png"));}
        EditorUtility.SetDirty(m);return m;
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
            var palette=new[]{Mat("Temple_Sandstone",new Color(.76f,.645f,.47f)),Mat("Temple_Limestone",new Color(.85f,.77f,.61f),.65f),Mat("Temple_Recess",new Color(.31f,.24f,.16f),.1f),Mat("Temple_Paving",new Color(.75f,.64f,.48f),1f),Mat("Temple_Turquoise",new Color(.10f,.29f,.265f)),Mat("Temple_Bronze",new Color(.41f,.285f,.13f)),Mat("Temple_Sand",Color.white,0)}.ToDictionary(m=>m.name);
            foreach(var kind in new[]{"wall-deco","wall-deco-anubis","god-la","la-dragon","osiris-dragon","obelisk-giant"}){
                string texturePath=Root+"/Relief_"+kind+".png";
                if(!File.Exists(texturePath))File.Copy("Docs/model-art/"+kind+".png",texturePath);
                AssetDatabase.ImportAsset(texturePath);
                var path=Root+"/Relief_"+kind+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
                material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));material.SetFloat("_Smoothness",.12f);
                palette["Relief_"+kind]=material;EditorUtility.SetDirty(material);
            }
            int hidden=0;
            foreach(var r in temple.GetComponentsInChildren<MeshRenderer>(true)){
                r.enabled=Walking(r.name);if(!r.enabled)hidden++;
                if(r.enabled)r.sharedMaterials=r.sharedMaterials.Select(_=>palette[r.name.EndsWith("_deck")?"Temple_Paving":"Temple_Limestone"]).ToArray();
            }
            // The old single collider includes the removed full pyramid. Keeping it would leave invisible walls.
            foreach(var col in temple.GetComponentsInChildren<Collider>(true))col.enabled=false;
            TempleCourtyardSurfaces.Build(temple);
            var importer=(ModelImporter)AssetImporter.GetAtPath(Root+"/Temple_Courtyard_v2.fbx");importer.importAnimation=false;importer.isReadable=true;importer.SaveAndReimport();
            var dress=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Temple_Courtyard_v2.fbx"));dress.name=DressName;
            dress.transform.SetParent(temple,true);dress.transform.position=Vector3.zero;dress.transform.rotation=Quaternion.identity;dress.transform.localScale=Vector3.one;
            foreach(var r in dress.GetComponentsInChildren<MeshRenderer>()){
                r.sharedMaterials=r.sharedMaterials.Select(m=>palette.TryGetValue(m.name,out var found)?found:palette["Temple_Sandstone"]).ToArray();r.shadowCastingMode=ShadowCastingMode.On;
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                if(r.name.StartsWith("C_")){r.enabled=false;r.gameObject.AddComponent<MeshCollider>().sharedMesh=r.GetComponent<MeshFilter>().sharedMesh;PrefabUtility.RecordPrefabInstancePropertyModifications(r);}
            }
            var visible=dress.GetComponentsInChildren<MeshRenderer>();
            if(visible.Count(r=>r.name.StartsWith("D_AuthoredRelief_"))>3)throw new Exception("Imported wall relief limit exceeded");
            if(visible.Count(r=>r.name.StartsWith("D_UniqueStatue_"))!=3)throw new Exception("Expected three unique sculptures");
            if(visible.Count(r=>r.name.StartsWith("V_ClosedRisers_"))!=18)throw new Exception("Expected 18 sealed stair flights");
            Physics.SyncTransforms();
            if(!sourceSnapshot.SequenceEqual(Snapshot(temple)))throw new Exception("Original platform or stair geometry changed");
            TempleCourtyardSurfaces.Validate(temple);
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
            TempleCourtyardAtmosphere.Apply();
            TemplePlayerViewSetup.Setup(); // Keep the saved scene playable after art regeneration.
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();ticks=0;EditorApplication.update+=Capture;
        }catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);}
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

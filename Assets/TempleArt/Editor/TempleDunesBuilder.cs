using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

public static class TempleDunesBuilder
{
    const string Root="Assets/TempleArt/Terrain";
    const string ScenePath=Root+"/Level_Dunes.unity";
    const string Output="Docs/LevelArt/TempleDunes";
    static Camera top, overview, ground;
    static int ticks;
    public static void LowerDunes(){ ReshapeDunes(); }
    public static void ReshapeDunes(){
        try{
            var data=AssetDatabase.LoadAssetAtPath<TerrainData>(Root+"/TempleDunes.asset");
            int n=data.heightmapResolution;var heights=new float[n,n];float max=0;
            for(int z=0;z<n;z++)for(int x=0;x<n;x++){
                float h=Height(x*1000f/(n-1)-500,z*1000f/(n-1)-500);
                if(float.IsNaN(h)||h<.79f||h>24)throw new Exception("Dune height outside expected range");
                heights[z,x]=h/data.size.y;max=Mathf.Max(max,h);
            }
            data.SetHeights(0,0,heights);EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();
            File.WriteAllText(Output+"/validation.txt","Buried-city terrain with localized curved high dunes and low intervening plains. Maximum elevation: "+max+" m; baseline 0.8 m. Central footprint preserved. Height range checked across all samples.\n");
            RefreshCapture();
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void RefreshCapture(){
        var scene=EditorSceneManager.OpenScene(ScenePath);
        top=GameObject.Find("Dunes - Top").GetComponent<Camera>();top.transform.rotation=Quaternion.Euler(90,0,0);
        overview=GameObject.Find("Dunes - Overview").GetComponent<Camera>();overview.orthographicSize=225;
        ground=GameObject.Find("Dunes - Ground").GetComponent<Camera>();
        Object.FindFirstObjectByType<Terrain>().heightmapPixelError=1;
        EditorSceneManager.SaveScene(scene);ticks=0;EditorApplication.update+=Capture;
    }
    public static void Build()
    {
        try {
            if(File.Exists(ScenePath))throw new Exception("Dune preview already exists; refusing overwrite.");
            Directory.CreateDirectory(Output);AssetDatabase.Refresh();
            var normal=(TextureImporter)AssetImporter.GetAtPath(Root+"/Textures/Sand_Normal.png");
            normal.textureType=TextureImporterType.NormalMap;normal.SaveAndReimport();
            var mask=(TextureImporter)AssetImporter.GetAtPath(Root+"/Textures/Sand_Mask.png");mask.sRGBTexture=false;mask.SaveAndReimport();
            AssetDatabase.CopyAsset("Assets/Resources/VFX/TempleSandstorm/Level_Sandstorm.unity",ScenePath);
            var scene=EditorSceneManager.OpenScene(ScenePath);
            foreach(var t in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Where(t=>t.name=="SandPlane"))t.gameObject.SetActive(false);
            foreach(var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))c.enabled=false;
            var data=new TerrainData {name="Temple Dunes",heightmapResolution=1025,size=new Vector3(1000,80,1000)};
            var heights=new float[1025,1025];
            float max=0;
            for(int z=0;z<1025;z++)for(int x=0;x<1025;x++){
                float wx=x*1000f/1024-500,wz=z*1000f/1024-500;
                float h=Height(wx,wz);if(float.IsNaN(h)||h<0||h>=80)throw new Exception("Invalid height");
                heights[z,x]=h/80;max=Mathf.Max(max,h);
            }
            data.SetHeights(0,0,heights);
            var layer=new TerrainLayer {name="Fine Sand",diffuseTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/Sand_BaseColor.png"),normalMapTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/Sand_Normal.png"),maskMapTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/Sand_Mask.png"),tileSize=new Vector2(6,6),normalScale=.35f,metallic=0,smoothness=.12f};
            AssetDatabase.CreateAsset(layer,Root+"/FineSand.terrainlayer");data.terrainLayers=new[]{layer};
            AssetDatabase.CreateAsset(data,Root+"/TempleDunes.asset");
            var go=Terrain.CreateTerrainGameObject(data);go.name="Desert Dunes - sculptable terrain";go.transform.position=new Vector3(-500,0,-500);
            var terrain=go.GetComponent<Terrain>();terrain.heightmapPixelError=3;terrain.basemapDistance=1500;
            var mat=new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));AssetDatabase.CreateAsset(mat,Root+"/SandTerrain.mat");terrain.materialTemplate=mat;
            top=Cam("Dunes - Top",new Vector3(0,650,0),Vector3.zero,330);
            overview=Cam("Dunes - Overview",new Vector3(440,380,-520),new Vector3(0,12,0),340);
            ground=Cam("Dunes - Ground",new Vector3(250,48,-290),new Vector3(10,26,0),0);
            overview.enabled=true;
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            File.WriteAllText(Output+"/validation.txt","Terrain: 1000 x 1000 m, 1025 height samples per axis. Maximum height: "+max+" m. Central temple footprint flat at 0.8 m; lower approach channels. TerrainCollider enabled. No prop placement. Original Level and storm height unchanged.\n");
            EditorApplication.update+=Capture;
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static float Height(float x,float z)
    {
        // Wind-aligned, curved crests with a broad windward slope and shorter lee slope.
        float u=x*.86f+z*.51f,v=-x*.51f+z*.86f;
        float dunes=0;
        for(int k=-9;k<=9;k++){
            float crest=k*73+20*Mathf.Sin(v*.014f+k*1.73f)+9*Mathf.Sin(v*.029f+k*.8f);
            float d=u-crest;
            float width=d<0?48:20;
            float along=.73f+.27f*Mathf.Sin(v*.018f+k*2.4f);
            dunes+= (20+7*Mathf.Sin(k*1.9f+1))*along*Mathf.Exp(-d*d/(width*width));
        }
        float edge=Mathf.Max(Mathf.Abs(x),Mathf.Abs(z));
        float mask=Mathf.SmoothStep(0,1,Mathf.InverseLerp(79,155,edge));
        float approach=1-.68f*Mathf.Exp(-x*x/180)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(105,220,Mathf.Abs(z))));
        float macro=3*Mathf.PerlinNoise(x*.008f+41,z*.008f+71);
        float outskirts=Mathf.Lerp(.045f,.125f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(110,280,edge)));
        float high=Ridge(x,z,-240,-155,17,85,.3f)
            +Ridge(x,z,230,155,19,100,-.2f)
            +Ridge(x,z,-115,290,14,80,.15f)
            +Ridge(x,z,170,-270,12,75,-.35f)
            +Ridge(x,z,-350,280,15,95,.25f)
            +Ridge(x,z,370,-110,16,90,-.1f);
        return .8f+mask*approach*((dunes+macro)*outskirts+high);
    }
    static float Ridge(float x,float z,float cx,float cz,float height,float length,float angle){
        float dx=x-cx,dz=z-cz;
        float u=dx*Mathf.Cos(angle)-dz*Mathf.Sin(angle);
        float v=dx*Mathf.Sin(angle)+dz*Mathf.Cos(angle);
        float d=u-22*(v/length)*(v/length);
        float width=d<0?48:24;
        return height*Mathf.Exp(-d*d/(width*width)-Mathf.Pow(v/length,4));
    }
    static Camera Cam(string name,Vector3 pos,Vector3 target,float size){
        var c=new GameObject(name).AddComponent<Camera>();c.transform.position=pos;c.transform.LookAt(target);c.orthographic=size>0;c.orthographicSize=size>0?size:1;c.fieldOfView=60;c.farClipPlane=2000;c.nearClipPlane=.3f;c.enabled=false;c.GetUniversalAdditionalCameraData().renderPostProcessing=false;return c;
    }
    static void Capture(){
        if(++ticks<40)return;EditorApplication.update-=Capture;
        try{
            Shot(top,"dunes-top",1800,1800);Shot(overview,"dunes-overview",1920,1280);Shot(ground,"dunes-ground",1920,1080);
            Debug.Log("TEMPLE_DUNES_COMPLETE");EditorApplication.Exit(0);
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Shot(Camera c,string name,int w,int h){
        var rt=new RenderTexture(w,h,24);rt.Create();c.targetTexture=rt;c.aspect=w/(float)h;c.Render();c.Render();var prev=RenderTexture.active;RenderTexture.active=rt;
        var tex=new Texture2D(w,h,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();File.WriteAllBytes(Output+"/"+name+".png",tex.EncodeToPNG());RenderTexture.active=prev;c.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);
    }
}

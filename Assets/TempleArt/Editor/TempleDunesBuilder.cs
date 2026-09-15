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
    const string Output="Docs/LevelArt/TempleDunes/SealedStorm";
    static Camera top, overview, ground;
    static int ticks;
    public static void SealStorm(){
        try{
            var scene=EditorSceneManager.OpenScene(ScenePath);
            var storm=Object.FindFirstObjectByType<DesertTower.VFX.TempleSandstorm>();
            storm.transform.position=new Vector3(0,-10,0);storm.transform.localScale=new Vector3(1,.55f,1);
            const int segments=256,rows=16;
            var vertices=new Vector3[(segments+1)*(rows+1)];var uv=new Vector2[vertices.Length];var triangles=new int[segments*rows*6];int ti=0;
            for(int a=0;a<=segments;a++)for(int b=0;b<=rows;b++){
                float u=a/(float)segments,v=b/(float)rows,angle=u*Mathf.PI*2;
                float radius=148+8*Mathf.Sin(v*Mathf.PI)+2*Mathf.Sin(angle*5)*Mathf.Sin(v*Mathf.PI);
                int index=a*(rows+1)+b;
                vertices[index]=new Vector3(Mathf.Cos(angle)*radius,Mathf.Lerp(-12,50,v),Mathf.Sin(angle)*radius);uv[index]=new Vector2(u,v);
                if(a<segments&&b<rows){int next=index+rows+1;triangles[ti++]=index;triangles[ti++]=next;triangles[ti++]=index+1;triangles[ti++]=index+1;triangles[ti++]=next;triangles[ti++]=next+1;}
            }
            var mesh=new Mesh{name="Continuous buried storm wall"};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,Root+"/StormOccluder.asset");
            var mat=new Material(Shader.Find("SandGuard/VFX/TempleSandstormOccluder"));mat.CopyPropertiesFromMaterial(storm.sandLayers[0].sharedMaterial);mat.renderQueue=2000;
            AssetDatabase.CreateAsset(mat,Root+"/StormOccluder.mat");
            var wall=new GameObject("Storm - opaque buried curtain");wall.AddComponent<MeshFilter>().sharedMesh=mesh;wall.AddComponent<MeshRenderer>().sharedMaterial=mat;
            var motion=wall.AddComponent<DesertTower.VFX.TempleSandstorm>();motion.sandLayers=new[]{wall.GetComponent<Renderer>()};
            Cam("Dunes - Sealed Interior",new Vector3(0,15,-45),new Vector3(0,20,150),0);
            Cam("Dunes - Summit View",new Vector3(0,55,0),new Vector3(0,52,170),0);
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();RefreshCapture();
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void LowerDunes(){ ReshapeDunes(); }
    public static void ReshapeDunes(){
        try{
            var data=AssetDatabase.LoadAssetAtPath<TerrainData>(Root+"/TempleDunes.asset");
            int n=data.heightmapResolution;var heights=new float[n,n];float max=0;
            for(int z=0;z<n;z++)for(int x=0;x<n;x++){
                float h=Height(x*1000f/(n-1)-500,z*1000f/(n-1)-500);
                if(float.IsNaN(h)||h<.79f||h>28)throw new Exception("Dune height outside expected range");
                heights[z,x]=h/data.size.y;max=Mathf.Max(max,h);
            }
            data.SetHeights(0,0,heights);EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();
            File.WriteAllText(Output+"/validation.txt","Buried-city terrain with localized curved high dunes and low intervening plains. Maximum elevation: "+max+" m; baseline 0.8 m. Central footprint preserved. Height range checked across all samples.\n");
            RefreshCapture();
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void RefreshCapture(){
        Directory.CreateDirectory(Output);
        var scene=EditorSceneManager.OpenScene(ScenePath);
        top=GameObject.Find("Dunes - Top").GetComponent<Camera>();top.transform.rotation=Quaternion.Euler(90,0,0);
        overview=GameObject.Find("Dunes - Overview").GetComponent<Camera>();overview.orthographicSize=225;
        ground=GameObject.Find("Dunes - Ground").GetComponent<Camera>();
        var interior=GameObject.Find("Dunes - Sealed Interior");if(interior){interior.transform.position=new Vector3(95,12,0);interior.transform.LookAt(new Vector3(160,18,0));}
        Object.FindFirstObjectByType<Terrain>().heightmapPixelError=1;
        foreach(var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l=>l.type==LightType.Directional && l.name!="Dunes - Sky Fill")){
            light.transform.rotation=Quaternion.Euler(28,110,0);light.intensity=1.25f;
            light.color=new Color(1,.91f,.77f);light.lightmapBakeType=LightmapBakeType.Realtime;
            light.shadows=LightShadows.Soft;light.shadowStrength=.85f;
        }
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.32f,.36f,.43f);
        RenderSettings.ambientEquatorColor=new Color(.22f,.20f,.17f);
        RenderSettings.ambientGroundColor=new Color(.12f,.10f,.08f);
        var fillObject=GameObject.Find("Dunes - Sky Fill")??new GameObject("Dunes - Sky Fill");
        var fill=fillObject.GetComponent<Light>();if(!fill)fill=fillObject.AddComponent<Light>();
        fill.type=LightType.Directional;fill.intensity=.55f;fill.color=new Color(.85f,.9f,1);
        fill.transform.rotation=Quaternion.Euler(65,-70,0);fill.shadows=LightShadows.None;
        fill.lightmapBakeType=LightmapBakeType.Realtime;
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
        // A continuous wind-deformed wave field: every trough joins the next crest.
        float u=x*.92f+z*.39f,v=-x*.39f+z*.92f;
        // Smooth spatial warping varies spacing and curvature without isolated bumps.
        float warp=110*(Mathf.PerlinNoise(u*.0038f+17,v*.0048f+43)-.5f)
            +38*(Mathf.PerlinNoise(u*.011f+71,v*.012f+29)-.5f);
        float bend=19*Mathf.Sin(v*.012f+u*.003f)+warp;
        float phase=(u+bend)/83f;
        float strength=Mathf.Clamp01((Mathf.PerlinNoise(u*.008f+31,v*.009f+53)-.25f)*2);
        float amplitude=7+12*strength;
        float crest=.64f+.15f*Mathf.PerlinNoise(u*.01f+13,v*.012f+81);
        float main=Wave(phase,crest)*amplitude;
        float secondary=Wave((u+warp*.55f+17*Mathf.Sin(v*.022f+u*.006f)+v*.12f)/37f+.35f)
            *(1+2.2f*Mathf.PerlinNoise(u*.015f+91,v*.012f+7));
        float swell=3*Mathf.PerlinNoise(u*.006f+5,v*.007f+61);
        float edge=Mathf.Max(Mathf.Abs(x),Mathf.Abs(z));
        float mask=Mathf.SmoothStep(0,1,Mathf.InverseLerp(79,150,edge));
        float approach=1-.65f*Mathf.Exp(-x*x/230)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(105,210,Mathf.Abs(z))));
        return .8f+mask*approach*(main+secondary+swell);
    }
    static float Wave(float phase,float crest=.72f)
    {
        float t=Mathf.Repeat(phase,1);
        // Long climbing face; short lee face. Rounded trough, distinct connected crest.
        float ramp=t<crest?t/crest:(1-t)/(1-crest);
        return ramp*ramp*(2-ramp);
    }
    static Camera Cam(string name,Vector3 pos,Vector3 target,float size){
        var c=new GameObject(name).AddComponent<Camera>();c.transform.position=pos;c.transform.LookAt(target);c.orthographic=size>0;c.orthographicSize=size>0?size:1;c.fieldOfView=60;c.farClipPlane=2000;c.nearClipPlane=.3f;c.enabled=false;c.GetUniversalAdditionalCameraData().renderPostProcessing=false;return c;
    }
    static void Capture(){
        if(++ticks<40)return;EditorApplication.update-=Capture;
        try{
            Shot(top,"dunes-top",1800,1800);Shot(overview,"dunes-overview",1920,1280);Shot(ground,"dunes-ground",1920,1080);
            foreach(var name in new[]{"Dunes - Sealed Interior","Dunes - Summit View"}){
                var obj=GameObject.Find(name);if(obj)Shot(obj.GetComponent<Camera>(),name=="Dunes - Sealed Interior"?"storm-interior":"storm-summit",1920,1080);
            }
            Debug.Log("TEMPLE_DUNES_COMPLETE");EditorApplication.Exit(0);
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Shot(Camera c,string name,int w,int h){
        var rt=new RenderTexture(w,h,24);rt.Create();c.targetTexture=rt;c.aspect=w/(float)h;c.Render();c.Render();var prev=RenderTexture.active;RenderTexture.active=rt;
        var tex=new Texture2D(w,h,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();File.WriteAllBytes(Output+"/"+name+".png",tex.EncodeToPNG());RenderTexture.active=prev;c.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);
    }
}

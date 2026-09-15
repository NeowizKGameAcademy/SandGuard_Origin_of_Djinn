using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class TempleHeroArtBuilder
{
    const string Root="Assets/TempleArt";
    const string Output="Docs/model-art/temple-heroes-v1";
    static readonly string[] Names={"SealStone","BrokenColumns","OfferingAltar","JackalGuardian"};
    static readonly Dictionary<string,Material> Palette=new Dictionary<string,Material>();
    static readonly List<string> Report=new List<string>();
    static GameObject[] Heroes;
    static GameObject[] Reused;
    static int ticks;
    static Camera galleryCamera;
    static GameObject galleryRoot;
    static GameObject[] galleryModels;

    [MenuItem("SandGuard/Art/Create Temple Hero Art Preview")]
    public static void Build()
    {
        try
        {
            if(!Application.isBatchMode && Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))throw new Exception("Save open scenes before creating the art preview.");
            foreach(var folder in new[]{"Materials","Prefabs","Scenes"})Directory.CreateDirectory(Root+"/"+folder);
            Directory.CreateDirectory(Output);AssetDatabase.Refresh();
            if(File.Exists(Root+"/Scenes/Level_TempleArt.unity"))throw new Exception("Art preview already exists; refusing to overwrite user edits.");
            CreateMaterials();
            Heroes=Names.Select(CreateHero).ToArray();
            Reused=new[]{
                CreateReuse("Desert/prefab/rock.prefab","RuinRock",1.3f),
                CreateReuse("Desert/prefab/small rock.prefab","RuinSmallRock",.45f),
                CreateReuse("Desert/prefab/tiled rock.prefab","BuriedPaving",.35f),
                CreateReuse("Forest/prefab/vase.prefab","OfferingJar",.8f)
            };
            BuildGallery();ticks=0;EditorApplication.update+=CaptureGallery;
        }
        catch(Exception e){Fail(e);}
    }

    static void CreateMaterials()
    {
        Make("Temple_Sandstone",new Color(.57f,.40f,.22f),0,.18f);
        Make("Temple_CutStone",new Color(.72f,.55f,.32f),0,.2f);
        Make("Temple_ShadowStone",new Color(.30f,.205f,.11f),0,.14f);
        Make("Temple_OldBronze",new Color(.35f,.235f,.095f),.65f,.32f);
        Make("Temple_WornGold",new Color(.62f,.41f,.15f),.65f,.40f);
        Make("Temple_Turquoise",new Color(.075f,.30f,.28f),.1f,.28f);
        Make("Temple_SealGlow",new Color(.12f,.72f,.65f),.1f,.35f,true);
    }
    static void Make(string name,Color color,float metal,float smooth,bool emission=false)
    {
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};
        m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);
        if(emission){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*1.1f);}
        AssetDatabase.CreateAsset(m,Root+"/Materials/"+name+".mat");Palette[name]=m;
    }
    static GameObject CreateHero(string name)
    {
        string path=Root+"/Models/"+name+".fbx";
        var importer=(ModelImporter)AssetImporter.GetAtPath(path);
        importer.globalScale=1;importer.useFileScale=true;importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;importer.isReadable=true;importer.SaveAndReimport();
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var root=new GameObject(name);var model=(GameObject)PrefabUtility.InstantiatePrefab(source);model.transform.SetParent(root.transform,false);
        foreach(var r in model.GetComponentsInChildren<Renderer>())
        {
            r.sharedMaterials=r.sharedMaterials.Select(m=>Palette.TryGetValue(m.name,out var match)?match:Palette["Temple_Sandstone"]).ToArray();
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
        }
        var bounds=BoundsOf(root);
        model.transform.localPosition-=new Vector3(0,bounds.min.y,0);
        PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
        int triangles=0;
        foreach(var f in model.GetComponentsInChildren<MeshFilter>())
        {
            var mesh=f.sharedMesh;
            if(mesh.vertices.Any(v=>float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)))throw new Exception(name+" has invalid vertices");
            if(mesh.uv.Length==0)throw new Exception(name+" has no UVs");
            triangles+=mesh.triangles.Length/3;
            var collider=f.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=mesh;collider.convex=false;
        }
        foreach(var t in root.GetComponentsInChildren<Transform>())t.gameObject.isStatic=true;
        var result=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/"+name+".prefab");
        Report.Add(name+": triangles="+triangles+", bounds="+bounds.size+", UVs=yes, static MeshCollider=yes");
        Object.DestroyImmediate(root);return result;
    }
    static GameObject CreateReuse(string relative,string name,float height)
    {
        var path="Assets/Resources/GeeZyyGames/"+relative;
        var root=new GameObject(name);var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));model.transform.SetParent(root.transform,false);
        foreach(var r in model.GetComponentsInChildren<Renderer>())
        {
            r.sharedMaterials=r.sharedMaterials.Select(m=>Palette["Temple_Sandstone"]).ToArray();
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
        }
        var bounds=BoundsOf(root);model.transform.localScale*=height/Mathf.Max(.01f,bounds.size.y);
        bounds=BoundsOf(root);model.transform.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
        PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
        foreach(var t in root.GetComponentsInChildren<Transform>())t.gameObject.isStatic=true;
        var result=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/"+name+".prefab");
        Report.Add(name+": reused from "+path+", palette override only; source untouched");Object.DestroyImmediate(root);return result;
    }
    static Bounds BoundsOf(GameObject g)
    {
        var rs=g.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);return b;
    }
    static Camera CameraAt(string name,Vector3 p,Vector3 target,float size)
    {
        var c=new GameObject(name).AddComponent<Camera>();c.transform.position=p;c.transform.LookAt(target);c.orthographic=true;c.orthographicSize=size;c.nearClipPlane=.1f;c.farClipPlane=1200;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.18f,.15f,.115f);
        var d=c.GetUniversalAdditionalCameraData();d.renderPostProcessing=false;d.renderShadows=true;return c;
    }
    static GameObject Spawn(GameObject prefab,Vector3 pos,float yaw,float scale,Transform parent=null)
    {
        var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab);g.transform.SetParent(parent,false);g.transform.position=pos;g.transform.rotation=Quaternion.Euler(0,yaw,0);g.transform.localScale=Vector3.one*scale;
        PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);return g;
    }
    static void BuildGallery()
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.49f,.39f);
        var light=new GameObject("Studio key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.color=new Color(1,.91f,.78f);light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(48,-32,0);
        var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.name="Studio floor";floor.transform.localScale=Vector3.one*8;
        floor.GetComponent<Renderer>().sharedMaterial=Palette["Temple_ShadowStone"];
        galleryRoot=new GameObject("Blender hero models");galleryModels=new GameObject[4];
        for(int i=0;i<4;i++)galleryModels[i]=Spawn(Heroes[i],new Vector3((i%2)*5.5f-2.75f,0,(i/2)*6f-3),0,1,galleryRoot.transform);
        for(int i=0;i<4;i++)Spawn(Reused[i],new Vector3(-4.5f+i*3,0,-7.5f),i*35,1);
        galleryCamera=CameraAt("Gallery camera",new Vector3(10,14,-19),new Vector3(0,1,-1),9.2f);
        EditorSceneManager.SaveScene(scene,Root+"/Scenes/TempleProps_Gallery.unity");AssetDatabase.SaveAssets();
    }
    static void CaptureGallery()
    {
        if(++ticks<20)return;EditorApplication.update-=CaptureGallery;
        try
        {
            Capture(galleryCamera,Output+"/unity-gallery.png",1800,1400);
            for(int i=0;i<4;i++)
            {
                var b=BoundsOf(galleryModels[i]);float size=Mathf.Max(b.size.y,b.size.x,b.size.z)*.73f;
                var c=CameraAt("Detail",b.center+new Vector3(5,3.3f,-7),b.center,size);
                Capture(c,Output+"/"+Names[i]+".png",1200,1200);Object.DestroyImmediate(c.gameObject);
            }
            BuildPlacement();ticks=0;EditorApplication.update+=CapturePlacement;
        }
        catch(Exception e){Fail(e);}
    }
    static Camera[] placedCameras;
    public static void AlignCityPlan(){
        try{
            var scene=EditorSceneManager.OpenScene(Root+"/Scenes/Level_TempleArt.unity");
            var root=GameObject.Find("Buried City - processional road and ruins").transform;
            var terrain=Object.FindFirstObjectByType<Terrain>();
            float Ground(float x,float z)=>terrain.SampleHeight(new Vector3(x,0,z))+terrain.transform.position.y;
            void Move(Transform t,float x,float z){float offset=t.position.y-Ground(t.position.x,t.position.z);t.position=new Vector3(x,Ground(x,z)+offset,z);PrefabUtility.RecordPrefabInstancePropertyModifications(t);}
            var children=root.Cast<Transform>().ToArray();
            foreach(var t in children.Where(t=>t.name.StartsWith("BuriedCityGate")))Move(t,0,-340);
            Vector2[] oldSites={new Vector2(-99,-52),new Vector2(-99,-14),new Vector2(-99,24),new Vector2(100,-52),new Vector2(100,-14)};
            Vector2[] newSites={new Vector2(-185,-75),new Vector2(-210,20),new Vector2(-175,120),new Vector2(190,-55),new Vector2(205,75)};
            for(int i=0;i<5;i++){
                var group=root.Find("Buried courtyard "+(i+1));
                float dy=Ground(newSites[i].x,newSites[i].y)-Ground(oldSites[i].x,oldSites[i].y);
                group.position+=new Vector3(newSites[i].x-oldSites[i].x,dy,newSites[i].y-oldSites[i].y);
            }
            foreach(var t in children.Where(t=>t.name.StartsWith("Ruin")||t.name.StartsWith("OfferingJar")||t.name.StartsWith("BrokenColumns"))){
                int nearest=-1;float distance=24;
                for(int i=0;i<5;i++){float d=Vector2.Distance(new Vector2(t.position.x,t.position.z),oldSites[i]);if(d<distance){nearest=i;distance=d;}}
                if(nearest>=0)Move(t,t.position.x+newSites[nearest].x-oldSites[nearest].x,t.position.z+newSites[nearest].y-oldSites[nearest].y);
            }
            var avenueColumns=children.Where(t=>t.name.StartsWith("BrokenColumns")&&Mathf.Abs(t.position.x)<40).OrderByDescending(t=>t.position.z).ToArray();
            for(int i=0;i<avenueColumns.Length;i++)Move(avenueColumns[i],i%2==0?-21:21,-120-(i/2)*78);
            foreach(var t in children.Where(t=>t.name=="Exposed processional paving")){
                float z=-82+(t.position.z+82)*6.2f;Move(t,t.position.x,z);
                t.localScale=new Vector3(t.localScale.x,.5f,4.8f);
            }
            var top=GameObject.Find("Art placement - top").GetComponent<Camera>();top.transform.position=new Vector3(0,700,-70);top.orthographicSize=380;
            var c=GameObject.Find("Art detail - seal and ruins").GetComponent<Camera>();c.transform.position=new Vector3(-230,55,-120);c.transform.LookAt(new Vector3(-185,5,-75));c.orthographicSize=38;
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Output+"/CityPlanAligned");
            Capture(top,Output+"/CityPlanAligned/top.png",1800,1800);
            Capture(c,Output+"/CityPlanAligned/housing.png",1600,1000);
            c.transform.position=new Vector3(50,48,-390);c.transform.LookAt(new Vector3(0,5,-325));c.orthographicSize=48;
            Capture(c,Output+"/CityPlanAligned/south-gate.png",1600,1000);
            File.WriteAllText(Output+"/CityPlanAligned/alignment.txt","Reference registered by temple footprint, north=+Z. South gate (0,-340); west housing (-185,-75),(-210,20),(-175,120); east housing (190,-55),(205,75). Avenue aligned X=0. Relative plan proportions used; illustrated scale bar not treated as a survey. Terrain-relative burial offsets preserved.\n");
            Debug.Log("CITY_PLAN_ALIGNED");EditorApplication.Exit(0);
        }catch(Exception e){Fail(e);}
    }
    public static void RefreshRuins(){
        var scene=EditorSceneManager.OpenScene(Root+"/Scenes/Level_TempleArt.unity");
        placedCameras=new[]{GameObject.Find("Art detail - south guardians").GetComponent<Camera>(),GameObject.Find("Art detail - seal and ruins").GetComponent<Camera>(),GameObject.Find("Art placement - top").GetComponent<Camera>()};
        placedCameras[0].transform.position=new Vector3(32,28,-116);placedCameras[0].transform.LookAt(new Vector3(0,3,-107));placedCameras[0].orthographicSize=25;
        placedCameras[1].transform.position=new Vector3(-112,26,-22);placedCameras[1].transform.LookAt(new Vector3(-98,3,-48));placedCameras[1].orthographicSize=22;
        EditorSceneManager.SaveScene(scene);ticks=0;EditorApplication.update+=CapturePlacement;
    }
    static GameObject artRoot;
    static void BuildPlacement()
    {
        var path=Root+"/Scenes/Level_TempleArt.unity";
        if(!AssetDatabase.CopyAsset("Assets/TempleArt/Terrain/Level_Dunes.unity",path))throw new Exception("Could not copy dune scene");
        var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
        artRoot=new GameObject("Buried City - processional road and ruins");
        var terrain=Object.FindFirstObjectByType<Terrain>();
        float Ground(float x,float z)=>terrain.SampleHeight(new Vector3(x,0,z))+terrain.transform.position.y;
        GameObject Buried(GameObject prefab,float x,float z,float yaw,float scale,float depth){
            return Spawn(prefab,new Vector3(x,Ground(x,z)-depth,z),yaw,scale,artRoot.transform);
        }
        var gate=CreateReuse("Desert/prefab/gate.prefab","BuriedCityGate",12);
        Buried(gate,0,-124,0,1,5);
        Buried(Heroes[3],-14,-101,0,2,2.4f);
        Buried(Heroes[3],14,-101,8,2,4.5f);
        Buried(Heroes[0],-28,-114,15,1.7f,2.1f);
        for(int i=0;i<6;i++)Buried(Heroes[1],i%2==0?-21:21,-91-i*6,12+i*41,1.5f,1.1f+i*.16f);
        var rng=new System.Random(731);
        // Discontinuous paving follows exposed sand; gaps retain the original buried road line.
        for(int row=0;row<14;row++)for(int col=0;col<4;col++){
            if(rng.NextDouble()<.35)continue;
            float x=-4.5f+col*3,z=-82-row*3;
            var stone=GameObject.CreatePrimitive(PrimitiveType.Cube);stone.name="Exposed processional paving";
            stone.transform.SetParent(artRoot.transform);stone.transform.position=new Vector3(x,Ground(x,z)-.12f,z);
            stone.transform.localScale=new Vector3(2.6f,.5f,2.6f);stone.transform.rotation=Quaternion.Euler(0,(float)rng.NextDouble()*8-4,0);
            stone.GetComponent<Renderer>().sharedMaterial=Palette["Temple_CutStone"];
        }
        // Low wall fragments describe courtyards; buried footing avoids complete houses perched on sand.
        for(int site=0;site<5;site++){
            float cx=site<3?-99:100,cz=-52+site%3*38;
            var group=new GameObject("Buried courtyard "+(site+1));group.transform.SetParent(artRoot.transform);
            float foundation=Ground(cx,cz)-2.2f;
            for(int side=0;side<3;side++)for(int segment=0;segment<5;segment++){
                if(rng.NextDouble()<.22)continue;
                float x=cx+(side==0?-8+segment*4:side==1?-8:8),z=cz+(side==0?-7:-7+segment*3.5f);
                float height=3.2f+(float)rng.NextDouble()*2.2f;
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Broken masonry wall";wall.transform.SetParent(group.transform);
                wall.transform.position=new Vector3(x,foundation+height*.5f,z);wall.transform.localScale=side==0?new Vector3(3.8f,height,1.2f):new Vector3(1.2f,height,3.3f);
                wall.GetComponent<Renderer>().sharedMaterial=Palette["Temple_Sandstone"];
            }
            Buried(Heroes[1],cx+5,cz+3,site*61,1.3f,1.6f);
            for(int j=0;j<6;j++)Buried(Reused[j%2],cx-10+(float)rng.NextDouble()*20,cz-8+(float)rng.NextDouble()*16,j*47,1.2f,.3f);
            Buried(Reused[3],cx-3,cz-6,12,1.4f,.35f);
        }
        Buried(Heroes[2],-36,-99,0,1.4f,.7f);
        foreach(var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))c.enabled=false;
        placedCameras=new[]{
            CameraAt("Art detail - south guardians",new Vector3(48,38,-147),new Vector3(0,5,-104),38),
            CameraAt("Art detail - seal and ruins",new Vector3(-138,33,-86),new Vector3(-99,5,-44),30),
            CameraAt("Art placement - top",new Vector3(0,260,0),Vector3.zero,145)
        };
        placedCameras[2].transform.rotation=Quaternion.Euler(90,0,0);
        placedCameras[1].enabled=false;placedCameras[2].enabled=false;
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Report.Add("Terrain-sampled placement: gate, two differently buried guardians, partial processional paving, columns, seal, altar and five courtyard ruins. Existing dune and opaque storm assets retained. Gameplay navigation not rebaked.");
    }
    static void CapturePlacement()
    {
        if(++ticks<25)return;EditorApplication.update-=CapturePlacement;
        try
        {
            var storm=Object.FindFirstObjectByType<DesertTower.VFX.TempleSandstorm>();
            if(storm){storm.animate=false;storm.ApplyTime(16);foreach(var ps in storm.GetComponentsInChildren<ParticleSystem>())ps.Simulate(12,false,true,true);}
            string[] labels={"placed-guardians","placed-seal","placed-top"};
            for(int i=0;i<3;i++)Capture(placedCameras[i],Output+"/"+labels[i]+".png",i==2?1600:1920,i==2?1600:1080);
            File.WriteAllLines(Output+"/unity-validation.txt",Report);
            Debug.Log("TEMPLE_HERO_ART_COMPLETE");if(Application.isBatchMode)EditorApplication.Exit(0);
        }
        catch(Exception e){Fail(e);}
    }
    static void Capture(Camera c,string path,int w,int h)
    {
        var rt=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32);rt.Create();c.targetTexture=rt;c.aspect=w/(float)h;c.Render();c.Render();
        var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(w,h,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());
        c.targetTexture=null;RenderTexture.active=old;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);
    }
    static void Fail(Exception e){EditorApplication.update-=CaptureGallery;EditorApplication.update-=CapturePlacement;Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);}
}

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
    static GameObject artRoot;
    static void BuildPlacement()
    {
        var path=Root+"/Scenes/Level_TempleArt.unity";
        if(!AssetDatabase.CopyAsset("Assets/Resources/VFX/TempleSandstorm/Level_Sandstorm.unity",path))throw new Exception("Could not copy storm scene");
        var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
        artRoot=new GameObject("Temple Art - heroes and reused debris");
        Spawn(Heroes[0],new Vector3(-94,.8f,-34),30,2,artRoot.transform);
        Spawn(Heroes[0],new Vector3(94,.8f,34),210,2,artRoot.transform);
        Spawn(Heroes[1],new Vector3(-88,.6f,48),-15,2.2f,artRoot.transform);
        Spawn(Heroes[1],new Vector3(88,.6f,-46),145,2,artRoot.transform);
        Spawn(Heroes[3],new Vector3(-33,.8f,-89),0,2,artRoot.transform);
        Spawn(Heroes[3],new Vector3(33,.8f,-89),0,2,artRoot.transform);
        var temple=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).First(t=>t.name.StartsWith("DesertTemple"));
        Physics.SyncTransforms();float altarY=.8f;
        var ray=new Ray(new Vector3(5.8f,160,4),Vector3.down);
        foreach(var col in temple.GetComponentsInChildren<Collider>())if(col.Raycast(ray,out var hit,220))altarY=Mathf.Max(altarY,hit.point.y);
        Spawn(Heroes[2],new Vector3(5.8f,altarY,4),0,1,artRoot.transform);
        var rng=new System.Random(47);
        for(int i=0;i<18;i++)
        {
            // Debris stays outside the 160m-square temple and clear of the four corner approaches.
            float x=(i%2==0?-1:1)*(88+(float)rng.NextDouble()*11),z=-49+(float)rng.NextDouble()*98;
            Spawn(Reused[i%2],new Vector3(x,.55f,z),(float)rng.NextDouble()*360,.7f+(float)rng.NextDouble(),artRoot.transform);
        }
        for(int i=0;i<5;i++)Spawn(Reused[2],new Vector3(-4+i*2,.67f,-94),0,1,artRoot.transform);
        Spawn(Reused[3],new Vector3(-92,.8f,-29),20,1.4f,artRoot.transform);
        Spawn(Reused[3],new Vector3(-90,.8f,-30),-20,1.1f,artRoot.transform);
        foreach(var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))c.enabled=false;
        placedCameras=new[]{
            CameraAt("Art detail - south guardians",new Vector3(-46,13,-110),new Vector3(-24,3,-87),17),
            CameraAt("Art detail - seal and ruins",new Vector3(-115,13,-55),new Vector3(-93,4,-30),12),
            CameraAt("Art placement - top",new Vector3(0,260,0),Vector3.zero,115)
        };
        placedCameras[2].transform.rotation=Quaternion.Euler(90,0,0);
        placedCameras[1].enabled=false;placedCameras[2].enabled=false;
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Report.Add("Placement: 2 seal stones, 2 column clusters, 2 jackal guardians, 1 offering altar; 25 reused debris props. Temple geometry and storm dimensions unchanged.");
        Report.Add("Altar surface Y="+altarY+"; only placement raycast checked, no gameplay path tests.");
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

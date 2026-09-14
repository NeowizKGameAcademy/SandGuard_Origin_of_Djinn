using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using DesertTower.VFX;
using Object = UnityEngine.Object;

public static class TempleSandstormBuilder
{
    const string Root = "Assets/Resources/VFX/TempleSandstorm";
    const string ScenePath = Root + "/Level_Sandstorm.unity";
    const string PrefabPath = Root + "/VFX_TempleSandstorm.prefab";
    const string Output = "Docs/LevelArt/TempleSandstorm";
    static TempleSandstorm storm;
    static Camera[] cameras;
    static int ticks;

    [MenuItem("DesertTower/VFX/Create Temple Sandstorm Preview")]
    public static void Build()
    {
        try
        {
            if(!Application.isBatchMode && Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i=>UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save your open scenes before creating the preview.");
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
                throw new InvalidOperationException("Preview already exists; refusing to overwrite an edited scene.");
            if (!AssetDatabase.CopyAsset("Assets/1.Scene/Level.unity",ScenePath)) throw new IOException("Could not copy Level scene");
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            var temple=all.First(t=>t.name.StartsWith("DesertTemple"));
            var rs=temple.GetComponentsInChildren<Renderer>().Where(r=>r.enabled && !(r is ParticleSystemRenderer)).ToArray();
            var bounds=rs[0].bounds; foreach(var r in rs.Skip(1)) bounds.Encapsulate(r.bounds);
            // The copied sky's low cartoon clouds intersect the environment storm and capture camera.
            // Disable only those cloud objects in this dedicated preview scene.
            foreach(var t in all.Where(t=>t.name=="Clouds")) t.gameObject.SetActive(false);
            PrepareBackdrop(all);
            foreach(var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled=false;

            var noise=CreateNoise();
            var shader=Shader.Find("SandGuard/VFX/TempleSandstorm");
            if(!shader || !shader.isSupported || ShaderUtil.ShaderHasError(shader)) throw new Exception("Storm shader failed to compile");
            var root=new GameObject("VFX_TempleSandstorm");
            storm=root.AddComponent<TempleSandstorm>();
            var layers=new Renderer[3];
            for(int i=0;i<3;i++)
            {
                var mesh=CreateShell(130+i*5,190+i*7,78+i*7,i*.8f);
                AssetDatabase.CreateAsset(mesh,Root+"/StormShell_"+i+".asset");
                var mat=new Material(shader){name="M_StormBody_"+i};
                mat.SetTexture("_NoiseTex",noise);
                mat.SetFloat("_Opacity",i==0?.88f:.74f);
                mat.SetFloat("_Phase",i*.37f);
                mat.SetFloat("_FlowSpeed",1-i*.14f);
                mat.SetColor("_DarkColor",new Color(.30f,.18f,.085f));
                mat.SetColor("_LightColor",new Color(.90f,.71f,.43f));
                mat.renderQueue=3000+i;
                AssetDatabase.CreateAsset(mat,Root+"/M_StormBody_"+i+".mat");
                var go=new GameObject("Sand wall layer "+(i+1)); go.transform.SetParent(root.transform,false);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=mat;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                layers[i]=renderer;
            }
            storm.sandLayers=layers;
            CreateDust(root);
            CreateSparks(root);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            Object.DestroyImmediate(root);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position=new Vector3(bounds.center.x,.85f,bounds.center.z);
            storm=instance.GetComponent<TempleSandstorm>();
            cameras=new[] {
                CameraAt("Preview - Overview",new Vector3(330,320,-430),new Vector3(0,24,0),false,0),
                CameraAt("Preview - Vertical Top",new Vector3(0,400,0),Vector3.zero,true,235),
                CameraAt("Preview - Inside Temple",new Vector3(18,61,-35),new Vector3(0,46,145),false,0)
            };
            cameras[0].enabled=true;
            cameras[0].fieldOfView=34;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            ticks=0;
            EditorApplication.update+=CaptureTick;
        }
        catch(Exception ex){ Debug.LogException(ex); if(Application.isBatchMode)EditorApplication.Exit(1); }
    }

    static Texture2D CreateNoise()
    {
        const int n=256;
        var texture=new Texture2D(n,n,TextureFormat.RGBA32,false,true){name="T_StormCloudNoise",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear};
        var colors=new Color[n*n];
        for(int y=0;y<n;y++)for(int x=0;x<n;x++)
        {
            float u=x/(float)n,v=y/(float)n,sum=0,weight=0;
            for(int o=0;o<4;o++)
            {
                float f=4*(1<<o),w=1f/(1<<o);
                float a=Mathf.PerlinNoise(u*f+13,v*f+7),b=Mathf.PerlinNoise((u-1)*f+13,v*f+7);
                float c=Mathf.PerlinNoise(u*f+13,(v-1)*f+7),d=Mathf.PerlinNoise((u-1)*f+13,(v-1)*f+7);
                sum+=Mathf.Lerp(Mathf.Lerp(a,b,u),Mathf.Lerp(c,d,u),v)*w;weight+=w;
            }
            float val=Mathf.Clamp01((sum/weight-.5f)*1.8f+.5f); colors[y*n+x]=new Color(val,val,val,1);
        }
        texture.SetPixels(colors);texture.Apply();
        AssetDatabase.CreateAsset(texture,Root+"/T_StormCloudNoise.asset");return texture;
    }

    static Mesh CreateShell(float inner,float outer,float height,float phase)
    {
        const int rings=192,steps=24;
        var v=new Vector3[(rings+1)*(steps+1)];var uv=new Vector2[v.Length];var triangles=new int[rings*steps*6];int k=0;
        for(int a=0;a<=rings;a++)for(int b=0;b<=steps;b++)
        {
            float u=a/(float)rings,s=b/(float)steps,theta=u*Mathf.PI*2;
            float skew=theta+.14f*Mathf.Sin(s*Mathf.PI);
            float radius=Mathf.Lerp(inner,outer,s)+Mathf.Sin(s*Mathf.PI)*(3*Mathf.Sin(theta*5+phase)+2*Mathf.Sin(theta*9));
            float h=Mathf.Pow(Mathf.Max(0,Mathf.Sin(s*Mathf.PI)),.7f)*height*(1+.07f*Mathf.Sin(theta*4+phase)+.035f*Mathf.Sin(theta*11));
            int index=a*(steps+1)+b;v[index]=new Vector3(Mathf.Cos(skew)*radius,h,Mathf.Sin(skew)*radius);uv[index]=new Vector2(u,s);
            if(a<rings&&b<steps){int q=index;triangles[k++]=q;triangles[k++]=q+steps+1;triangles[k++]=q+1;triangles[k++]=q+1;triangles[k++]=q+steps+1;triangles[k++]=q+steps+2;}
        }
        if(v.Any(p=>float.IsNaN(p.y)||float.IsInfinity(p.y)))throw new Exception("Non-finite storm vertex");
        var mesh=new Mesh{name="Storm shell",vertices=v,uv=uv,triangles=triangles};mesh.RecalculateNormals();mesh.RecalculateBounds();
        var bounds=mesh.bounds;bounds.Expand(12);mesh.bounds=bounds;return mesh;
    }

    static void CreateDust(GameObject parent)
    {
        var original=AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/VFX/Materials/M_VFX_Smoke.mat");
        var mat=new Material(original){name="M_TempleStormDust"};
        AssetDatabase.CreateAsset(mat,Root+"/M_TempleStormDust.mat");
        var go=new GameObject("Ground sweeping dust");go.transform.SetParent(parent.transform,false);
        var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var m=ps.main;m.loop=true;m.duration=12;m.startLifetime=9;m.startSpeed=0;m.startSize=new ParticleSystem.MinMaxCurve(12,23);m.startColor=new Color(.70f,.51f,.27f,.25f);m.maxParticles=320;m.simulationSpace=ParticleSystemSimulationSpace.Local;m.prewarm=true;
        var e=ps.emission;e.rateOverTime=24;
        var sh=ps.shape;sh.shapeType=ParticleSystemShapeType.Circle;sh.radius=155;sh.radiusThickness=.13f;sh.rotation=new Vector3(90,0,0);
        var vel=ps.velocityOverLifetime;vel.enabled=true;vel.space=ParticleSystemSimulationSpace.Local;vel.orbitalY=.12f;vel.y=1.2f;
        var noise=ps.noise;noise.enabled=true;noise.strength=2;noise.frequency=.06f;noise.scrollSpeed=.1f;
        var col=ps.colorOverLifetime;col.enabled=true;var g=new Gradient();g.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.8f,.15f),new GradientAlphaKey(.6f,.7f),new GradientAlphaKey(0,1)});col.color=g;
        var tsa=ps.textureSheetAnimation;tsa.enabled=true;tsa.numTilesX=2;tsa.numTilesY=2;tsa.frameOverTime=new ParticleSystem.MinMaxCurve(0f,.999f);
        var r=ps.GetComponent<ParticleSystemRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;
        ps.Play();
    }

    static void PrepareBackdrop(Transform[] all)
    {
        foreach(var t in all)
        {
            var r=t.GetComponent<Renderer>();
            if(!r)continue;
            if(t.name=="SkyDome" || t.name=="Stars" || t.name=="Sun 1" || t.name=="Moon 1" || r.sharedMaterials.Any(m=>m && AssetDatabase.GetAssetPath(m).Contains("/6.Materials/Range/")))
            {
                r.enabled=false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            }
        }
    }

    public static void RefreshPreview()
    {
        try
        {
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            for(int i=0;i<3;i++)
            {
                var mesh=CreateShell(130+i*5,190+i*7,78+i*7,i*.8f);
                var asset=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/StormShell_"+i+".asset");
                EditorUtility.CopySerialized(mesh,asset);EditorUtility.SetDirty(asset);Object.DestroyImmediate(mesh);
            }
            PrepareBackdrop(scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray());
            storm=Object.FindFirstObjectByType<TempleSandstorm>();
            cameras=new[]{GameObject.Find("Preview - Overview").GetComponent<Camera>(),GameObject.Find("Preview - Vertical Top").GetComponent<Camera>(),GameObject.Find("Preview - Inside Temple").GetComponent<Camera>()};
            cameras[0].fieldOfView=34;
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            ticks=0;EditorApplication.update+=CaptureTick;
        }
        catch(Exception ex){Debug.LogException(ex);if(Application.isBatchMode)EditorApplication.Exit(1);}
    }

    static void CreateSparks(GameObject parent)
    {
        var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/VFX/Materials/M_VFX_Glow_Soft.mat");
        var go=new GameObject("Seal embers");go.transform.SetParent(parent.transform,false);go.transform.localPosition=Vector3.up*24;
        var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var m=ps.main;m.loop=true;m.duration=10;m.startLifetime=4;m.startSpeed=0;m.startSize=new ParticleSystem.MinMaxCurve(.16f,.45f);m.startColor=new Color(.2f,1.2f,1.1f,.6f);m.maxParticles=90;m.prewarm=true;m.simulationSpace=ParticleSystemSimulationSpace.Local;
        var e=ps.emission;e.rateOverTime=12;var sh=ps.shape;sh.shapeType=ParticleSystemShapeType.Circle;sh.radius=140;sh.radiusThickness=.035f;sh.rotation=new Vector3(90,0,0);
        var vel=ps.velocityOverLifetime;vel.enabled=true;vel.orbitalY=.16f;vel.y=5;
        var col=ps.colorOverLifetime;col.enabled=true;var g=new Gradient();g.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.2f),new GradientAlphaKey(0,1)});col.color=g;
        var r=ps.GetComponent<ParticleSystemRenderer>();r.sharedMaterial=mat;r.renderMode=ParticleSystemRenderMode.Stretch;r.lengthScale=3;r.velocityScale=.12f;r.shadowCastingMode=ShadowCastingMode.Off;
        ps.Play();
    }

    static Camera CameraAt(string name,Vector3 position,Vector3 target,bool ortho,float size)
    {
        var c=new GameObject(name).AddComponent<Camera>();c.transform.position=position;c.transform.LookAt(target);
        if(ortho)c.transform.rotation=Quaternion.Euler(90,0,0);
        c.orthographic=ortho;c.orthographicSize=size;c.fieldOfView=52;c.nearClipPlane=.3f;c.farClipPlane=1800;c.enabled=false;
        c.clearFlags=CameraClearFlags.Skybox;c.useOcclusionCulling=false;
        var d=c.GetUniversalAdditionalCameraData();d.requiresDepthTexture=true;d.renderPostProcessing=false;
        return c;
    }

    static void CaptureTick()
    {
        if(++ticks<35)return;
        EditorApplication.update-=CaptureTick;
        try
        {
            storm.animate=false;storm.previewTime=16;storm.ApplyTime(16);
            foreach(var ps in storm.GetComponentsInChildren<ParticleSystem>()){ps.useAutoRandomSeed=false;ps.randomSeed=47;ps.Simulate(12,false,true,true);}
            string[] names={"overview","top","inside"};
            for(int i=0;i<cameras.Length;i++) Capture(cameras[i],Output+"/sandstorm-"+names[i]+".png",i==1?1600:1920,i==1?1600:1080);
            storm.ApplyTime(21);
            Capture(cameras[0],Output+"/sandstorm-overview-motion-check.png",1920,1080);
            var shader=Shader.Find("SandGuard/VFX/TempleSandstorm");
            if(ShaderUtil.ShaderHasError(shader))throw new Exception("Shader compiler reported errors after rendering");
            File.WriteAllText(Output+"/validation.txt","Scene: "+ScenePath+"\nPrefab: "+PrefabPath+"\n3 shell renderers; 2 bounded particle systems; no colliders; no runtime navigation or gameplay changes.\nShader compiled successfully. Captured 3 cameras and second animation time.\n");
            Debug.Log("TEMPLE_STORM_COMPLETE");if(Application.isBatchMode)EditorApplication.Exit(0);
        }
        catch(Exception ex){Debug.LogException(ex);if(Application.isBatchMode)EditorApplication.Exit(1);}
    }

    static void Capture(Camera c,string path,int width,int height)
    {
        var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);rt.Create();c.targetTexture=rt;c.aspect=width/(float)height;
        c.Render();c.Render();
        var previous=RenderTexture.active;RenderTexture.active=rt;
        var tex=new Texture2D(width,height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());
        RenderTexture.active=previous;c.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);
    }
}

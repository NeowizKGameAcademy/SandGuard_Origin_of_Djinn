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
using Object=UnityEngine.Object;

namespace DesertTower.VFX.Editor
{
    public static class GoldenShieldBuilder
    {
        const string Root="Assets/Resources/VFX";
        const string Path=Root+"/Prefabs/VFX_Chief_Golden_Shield_Loop.prefab";
        const string Docs="Docs/vfx-preview/GoldenShield";
        [MenuItem("DesertTower/VFX/Build Chief Summoned Golden Shield")]
        public static void Build()
        {
            Directory.CreateDirectory(Docs);Directory.CreateDirectory(Root+"/Meshes");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Art/Equipment/TowerShield.prefab");
            if(!source)throw new Exception("TowerShield reference missing");
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var triangles=new List<int>();
            foreach(var f in source.GetComponentsInChildren<MeshFilter>(true))
            {
                int offset=vertices.Count;
                vertices.AddRange(f.sharedMesh.vertices.Select(v=>f.transform.TransformPoint(v)));
                normals.AddRange(f.sharedMesh.normals.Select(n=>f.transform.TransformDirection(n)));
                triangles.AddRange(f.sharedMesh.triangles.Select(i=>i+offset));
            }
            var bounds=new Bounds(vertices[0],Vector3.zero);foreach(var v in vertices)bounds.Encapsulate(v);
            float[] sizes={bounds.size.x,bounds.size.y,bounds.size.z};
            var forward=Vector3.zero;forward[Array.IndexOf(sizes,sizes.Min())]=1;
            var up=Vector3.zero;up[Array.IndexOf(sizes,sizes.Max())]=1;
            var rotation=Quaternion.Inverse(Quaternion.LookRotation(forward,up));
            var surface=new Mesh{name="ChiefGoldenShield_Surface"};
            surface.SetVertices(vertices.Select(v=>rotation*(v-bounds.center)).ToList());
            surface.SetNormals(normals.Select(n=>rotation*n).ToList());surface.SetTriangles(triangles,0);surface.RecalculateBounds();
            surface=SaveMesh(surface);
            var edges=SaveMesh(BuildEdges(surface));
            var glass=Material("M_ChiefShield_GoldGlass",.085f,1.25f,.12f);
            var outline=Material("M_ChiefShield_GoldEdges",.8f,1.8f,0);
            var scene=EditorSceneManager.NewPreviewScene();
            var owned=new List<Object>();
            try
            {
                var root=new GameObject("VFX_Chief_Golden_Shield_Loop");SceneManager.MoveGameObjectToScene(root,scene);root.transform.localScale=Vector3.one*1.12f;
                MeshRenderer Add(string name,Mesh mesh,Material material)
                {var o=new GameObject(name);o.transform.SetParent(root.transform,false);o.AddComponent<MeshFilter>().sharedMesh=mesh;var r=o.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return r;}
                var shell=Add("Transparent original shield",surface,glass);var lines=Add("Glowing frame and reinforcement",edges,outline);
                var fx=new GameObject("Rising gold motes");fx.transform.SetParent(root.transform,false);fx.transform.localPosition=new Vector3(0,-surface.bounds.extents.y,.06f);
                var motes=fx.AddComponent<ParticleSystem>();var main=motes.main;main.loop=true;main.duration=2;main.startLifetime=new ParticleSystem.MinMaxCurve(.5f,.9f);main.startSpeed=.16f;main.startSize=new ParticleSystem.MinMaxCurve(.004f,.011f);main.startColor=new Color(1,.73f,.15f,.7f);main.maxParticles=20;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
                var emission=motes.emission;emission.rateOverTime=10;var shape=motes.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(surface.bounds.size.x,.04f,.025f);
                var renderer=motes.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=VfxBuildKit.GetShared().MeshAdditive;
                var fade=motes.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(1,.72f,.12f),0),new GradientColorKey(new Color(1,.9f,.4f),1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.7f,.2f),new GradientAlphaKey(0,1)});fade.color=gradient;
                var control=root.AddComponent<VfxGoldenShield>();control.shieldRenderers=new Renderer[]{shell,lines};control.motes=motes;control.Restart();
                PrefabUtility.SaveAsPrefabAsset(root,Path);AssetDatabase.SaveAssets();
                var saved=AssetDatabase.LoadAssetAtPath<GameObject>(Path);
                if(saved.GetComponentsInChildren<Collider>(true).Length!=0 || !saved.GetComponent<VfxGoldenShield>())throw new Exception("Invalid VFX structure");
                control.Tick(.6f);if(!control.IsVisible)throw new Exception("Summon failed");control.PulseHit();control.Dismiss();control.Tick(1);if(control.IsVisible)throw new Exception("Dismiss failed");control.Restart();control.Tick(.6f);
                var solid=(GameObject)PrefabUtility.InstantiatePrefab(source,scene);
                var wrapper=new GameObject("Original shield reference");SceneManager.MoveGameObjectToScene(wrapper,scene);solid.transform.SetParent(wrapper.transform,false);
                wrapper.transform.rotation=rotation;wrapper.transform.position=new Vector3(.62f,0,0)-rotation*bounds.center;
                foreach(var r in solid.GetComponentsInChildren<Renderer>())
                {var mats=r.sharedMaterials;for(int i=0;i<mats.Length;i++)if(!mats[i].shader.name.StartsWith("Universal Render Pipeline/")){var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));owned.Add(m);m.color=mats[i].HasProperty("_Color")?mats[i].color:Color.gray;mats[i]=m;}r.sharedMaterials=mats;}
                root.transform.position=new Vector3(-.62f,0,.04f);
                // Identical background bars make transparency visibly verifiable.
                for(int side=0;side<2;side++)for(int row=0;row<8;row++)
                {
                    var bar=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(bar,scene);
                    bar.transform.position=new Vector3(side==0?.62f:-.62f,-.6f+row*.17f,-.18f);bar.transform.localScale=new Vector3(.80f,.078f,.025f);
                    var m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));m.color=row%2==0?new Color(.14f,.24f,.34f):new Color(.07f,.12f,.18f);owned.Add(m);bar.GetComponent<Renderer>().sharedMaterial=m;
                }
                var camera=new GameObject("Golden shield camera").AddComponent<Camera>();SceneManager.MoveGameObjectToScene(camera.gameObject,scene);camera.scene=scene;
                camera.gameObject.AddComponent<UniversalAdditionalCameraData>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.04f,.065f);camera.orthographic=true;camera.orthographicSize=.85f;
                camera.transform.position=new Vector3(0,0,4);camera.transform.LookAt(Vector3.zero);
                var light=new GameObject("Key").AddComponent<Light>();SceneManager.MoveGameObjectToScene(light.gameObject,scene);light.type=LightType.Directional;light.intensity=1.4f;light.transform.rotation=Quaternion.Euler(20,145,0);
                motes.useAutoRandomSeed=false;motes.randomSeed=9;motes.Simulate(.65f,true,true);
                Render(camera,"Comparison");
                wrapper.SetActive(false);
                camera.orthographicSize=.78f;camera.transform.position=root.transform.position+new Vector3(.9f,.20f,3);camera.transform.LookAt(root.transform.position);
                Render(camera,"Angled");
                control.PulseHit();Render(camera,"Hit");
                control.Restart();control.Tick(.14f);Render(camera,"Summon");
                File.WriteAllText(Docs+"/validation.txt",$"PASS\nExact TowerShield source geometry reused: {surface.vertexCount} vertices, {surface.triangles.Length/3} triangles\nGolden shield scale: 1.12\nTransparent shell, shared-edge glow mesh, 10 motes/second\nPersistent summon / hit / dismiss / restart checked\nNo collision or damage components\nFour Unity renders\n");
                Debug.Log("GOLDEN_SHIELD_PASS "+Path);
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);foreach(var o in owned)Object.DestroyImmediate(o);}
        }

        static Mesh SaveMesh(Mesh m)
        {string path=Root+"/Meshes/"+m.name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){EditorUtility.CopySerialized(m,old);Object.DestroyImmediate(m);m=old;}else AssetDatabase.CreateAsset(m,path);m.UploadMeshData(false);EditorUtility.SetDirty(m);return m;}
        static Material Material(string name,float opacity,float emission,float rim)
        {string path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("SandGuard/VFX/GoldenShield"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_Tint",new Color(1,.62f,.12f));m.SetFloat("_Opacity",opacity);m.SetFloat("_Emission",emission);m.SetFloat("_Rim",rim);m.SetFloat("_EffectFade",1);EditorUtility.SetDirty(m);return m;}

        static Mesh BuildEdges(Mesh source)
        {
            var v=source.vertices;var tris=source.triangles;
            var unique=new List<Vector3>();var ids=new Dictionary<Vector3Int,int>();var map=new int[v.Length];
            for(int i=0;i<v.Length;i++){var key=Vector3Int.RoundToInt(v[i]*100000);if(!ids.TryGetValue(key,out int id)){id=unique.Count;ids.Add(key,id);unique.Add(v[i]);}map[i]=id;}
            var edges=new Dictionary<(int,int),List<Vector3>>();
            for(int i=0;i<tris.Length;i+=3)
            {
                var n=Vector3.Cross(v[tris[i+1]]-v[tris[i]],v[tris[i+2]]-v[tris[i]]).normalized;
                for(int j=0;j<3;j++){int a=map[tris[i+j]],b=map[tris[i+(j+1)%3]];var key=(Mathf.Min(a,b),Mathf.Max(a,b));if(!edges.TryGetValue(key,out var list)){list=new List<Vector3>();edges.Add(key,list);}list.Add(n);}
            }
            var points=new List<Vector3>();var indices=new List<int>();
            foreach(var pair in edges)
            {
                if(pair.Value.Count>1 && pair.Value.All(n=>Vector3.Dot(n,pair.Value[0])>.7f))continue;
                var a=unique[pair.Key.Item1];var b=unique[pair.Key.Item2];var d=b-a;if(d.magnitude<.009f)continue;
                var t=d.normalized;var u=Vector3.Cross(t,Mathf.Abs(t.y)<.9f?Vector3.up:Vector3.right).normalized*.0016f;var w=Vector3.Cross(t,u);
                int start=points.Count;
                for(int end=0;end<2;end++)for(int k=0;k<4;k++){float angle=k*Mathf.PI/2;points.Add((end==0?a:b)+Mathf.Cos(angle)*u+Mathf.Sin(angle)*w);}
                for(int k=0;k<4;k++){int next=(k+1)%4;indices.AddRange(new[]{start+k,start+next,start+4+next,start+k,start+4+next,start+4+k});}
            }
            var mesh=new Mesh{name="ChiefGoldenShield_Edges"};mesh.SetVertices(points);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static void Render(Camera camera,string name)
        {var rt=new RenderTexture(1400,1000,24);var pixels=new Texture2D(1400,1000,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1400,1000),0,0);pixels.Apply();File.WriteAllBytes(Docs+"/GoldenShield_"+name+".png",pixels.EncodeToPNG());}
            finally{camera.targetTexture=null;RenderTexture.active=previous;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(pixels);}}
    }
}

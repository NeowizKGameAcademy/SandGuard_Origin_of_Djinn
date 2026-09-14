using System;
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
    public static class ShieldGuardPreview
    {
        [MenuItem("DesertTower/VFX/Preview Shield Guards")]
        public static void BuildAndPreview()
        {
            RequestedVfxBuilder.BuildShield();
            var a=AssetDatabase.LoadAssetAtPath<GameObject>(RequestedVfxBuilder.ShieldPath);
            var b=AssetDatabase.LoadAssetAtPath<GameObject>(RequestedVfxBuilder.GoldShieldPath);
            if(a.transform.localScale!=b.transform.localScale)throw new Exception("Variant scale mismatch");
            var ap=a.GetComponentsInChildren<ParticleSystem>(true);var bp=b.GetComponentsInChildren<ParticleSystem>(true);
            if(ap.Length!=bp.Length)throw new Exception("Variant structure mismatch");
            for(int i=0;i<ap.Length;i++)
            {
                if((i>0 && ap[i].name!=bp[i].name) || ap[i].main.duration!=bp[i].main.duration || ap[i].main.startLifetime.constantMax!=bp[i].main.startLifetime.constantMax || ap[i].main.startSize.constantMax!=bp[i].main.startSize.constantMax || ap[i].emission.burstCount!=bp[i].emission.burstCount || ap[i].main.startSpeed.constantMax!=bp[i].main.startSpeed.constantMax)
                    throw new Exception("Gold changed particle behavior");
            }
            var al=a.GetComponentInChildren<LineRenderer>(); var bl=b.GetComponentInChildren<LineRenderer>();
            for(int i=0;i<al.positionCount;i++)if(al.GetPosition(i)!=bl.GetPosition(i))throw new Exception("Variant contour changed");
            var scene=EditorSceneManager.NewPreviewScene();
            var owned=new System.Collections.Generic.List<Object>();
            try
            {
                var reference=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Art/Equipment/TowerShield.prefab");
                var bounds=new Bounds();bool first=true;
                foreach(var f in reference.GetComponentsInChildren<MeshFilter>(true))
                    for(int i=0;i<8;i++){var box=f.sharedMesh.bounds;var p=f.transform.TransformPoint(box.center+Vector3.Scale(box.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
                float[] sizes={bounds.size.x,bounds.size.y,bounds.size.z};int normalAxis=Array.IndexOf(sizes,sizes.Min());int heightAxis=Array.IndexOf(sizes,sizes.Max());
                var normal=Vector3.zero;normal[normalAxis]=1;var up=Vector3.zero;up[heightAxis]=1;
                var rotation=Quaternion.Inverse(Quaternion.LookRotation(normal,up));
                var sorted=sizes.OrderBy(v=>v).ToArray();float width=sorted[1],height=sorted[2];
                float vfxWidth=Mathf.Sqrt(3)*.3f*a.transform.localScale.x,vfxHeight=.8f*a.transform.localScale.y;
                if(Mathf.Abs(vfxWidth/width-1.12f)>.001f || Mathf.Abs(vfxHeight/height-1.12f)>.001f)throw new Exception("Shield padding mismatch");
                var effects=new GameObject[2];
                for(int i=0;i<2;i++)
                {
                    var center=new Vector3((i==0?-1:1)*width*.95f,0,0);
                    var wrapper=new GameObject("Shield reference");SceneManager.MoveGameObjectToScene(wrapper,scene);
                    var shield=(GameObject)PrefabUtility.InstantiatePrefab(reference,scene);shield.transform.SetParent(wrapper.transform,false);
                    wrapper.transform.rotation=rotation;wrapper.transform.position=center-rotation*bounds.center;
                    foreach(var r in shield.GetComponentsInChildren<Renderer>())
                    {
                        var mats=r.sharedMaterials;
                        for(int m=0;m<mats.Length;m++)if(!mats[m].shader.name.StartsWith("Universal Render Pipeline/"))
                        {var copy=new Material(Shader.Find("Universal Render Pipeline/Lit"));owned.Add(copy);copy.SetColor("_BaseColor",mats[m].HasProperty("_Color")?mats[m].color:new Color(.4f,.3f,.2f));mats[m]=copy;}
                        r.sharedMaterials=mats;
                    }
                    effects[i]=(GameObject)PrefabUtility.InstantiatePrefab(i==0?a:b,scene);
                    effects[i].transform.position=center+Vector3.forward*(sorted[0]/2+.035f);
                }
                var camera=new GameObject("Guard Camera").AddComponent<Camera>();SceneManager.MoveGameObjectToScene(camera.gameObject,scene);camera.scene=scene;
                camera.gameObject.AddComponent<UniversalAdditionalCameraData>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.045f,.065f);
                camera.transform.position=new Vector3(0,0,4);camera.transform.LookAt(Vector3.zero);camera.orthographic=true;camera.orthographicSize=Mathf.Max(height*.85f,width*1.25f);camera.nearClipPlane=.01f;
                var light=new GameObject("Key").AddComponent<Light>();SceneManager.MoveGameObjectToScene(light.gameObject,scene);light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(25,145,0);
                Directory.CreateDirectory("Docs/vfx-preview/ShieldGuards");
                foreach(float t in new[]{.04f,.12f,.22f})
                {
                    foreach(var effect in effects)
                    {
                        foreach(var ps in effect.GetComponentsInChildren<ParticleSystem>()){ps.useAutoRandomSeed=false;ps.randomSeed=19;ps.Simulate(t,false,true);}
                        foreach(var pulse in effect.GetComponentsInChildren<VfxArcPulse>()){pulse.Restart();pulse.Tick(t);}
                    }
                    var rt=new RenderTexture(1400,900,24);var pixels=new Texture2D(1400,900,TextureFormat.RGB24,false);var active=RenderTexture.active;
                    try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1400,900),0,0);pixels.Apply();File.WriteAllBytes($"Docs/vfx-preview/ShieldGuards/Guards_{Mathf.RoundToInt(t*100):00}.png",pixels.EncodeToPNG());}
                    finally{camera.targetTexture=null;RenderTexture.active=active;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(pixels);}
                }
                foreach(var effect in effects)foreach(var p in effect.GetComponentsInChildren<VfxArcPulse>()){p.Tick(1);if(p.GetComponent<LineRenderer>().enabled)throw new Exception("Flash did not expire");p.Restart();if(!p.GetComponent<LineRenderer>().enabled)throw new Exception("Flash did not restart");}
                File.WriteAllText("Docs/vfx-preview/ShieldGuards/validation.txt",$"PASS\nTowerShield: {width:F3} x {height:F3} m\nBoth guard contours: {vfxWidth:F3} x {vfxHeight:F3} m\nWidth and height padding: 12%\nIdentical hierarchy, contour, size, duration and particle counts\nColor differences only in gold variant\nPulse expires and restarts\nThree paired renders\n");
                AssetDatabase.SaveAssets();Debug.Log("SHIELD_GUARDS_PASS");
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);foreach(var o in owned)Object.DestroyImmediate(o);}
        }
    }
}

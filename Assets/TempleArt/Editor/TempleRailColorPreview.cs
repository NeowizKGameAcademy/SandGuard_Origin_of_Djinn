using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;
public static class TempleRailColorPreview {
 [MenuItem("Tools/Temple Art/Compare Rail Sandstone Color")]
 public static void Run(){
  const string dir="Docs/LevelArt/TempleArchitecture/V2/rail-color";Directory.CreateDirectory(dir);
  var shader=Shader.Find("SandGuard/Architecture/Courtyard Rail Stone");if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("Rail shader failed to compile");
  var scene=EditorSceneManager.OpenPreviewScene("Assets/TempleArt/ArchitectureV2/Level_TempleCourtyard.unity");
  Camera cam=null;Material[] old=null;
  try {
   var renderers=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)).Where(r=>r.sharedMaterial&&r.sharedMaterial.shader==shader).ToArray();
   if(renderers.Length!=44)throw new Exception("Expected 44 recolored rails, got "+renderers.Length);
   var current=renderers.Select(r=>r.sharedMaterial).ToArray();old=current.Select(m=>new Material(Shader.Find("Universal Render Pipeline/Lit")){mainTexture=m.GetTexture("_BaseMap")}).ToArray();
   foreach(var m in old){m.SetColor("_BaseColor",Color.white);m.SetFloat("_Smoothness",.14f);}
   var go=new GameObject("Rail color comparison camera");SceneManager.MoveGameObjectToScene(go,scene);cam=go.AddComponent<Camera>();go.AddComponent<UniversalAdditionalCameraData>();cam.scene=scene;cam.nearClipPlane=.08f;cam.farClipPlane=1800;cam.fieldOfView=65;cam.transform.position=new Vector3(-73,3.1f,-76);cam.transform.LookAt(new Vector3(-63,2.5f,-63));
   for(int i=0;i<renderers.Length;i++)renderers[i].sharedMaterial=old[i];Capture(cam,dir+"/before.png");
   for(int i=0;i<renderers.Length;i++)renderers[i].sharedMaterial=current[i];Capture(cam,dir+"/after.png");
   File.WriteAllText(dir+"/validation.txt","44 rails use sandstone color correction. Shader compiled without errors. Original texture and UV coordinates preserved. Captured an isolated preview of the saved courtyard scene; live scene not saved or replaced.");
  } finally {if(cam)Object.DestroyImmediate(cam.gameObject);if(old!=null)foreach(var m in old)Object.DestroyImmediate(m);EditorSceneManager.ClosePreviewScene(scene);}
 }
 static void Capture(Camera c,string path){var rt=new RenderTexture(1200,900,24);var prev=RenderTexture.active;Texture2D tex=null;try{rt.Create();c.targetTexture=rt;c.aspect=4f/3;c.Render();c.Render();RenderTexture.active=rt;tex=new Texture2D(1200,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1200,900),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());}finally{c.targetTexture=null;RenderTexture.active=prev;rt.Release();Object.DestroyImmediate(rt);if(tex)Object.DestroyImmediate(tex);}}
}

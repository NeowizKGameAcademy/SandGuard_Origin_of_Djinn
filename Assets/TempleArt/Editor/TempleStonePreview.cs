using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;
public static class TempleStonePreview {


 public static void Run(){
  var args=Environment.GetCommandLineArgs();var idx=Array.IndexOf(args,"-stoneStage");var stage=idx>=0?args[idx+1]:"after";
  CaptureStage(stage);
 }
 public static void CaptureStage(string stage){
  string dir="Docs/LevelArt/TempleArchitecture/V2/stone-weathering";Directory.CreateDirectory(dir);
  var shader=Shader.Find("SandGuard/Architecture/Courtyard Stone");if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("Stone shader error");
  var scene=EditorSceneManager.OpenPreviewScene("Assets/TempleArt/ArchitectureV2/Level_TempleCourtyard.unity");Camera c=null;
  try{
   var go=new GameObject("Stone preview camera");SceneManager.MoveGameObjectToScene(go,scene);c=go.AddComponent<Camera>();go.AddComponent<UniversalAdditionalCameraData>();c.scene=scene;c.nearClipPlane=.08f;c.farClipPlane=1800;c.fieldOfView=65;
   foreach(var view in new[]{("entry",new Vector3(-73,3.1f,-76),new Vector3(-63,2.5f,-63)),("paving",new Vector3(-66,5,-66),new Vector3(-60,2,-56)),("wall",new Vector3(47,9,-46),new Vector3(26,14,-24))}){
    c.transform.position=view.Item2;c.transform.LookAt(view.Item3);var rt=new RenderTexture(1440,1000,24);var prev=RenderTexture.active;Texture2D tex=null;
    try{rt.Create();c.targetTexture=rt;c.aspect=1.44f;c.Render();c.Render();RenderTexture.active=rt;tex=new Texture2D(1440,1000,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1440,1000),0,0);tex.Apply();File.WriteAllBytes(dir+"/"+stage+"-"+view.Item1+".png",tex.EncodeToPNG());}
    finally{c.targetTexture=null;RenderTexture.active=prev;rt.Release();Object.DestroyImmediate(rt);if(tex)Object.DestroyImmediate(tex);}
   }
   File.WriteAllText(dir+"/"+stage+"-validation.txt","Stone shader compiled; three preview views rendered. Scene not saved; geometry and collision unmodified.");
  }finally{if(c)Object.DestroyImmediate(c.gameObject);EditorSceneManager.ClosePreviewScene(scene);}
 }
}

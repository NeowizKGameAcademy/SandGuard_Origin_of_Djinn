using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;
public static class TempleConceptView {


 public static void Run(){
  var args=Environment.GetCommandLineArgs();var idx=Array.IndexOf(args,"-stoneStage");var stage=idx>=0?args[idx+1]:"concept-view";
  CaptureStage(stage);
 }
 public static void CaptureStage(string stage){
  string dir="Docs/LevelArt/TempleArchitecture/V2/concept-view";Directory.CreateDirectory(dir);
  var shader=Shader.Find("SandGuard/Architecture/Courtyard Stone");if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("Stone shader error");
  var scene=EditorSceneManager.OpenPreviewScene("Assets/TempleArt/ArchitectureV2/Level_TempleCourtyard.unity");Camera c=null;var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;float shadowDistance=pipeline?pipeline.shadowDistance:0;if(pipeline)pipeline.shadowDistance=600;
  if(stage.StartsWith("storm"))foreach(var storm in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DesertTower.VFX.TempleSandstorm>(true))){storm.animate=false;storm.previewTime=stage=="storm-motion"?6:0;storm.ApplyTime(storm.previewTime);}
  try{
   var go=new GameObject("Stone preview camera");SceneManager.MoveGameObjectToScene(go,scene);c=go.AddComponent<Camera>();go.AddComponent<UniversalAdditionalCameraData>();c.scene=scene;c.nearClipPlane=.08f;c.farClipPlane=1800;c.fieldOfView=65;c.orthographic=true;c.orthographicSize=105;
   foreach(var view in new[]{("overview",new Vector3(150,175,-210),new Vector3(0,15,0))}){
    c.transform.position=view.Item2;c.transform.LookAt(view.Item3);var rt=new RenderTexture(1800,1400,24);var prev=RenderTexture.active;Texture2D tex=null;
    try{rt.Create();c.targetTexture=rt;c.aspect=1800f/1400f;c.Render();c.Render();RenderTexture.active=rt;tex=new Texture2D(1800,1400,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1800,1400),0,0);tex.Apply();File.WriteAllBytes(dir+"/"+stage+"-"+view.Item1+".png",tex.EncodeToPNG());}
    finally{c.targetTexture=null;RenderTexture.active=prev;rt.Release();Object.DestroyImmediate(rt);if(tex)Object.DestroyImmediate(tex);}
   }
   File.WriteAllText(dir+"/"+stage+"-validation.txt","Stone shader compiled; concept-angle overview rendered. Scene not saved; geometry and collision unmodified.");
  }finally{if(pipeline)pipeline.shadowDistance=shadowDistance;if(c)Object.DestroyImmediate(c.gameObject);EditorSceneManager.ClosePreviewScene(scene);}
 }
}

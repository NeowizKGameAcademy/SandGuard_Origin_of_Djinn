using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
public static class TempleStormTuning {
 const string Root="Assets/TempleArt/ArchitectureV2/Environment";
 public static void Configure(DesertTower.VFX.TempleSandstorm storm){
  storm.animationSpeed=2f;storm.animate=true;PrefabUtility.RecordPrefabInstancePropertyModifications(storm);EditorUtility.SetDirty(storm);
  int i=0;
  foreach(var f in storm.GetComponentsInChildren<MeshFilter>(true)){
   if(!f.sharedMesh)continue;
   var source=AssetDatabase.LoadAssetAtPath<Mesh>(storm.name.Contains("curtain")?"Assets/TempleArt/Terrain/StormOccluder.asset":"Assets/Resources/VFX/TempleSandstorm/StormShell_"+i+".asset");if(!source)throw new Exception("Missing original storm mesh");var vertices=source.vertices;var center=storm.transform.position;
   var world=vertices.Select(f.transform.TransformPoint).ToArray();var radii=world.Select(v=>new Vector2(v.x-center.x,v.z-center.z).magnitude).ToArray();float low=radii.Min(),high=radii.Max();if(high-low<1)continue;
   float inner=126+i*4,outer=235+i*5;
   for(int n=0;n<vertices.Length;n++){var v=world[n]-center;float r=radii[n];float nearby=(98+i*3)*r/Mathf.Max(Mathf.Abs(v.x),Mathf.Abs(v.z));float target=Mathf.Lerp(nearby,outer,(r-low)/(high-low));v.x*=target/r;v.z*=target/r;vertices[n]=f.transform.InverseTransformPoint(center+v);}
   var mesh=Object.Instantiate(source);mesh.name="Courtyard storm shell "+i;mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();
   string path=Root+"/Storm_"+storm.name.Replace(" ","_")+"_"+i+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,path);
   f.sharedMesh=mesh;PrefabUtility.RecordPrefabInstancePropertyModifications(f);EditorUtility.SetDirty(f);
   var mat=f.GetComponent<Renderer>().sharedMaterial;mat.SetFloat("_Opacity",.48f);mat.SetFloat("_FlowSpeed",1.5f);EditorUtility.SetDirty(mat);
   Debug.Log("STORM_TUNED "+storm.name+" shell="+i+" previous="+low+".."+high+" new="+inner+".."+outer);i++;
  }
 }
 public static void Run(){
  var scene=EditorSceneManager.OpenScene("Assets/TempleArt/ArchitectureV2/Level_TempleCourtyard.unity");
  foreach(var storm in Object.FindObjectsByType<DesertTower.VFX.TempleSandstorm>(FindObjectsSortMode.None))Configure(storm);
  EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();TempleConceptView.CaptureStage("storm");TempleConceptView.CaptureStage("storm-motion");
 }
}


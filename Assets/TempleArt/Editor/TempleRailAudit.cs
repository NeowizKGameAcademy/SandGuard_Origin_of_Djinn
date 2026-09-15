using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;
public static class TempleRailAudit {
 [Serializable] public class Side { public string name; public Vector3[] vertices,normals; public Vector2[] uv; public int[] triangles; public Vector3 scale; }
 [Serializable] public class Pair { public string name; public Side[] sides; }
 static string dir="Docs/LevelArt/TempleArchitecture/V2/rail-audit";
 public static void ApplyFix() {
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DesertTowerLevels/TrimSheet/Prefabs/DesertTemple_TrimRails_v2.prefab");
  int changed=0,count=0;
  foreach(var f in prefab.GetComponentsInChildren<MeshFilter>(true).Where(f=>(f.name=="Left"||f.name=="Right")&&f.transform.parent.name.StartsWith("RAILS_REBUILT"))) {
   var m=f.sharedMesh;string path=AssetDatabase.GetAssetPath(m);
   if(!path.StartsWith("Assets/DesertTowerLevels/TrimSheet/Meshes/RailsV2/"))throw new Exception("Unexpected rail mesh "+path);
   var v=m.vertices;var uv=m.uv;var bounds=m.bounds;var center=v.Aggregate(Vector3.zero,(a,b)=>a+b)/v.Length;
   double volume=0;var tri=m.triangles;for(int i=0;i<tri.Length;i+=3)volume+=Vector3.Dot(v[tri[i]]-center,Vector3.Cross(v[tri[i+1]]-center,v[tri[i+2]]-center));
   if(volume<0) {
    for(int sub=0;sub<m.subMeshCount;sub++){var ids=m.GetIndices(sub);for(int i=0;i<ids.Length;i+=3){int t=ids[i+1];ids[i+1]=ids[i+2];ids[i+2]=t;}m.SetIndices(ids,MeshTopology.Triangles,sub,false);}
    m.normals=m.normals.Select(n=>-n).ToArray();var tangents=m.tangents;for(int i=0;i<tangents.Length;i++)tangents[i].w=-tangents[i].w;m.tangents=tangents;
    EditorUtility.SetDirty(m);changed++;
   }
   if(!v.SequenceEqual(m.vertices)||!uv.SequenceEqual(m.uv)||bounds!=m.bounds)throw new Exception("Rail geometry or UV changed "+path);
   count++;
  }
  if(count!=44)throw new Exception("Unexpected rail count "+count);
  AssetDatabase.SaveAssets();Debug.Log("RAIL_ASSET_FIX "+changed+" / "+count);
  VerifyFix();
 }
 public static void VerifyFix() { TempleTraversalVerification.Run(); dir="Docs/LevelArt/TempleArchitecture/V2/rail-audit/after"; Run(); TempleCourtyardSurfaces.Validate(Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).First(t=>t.Find("Source Level walking collision"))); }
 public static void Run() {
  Directory.CreateDirectory(dir);
  EditorSceneManager.OpenScene("Assets/TempleArt/ArchitectureV2/Level_TempleCourtyard.unity");
  var pairs=Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Where(f=>f.name.StartsWith("RAILS_REBUILT")&&f.transform.Find("Left")).ToArray();
  var chosen=pairs.OrderBy(f=>Vector3.Distance(f.GetComponent<Renderer>().bounds.center,new Vector3(-68,1,-69))).First();
  var sides=new[]{chosen.transform.Find("Left").GetComponent<MeshFilter>(),chosen.transform.Find("Right").GetComponent<MeshFilter>()};
  var data=new Pair{name=chosen.name,sides=sides.Select(f=>Read(f)).ToArray()};
  File.WriteAllText(dir+"/mesh.json",JsonUtility.ToJson(data,true));
  File.WriteAllLines(dir+"/pairs.txt",pairs.Select(f=>f.name+" "+f.GetComponent<Renderer>().bounds));
  var cam=new GameObject("Rail audit camera").AddComponent<Camera>();cam.gameObject.AddComponent<UniversalAdditionalCameraData>();cam.nearClipPlane=.03f;cam.farClipPlane=1800;cam.fieldOfView=65;
  Capture(cam,"01-entry",new Vector3(-73,3.1f,-76),new Vector3(-63,2.5f,-63));
  var bounds=sides[0].GetComponent<Renderer>().bounds;bounds.Encapsulate(sides[1].GetComponent<Renderer>().bounds);
  Vector3 center=bounds.center;
  Capture(cam,"02-reverse",center+new Vector3(7,4,9),center);
  foreach(var terrain in Terrain.activeTerrains)terrain.drawHeightmap=false; RenderSettings.fog=false; foreach(var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))r.enabled=false;
  foreach(var f in sides)f.GetComponent<Renderer>().enabled=true;
  cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.12f,.15f,.19f);cam.orthographic=true;cam.orthographicSize=bounds.size.magnitude*.46f;
  var across=(sides[1].GetComponent<Renderer>().bounds.center-sides[0].GetComponent<Renderer>().bounds.center);across.y=0;across.Normalize();
  var along=Vector3.Cross(across,Vector3.up).normalized;float distance=bounds.size.magnitude*1.5f;
  Capture(cam,"03-lit-side-a",center+(across+Vector3.up*.5f)*distance,center);
  Capture(cam,"04-lit-side-b",center+(-across+Vector3.up*.5f)*distance,center);
  var mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));mat.SetColor("_BaseColor",new Color(.75f,.72f,.64f));mat.SetFloat("_Cull",2);
  foreach(var f in sides)f.GetComponent<Renderer>().sharedMaterial=mat;
  Capture(cam,"05-unlit-front",center+(along+Vector3.up*.55f)*distance,center);
  Capture(cam,"06-unlit-back",center+(-along+Vector3.up*.55f)*distance,center);
  Capture(cam,"07-unlit-top",center+(Vector3.up+along*.001f)*distance,center);
  Capture(cam,"08-unlit-side",center+(across+Vector3.up*.35f)*distance,center);
  Debug.Log("RAIL_AUDIT_DONE "+chosen.name+" "+bounds);
 }
 static Side Read(MeshFilter f) {
  using(var d=Mesh.AcquireReadOnlyMeshData(f.sharedMesh)) {
   int n=d[0].vertexCount;
   var v=new Unity.Collections.NativeArray<Vector3>(n,Unity.Collections.Allocator.Temp);var ns=new Unity.Collections.NativeArray<Vector3>(n,Unity.Collections.Allocator.Temp);var uv=new Unity.Collections.NativeArray<Vector2>(n,Unity.Collections.Allocator.Temp);
   d[0].GetVertices(v);d[0].GetNormals(ns);d[0].GetUVs(0,uv);
   var ids=new System.Collections.Generic.List<int>();for(int s=0;s<d[0].subMeshCount;s++){var a=new Unity.Collections.NativeArray<int>(d[0].GetSubMesh(s).indexCount,Unity.Collections.Allocator.Temp);d[0].GetIndices(a,s);ids.AddRange(a.ToArray());a.Dispose();}
   var result=new Side{name=f.name,vertices=v.ToArray().Select(f.transform.TransformPoint).ToArray(),normals=ns.ToArray().Select(x=>f.transform.localToWorldMatrix.inverse.transpose.MultiplyVector(x).normalized).ToArray(),uv=uv.ToArray(),triangles=ids.ToArray(),scale=f.transform.lossyScale};v.Dispose();ns.Dispose();uv.Dispose();return result;
  }
 }
 static void Capture(Camera c,string name,Vector3 pos,Vector3 target) {
  c.transform.position=pos;c.transform.LookAt(target);var rt=new RenderTexture(1200,900,24);rt.Create();c.targetTexture=rt;c.aspect=4f/3;c.Render();c.Render();var old=RenderTexture.active;RenderTexture.active=rt;
  var tex=new Texture2D(1200,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1200,900),0,0);tex.Apply();File.WriteAllBytes(dir+"/"+name+".png",tex.EncodeToPNG());c.targetTexture=null;RenderTexture.active=old;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);
 }
}



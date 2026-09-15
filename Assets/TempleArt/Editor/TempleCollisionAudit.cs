using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
public static class TempleCollisionAudit {
 [Serializable] public class Part {public string name,path;public Vector3[] vertices;public int[] triangles; public bool enabled; }
 [Serializable] public class Survey {public List<Part> parts=new List<Part>();}
 static Part Read(Mesh m,Transform t,string path,bool enabled) {
  using(var d=Mesh.AcquireReadOnlyMeshData(m)){
   var v=new Unity.Collections.NativeArray<Vector3>(d[0].vertexCount,Unity.Collections.Allocator.Temp); d[0].GetVertices(v);
   var p=new Part{name=t.name,path=path,enabled=enabled,vertices=v.ToArray().Select(t.TransformPoint).ToArray()};v.Dispose();
   var ids=new List<int>();for(int s=0;s<d[0].subMeshCount;s++){var a=new Unity.Collections.NativeArray<int>(d[0].GetSubMesh(s).indexCount,Unity.Collections.Allocator.Temp);d[0].GetIndices(a,s);ids.AddRange(a.ToArray());a.Dispose();}p.triangles=ids.ToArray();return p;
  }
 }
 public static void Run(){
  Directory.CreateDirectory("Library/TempleCollisionAudit");
  foreach(var pair in new[]{("source","Assets/1.Scene/Level.unity"),("courtyard","Assets/TempleArt/ArchitectureV2/Level_TempleCourtyard.unity")}){
   EditorSceneManager.OpenScene(pair.Item2);var s=new Survey();var lines=new List<string>();
   foreach(var c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
    lines.Add(c.name+" "+c.GetType().Name+" enabled="+c.enabled+" active="+c.gameObject.activeInHierarchy+" bounds="+c.bounds);
    if(c is MeshCollider mc && mc.sharedMesh)s.parts.Add(Read(mc.sharedMesh,c.transform,AssetDatabase.GetAssetPath(mc.sharedMesh),c.enabled&&c.gameObject.activeInHierarchy));
   }
   File.WriteAllText("Library/TempleCollisionAudit/"+pair.Item1+"-colliders.json",JsonUtility.ToJson(s));File.WriteAllLines("Library/TempleCollisionAudit/"+pair.Item1+"-colliders.txt",lines);
   var visual=new Survey();
   foreach(var f in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(f=>f.name.EndsWith("_deck")||f.name.StartsWith("TREADS_FLUSH")||f.name.StartsWith("RAILS_REBUILT"))){
    if(f.sharedMesh)visual.parts.Add(Read(f.sharedMesh,f.transform,AssetDatabase.GetAssetPath(f.sharedMesh),true));
   }
   File.WriteAllText("Library/TempleCollisionAudit/"+pair.Item1+"-surfaces.json",JsonUtility.ToJson(visual));
  }
 }
}



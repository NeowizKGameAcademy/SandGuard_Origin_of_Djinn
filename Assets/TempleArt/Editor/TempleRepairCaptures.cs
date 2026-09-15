using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;
public static class TempleRepairCaptures {
 public static void Run() {
  TempleTraversalVerification.Run();
  EditorSceneManager.OpenScene("Assets/TempleArt/ArchitectureV2/Level_TempleCourtyard.unity");
  var camera=new GameObject("Repair verification camera").AddComponent<Camera>();
  camera.gameObject.AddComponent<UniversalAdditionalCameraData>();camera.nearClipPlane=.08f;camera.farClipPlane=500;camera.fieldOfView=65;
  foreach(var p in new[]{
   ("entry",new Vector3(-73,3.1f,-76),new Vector3(-63,2.5f,-63)),
   ("platform",new Vector3(-66,3.7f,-66),new Vector3(-60,2.2f,-56)),
   ("rails",new Vector3(-64,3.7f,-55),new Vector3(-60,2.8f,-38)),
   ("tower-interior",new Vector3(-47.4f,5.5f,-3),new Vector3(-43,5.7f,0)),
   ("statue-la",new Vector3(-40,4,-51),new Vector3(-34,4,-43)),
   ("statue-osiris",new Vector3(28,4,-51),new Vector3(34,4,-43)),
   ("statue-obelisk",new Vector3(-49,5,33),new Vector3(-42,5,42)),
   ("wall-reliefs",new Vector3(15,4,-80),new Vector3(9,6,-73))}) {
    var position=p.Item2;var target=p.Item3;
    if(p.Item1.StartsWith("statue-")) {
     var kind=p.Item1=="statue-la"?"la-dragon":p.Item1=="statue-osiris"?"osiris-dragon":"obelisk-giant";
     var sculpture=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Single(r=>r.name=="D_UniqueStatue_"+kind);
     target=sculpture.bounds.center;float span=sculpture.bounds.size.magnitude;
     position=target+new Vector3(-span*.65f,span*.15f,-span*.85f);
     Debug.Log("SCULPTURE_CAPTURE "+kind+" bounds="+sculpture.bounds);
    }
    camera.transform.position=position;camera.transform.LookAt(target);
    var rt=new RenderTexture(1440,1000,24);rt.Create();camera.targetTexture=rt;camera.aspect=1.44f;
    camera.Render();camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
    var tex=new Texture2D(1440,1000,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1440,1000),0,0);tex.Apply();
    File.WriteAllBytes("Docs/LevelArt/TempleArchitecture/V2/05-player-"+p.Item1+".png",tex.EncodeToPNG());
    camera.targetTexture=null;RenderTexture.active=previous;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);
  }
  Object.DestroyImmediate(camera.gameObject);
 }
}

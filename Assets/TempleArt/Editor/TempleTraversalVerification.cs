using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
public static class TempleTraversalVerification {
 const string ScenePath="Assets/TempleArt/ArchitectureV2/Level_TempleCourtyard.unity";
 public static void Run() {
  bool source=Environment.GetCommandLineArgs().Contains("-templeVerifySource");
  EditorSceneManager.OpenScene(source?"Assets/1.Scene/Level.unity":ScenePath);Physics.SyncTransforms();
  // Installed core and tower anchors intentionally occupy deck space. This check targets architecture.
  foreach(var c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
    if(c.name=="TowerBaseAnchor"||c.name=="Core")c.enabled=false;
  var player=Object.FindFirstObjectByType<SandGuard.Player.PlayerMotor>();
  if(player)player.gameObject.SetActive(false);
  var go=new GameObject("Temporary traversal capsule");
  var cc=go.AddComponent<CharacterController>();cc.height=2;cc.radius=.35f;cc.center=Vector3.up;cc.skinWidth=.03f;cc.stepOffset=.45f;cc.slopeLimit=50;
  var lines=new List<string>();int failures=0,ramps=0,decks=0;
  void Place(Vector3 p){cc.enabled=false;go.transform.position=p;cc.enabled=true;Physics.SyncTransforms();}
  bool Walk(Vector3 start,Vector3 end) {
   if(Physics.Raycast(start+Vector3.up,Vector3.down,out var startFloor,2,~0,QueryTriggerInteraction.Ignore))start.y=startFloor.point.y;
   if(Physics.Raycast(end+Vector3.up,Vector3.down,out var endFloor,2,~0,QueryTriggerInteraction.Ignore))end.y=endFloor.point.y;
   Place(start+Vector3.up*.04f);var direction=Vector3.ProjectOnPlane(end-start,Vector3.up).normalized;
   float distance=Vector3.ProjectOnPlane(end-start,Vector3.up).magnitude;int steps=Mathf.CeilToInt(distance/.08f)*2+60;
   for(int i=0;i<steps;i++){
    var delta=Vector3.ProjectOnPlane(end-go.transform.position,Vector3.up);
    cc.Move(Vector3.ClampMagnitude(delta,.08f)+Vector3.down*.07f);
   }
   float horizontal=Vector3.ProjectOnPlane(go.transform.position-end,Vector3.up).magnitude;
   float vertical=Mathf.Abs(go.transform.position.y-end.y);
   if(horizontal>.25f||vertical>.3f){var center=go.transform.position+Vector3.up; lines.Add("CONTACTS "+string.Join(", ",Physics.OverlapCapsule(center+Vector3.up*.65f,center-Vector3.up*.65f,.42f,~0,QueryTriggerInteraction.Ignore).Select(c=>c.name)));}
   if(horizontal>.25f||vertical>.3f)lines.Add("FAIL motion "+start+" -> "+end+" actual "+go.transform.position+" horizontal="+horizontal+" vertical="+vertical);
   return horizontal<=.25f&&vertical<=.3f;
  }
  foreach(var p in TempleCourtyardSurfaces.Reference().parts) {
   if(p.name.EndsWith("_deck")){
    var center=p.outline.Aggregate(Vector3.zero,(a,b)=>a+b)/p.outline.Length;
    bool ok=true;
    foreach(var point in p.outline)ok&=Walk(Vector3.Lerp(center,point,.8f),Vector3.Lerp(center,p.outline[(Array.IndexOf(p.outline,point)+p.outline.Length/2)%p.outline.Length],.8f));
    lines.Add(p.name+" deck sweep "+(ok?"PASS":"FAIL"));if(!ok)failures++;decks++;
   } else {
    Vector3 a,b;
    if(p.normal.y<.999f) {
     var low=p.outline.Min(v=>v.y);var high=p.outline.Max(v=>v.y);
     var av=p.outline.Where(v=>v.y<low+.01f).ToArray();var bv=p.outline.Where(v=>v.y>high-.01f).ToArray();
     a=av.Aggregate(Vector3.zero,(x,y)=>x+y)/av.Length;b=bv.Aggregate(Vector3.zero,(x,y)=>x+y)/bv.Length;
    } else {
     var xsize=p.outline.Max(v=>v.x)-p.outline.Min(v=>v.x);var zsize=p.outline.Max(v=>v.z)-p.outline.Min(v=>v.z);
     int axis=xsize>zsize?0:2;float min=p.outline.Min(v=>v[axis]),max=p.outline.Max(v=>v[axis]);
     var av=p.outline.Where(v=>v[axis]<min+.25f).ToArray();var bv=p.outline.Where(v=>v[axis]>max-.25f).ToArray();
     a=av.Aggregate(Vector3.zero,(x,y)=>x+y)/av.Length;b=bv.Aggregate(Vector3.zero,(x,y)=>x+y)/bv.Length;
    }
    var delta=(b-a).normalized;a+=delta*.5f;b-=delta*.5f;
    bool up=Walk(a,b),down=Walk(b,a);
    lines.Add(p.name+" forward="+up+" reverse="+down);if(!up||!down)failures++;ramps++;
   }
  }
  // Push a capsule against unbroken vertical wall faces without upward input.
  int walls=0;float maxRise=0,maxJumpRise=0;
  foreach(var col in Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).Where(c=>c.enabled&&(c.name.StartsWith("C_Pylon")||c.name.StartsWith("C_GatePylon")))) {
   var bounds=col.bounds;
   foreach(var axis in new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back}) {
    var from=bounds.center+axis*(bounds.extents.magnitude+3);from.y=Mathf.Max(3,bounds.min.y+2);
    if(!col.Raycast(new Ray(from,-axis),out var wall,bounds.size.magnitude+6)||Mathf.Abs(wall.normal.y)>.1f)continue;
    var feet=wall.point+axis*.6f;feet.y-=1;
    if(!Physics.Raycast(feet+Vector3.up*.1f,Vector3.down,out var ground,6,~0,QueryTriggerInteraction.Ignore))continue;
    feet.y=ground.point.y+.04f;Place(feet);
    for(int i=0;i<120;i++)cc.Move(-axis*.06f+Vector3.down*.07f);
    maxRise=Mathf.Max(maxRise,go.transform.position.y-feet.y);
    Place(feet);float verticalSpeed=0;
    for(int i=0;i<600;i++){
     if(cc.isGrounded && verticalSpeed<=0)verticalSpeed=9;
     verticalSpeed-=25f/60;
     var flags=cc.Move(-axis*.05f+Vector3.up*(verticalSpeed/60));
     if((flags&CollisionFlags.Above)!=0&&verticalSpeed>0)verticalSpeed=0;
     maxJumpRise=Mathf.Max(maxJumpRise,go.transform.position.y-feet.y);
    }
    walls++;
   }
  }
  if(maxRise>.5f||maxJumpRise>3)failures++;
  lines.Add($"SUMMARY decks={decks}, passages={ramps}, wall pushes={walls}, maximum wall rise={maxRise:R}, repeated jump rise={maxJumpRise:R}, failures={failures}");
  Object.DestroyImmediate(go);
  File.WriteAllLines("Docs/LevelArt/TempleArchitecture/V2/"+(source?"source-traversal-baseline.txt":"traversal-verification.txt"),lines);
  Debug.Log(lines.Last());if(failures>0)throw new Exception("Traversal verification failed; see report.");
 }
}


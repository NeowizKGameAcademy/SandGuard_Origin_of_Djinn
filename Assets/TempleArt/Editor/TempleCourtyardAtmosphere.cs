using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// Courtyard-only atmosphere and scenery. Never edits shared terrain heights or source storm materials.
public static class TempleCourtyardAtmosphere {
 const string Root="Assets/TempleArt/ArchitectureV2/Environment";
 const string Group="Courtyard - distant rocks and ruins";
 static Material CopyMaterial(string name,Material source) {
  string path=Root+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(!m){m=new Material(source);AssetDatabase.CreateAsset(m,path);}else {m.shader=source.shader;m.CopyPropertiesFromMaterial(source);}
  m.name=name;EditorUtility.SetDirty(m);return m;
 }
 static Mesh Rock(int variant) {
  const int sides=9,rings=5;var vertices=new List<Vector3>();var triangles=new List<int>();
  var profile=new[]{.72f,1f,.94f,.80f,.61f};
  for(int ring=0;ring<rings;ring++)for(int j=0;j<sides;j++) {
   float angle=j*Mathf.PI*2/sides;float noise=Mathf.PerlinNoise(j*.72f+variant*3.7f,ring*.47f+12);
   float radius=profile[ring]*(.82f+noise*.30f);
   vertices.Add(new Vector3(Mathf.Cos(angle)*radius,(ring/4f)*1.3f-.3f+(noise-.5f)*.15f,Mathf.Sin(angle)*radius));
  }
  for(int r=0;r<rings-1;r++)for(int j=0;j<sides;j++) {
   int a=r*sides+j,b=r*sides+(j+1)%sides,c=b+sides,d=a+sides;
   triangles.AddRange(new[]{a,d,b,b,d,c});
  }
  for(int j=1;j<sides-1;j++){triangles.AddRange(new[]{0,j,j+1});triangles.AddRange(new[]{(rings-1)*sides,(rings-1)*sides+j+1,(rings-1)*sides+j});}
  // Flat faces establish broad rock planes; the stone shader supplies restrained grain.
  var flat=triangles.Select(i=>vertices[i]).ToArray();var mesh=new Mesh{name="Wind-eroded rock "+variant};
  mesh.vertices=flat;mesh.triangles=Enumerable.Range(0,flat.Length).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
  var path=Root+"/Rock_"+variant+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
  if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);return old;}
  AssetDatabase.CreateAsset(mesh,path);return mesh;
 }
 static float Ground(Terrain t,float x,float z)=>t?t.SampleHeight(new Vector3(x,0,z))+t.transform.position.y:.8f;
 static GameObject Piece(Transform parent,string name,Mesh mesh,Material mat,Vector3 pos,Vector3 scale,float yaw=0) {
  var go=new GameObject(name);go.transform.SetParent(parent);go.transform.position=pos;go.transform.localScale=scale;go.transform.rotation=Quaternion.Euler(0,yaw,0);
  go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;go.isStatic=true;return go;
 }
 static Mesh Drift(Terrain terrain,Vector2 center,int index) {
  const int nx=65,nz=49;float ground=Ground(terrain,center.x,center.y);
  var v=new Vector3[nx*nz];var uv=new Vector2[v.Length];var tri=new List<int>();
  for(int j=0;j<nz;j++)for(int i=0;i<nx;i++) {
   float x=(i/(float)(nx-1)-.5f)*46,z=(j/(float)(nz-1)-.5f)*32;
   float t=Mathf.Clamp01(Mathf.Sqrt(x*x/(23*23)+z*z/(16*16)));
   float y=Ground(terrain,center.x+x,center.y+z)-ground+.012f+.66f*(1-t)*(1-t);
   int k=j*nx+i;v[k]=new Vector3(x,y,z);uv[k]=new Vector2((center.x+x+500)/6,(center.y+z+500)/6);
   if(i<nx-1&&j<nz-1)tri.AddRange(new[]{k,k+nx,k+1,k+1,k+nx,k+nx+1});
  }
  var mesh=new Mesh{name="Terrain-following wind drift "+index};mesh.vertices=v;mesh.uv=uv;mesh.triangles=tri.ToArray();mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
  string path=Root+"/Drift_"+index+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
  if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);return old;}AssetDatabase.CreateAsset(mesh,path);return mesh;
 }
 static Mesh DistantSand(Terrain terrain) {
  const int n=129;var vertices=new Vector3[n*n];var uv=new Vector2[n*n];var triangles=new List<int>();
  for(int j=0;j<n;j++)for(int i=0;i<n;i++) {
   float x=(i/(float)(n-1)-.5f)*2400,z=(j/(float)(n-1)-.5f)*2400;
   float edge=Mathf.Max(Mathf.Abs(x),Mathf.Abs(z));float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(500,760,edge));
   float near=Ground(terrain,Mathf.Clamp(x,-499,499),Mathf.Clamp(z,-499,499));
   float distant=.8f+14*Mathf.PerlinNoise(x*.008f+21,z*.009f+64);
   int k=j*n+i;vertices[k]=new Vector3(x,Mathf.Lerp(near,distant,blend)-.15f,z);uv[k]=new Vector2((x+500)/6,(z+500)/6);
   if(i<n-1&&j<n-1) {
    float right=x+2400f/(n-1),back=z+2400f/(n-1);
    if(x<460&&right>-460&&z<460&&back>-460)continue;
    triangles.AddRange(new[]{k,k+n,k+1,k+1,k+n,k+n+1});
   }
  }
  var mesh=new Mesh{name="Distant dune skirt - central 920m hole"};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
  string path=Root+"/DistantSand.asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
  if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);return old;}AssetDatabase.CreateAsset(mesh,path);return mesh;
 }
 public static void Apply() {
  Directory.CreateDirectory(Root);AssetDatabase.Refresh();
  var old=GameObject.Find(Group);if(old)Object.DestroyImmediate(old);var group=new GameObject(Group);
  var terrain=Object.FindFirstObjectByType<Terrain>();
  var stone=AssetDatabase.LoadAssetAtPath<Material>("Assets/TempleArt/ArchitectureV2/Temple_Sandstone.mat");
  var rock=CopyMaterial("ErodedRock",stone);rock.SetFloat("_Masonry",0);rock.SetColor("_BaseColor",new Color(.57f,.43f,.30f,1));rock.SetFloat("_BumpStrength",.20f);
  var ruin=CopyMaterial("BuriedRuins",stone);ruin.SetColor("_BaseColor",new Color(.68f,.56f,.40f,1));
  var meshes=Enumerable.Range(0,5).Select(Rock).ToArray();var rng=new System.Random(9019);int count=0;
  foreach(var c in new[]{new Vector2(-115,-97),new Vector2(119,-103),new Vector2(-141,96),new Vector2(128,120),new Vector2(-215,-40),new Vector2(190,52)}) {
   var sand=AssetDatabase.LoadAssetAtPath<Material>(Root+"/WindDrift.mat");
   if(!sand){sand=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(sand,Root+"/WindDrift.mat");}
   sand.SetColor("_BaseColor",Color.white);sand.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TempleArt/Terrain/Textures/Sand_BaseColor.png"));
   sand.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TempleArt/Terrain/Textures/Sand_Normal.png"));sand.SetFloat("_BumpScale",.35f);sand.EnableKeyword("_NORMALMAP");sand.SetFloat("_Smoothness",.12f);EditorUtility.SetDirty(sand);
   Piece(group.transform,"Wind drift "+count,Drift(terrain,c,count),sand,new Vector3(c.x,Ground(terrain,c.x,c.y),c.y),Vector3.one);
   for(int j=0;j<6;j++) {
    float x=c.x+(float)(rng.NextDouble()-.5)*22,z=c.y+(float)(rng.NextDouble()-.5)*20;
    float width=3.5f+(float)rng.NextDouble()*6,height=3+(float)rng.NextDouble()*9,depth=3+(float)rng.NextDouble()*5;
    Piece(group.transform,"Scenery rock "+count,meshes[count%5],rock,new Vector3(x,Ground(terrain,x,z),z),new Vector3(width,height,depth),(float)rng.NextDouble()*180);count++;
   }
  }
  var temporary=GameObject.CreatePrimitive(PrimitiveType.Cube);var cube=temporary.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(temporary);
  int ruins=0;
  foreach(var center in new[]{new Vector2(-120,36),new Vector2(130,55),new Vector2(-154,-126)}) {
   // Broken stacked blocks with individually grounded bases avoid floating ruins on sloped dunes.
   for(int column=0;column<4;column++) {
    float x=center.x+(column%2)*6,z=center.y+(column/2)*8;float ground=Ground(terrain,x,z);int courses=2+(column*3+ruins)%5;
    for(int k=0;k<courses;k++)Piece(group.transform,"Ruin pier "+ruins+" course "+k,cube,ruin,new Vector3(x,ground+.72f+k*1.48f,z),new Vector3(2.2f-k*.04f,1.46f,2.3f-k*.04f));
    Piece(group.transform,"Broken crown "+ruins,meshes[ruins%5],ruin,new Vector3(x,ground+courses*1.48f-.2f,z),new Vector3(1.12f,.75f,1.15f),column*17);ruins++;
   }
   for(int j=0;j<8;j++) {
    float x=center.x-4+j*1.8f,z=center.y-3+(j%3)*1.5f;
    Piece(group.transform,"Buried wall fragment "+ruins+"-"+j,cube,ruin,new Vector3(x,Ground(terrain,x,z)+.45f,z),new Vector3(1.65f,.9f,1.05f),j*21);
   }
  }
  Piece(group.transform,"Distant dune skirt",DistantSand(terrain),AssetDatabase.LoadAssetAtPath<Material>(Root+"/WindDrift.mat"),Vector3.zero,Vector3.one);
  // Fade the existing visual seal, keeping every original transform and collider.
  int layers=0;
  foreach(var storm in Object.FindObjectsByType<DesertTower.VFX.TempleSandstorm>(FindObjectsSortMode.None)) {
   storm.animationSpeed=2f;EditorUtility.SetDirty(storm);
   foreach(var renderer in storm.GetComponentsInChildren<Renderer>(true)) {
    var source=renderer.sharedMaterial;if(!source)continue;
    string name="Dust_"+renderer.GetInstanceID();
    // Stable hierarchy-derived name makes regeneration reuse the same asset.
    name="Dust_"+storm.name.Replace(" ","_").Replace(":","_")+"_"+renderer.name.Replace(" ","_").Replace(":","_");
    var mat=CopyMaterial(name,source);mat.shader=Shader.Find("SandGuard/VFX/TempleSandstorm");
    mat.SetFloat("_Opacity",.48f);mat.SetFloat("_FlowSpeed",1.5f);
    mat.SetColor("_DarkColor",new Color(.58f,.43f,.29f,1));mat.SetColor("_LightColor",new Color(.91f,.77f,.56f,1));
    mat.renderQueue=3000;renderer.sharedMaterial=mat;renderer.shadowCastingMode=ShadowCastingMode.Off;
    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);EditorUtility.SetDirty(mat);layers++;
   }
   TempleStormTuning.Configure(storm);
  }
  foreach(var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l=>l.type==LightType.Directional&&l.name!="Dunes - Sky Fill")) {
   light.transform.rotation=Quaternion.Euler(32,-38,0);light.intensity=1.45f;light.color=new Color(1,.90f,.73f);light.shadows=LightShadows.Soft;light.shadowStrength=.85f;
   PrefabUtility.RecordPrefabInstancePropertyModifications(light);PrefabUtility.RecordPrefabInstancePropertyModifications(light.transform);EditorUtility.SetDirty(light);
  }
  var fillObject=GameObject.Find("Dunes - Sky Fill");if(fillObject){var fill=fillObject.GetComponent<Light>();fill.intensity=.30f;fill.color=new Color(.70f,.80f,1);PrefabUtility.RecordPrefabInstancePropertyModifications(fill);EditorUtility.SetDirty(fill);}
  RenderSettings.ambientMode=AmbientMode.Trilight;
  RenderSettings.ambientSkyColor=new Color(.32f,.38f,.49f);RenderSettings.ambientEquatorColor=new Color(.27f,.24f,.20f);RenderSettings.ambientGroundColor=new Color(.20f,.16f,.12f);
  RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.0012f;RenderSettings.fogColor=new Color(.73f,.66f,.53f);
  if(RenderSettings.skybox){var sky=CopyMaterial("CourtyardSky",RenderSettings.skybox);if(sky.HasProperty("_Exposure"))sky.SetFloat("_Exposure",1.1f);RenderSettings.skybox=sky;}
  foreach(var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)){camera.farClipPlane=1800;PrefabUtility.RecordPrefabInstancePropertyModifications(camera);EditorUtility.SetDirty(camera);}
  DynamicGI.UpdateEnvironment();
  int invalid=0;
  foreach(var r in group.GetComponentsInChildren<Renderer>()) {
   if(r.name=="Distant dune skirt")continue; // Its mesh has a 920m-wide central hole.
   var b=r.bounds;if(b.min.x<88&&b.max.x>-88&&b.min.z<88&&b.max.z>-88)invalid++;
  }
  if(invalid>0)throw new Exception("Scenery intrudes into protected courtyard and approach buffer");
  File.WriteAllText("Docs/LevelArt/TempleArchitecture/V2/09-environment-validation.txt",$"Scenery rocks: {count}\nRuin piers: {ruins}\nStorm renderer overrides: {layers}\nScenery overlapping +/-88m protected square: {invalid}\nNo scenery colliders; original terrain heights, routes, platforms and sculpture geometry unchanged.\n");
  AssetDatabase.SaveAssets();
 }
}
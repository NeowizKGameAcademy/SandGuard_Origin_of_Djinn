using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static class TempleCourtyardSurfaces
{
    const string Root="Assets/TempleArt/ArchitectureV2";
    const string Docs="Docs/LevelArt/TempleArchitecture/V2";
    [Serializable] public class Part { public string name; public Vector3[] vertices,samples,outline; public int[] triangles; public Vector3 normal; }
    [Serializable] public class Data { public Part[] parts; }
    public static Data Reference()=>JsonUtility.FromJson<Data>(File.ReadAllText(Docs+"/route-collision-reference.json"));
    public static void RepairRoutesBatch() {
        var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Root+"/Level_TempleCourtyard.unity");
        var temple=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).First(t=>t.name=="DesertTemple_V7"||t.name=="DesertTemple_V6");
        Build(temple);Validate(temple);
        TempleStairGapRepair.Apply(temple);
        PrefabUtility.SaveAsPrefabAsset(temple.gameObject,Root+"/DesertTemple_Courtyard.prefab");
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        TempleTraversalVerification.Run();
    }
    static Mesh SaveMesh(string path, Mesh mesh) {
        var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;}
        AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    public static void Build(Transform temple) {
        Directory.CreateDirectory(Root+"/Collision");
        foreach(var name in new[]{"Original walking collision","Source Level walking collision"}) {
            var old=temple.Find(name);if(old)Object.DestroyImmediate(old.gameObject);
        }
        var root=new GameObject("Source Level walking collision").transform;root.SetParent(temple,true);
        root.SetPositionAndRotation(Vector3.zero,Quaternion.identity);root.localScale=Vector3.one;
        foreach(var p in Reference().parts) {
            var mesh=new Mesh{name=p.name+" source collision",indexFormat=IndexFormat.UInt32};
            mesh.vertices=p.vertices;mesh.triangles=p.triangles;mesh.RecalculateBounds();
            mesh=SaveMesh(Root+"/Collision/"+p.name+".asset",mesh);
            var go=new GameObject(p.name);go.transform.SetParent(root,false);
            go.AddComponent<MeshCollider>().sharedMesh=mesh;go.isStatic=true;
        }
        ApplyRails(temple);
        var railGo=new GameObject("Preserved rail collision");railGo.transform.SetParent(root,false);
        railGo.AddComponent<MeshCollider>();
        RebuildRailCollision(temple);
    }
    // Use the repaired, separated rail shells. The hidden legacy parent meshes have
    // inward-facing left rails and internal end caps which trap the character capsule.
    public static void RebuildRailCollision(Transform temple) {
        var collider=temple.Find("Source Level walking collision/Preserved rail collision").GetComponent<MeshCollider>();
        var rail=temple.GetComponentsInChildren<MeshFilter>(true)
            .Where(f=>(f.name=="Left"||f.name=="Right")&&f.transform.parent.name.StartsWith("RAILS_REBUILT")).ToArray();
        if(rail.Length!=44)throw new Exception("Expected 44 separated rail shells, got "+rail.Length);
        var routes=Reference().parts.ToDictionary(p=>p.name);
        var vertices=new List<Vector3>();var triangles=new List<int>();
        foreach(var f in rail) using(var data=Mesh.AcquireReadOnlyMeshData(f.sharedMesh)) {
            int offset=vertices.Count;
            var v=new Unity.Collections.NativeArray<Vector3>(data[0].vertexCount,Unity.Collections.Allocator.Temp);
            data[0].GetVertices(v);
            var local=v.ToArray().Select(p=>collider.transform.InverseTransformPoint(f.transform.TransformPoint(p))).ToArray();v.Dispose();
            var route=routes[f.transform.parent.name.Replace("RAILS_REBUILT_","TREADS_FLUSH_")];
            // The shallow entry ramps otherwise let the capsule start an automatic
            // step onto the rail, then wedge against its side. Keep their collision
            // crest above stepOffset + capsule radius; retain the tapered endpoints.
            for(int i=0;route.name.StartsWith("TREADS_FLUSH_ENTRY_")&&i<local.Length;i++) {
                var world=collider.transform.TransformPoint(local[i]);
                float floorY=route.outline[0].y-(route.normal.x*(world.x-route.outline[0].x)+route.normal.z*(world.z-route.outline[0].z))/route.normal.y;
                if(world.y-floorY>.3f)local[i]+=collider.transform.InverseTransformVector(Vector3.up*.4f);
            }
            vertices.AddRange(local);
            var shell=new List<int>();
            for(int s=0;s<data[0].subMeshCount;s++){
                var ids=new Unity.Collections.NativeArray<int>(data[0].GetSubMesh(s).indexCount,Unity.Collections.Allocator.Temp);
                data[0].GetIndices(ids,s);shell.AddRange(ids.ToArray());ids.Dispose();
            }
            var center=local.Aggregate(Vector3.zero,(a,b)=>a+b)/local.Length;
            double volume=0;
            for(int i=0;i<shell.Count;i+=3)
                volume+=Vector3.Dot(local[shell[i]]-center,Vector3.Cross(local[shell[i+1]]-center,local[shell[i+2]]-center));
            if(Math.Abs(volume)<.0001)throw new Exception("Degenerate rail shell: "+f.transform.parent.name+"/"+f.name);
            for(int i=0;i<shell.Count;i+=3) {
                int a=offset+shell[i],b=offset+shell[i+(volume>0?1:2)],c=offset+shell[i+(volume>0?2:1)];
                // These rails are convex prisms, but the visual meshes also retain
                // cross-section caps at the taper joins. Those internal walls snag
                // capsules ascending the shallow entry ramps. Keep only hull faces.
                var normal=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]).normalized;
                if(local.Any(p=>Vector3.Dot(normal,p-vertices[a])>.001f))continue;
                triangles.Add(a);triangles.Add(b);triangles.Add(c);
            }
        }
        var rm=new Mesh{name="Outward facing rail shells",indexFormat=IndexFormat.UInt32};rm.SetVertices(vertices);rm.SetTriangles(triangles,0);rm.RecalculateBounds();
        var saved=SaveMesh(Root+"/OriginalRailCollision.asset",rm);
        collider.sharedMesh=null;collider.sharedMesh=saved;
    }
    static void ApplyRails(Transform temple) {
        const string Trim="Assets/DesertTowerLevels/TrimSheet";
        var template=AssetDatabase.LoadAssetAtPath<GameObject>(Trim+"/Prefabs/DesertTemple_TrimRails_v2.prefab");
        var sourceMat=AssetDatabase.LoadAssetAtPath<Material>(Trim+"/Materials/SandGuard_Sandstone_Trim_v1.mat");
        Directory.CreateDirectory(Root+"/Rails");AssetDatabase.Refresh();
        var materials=new Dictionary<string,Material>();
        foreach(var side in new[]{"Left","Right"}) {
            var texturePath=Root+"/Rails/"+side+"_Sandstone.png";
            if(!File.Exists(texturePath))File.Copy(Trim+"/Textures/SandGuard_Sandstone_Trim_v1.png",texturePath);
            AssetDatabase.ImportAsset(texturePath);
            var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.wrapModeU=TextureWrapMode.Mirror;importer.wrapModeV=TextureWrapMode.Clamp;importer.anisoLevel=8;importer.SaveAndReimport();
            string path=Root+"/Rails/"+side+"_Sandstone.mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(sourceMat);AssetDatabase.CreateAsset(mat,path);}
            mat.shader=Shader.Find("SandGuard/Architecture/Courtyard Rail Stone");
            mat.SetColor("_BaseColor",new Color(.76f,.645f,.47f,1));
            mat.SetFloat("_DetailContrast",.7f);mat.SetFloat("_TextureMidpoint",.35f);
            mat.SetFloat("_Metallic",0);mat.SetFloat("_Smoothness",.1f);
            mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));EditorUtility.SetDirty(mat);materials[side]=mat;
        }
        int count=0;
        foreach(var f in temple.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("RAILS_REBUILT")).ToArray()) {
            var source=template.GetComponentsInChildren<Transform>(true).Single(t=>t.name==f.name);
            var expected=f.GetComponent<Renderer>().bounds;
            f.GetComponent<Renderer>().enabled=false;
            foreach(var side in new[]{"Left","Right"}) {
                var old=f.transform.Find(side);if(old)Object.DestroyImmediate(old.gameObject);
                var child=Object.Instantiate(source.Find(side).gameObject,f.transform,false);child.name=side;
                var renderer=child.GetComponent<MeshRenderer>();renderer.enabled=true;renderer.sharedMaterial=materials[side];
                if(child.GetComponent<MeshFilter>().sharedMesh.uv.Length==0)throw new Exception("Missing rail UV");
                count++;
            }
            var rs=new[]{f.transform.Find("Left").GetComponent<Renderer>(),f.transform.Find("Right").GetComponent<Renderer>()};
            var bounds=rs[0].bounds;bounds.Encapsulate(rs[1].bounds);
            if(Vector3.Distance(bounds.min,expected.min)>.005f||Vector3.Distance(bounds.max,expected.max)>.005f)
                throw new Exception("Separated rail placement differs: "+f.name+" "+bounds+" / "+expected);
        }
        if(count!=44)throw new Exception("Expected 44 rails, got "+count);
    }
    public static void Validate(Transform temple) {
        Physics.SyncTransforms();var root=temple.Find("Source Level walking collision");int count=0,missing=0;float error=0;
        foreach(var p in Reference().parts) {
            var col=root.Find(p.name).GetComponent<MeshCollider>();
            foreach(var sample in p.samples) {
                count++;
                if(!col.Raycast(new Ray(sample+Vector3.up*.25f,Vector3.down),out var hit,.5f)){missing++;Debug.LogError("MISSING ROUTE "+p.name+" "+sample.ToString("F5"));continue;}
                error=Mathf.Max(error,Mathf.Abs(sample.y-hit.point.y));
            }
        }
        File.WriteAllText(Docs+"/collision-repair-validation.txt",$"Original Level: 17 platforms, 18 smooth stair ramps, 4 level links\nSource surface probes: {count}\nMissing surface probes: {missing}\nMaximum original surface height error: {error:R} m\n44 separate rail meshes with individual UVs; Left/Right textures and materials\nUnbeveled structural collision meshes; no tread or decorative colliders\n");
        if(missing>0||error>.005f)throw new Exception("Original walking collision mismatch");
    }
}



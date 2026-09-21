using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Reuses the same solid ramp mesh at all four courtyard corner decks.
public static class TempleT1CourtyardRamp
{
    const string Root = "Assets/TempleArt/ArchitectureV2";
    const string Output = "Docs/LevelArt/TempleArchitecture/V2/courtyard-ramps";


    public static void Run()
    {
        Directory.CreateDirectory(Output);
        var mesh = BuildMesh();
        string meshPath = Root + "/Collision/T1_Courtyard_Ramp.asset";
        var saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (saved) { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); }
        else { AssetDatabase.CreateAsset(mesh, meshPath); saved = mesh; }
        var prefab = PrefabUtility.LoadPrefabContents(Root + "/DesertTemple_Courtyard.prefab");
        try
        {
            for (int deck = 0; deck < 4; deck++)
            {
                string rampName = $"T{deck + 1}_Courtyard_Ramp";
                var old = prefab.transform.Find(rampName);

                var ramp = old ? old.gameObject : new GameObject(rampName);
                ramp.transform.SetParent(prefab.transform, false);
                ramp.transform.localPosition = deck == 1 ? new Vector3(0,0,128) : deck == 3 ? new Vector3(0,0,-128) : Vector3.zero;
                ramp.transform.localRotation = Quaternion.Euler(0, deck >= 2 ? 180 : 0, 0);
                ramp.isStatic = true;
                var filter = ramp.GetComponent<MeshFilter>();
                if (!filter) filter = ramp.AddComponent<MeshFilter>();
                filter.sharedMesh = saved;
                var renderer = ramp.GetComponent<MeshRenderer>();
                if (!renderer) renderer = ramp.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = new[] {
                    AssetDatabase.LoadAssetAtPath<Material>(Root + "/Temple_Paving.mat"),
                    AssetDatabase.LoadAssetAtPath<Material>(Root + "/Temple_Sandstone.mat")
                };
                var collider = ramp.GetComponent<MeshCollider>();
                if (!collider) collider = ramp.AddComponent<MeshCollider>();
                collider.sharedMesh = saved;
            }
            PrefabUtility.SaveAsPrefabAsset(prefab, Root + "/DesertTemple_Courtyard.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/1.Scene/Level.unity");
        Physics.SyncTransforms();
        Verify();
        Capture();
        TempleT1NavigationBake.Run();
    }

    public static Vector3 Map(Vector3 point, int deck)
    {
        if (deck >= 2) { point.x = -point.x; point.z = -point.z; }
        point.z += deck == 1 ? 128 : deck == 3 ? -128 : 0;
        return point;
    }

    static Mesh BuildMesh()
    {
        // Short overlap at the deck; the foot ends just below the courtyard paving.
        const float highX = -59.03f, lowX = -55f, highY = 2f, lowY = 1.125f;
        const float south = -66f, north = -62f, bottom = .95f;
        var a = new Vector3(highX, highY, south); var b = new Vector3(highX, highY, north);
        var c = new Vector3(lowX, lowY, north); var d = new Vector3(lowX, lowY, south);
        var e = new Vector3(highX, bottom, south); var f = new Vector3(highX, bottom, north);
        var g = new Vector3(lowX, bottom, north); var h = new Vector3(lowX, bottom, south);
        var vertices = new List<Vector3>(); var uv = new List<Vector2>();
        var top = new List<int>(); var sides = new List<int>();
        void Face(Vector3 p, Vector3 q, Vector3 r, Vector3 s, bool paving = false)
        {
            int i = vertices.Count; vertices.AddRange(new[] { p, q, r, s });
            uv.AddRange(new[] { new Vector2(0,0),new Vector2(0,Vector3.Distance(p,q)),new Vector2(Vector3.Distance(q,r),Vector3.Distance(p,q)),new Vector2(Vector3.Distance(q,r),0) });
            (paving ? top : sides).AddRange(new[] { i,i+1,i+2,i,i+2,i+3 });
        }
        Face(a,b,c,d,true); Face(e,a,d,h); Face(f,g,c,b); Face(e,f,b,a); Face(h,d,c,g); Face(e,h,g,f);
        var mesh = new Mesh { name = "T1 courtyard ramp - paving and solid stone sides" };
        mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.subMeshCount=2;
        mesh.SetTriangles(top,0);mesh.SetTriangles(sides,1);mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();
        return mesh;
    }

    static void Verify()
    {
        var player = Object.FindFirstObjectByType<SandGuard.Player.PlayerMotor>();
        var original = player ? player.GetComponent<CharacterController>() : null;
        var go = new GameObject("Temporary ramp traversal check");
        var cc = go.AddComponent<CharacterController>();
        cc.height = original ? original.height : 2; cc.radius = original ? original.radius : .35f;
        cc.center = original ? original.center : Vector3.up; cc.skinWidth = original ? original.skinWidth : .03f;
        cc.stepOffset = original ? original.stepOffset : .45f; cc.slopeLimit = original ? original.slopeLimit : 50;
        if(player)player.gameObject.SetActive(false);
        var lines = new List<string>{"Level scene: T1-T4 decks to courtyard, width 4m, run 4.03m, rise 0.875m, slope 12.25 degrees.",
            $"Controller: height={cc.height}, radius={cc.radius}, step={cc.stepOffset}, slope={cc.slopeLimit}"};
        int failures=0;
        for(int deck=0;deck<4;deck++) foreach(float z in new[]{-65.3f,-64f,-62.7f}) foreach(bool ascending in new[]{true,false})
        {
            var start=new Vector3(ascending?-54.1f:-60.2f,ascending?1.13f:2f,z);
            var end=new Vector3(ascending?-60.2f:-54.1f,ascending?2f:1.13f,z);
            start=Map(start,deck);end=Map(end,deck);
            cc.enabled=false;go.transform.position=start+Vector3.up*.04f;cc.enabled=true;Physics.SyncTransforms();
            for(int i=0;i<220;i++)cc.Move(Vector3.ClampMagnitude(Vector3.ProjectOnPlane(end-go.transform.position,Vector3.up),.055f)+Vector3.down*.035f);
            float horizontal=Vector3.ProjectOnPlane(go.transform.position-end,Vector3.up).magnitude;
            float vertical=Mathf.Abs(go.transform.position.y-end.y);
            bool pass=horizontal<.15f&&vertical<.2f;
            lines.Add($"{(pass?"PASS":"FAIL")} T{deck+1} {(ascending?"up":"down")} z={z}: horizontal error {horizontal:F4}, height error {vertical:F4}");
            if(!pass)failures++;
        }
        Object.DestroyImmediate(go);
        File.WriteAllLines(Output+"/verification.txt",lines);
        if(failures>0)throw new Exception("Ramp traversal failed. See "+Output+"/verification.txt");
        Debug.Log("T1-T4 courtyard ramps: all 24 traversal checks passed.");
    }

    public static void CaptureSaved()
    {
        EditorSceneManager.OpenScene("Assets/1.Scene/Level.unity");
        Capture();
    }

    static void Capture()
    {
        for(int deck=0;deck<4;deck++)
        {
        var cam=new GameObject("Ramp preview").AddComponent<Camera>();
        cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        cam.transform.position=Map(new Vector3(-49,10,deck % 2 == 0 ? -54 : -74),deck);cam.transform.LookAt(Map(new Vector3(-60,1.5f,-64),deck));cam.farClipPlane=500;cam.fieldOfView=55;
        var rt=new RenderTexture(1200,800,24);cam.targetTexture=rt;cam.aspect=1.5f;cam.Render();cam.Render();
        var previous=RenderTexture.active;RenderTexture.active=rt;
        var tex=new Texture2D(1200,800,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1200,800),0,0);tex.Apply();
        File.WriteAllBytes(Output+$"/T{deck+1}-after.png",tex.EncodeToPNG());RenderTexture.active=previous;cam.targetTexture=null;
        Object.DestroyImmediate(tex);Object.DestroyImmediate(rt);Object.DestroyImmediate(cam.gameObject);
        }
    }
}

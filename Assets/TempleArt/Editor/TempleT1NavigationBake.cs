using System;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

public static class TempleT1NavigationBake
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/1.Scene/Level.unity");
        Physics.SyncTransforms();
        var surface = Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None).Single();
        var old = surface.navMeshData;
        string path = AssetDatabase.GetAssetPath(old);
        if (!old || string.IsNullOrEmpty(path)) throw new Exception("Expected existing Level navigation asset.");
        // Exterior spawn terrain is outside the Level hierarchy. Include it in a bounded bake.
        surface.collectObjects = CollectObjects.Volume;
        surface.center = new Vector3(0, 30, 0);
        surface.size = new Vector3(200, 100, 200);
        surface.BuildNavMesh();
        var baked = surface.navMeshData;
        if (!baked) throw new Exception("NavMesh build returned no data.");
        surface.RemoveData();
        EditorUtility.CopySerialized(baked, old);
        EditorUtility.SetDirty(old);
        surface.navMeshData = old;
        surface.AddData();
        Object.DestroyImmediate(baked);
        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(surface);
        EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
        EditorSceneManager.SaveScene(surface.gameObject.scene);
        // Reopen to verify the persisted data, rather than the temporary bake result.
        EditorSceneManager.OpenScene("Assets/1.Scene/Level.unity");
        var report = new System.Collections.Generic.List<string>{"Baked Level navigation: " + path};
        for(int deck=0;deck<4;deck++) foreach(float z in new[]{-65.3f,-64f,-62.7f}) foreach(bool up in new[]{true,false})
        {
            var start = new Vector3(up?-54.1f:-60.2f,up?1.13f:2f,z);
            var end = new Vector3(up?-60.2f:-54.1f,up?2f:1.13f,z);
            start=TempleT1CourtyardRamp.Map(start,deck);end=TempleT1CourtyardRamp.Map(end,deck);
            if(!NavMesh.SamplePosition(start,out var a,.3f,NavMesh.AllAreas) || !NavMesh.SamplePosition(end,out var b,.3f,NavMesh.AllAreas)) throw new Exception("Endpoint not on saved navigation.");
            var route = new NavMeshPath();
            bool ok=NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,route);
            float length=0;for(int i=1;i<route.corners.Length;i++)length+=Vector3.Distance(route.corners[i-1],route.corners[i]);
            report.Add($"{(ok && route.status==NavMeshPathStatus.PathComplete && length<8 ? "PASS" : "FAIL")} T{deck+1} z={z} up={up} status={route.status} length={length:F3}");
        }
        foreach(var node in Object.FindObjectsByType<DesertTower.LevelIntegration.RouteNode>(FindObjectsSortMode.None)) {
            bool found=NavMesh.SamplePosition(node.transform.position,out var from,3,NavMesh.AllAreas);
            report.Add((found?"PASS":"FAIL")+" node "+node.label);
            foreach(var link in node.outgoing.Where(l=>l.available && l.target)) {
                var route=new NavMeshPath();
                bool complete=found && NavMesh.SamplePosition(link.target.transform.position,out var to,3,NavMesh.AllAreas) && NavMesh.CalculatePath(from.position,to.position,NavMesh.AllAreas,route) && route.status==NavMeshPathStatus.PathComplete;
                report.Add((complete?"PASS":"FAIL")+" graph "+node.label+" -> "+link.target.label);
            }
        }
        File.WriteAllLines("Docs/LevelArt/TempleArchitecture/V2/courtyard-ramps/navigation-bake.txt",report);
        if(report.Any(l=>l.StartsWith("FAIL")))throw new Exception("Ramp navigation check failed.");
        Debug.Log("Saved Level navigation and passed 24 direct ramp path checks.");
    }
}

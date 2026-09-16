using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class TempleRailCollisionVerification
{
    const string Root = "Assets/TempleArt/ArchitectureV2";
    const string Report = "Docs/LevelArt/TempleArchitecture/V2/rail-collision-verification.txt";

    public static void Repair()
    {
        // Run in an empty scene in batch mode so no open editor scene is changed
        // or saved and unrelated scene colliders cannot affect the capsule.
        if (!Application.isBatchMode) throw new InvalidOperationException("Run this verification with Unity -batchmode -executeMethod TempleRailCollisionVerification.Repair.");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var temple = (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/DesertTemple_Courtyard.prefab"), scene);
        var lines = new List<string>();
        try
        {
            Check(temple.transform, lines, "BEFORE");
            TempleCourtyardSurfaces.RebuildRailCollision(temple.transform);
            AssetDatabase.SaveAssets();
            int failures = Check(temple.transform, lines, "AFTER");
            File.WriteAllLines(Report, lines);
            if (failures != 0) throw new Exception("Rail collision verification failed; see " + Report);
        }
        finally { Object.DestroyImmediate(temple); }
        // The existing suite also covers central routes, decks, and wall climbing.
        if (Application.isBatchMode) TempleTraversalVerification.Run();
    }

    static int Check(Transform temple, List<string> lines, string label)
    {
        Physics.SyncTransforms();
        var rail = temple.Find("Source Level walking collision/Preserved rail collision").GetComponent<MeshCollider>();
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Generated/Player.prefab").GetComponent<CharacterController>();
        var probe = new GameObject("Temporary rail traversal capsule");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(probe, temple.gameObject.scene);
        var cc = probe.AddComponent<CharacterController>();
        cc.height = player.height; cc.radius = player.radius; cc.center = player.center;
        cc.skinWidth = player.skinWidth; cc.stepOffset = player.stepOffset; cc.slopeLimit = player.slopeLimit;
        cc.minMoveDistance = 0;
        int rays = 0, misses = 0, walks = 0, failures = 0;
        try
        {
            foreach (var part in TempleCourtyardSurfaces.Reference().parts.Where(p => p.name.StartsWith("TREADS_FLUSH_")))
            {
                var pair = temple.GetComponentsInChildren<Transform>(true).Single(t => t.name == part.name.Replace("TREADS_FLUSH_", "RAILS_REBUILT_"));
                var left = pair.Find("Left").GetComponent<Renderer>().bounds.center;
                var right = pair.Find("Right").GetComponent<Renderer>().bounds.center;
                var across = Vector3.ProjectOnPlane(right - left, Vector3.up).normalized;
                var along = Vector3.Cross(across, Vector3.up).normalized;
                float lo = part.outline.Min(p => Vector3.Dot(p, along));
                float hi = part.outline.Max(p => Vector3.Dot(p, along));
                var floor = temple.Find("Source Level walking collision/" + part.name).GetComponent<MeshCollider>();
                foreach (var sign in new[] { -1f, 1f })
                {
                    var side = pair.Find(sign < 0 ? "Left" : "Right").GetComponent<MeshFilter>();
                    var verts = side.sharedMesh.vertices.Select(side.transform.TransformPoint).ToArray();
                    float edge = sign < 0 ? verts.Max(p => Vector3.Dot(p, across)) : verts.Min(p => Vector3.Dot(p, across));
                    Vector3 Point(float t, float inward)
                    {
                        var p = along * Mathf.Lerp(lo, hi, t) + across * (edge - sign * inward);
                        p.y = part.outline[0].y - (part.normal.x * (p.x - part.outline[0].x) + part.normal.z * (p.z - part.outline[0].z)) / part.normal.y;
                        return p;
                    }
                    foreach (float t in new[] { .2f, .5f, .8f })
                    {
                        var p = Point(t, .7f) + Vector3.up * .25f;
                        rays++;
                        if (!rail.Raycast(new Ray(p, across * sign), out var hit, .85f) || Vector3.Dot(hit.normal, across * sign) > -.9f)
                        {
                            misses++;
                            lines.Add(label + " inward wall ray FAIL " + part.name + "/" + side.name + " t=" + t);
                        }
                    }
                    foreach (int direction in new[] { -1, 1 })
                    {
                        var start = Point(direction > 0 ? .08f : .92f, cc.radius + .05f);
                        var end = Point(direction > 0 ? .92f : .08f, cc.radius + .05f);
                        cc.enabled = false; probe.transform.position = start + Vector3.up * .04f; cc.enabled = true;
                        Physics.SyncTransforms();
                        float distance = Mathf.Abs(Vector3.Dot(end - start, along));
                        int frames = Mathf.CeilToInt(distance / .08f) * 2 + 60;
                        for (int i = 0; i < frames; i++)
                        {
                            float remaining = Vector3.Dot(end - probe.transform.position, along) * direction;
                            if (remaining <= .08f) break;
                            // Brush the inner rail face, without steering continuously
                            // over the low rail (which the capsule can step onto).
                            float lateralTarget = edge - sign * (cc.radius - cc.skinWidth);
                            float lateralStep = Mathf.Clamp(lateralTarget - Vector3.Dot(probe.transform.position, across), -.025f, .025f);
                            cc.Move(along * (direction * .08f) + across * lateralStep + Vector3.down * .07f);
                        }
                        float shortfall = Vector3.Dot(end - probe.transform.position, along) * direction;
                        var foot = probe.transform.position;
                        bool supported = floor.Raycast(new Ray(foot + Vector3.up * .1f, Vector3.down), out var support, .85f);
                        bool ok = shortfall < .25f && supported;
                        walks++;
                        if (!ok) failures++;
                        lines.Add(label + " " + part.name + "/" + side.name + " direction=" + direction + " " + (ok ? "PASS" : "FAIL") + " shortfall=" + shortfall.ToString("F4") + " supported=" + supported + " actual=" + foot.ToString("F4") + " target=" + end.ToString("F4") + " lateral=" + (Vector3.Dot(foot, across)-edge).ToString("F4"));
                    }
                }
            }
        }
        finally { Object.DestroyImmediate(probe); }
        lines.Add(label + " SUMMARY wall rays=" + rays + " missed=" + misses + " edge walks=" + walks + " failed=" + failures);
        Debug.Log(lines.Last());
        return misses + failures;
    }
}

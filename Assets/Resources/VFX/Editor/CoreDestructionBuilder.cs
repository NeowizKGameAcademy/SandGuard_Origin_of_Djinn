using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static DesertTower.VFX.Editor.VfxBuildKit;

namespace DesertTower.VFX.Editor
{
    public static class CoreDestructionBuilder
    {
        public const string ReferencePath = "Assets/2.Model/Prefabs/New Core.prefab";
        public const string PrefabPath = PrefabDir + "/VFX_Core_Destruction.prefab";

        [MenuItem("DesertTower/VFX/Build Core Destruction (Level Model)")]
        public static void Build()
        {
            var reference = PrefabUtility.LoadPrefabContents(ReferencePath);
            var root = new GameObject("VFX_Core_Destruction");
            try
            {
                var ctrl = root.AddComponent<VfxCoreDestruction>();
                ctrl.WholeModel = true;
                ctrl.BurstTime = BlueDestructionBursts.BreakTime;
                ctrl.FlightTime = 0.65f;
                var proxy = Child(root, "Overload Level Core");
                ctrl.Overload = proxy.transform;
                var shards = new List<Transform>(); var starts = new List<Vector3>();
                var ends = new List<Vector3>(); var rotations = new List<Quaternion>();
                var paths = new List<string>(); var proxies = new List<Transform>();
                var matrices = new List<Matrix4x4>(); var groups = new List<int>();
                var bounds = BlueDestructionBursts.MeshBounds(reference);
                ctrl.Center = bounds.center; ctrl.Lift = bounds.size.y * 0.16f;
                int number = 0;
                foreach (var filter in reference.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    // Keep all source proxies for pose capture. Runtime displays fragments only for
                    // the central crystal; the other proxies become intact falling debris.
                    if (!renderer || !renderer.enabled || !filter.sharedMesh || filter.name != "Mesh") continue;
                    var part = Child(proxy, filter.transform.parent.name);
                    part.transform.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
                    part.transform.localScale = filter.transform.lossyScale;
                    part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    part.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
                    paths.Add(AnimationUtility.CalculateTransformPath(filter.transform, reference.transform));
                    proxies.Add(part.transform);
                    matrices.Add(filter.transform.localToWorldMatrix);
                    if (filter.transform.parent.name != "Core Crystal") { number++; continue; }
                    int first = shards.Count;
                    CobraDestructionBuilder.Fracture(root, filter, renderer, number, shards, starts, ends, rotations,
                        "LevelCoreDestruction", 6, true);
                    for (int i = first; i < shards.Count; i++)
                    {
                        groups.Add(number);
                        float angle = i * 2.4f;
                        float radius = Mathf.Max(bounds.extents.x, bounds.extents.z) * (1.65f + (i % 4) * 0.18f);
                        ends[i] = new Vector3(bounds.center.x + Mathf.Cos(angle) * radius, ends[i].y,
                            bounds.center.z + Mathf.Sin(angle) * radius);
                    }
                    number++;
                }
                ctrl.Shards = shards.ToArray(); ctrl.Starts = starts.ToArray();
                ctrl.Landings = ends.ToArray(); ctrl.LandingRotations = rotations.ToArray();
                ctrl.MeshPaths = paths.ToArray(); ctrl.ProxyMeshes = proxies.ToArray();
                ctrl.ReferenceMatrices = matrices.ToArray(); ctrl.ShardMeshIndices = groups.ToArray();
                BlueDestructionBursts.Add(root, bounds);
                AddCrystalFirework(root, bounds);
                DestructionAudioSetup.Apply(root);
                ctrl.Restart();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[VFX] Level core: {number} original meshes, {shards.Count} textured fragments, 5 blue bursts.");
            }
            finally { Object.DestroyImmediate(root); PrefabUtility.UnloadPrefabContents(reference); }
        }

        static void AddCrystalFirework(GameObject root, Bounds bounds)
        {
            var glow = AssetDatabase.LoadAssetAtPath<Material>(MatGlowWhitePath);
            var sparks = Child(root, "Crystal Firework Sparks");
            sparks.transform.localPosition = bounds.center;
            var ps = AddSystem(sparks, out var renderer);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 2f;
            main.startDelay = BlueDestructionBursts.BreakTime;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(12f, 20f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
            main.gravityModifier = 0.25f;
            main.maxParticles = 72;
            SphereShape(ps, 0.35f);
            Burst(ps, 64);
            UseBillboard(renderer, glow, ParticleSystemRenderMode.Stretch);
            renderer.velocityScale = 0.065f;
            renderer.lengthScale = 2.5f;
            ColorRampFade(ps, Color.white, new Color(0.15f, 1f, 1f), new Color(0.03f, 0.3f, 0.8f));
            Size(ps, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var flash = Child(root, "Crystal Burst Flash");
            flash.transform.localPosition = bounds.center;
            var pulse = AddSystem(flash, out var flashRenderer);
            pulse.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var pulseMain = pulse.main;
            pulseMain.startDelay = BlueDestructionBursts.BreakTime;
            pulseMain.startLifetime = 0.12f;
            pulseMain.startSpeed = 0f;
            pulseMain.startSize = bounds.size.y * 0.7f;
            Burst(pulse, 1);
            UseBillboard(flashRenderer, glow);
            AlphaFade(pulse, new Color(0.65f, 1f, 1f), 0f);
            Size(pulse, AnimationCurve.Linear(0f, 0.4f, 1f, 1.4f));
        }

        public static Bounds WorldBounds(MeshFilter filter)
        {
            var b = filter.sharedMesh.bounds; var result = new Bounds(filter.transform.TransformPoint(b.center), Vector3.zero);
            for (int i = 0; i < 8; i++) result.Encapsulate(filter.transform.TransformPoint(new Vector3(
                (i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z)));
            return result;
        }
    }
}

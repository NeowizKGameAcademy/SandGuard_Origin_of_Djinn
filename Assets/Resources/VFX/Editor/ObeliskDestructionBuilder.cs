using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static DesertTower.VFX.Editor.VfxBuildKit;

namespace DesertTower.VFX.Editor
{
    public static class ObeliskDestructionBuilder
    {
        public const string PrefabPath = PrefabDir + "/VFX_Obelisk_Destruction.prefab";
        public const string BurstPath = PrefabDir + "/VFX_Blue_Partial_Explosion.prefab";
        public const string ReferencePath = "Assets/2.Model/Prefabs/Tower_Obelisk.prefab";
        static readonly Color Blue = new Color(0.08f, 0.38f, 1f);
        static readonly Color Ice = new Color(0.48f, 0.85f, 1f);

        [MenuItem("DesertTower/VFX/Build Obelisk Destruction + Wire")]
        public static void BuildAndWire()
        {
            Build();
            var tower = PrefabUtility.LoadPrefabContents(ReferencePath);
            try
            {
                // The death listener must share TowerHealth's object, not the prefab root.
                var health = FindHealth(tower);
                if (!health) throw new System.InvalidOperationException("Obelisk TowerHealth missing");
                var death = health.GetComponent<VfxDestructionOnDeath>();
                if (!death) death = health.gameObject.AddComponent<VfxDestructionOnDeath>();
                death.Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                death.Target = health.transform;
                death.Lifetime = 4f;
                PrefabUtility.SaveAsPrefabAsset(tower, ReferencePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(tower); }
            AssetDatabase.SaveAssets();
            Debug.Log("OBELISK_VFX_READY: blue partial explosion, staged collapse, death listener wired.");
        }

        public static MonoBehaviour FindHealth(GameObject root)
        {
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (component && component.GetType().FullName == "Tower.TowerHealth") return component;
            return null;
        }

        public static void Build()
        {
            var burst = ImpactVfxBuilder.Build(new ImpactSpec {
                PrefabName = "VFX_Blue_Partial_Explosion",
                FlashSize = 1.25f, FlashColor = new Color(0.75f, 0.93f, 1f),
                CoreCount = 4, CoreSize = 0.17f, CoreA = Ice, CoreB = Blue,
                CoreC = new Color(0.04f, 0.12f, 0.48f),
                Shockwave = true, ShockwaveSize = 1.8f, ShockwaveColor = Ice,
                DebrisCount = 16, DebrisSize = new Vector2(0.035f, 0.09f),
                DebrisSpeed = new Vector2(1.8f, 3.4f), DebrisLife = new Vector2(0.25f, 0.55f),
                DebrisGravity = 0.25f, DebrisA = Ice, DebrisB = Blue,
                DebrisC = new Color(0.03f, 0.1f, 0.35f),
                EmberCount = 8, EmberCubes = true, EmberColor = Blue,
                EmberSize = new Vector2(0.04f, 0.08f), EmberLife = new Vector2(0.3f, 0.65f),
                DustCount = 7, DustColor = new Color(0.22f, 0.38f, 0.6f, 0.48f),
                DustSize = new Vector2(0.3f, 0.65f), DustLife = new Vector2(0.35f, 0.7f),
                DustSpeed = new Vector2(0.25f, 0.6f), LightIntensity = 2.8f, LightRange = 2.5f
            }, BurstPath);

            var reference = PrefabUtility.LoadPrefabContents(ReferencePath);
            var root = new GameObject("VFX_Obelisk_Destruction");
            try
            {
                var ctrl = root.AddComponent<VfxObeliskDestruction>();
                ctrl.Proxy = Child(root, "Intact Obelisk");
                var pieces = new List<Transform>(); var starts = new List<Vector3>();
                var ends = new List<Vector3>(); var rotations = new List<Quaternion>();
                Bounds bounds = default; int number = 0;
                var body = FindHealth(reference);
                if (!body) throw new System.InvalidOperationException("Obelisk TowerHealth missing");
                foreach (var filter in body.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    // Only the stone body and emitter: exclude the gameplay range disc.
                    if (!renderer || !renderer.enabled || !filter.sharedMesh || filter.name != "Mesh") continue;
                    var meshBounds = CoreDestructionBuilder.WorldBounds(filter);
                    if (number == 0) bounds = meshBounds; else bounds.Encapsulate(meshBounds);
                    var proxy = Child(ctrl.Proxy, "Original Mesh " + number);
                    proxy.transform.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
                    proxy.transform.localScale = filter.transform.lossyScale;
                    proxy.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    proxy.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
                    CobraDestructionBuilder.Fracture(root, filter, renderer, number++, pieces, starts, ends, rotations,
                        "ObeliskDestruction", 3);
                }
                if (pieces.Count == 0) throw new System.InvalidOperationException("No obelisk stone meshes found");
                ctrl.Pieces = pieces.ToArray(); ctrl.Starts = starts.ToArray();
                ctrl.Landings = ends.ToArray(); ctrl.Rotations = rotations.ToArray();
                // Four localized pops climb around the body, followed by a larger central break.
                var points = new[] {
                    new Vector3(-0.38f, 0.32f, -0.2f), new Vector3(0.32f, 0.62f, 0.15f),
                    new Vector3(-0.22f, 0.88f, 0.08f), new Vector3(0.24f, 0.44f, -0.35f),
                    new Vector3(0f, 0.55f, 0f)
                };
                float[] times = { 0.04f, 0.21f, 0.38f, 0.55f, ctrl.BurstTime };
                for (int i = 0; i < points.Length; i++)
                {
                    var pop = (GameObject)PrefabUtility.InstantiatePrefab(burst, root.transform);
                    pop.name = i == 4 ? "Final Blue Break" : "Blue Pop " + (i + 1);
                    pop.transform.localPosition = new Vector3(bounds.center.x + points[i].x * bounds.size.x,
                        bounds.min.y + points[i].y * bounds.size.y, bounds.center.z + points[i].z * bounds.size.z);
                    float size = Mathf.Max(0.5f, bounds.size.y * (i == 4 ? 0.48f : 0.22f));
                    pop.transform.localScale = Vector3.one * size;
                    foreach (var ps in pop.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        var main = ps.main;
                        main.startDelay = main.startDelay.constant + times[i];
                        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    }
                }
                ctrl.Restart();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[VFX] Obelisk: " + pieces.Count + " source-mesh fragments, 5 blue detonations.");
            }
            finally { Object.DestroyImmediate(root); PrefabUtility.UnloadPrefabContents(reference); }
        }
    }
}

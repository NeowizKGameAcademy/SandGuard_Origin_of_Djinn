using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static DesertTower.VFX.Editor.VfxBuildKit;

namespace DesertTower.VFX.Editor
{
    public static class SummonerTowerDestructionBuilder
    {
        [MenuItem("DesertTower/VFX/Build Anubis and Coffin Destruction + Preview")]
        public static void BuildAndPreview()
        {
            foreach (string kind in new[] { "Anubis", "Coffin" })
            {
                BuildAndWire(kind);
                Render(kind);
            }
            Debug.Log("SUMMONER_DESTRUCTION_READY: Anubis and Coffin generated, wired and rendered.");
        }

        public static string SourcePath(string kind) => "Assets/2.Model/Prefabs/Tower_" + kind + ".prefab";
        public static string EffectPath(string kind) => PrefabDir + "/VFX_" + kind + "_Destruction.prefab";

        public static void RenderAnubis() => Render("Anubis");

        static void Render(string kind) => ObeliskDestructionPreview.Render(SourcePath(kind), EffectPath(kind),
            "Docs/vfx-preview/" + kind + "Destruction", kind == "Anubis" ? new Vector3(8f, 6f, 10f) : new Vector3(8f, 6f, -10f));

        static void BuildAndWire(string kind)
        {
            var source = PrefabUtility.LoadPrefabContents(SourcePath(kind));
            var root = new GameObject("VFX_" + kind + "_Destruction");
            try
            {
                var body = ObeliskDestructionBuilder.FindHealth(source);
                if (!body) throw new System.InvalidOperationException(kind + " TowerHealth missing");
                // Reuse the proven staged stone-collapse controller for both tower models.
                var ctrl = root.AddComponent<VfxObeliskDestruction>();
                ctrl.Proxy = Child(root, "Intact " + kind);
                ctrl.BurstTime = BlueDestructionBursts.BreakTime;
                var pieces = new List<Transform>(); var starts = new List<Vector3>();
                var ends = new List<Vector3>(); var rotations = new List<Quaternion>();
                Bounds bounds = BlueDestructionBursts.MeshBounds(body.gameObject);
                int number = 0;
                foreach (var filter in body.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (!renderer || !renderer.enabled || !filter.sharedMesh || filter.name != "Mesh") continue;
                    var proxy = Child(ctrl.Proxy, "Original Mesh " + number);
                    proxy.transform.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
                    proxy.transform.localScale = filter.transform.lossyScale;
                    proxy.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    proxy.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
                    CobraDestructionBuilder.Fracture(root, filter, renderer, number++, pieces, starts, ends, rotations,
                        kind + "Destruction", 3);
                }
                if (pieces.Count < 4) throw new System.InvalidOperationException(kind + " fragments missing");
                // Match the spread to each model's footprint while retaining the computed ground height.
                for (int i = 0; i < ends.Count; i++)
                {
                    float angle = i * 2.4f;
                    float radius = Mathf.Max(1.4f, Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.15f) + i % 3 * 0.25f;
                    ends[i] = new Vector3(bounds.center.x + Mathf.Cos(angle) * radius, ends[i].y,
                        bounds.center.z + Mathf.Sin(angle) * radius);
                }
                ctrl.Pieces = pieces.ToArray(); ctrl.Starts = starts.ToArray();
                ctrl.Landings = ends.ToArray(); ctrl.Rotations = rotations.ToArray();
                BlueDestructionBursts.Add(root, bounds);
                DestructionAudioSetup.Apply(root);
                ctrl.Restart();
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, EffectPath(kind));
                var death = body.GetComponent<VfxDestructionOnDeath>();
                if (!death) death = body.gameObject.AddComponent<VfxDestructionOnDeath>();
                death.Prefab = prefab; death.Target = body.transform; death.Lifetime = 4f;
                // Avoid a second generic death burst if an older hit-reaction setup supplied one.
                var hit = body.GetComponent<VfxHitReaction>();
                if (hit) hit.DeathPrefab = null;
                PrefabUtility.SaveAsPrefabAsset(source, SourcePath(kind));
                AssetDatabase.SaveAssets();
                Debug.Log($"[VFX] {kind}: {number} original meshes, {pieces.Count} textured fragments; death wired.");
            }
            finally { Object.DestroyImmediate(root); PrefabUtility.UnloadPrefabContents(source); }
        }
    }
}

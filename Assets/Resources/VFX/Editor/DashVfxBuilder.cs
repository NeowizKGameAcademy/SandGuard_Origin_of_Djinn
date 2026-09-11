using UnityEditor;
using UnityEngine;
using SandGuard.Player;
using static DesertTower.VFX.Editor.VfxBuildKit;

namespace DesertTower.VFX.Editor
{
    public static class DashVfxBuilder
    {
        public const string LaunchPath = PrefabDir + "/VFX_Dash_SonicBoom.prefab";
        public const string AirflowPath = PrefabDir + "/VFX_Dash_Airflow.prefab";
        static readonly Color Air = new Color(1f, .89f, .67f, .52f);

        [MenuItem("DesertTower/VFX/Build and Connect Sand Dash")]
        public static void BuildAndWire()
        {
            var material = BuildMaterial(MatDir + "/M_VFX_Dash_Air.mat", null, Color.white, false, false);
            var ringMesh = MeshAsset("DashRing", true);
            var ribbonMesh = MeshAsset("DashRibbon", false);
            var launch = new GameObject("VFX_Dash_SonicBoom");
            AddHub(launch);
            var ring = AddSystem(Child(launch, "CompressionRing"), out var rr);
            Configure(ring, .28f, 1f, 1f);
            Burst(ring, 1); UseMesh(rr, ringMesh, material); rr.alignment = ParticleSystemRenderSpace.Local;
            Size(ring, EaseOut(.65f, 1.8f)); AlphaFade(ring, Color.white, .12f);
            var grains = AddSystem(Child(launch, "SandEdge"), out var gr);
            Configure(grains, .36f, .015f, .035f);
            Burst(grains, 28); CircleShape(grains, .68f, .05f);
            var gm = grains.main; gm.startColor = new Color(.85f, .64f, .31f, .8f); gm.startSpeed = .65f;
            gm.gravityModifier = .08f; RandomRotation3D(gm);
            UseMesh(gr, Resources.GetBuiltinResource<Mesh>("Cube.fbx"), material);
            AlphaFade(grains, Color.white, .3f);
            SavePrefab(launch, LaunchPath);

            var flow = new GameObject("VFX_Dash_Airflow"); AddHub(flow);
            for (int i = 0; i < 3; i++)
            {
                var go = Child(flow, "Slipstream" + (i + 1));
                go.transform.localPosition = i == 0 ? new Vector3(-.36f, .32f, 0) : i == 1 ? new Vector3(.36f, .15f, -.1f) : new Vector3(-.24f, -.35f, 0);
                go.transform.localRotation = Quaternion.Euler(0, 0, i * 115f);
                var ps = AddSystem(go, out var r, true);
                Configure(ps, .17f, .9f, 1.1f); Rate(ps, 7f); Burst(ps, 1);
                UseMesh(r, ribbonMesh, material); r.alignment = ParticleSystemRenderSpace.Local;
                Size(ps, InOut(.12f, .5f)); AlphaFade(ps, Color.white, .25f);
            }
            SavePrefab(flow, AirflowPath);
            const string playerPath = "Assets/Player/Generated/Player.prefab";
            var player = PrefabUtility.LoadPrefabContents(playerPath);
            try
            {
                var vfx = player.GetComponent<PlayerDashVfx>();
                if (vfx == null) vfx = player.AddComponent<PlayerDashVfx>();
                vfx.launchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LaunchPath);
                vfx.airflowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AirflowPath);
                PrefabUtility.SaveAsPrefabAsset(player, playerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            AssetDatabase.SaveAssets();
        }

        static void Configure(ParticleSystem ps, float lifetime, float minSize, float maxSize)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.duration = .3f; main.startLifetime = lifetime;
            main.startSpeed = 0f; main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = Air; main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 40;
        }

        static Mesh MeshAsset(string name, bool ring)
        {
            const int segments = 80;
            var vertices = new Vector3[(segments + 1) * 2];
            var colors = new Color[vertices.Length];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                if (ring)
                {
                    float a = t * Mathf.PI * 2f;
                    var radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                    float width = .015f + .03f * Mathf.Pow(.5f + .5f * Mathf.Sin(a * 5f), 2f);
                    vertices[i * 2] = radial * (.7f - width);
                    vertices[i * 2 + 1] = radial * (.7f + width);
                    float alpha = Mathf.Clamp01((Mathf.Sin(a * 3f + .4f) + .65f) * 2f);
                    colors[i * 2] = new Color(1, 1, 1, alpha); colors[i * 2 + 1] = new Color(1, 1, 1, 0);
                }
                else
                {
                    var center = new Vector3(Mathf.Sin(t * 5f) * .1f, .04f * Mathf.Sin(t * 4f), .15f - t * 1.7f);
                    float width = Mathf.Sin(t * Mathf.PI) * .035f;
                    vertices[i * 2] = center - Vector3.right * width;
                    vertices[i * 2 + 1] = center + Vector3.right * width;
                    colors[i * 2] = colors[i * 2 + 1] = new Color(1, 1, 1, Mathf.Sin(t * Mathf.PI));
                }
                if (i == segments) continue;
                int k = i * 6, v = i * 2;
                triangles[k] = v; triangles[k + 1] = v + 2; triangles[k + 2] = v + 1;
                triangles[k + 3] = v + 1; triangles[k + 4] = v + 2; triangles[k + 5] = v + 3;
            }
            string path = RootDir + "/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
            mesh.Clear(); mesh.vertices = vertices; mesh.colors = colors; mesh.triangles = triangles;
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}

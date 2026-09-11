using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static DesertTower.VFX.Editor.VfxBuildKit;

namespace DesertTower.VFX.Editor
{
    public static class SandRootVfxBuilder
    {
        public const string Path = PrefabDir + "/VFX_Sand_Root.prefab";

        [MenuItem("DesertTower/VFX/Build Sand Root")]
        public static void Build()
        {
            var smoke = AssetDatabase.LoadAssetAtPath<Material>(MatSmokePath);
            if (smoke == null) throw new System.InvalidOperationException("Missing shared VFX smoke material.");
            var grains = BuildMaterial(MatDir + "/M_VFX_Sand_Root_Grain.mat", null, Color.white, false, false);
            var root = new GameObject("VFX_Sand_Root");
            AddHub(root);
            var sand = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/M_VFX_Sand_Root_Mass.mat");
            if (sand == null) { sand = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(sand, MatDir + "/M_VFX_Sand_Root_Mass.mat"); }
            sand.SetColor("_BaseColor", new Color(.58f, .38f, .16f));
            sand.SetFloat("_Smoothness", 0f);
            EditorUtility.SetDirty(sand);
            var mass = Child(root, "BindingMass");
            BuildCoil(mass, sand, "LeftAnkle", -.18f);
            BuildCoil(mass, sand, "RightAnkle", .18f);
            var baseGo = Child(mass, "GroundAnchor");
            baseGo.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            baseGo.AddComponent<MeshRenderer>().sharedMaterial = sand;
            baseGo.transform.localScale = new Vector3(1.05f, .14f, .73f);
            baseGo.transform.localPosition = Vector3.up * .025f;

            var dust = Layer(root, "AnkleSand", .1f, .43f, 12f, .4f, .7f, .06f, .12f, out var dr);
            UseBillboard(dr, smoke);
            RandomTile(dust, 2, 2);
            Swirl(dust, .07f, 1.4f);
            Noise(dust, .035f, 1.8f, .3f);

            var skirt = Layer(root, "GroundDust", .055f, .53f, 14f, .45f, .7f, .08f, .16f, out var sr);
            UseBillboard(sr, smoke);
            RandomTile(skirt, 2, 2);
            Swirl(skirt, .015f, -.6f);

            var grit = Layer(root, "BindingGrains", .09f, .36f, 100f, .45f, .8f, .012f, .025f, out var gr);
            UseMesh(gr, Resources.GetBuiltinResource<Mesh>("Cube.fbx"), grains);
            var gm = grit.main;
            RandomRotation3D(gm);
            Swirl(grit, .25f, 3.5f);
            Tumble(grit, 2f);
            Burst(grit, 45);
            root.AddComponent<VfxSandRoot>();
            SavePrefab(root, Path);
        }

        static void BuildCoil(GameObject parent, Material material, string name, float x)
        {
            const int segments = 100, sides = 7;
            var vertices = new Vector3[(segments + 1) * sides];
            var triangles = new int[segments * sides * 6];
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments, a = t * Mathf.PI * 5.4f;
                float radius = Mathf.Lerp(.24f, .15f, t);
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var center = radial * radius + Vector3.up * (.07f + .36f * t);
                for (int j = 0; j < sides; j++)
                {
                    float b = j * Mathf.PI * 2f / sides;
                    float rough = 1f + .09f * Mathf.Sin(i * 2.7f + j * 3.1f);
                    vertices[i * sides + j] = center + (radial * Mathf.Cos(b) * 1.3f + Vector3.up * Mathf.Sin(b) * .72f) * (.065f * rough);
                    if (i == segments) continue;
                    int p = i * sides + j, q = i * sides + (j + 1) % sides, k = (i * sides + j) * 6;
                    triangles[k] = p; triangles[k + 1] = q; triangles[k + 2] = p + sides;
                    triangles[k + 3] = q; triangles[k + 4] = q + sides; triangles[k + 5] = p + sides;
                }
            }
            string meshPath = RootDir + "/SandRootCoil.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, meshPath); }
            mesh.Clear(); mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
            var go = Child(parent, name); go.transform.localPosition = Vector3.right * x;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        static ParticleSystem Layer(GameObject root, string name, float height, float radius, float rate,
            float lifeMin, float lifeMax, float sizeMin, float sizeMax, out ParticleSystemRenderer renderer)
        {
            var go = Child(root, name);
            go.transform.localPosition = Vector3.up * height;
            var ps = AddSystem(go, out renderer, true);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 2f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.55f, .35f, .14f), new Color(.86f, .66f, .34f));
            main.maxParticles = 180;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Rate(ps, rate);
            CircleShape(ps, radius, .85f);
            var shape = ps.shape; shape.rotation = new Vector3(90f, 0f, 0f);
            Size(ps, InOut(.14f, .6f));
            AlphaFade(ps, Color.white, .45f);
            return ps;
        }

        static void Swirl(ParticleSystem ps, float rise, float rotation)
        {
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.y = rise;
            velocity.orbitalY = rotation;
        }

        // A close character-scale render, isolated from the user's scenes.
        public static void BuildAndPreview()
        {
            Build();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(.18f, .21f, .25f);
            ground.GetComponent<Renderer>().sharedMaterial = mat;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Art/Protagonist/ProtagonistVisual.prefab");
            if (model != null) Object.Instantiate(model, Vector3.zero, Quaternion.identity);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.5f;
            sun.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            RenderSettings.ambientLight = Color.gray;
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            camera.transform.position = new Vector3(2.1f, 1.65f, 2.8f);
            camera.transform.LookAt(new Vector3(0f, .65f, 0f));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.055f, .07f, .10f);
            var rt = new RenderTexture(960, 960, 24);
            camera.targetTexture = rt;
            var fx = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Path));
            foreach (float time in new[] { .12f, .65f, 2f })
            {
                foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>()) ps.Simulate(time, false, true);
                camera.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(960, 960, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 960, 960), 0, 0); tex.Apply();
                Directory.CreateDirectory("Logs/sand-root-preview");
                File.WriteAllBytes("Logs/sand-root-preview/root-" + time.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ".png", tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            RenderTexture.active = null; camera.targetTexture = null;
            rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(mat);
            Debug.Log("[SandRoot] Build and character preview complete.");
        }
    }
}

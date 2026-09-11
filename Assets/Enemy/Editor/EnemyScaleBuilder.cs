using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace SandGuard.Enemy.Editor
{
    public static class EnemyScaleBuilder
    {
        public static readonly string[] Names = { "Swordsman", "Assassin", "ShieldGuard", "HammerBrute", "Chief" };
        public static readonly float[] Ratios = { 1f, 1f, 1.5f, 1.5f, 2f };
        const string Hero = "Assets/Player/Art/Protagonist/Protagonist.fbx";
        public static float HeroHeight => Measure(AssetDatabase.LoadAssetAtPath<GameObject>(Hero));

        static float Measure(GameObject prefab)
        {
            if (prefab == null) throw new InvalidOperationException("Missing size reference prefab.");
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                foreach (var animator in instance.GetComponentsInChildren<Animator>())
                { animator.Rebind(); animator.Update(0f); animator.enabled = false; }
                var body = instance.GetComponentsInChildren<SkinnedMeshRenderer>().OrderByDescending(r => r.bounds.size.y).First();
                // FBX meshes can use rotated, centimetre-based local axes. Measure in world space.
                return body.bounds.size.y;
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        public static void Configure(GameObject root, string name)
        {
            int index = Array.IndexOf(Names, name);
            if (index < 0) throw new ArgumentException(name);
            var visuals = root.GetComponent<EnemyVisuals>();
            float target = HeroHeight * Ratios[index];
            float source = Measure(visuals.visualPrefab);
            if (source < .5f || source > 4f || target < .5f || target > 6f)
                throw new InvalidOperationException("Invalid world-space body height: " + name);
            visuals.localScale = Vector3.one * (target / source);
            var capsule = root.GetComponent<CapsuleCollider>();
            if (capsule != null) { capsule.height = target; capsule.radius = target * .19f; capsule.center = Vector3.up * target * .5f; }
            var agent = root.GetComponent<NavMeshAgent>();
            if (agent != null) { agent.height = target; agent.radius = target * .19f; agent.baseOffset = 0; }
            Debug.Log($"[EnemyScale] {name}: hero={HeroHeight:F3}m target={target:F3}m ratio={Ratios[index]} visualScale={visuals.localScale.x:F4}");
        }

        [MenuItem("SandGuard/Enemy/Apply Player Relative Sizes")]
        public static void Apply()
        {
            ApplyPrefab(EnemyCombatArtBuilder.BasePrefab, "Swordsman");
            foreach (var name in Names) ApplyPrefab(EnemyCombatArtBuilder.VariantPath(name), name);
            AssetDatabase.SaveAssets();
            BuildPreview();
        }

        static void ApplyPrefab(string path, string name)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Configure(root, name); root.GetComponent<EnemyVisuals>().RebuildVisual();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void BuildPreview()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var hero = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Art/Protagonist/ProtagonistVisual.prefab"));
            Pose(hero, new Vector3(-7f, 0, 0)); Label("PLAYER / 1x", -7f);
            for (int i = 0; i < Names.Length; i++)
            {
                var enemy = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyCombatArtBuilder.VariantPath(Names[i])));
                // This scene is a stationary comparison, not a combat arena.
                foreach (var b in enemy.GetComponentsInChildren<MonoBehaviour>()) b.enabled = false;
                enemy.GetComponent<NavMeshAgent>().enabled = false;
                Pose(enemy, new Vector3(-4.4f + i * 2.9f, 0, 0)); Label(Names[i] + " / " + Ratios[i] + "x", enemy.transform.position.x);
                float ratio = enemy.GetComponent<EnemyVisuals>().localScale.x * Measure(enemy.GetComponent<EnemyVisuals>().visualPrefab) / HeroHeight;
                if (Mathf.Abs(ratio - Ratios[i]) > .005f) throw new InvalidOperationException("Scale verification failed: " + Names[i]);
            }
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); ground.transform.localScale = Vector3.one * 3;
            var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.3f; sun.transform.rotation = Quaternion.Euler(45, -25, 0);
            RenderSettings.ambientLight = Color.gray;
            var camera = new GameObject("Size comparison camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(.25f, 3.8f, 19f); camera.transform.LookAt(new Vector3(.25f, 1.5f, 0));
            camera.orthographic = true; camera.orthographicSize = 3.45f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.17f,.20f,.24f);
            EditorSceneManager.SaveScene(scene, "Assets/Enemy/Generated/EnemyScalePreview.unity");
            var rt = new RenderTexture(1800, 700, 24); var tex = new Texture2D(1800, 700, TextureFormat.RGB24, false);
            var old = RenderTexture.active;
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1800, 700), 0, 0); tex.Apply();
                Directory.CreateDirectory("Logs/enemy-scale"); File.WriteAllBytes("Logs/enemy-scale/comparison.png", tex.EncodeToPNG());
            }
            finally { camera.targetTexture = null; RenderTexture.active = old; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex); }
        }

        static void Pose(GameObject go, Vector3 position)
        {
            go.transform.position = position;
            foreach (var animator in go.GetComponentsInChildren<Animator>())
            { animator.Rebind(); animator.Update(0f); animator.enabled = false; }
        }
        static void Label(string text, float x)
        {
            var label = new GameObject(text).AddComponent<TextMesh>(); label.text = text; label.fontSize = 48;
            label.characterSize = .075f; label.anchor = TextAnchor.MiddleCenter;
            label.transform.position = new Vector3(x, -.02f, .65f); label.transform.rotation = Quaternion.Euler(75, 180, 0);
        }
    }
}

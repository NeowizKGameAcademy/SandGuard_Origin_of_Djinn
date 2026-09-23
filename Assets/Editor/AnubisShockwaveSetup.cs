using System.IO;
using DesertTower.VFX.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AnubisShockwaveSetup
{
    public const string EffectPath = "Assets/Resources/VFX/Prefabs/VFX_Anubis_Shockwave.prefab";
    [MenuItem("SandGuard/Anubis/Build Shockwave")]
    public static void Build()
    {
        var spec = ImpactVfxBuilder.SandBurst();
        spec.PrefabName = "VFX_Anubis_Shockwave";
        spec.Scale = 1f;
        spec.ShockwaveSize = 20f;
        spec.ShockwaveCount = 2;
        spec.ShockwaveInterval = 0.1f;
        spec.FlashSize = 3f;
        spec.DebrisCount = 36;
        spec.DebrisSpeed = new Vector2(5f, 8f);
        spec.DebrisLife = new Vector2(0.6f, 1f);
        spec.EmberCount = 24;
        spec.EmberSpeed = new Vector2(3f, 5f);
        spec.DustCount = 18;
        spec.DustSize = new Vector2(1f, 2f);
        spec.DustSpeed = new Vector2(2f, 4f);
        spec.LightRange = 8f;
        var effect = ImpactVfxBuilder.Build(spec, EffectPath);
        const string path = "Assets/2.Model/Prefabs/Anubis.prefab";
        var unit = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var skill = unit.GetComponent<Tower.AnubisSkill>();
            skill.shockwaveVfx = effect;
            skill.vfxLifetime = 3f;
            PrefabUtility.SaveAsPrefabAsset(unit, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(unit); }
        AssetDatabase.SaveAssets();
    }

    public static void BuildAndPreview()
    {
        Build();
        var scene = EditorSceneManager.NewPreviewScene();
        var previous = RenderTexture.active;
        RenderTexture rt = null;
        Texture2D pixels = null, sheet = null;
        Material groundMaterial = null;
        try
        {
            var camera = new GameObject("Preview Camera").AddComponent<Camera>();
            SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.04f, 0.055f);
            camera.transform.position = new Vector3(17f, 20f, -23f);
            camera.transform.LookAt(Vector3.up);
            camera.orthographic = true; camera.orthographicSize = 13f;
            var light = new GameObject("Key").AddComponent<Light>();
            SceneManager.MoveGameObjectToScene(light.gameObject, scene);
            light.type = LightType.Directional; light.intensity = 1.5f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(ground, scene);
            ground.transform.position = Vector3.down * 0.12f;
            ground.transform.localScale = new Vector3(28f, 0.2f, 28f);
            groundMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            groundMaterial.color = new Color(0.25f, 0.20f, 0.14f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;
            var unit = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/2.Model/Prefabs/Anubis.prefab"));
            SceneManager.MoveGameObjectToScene(unit, scene);
            unit.transform.position = Vector3.zero;
            float bottom = float.PositiveInfinity;
            foreach (var renderer in unit.GetComponentsInChildren<Renderer>())
                bottom = Mathf.Min(bottom, renderer.bounds.min.y);
            if (!float.IsInfinity(bottom)) unit.transform.position += Vector3.up * -bottom;
            var effect = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EffectPath));
            SceneManager.MoveGameObjectToScene(effect, scene);
            var rootParticles = effect.GetComponent<ParticleSystem>();
            rt = new RenderTexture(640, 480, 24);
            pixels = new Texture2D(640, 480, TextureFormat.RGB24, false);
            sheet = new Texture2D(1280, 960, TextureFormat.RGB24, false);
            camera.targetTexture = rt;
            float[] times = { 0.06f, 0.18f, 0.35f, 0.8f };
            for (int i = 0; i < times.Length; i++)
            {
                rootParticles.Simulate(times[i], true, true, true);
                camera.Render();
                RenderTexture.active = rt;
                pixels.ReadPixels(new Rect(0, 0, 640, 480), 0, 0); pixels.Apply();
                sheet.SetPixels((i % 2) * 640, (1 - i / 2) * 480, 640, 480, pixels.GetPixels());
            }
            sheet.Apply();
            Directory.CreateDirectory("Docs/vfx-preview");
            File.WriteAllBytes("Docs/vfx-preview/AnubisShockwave.png", sheet.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            EditorSceneManager.ClosePreviewScene(scene);
            if (rt) Object.DestroyImmediate(rt);
            if (pixels) Object.DestroyImmediate(pixels);
            if (sheet) Object.DestroyImmediate(sheet);
            if (groundMaterial) Object.DestroyImmediate(groundMaterial);
        }
    }
}

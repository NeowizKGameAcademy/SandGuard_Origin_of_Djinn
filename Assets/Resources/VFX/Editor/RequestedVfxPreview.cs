using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DesertTower.VFX.Editor
{
    public static class RequestedVfxPreview
    {
        [MenuItem("DesertTower/VFX/Preview Requested Set (8, No UI)")]
        public static void Render()
        {
            string output = "Docs/vfx-preview";
            Directory.CreateDirectory(output);
            var scene = EditorSceneManager.NewPreviewScene();
            var owned = new System.Collections.Generic.List<Object>();
            try
            {
                var camera = new GameObject("VFXPreviewCamera").AddComponent<Camera>();
                SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.035f, 0.04f, 0.055f);
                camera.transform.position = new Vector3(3.4f, 3.1f, -5.8f);
                camera.transform.LookAt(new Vector3(0f, 0.7f, 0f));
                camera.orthographic = true; camera.orthographicSize = 2.2f; camera.allowHDR = true;
                camera.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
                var volume = new GameObject("Bloom").AddComponent<Volume>();
                SceneManager.MoveGameObjectToScene(volume.gameObject, scene);
                volume.isGlobal = true;
                var profile = ScriptableObject.CreateInstance<VolumeProfile>(); owned.Add(profile);
                var bloom = profile.Add<Bloom>(true); bloom.threshold.value = 0.9f; bloom.intensity.value = 0.65f;
                volume.sharedProfile = profile;
                var light = new GameObject("Key").AddComponent<Light>();
                SceneManager.MoveGameObjectToScene(light.gameObject, scene);
                light.type = LightType.Directional; light.intensity = 1.3f;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                var groundMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); owned.Add(groundMat);
                groundMat.color = new Color(0.25f, 0.2f, 0.14f);
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                SceneManager.MoveGameObjectToScene(ground, scene);
                ground.transform.position = new Vector3(0f, -0.08f, 0f);
                ground.transform.localScale = new Vector3(12f, 0.1f, 12f);
                ground.GetComponent<Renderer>().sharedMaterial = groundMat;
                var rt = new RenderTexture(640, 480, 24, RenderTextureFormat.ARGBHalf); owned.Add(rt);
                var pixels = new Texture2D(640, 480, TextureFormat.RGB24, false); owned.Add(pixels);
                var sheet = new Texture2D(2560, 960, TextureFormat.RGB24, false); owned.Add(sheet);
                camera.targetTexture = rt;
                for (int index = 0; index < RequestedVfxBuilder.Paths.Length; index++)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RequestedVfxBuilder.Paths[index]);
                    if (prefab == null) throw new Exception("Missing prefab: " + RequestedVfxBuilder.Paths[index]);
                    if (prefab.GetComponentInChildren<Canvas>(true) != null) throw new Exception("UI found: " + prefab.name);
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab) != 0) throw new Exception("Missing script: " + prefab.name);
                    foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                    {
                        if (!renderer.enabled) continue; // Empty particle hub is deliberately invisible.
                        foreach (var mat in renderer.sharedMaterials)
                            if (mat == null || mat.shader == null || !mat.shader.isSupported) throw new Exception("Invalid material: " + prefab.name);
                    }
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    GameObject target = null;
                    Renderer[] targetRenderers = null;
                    Material[][] originalMaterials = null;
                    try
                    {
                        camera.orthographicSize = 2.2f;
                        camera.transform.position = new Vector3(3.4f, 3.1f, -5.8f);
                        camera.transform.LookAt(new Vector3(0f, 0.7f, 0f));
                        if (index == 0 || index == 1 || index == 2) instance.transform.position = new Vector3(0f, 1f, -0.4f);
                        if (index == 4)
                        {
                            target = VfxShowcaseBuilder.CobraReference(Vector3.zero);
                            SceneManager.MoveGameObjectToScene(target, scene);
                            var bounds = RequestedVfxBuilder.CobraBounds();
                            camera.orthographicSize = Mathf.Max(bounds.size.y, Mathf.Max(bounds.size.x, bounds.size.z)) * 0.85f;
                            camera.transform.position = bounds.center + new Vector3(3.4f, 2.4f, -5.8f);
                            camera.transform.LookAt(bounds.center);
                            targetRenderers = target.GetComponentsInChildren<Renderer>(true);
                            originalMaterials = new Material[targetRenderers.Length][];
                            for (int r = 0; r < targetRenderers.Length; r++) originalMaterials[r] = targetRenderers[r].sharedMaterials;
                            instance.GetComponent<VfxDisabledVisual>().BindTarget(target.transform);
                        }
                        var systems = instance.GetComponentsInChildren<ParticleSystem>();
                        var arcs = instance.GetComponentsInChildren<VfxArcPulse>();
                        var lightning = instance.GetComponentsInChildren<VfxLightningPath>();
                        foreach (var path in lightning) path.Restart();
                        foreach (var arc in arcs) arc.Restart();
                        foreach (var ps in systems) { ps.useAutoRandomSeed = false; ps.randomSeed = 123; ps.Simulate(0f, false, true); }
                        float time = index == 0 ? 0.08f : index == 3 ? 0.2f : index == 4 || index == 6 ? 0.65f : index == 7 ? 0.4f : 0.08f;
                        for (float t = 0f; t < time; t += 1f / 60f)
                        {
                            float dt = Mathf.Min(1f / 60f, time - t);
                            foreach (var ps in systems) ps.Simulate(dt, false, false, false);
                            foreach (var arc in arcs) arc.Tick(dt);
                            foreach (var path in lightning) path.Tick(dt);
                            foreach (var spin in instance.GetComponentsInChildren<VfxSpin>()) spin.Tick(dt);
                        }
                        camera.Render();
                        var active = RenderTexture.active;
                        try { RenderTexture.active = rt; pixels.ReadPixels(new Rect(0, 0, 640, 480), 0, 0); pixels.Apply(); }
                        finally { RenderTexture.active = active; }
                        File.WriteAllBytes(Path.Combine(output, $"{index + 1:00}_{prefab.name}.png"), pixels.EncodeToPNG());
                        sheet.SetPixels((index % 4) * 640, (1 - index / 4) * 480, 640, 480, pixels.GetPixels());
                        // Pool reuse and effect lifetimes: flashes end, loops survive, tint restores.
                        foreach (var arc in arcs)
                        {
                            arc.Tick(2f);
                            if (!arc.Loop && arc.GetComponent<LineRenderer>().enabled) throw new Exception("Flash did not end");
                            arc.Restart();
                            if (!arc.GetComponent<LineRenderer>().enabled) throw new Exception("Flash did not restart");
                        }
                        if (index == 4)
                        {
                            CheckLightning(instance, lightning);
                            RenderLightningSequence(instance, lightning, systems, camera, rt, pixels, output);
                            instance.SetActive(false);
                            // Edit-mode preview does not dispatch normal MonoBehaviour lifecycle callbacks.
                            instance.GetComponent<VfxDisabledVisual>().Restore();
                            for (int r = 0; r < targetRenderers.Length; r++)
                            {
                                var restored = targetRenderers[r].sharedMaterials;
                                for (int m = 0; m < restored.Length; m++)
                                    if (restored[m] != originalMaterials[r][m]) throw new Exception("Cobra tint did not restore");
                            }
                        }
                        if (index == 6 && instance.GetComponentInChildren<MeshRenderer>() == null) throw new Exception("XP body missing");
                    }
                    finally
                    {
                        var tint = instance.GetComponent<VfxDisabledVisual>();
                        if (tint != null) tint.Restore();
                        Object.DestroyImmediate(instance);
                        if (target != null) Object.DestroyImmediate(target);
                    }
                }
                sheet.Apply(); File.WriteAllBytes(Path.Combine(output, "RequestedVFX_Overview.png"), sheet.EncodeToPNG());
                File.WriteAllText(Path.Combine(output, "validation.txt"), "PASS: 8 prefabs; materials/shaders; no Canvas; arc end/restart; Cobra tint restoration; persistent XP body; sphere paths; moving discharge head; quiet interval; restart; 24 lightning animation frames.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                foreach (var value in owned) if (value != null) Object.DestroyImmediate(value);
            }
        }

        static void CheckLightning(GameObject instance, VfxLightningPath[] paths)
        {
            var guide = instance.GetComponent<VfxLightningGuide>();
            if (guide == null || paths.Length != 15) throw new Exception("Expected five main paths and ten forks");
            foreach (var path in paths)
                foreach (var point in path.Points)
                    if (Mathf.Abs(Vector3.Distance(point, guide.Center) - guide.Radius) > 0.001f)
                        throw new Exception("Lightning path left the spherical guide");
            var first = paths[0];
            first.Restart(); first.Tick(0.08f);
            var start = first.HeadPosition;
            first.Tick(0.08f);
            if (!first.IsDischarging || Vector3.Distance(start, first.HeadPosition) < 0.1f)
                throw new Exception("Discharge is not moving");
            first.Tick(0.5f);
            if (first.IsDischarging || first.Glow.enabled || first.Core.enabled) throw new Exception("Dormant path remains visible");
            first.Restart(); first.Tick(0.08f);
            if (Vector3.Distance(start, first.HeadPosition) > 0.001f) throw new Exception("Discharge restart failed");
        }

        static void RenderLightningSequence(GameObject instance, VfxLightningPath[] paths, ParticleSystem[] systems,
            Camera camera, RenderTexture rt, Texture2D pixels, string output)
        {
            string folder = Path.Combine(output, "Lightning"); Directory.CreateDirectory(folder);
            foreach (var ps in systems) ps.Simulate(0f, false, true, false);
            foreach (var path in paths) path.Restart();
            for (int frame = 0; frame < 24; frame++)
            {
                for (int sub = 0; sub < 2; sub++)
                {
                    foreach (var ps in systems) ps.Simulate(0.02f, false, false, false);
                    foreach (var path in paths) path.Tick(0.02f);
                }
                camera.Render();
                var active = RenderTexture.active;
                try { RenderTexture.active = rt; pixels.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); pixels.Apply(); }
                finally { RenderTexture.active = active; }
                File.WriteAllBytes(Path.Combine(folder, $"frame-{frame:00}.png"), pixels.EncodeToPNG());
            }
        }
    }
}

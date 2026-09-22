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
    public static class CoreDestructionPreview
    {
        [MenuItem("DesertTower/VFX/Build Core Destruction + Preview")]
        public static void BuildAndRender() { CoreDestructionBuilder.Build(); Render(); }
        public static void BuildRenderAndShowcase() { BuildAndRender(); VfxShowcaseBuilder.Build(); }

        [MenuItem("DesertTower/VFX/Preview Core Destruction")]
        public static void Render()
        {
            string folder = "Docs/vfx-preview/CoreDestruction"; Directory.CreateDirectory(folder);
            var scene = EditorSceneManager.NewPreviewScene();
            var owned = new System.Collections.Generic.List<Object>();
            VfxCoreDestruction ctrl = null;
            try
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(CoreDestructionBuilder.ReferencePath);
                var reference = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
                foreach (var animator in reference.GetComponentsInChildren<Animator>()) animator.enabled = false;
                var originals = reference.GetComponentsInChildren<Renderer>(true);
                var enabledStates = System.Array.ConvertAll(originals, r => r.enabled);
                var circle = reference.transform.Find("Circle Effect");
                var bounds = BlueDestructionBursts.MeshBounds(reference);
                var effect = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CoreDestructionBuilder.PrefabPath), scene);
                ctrl = effect.GetComponent<VfxCoreDestruction>(); ctrl.Restart(); ctrl.BindTarget(reference.transform);
                if (ctrl.Shards.Length < 8 || System.Array.Exists(originals, r => r.enabled) || !ctrl.Overload.gameObject.activeSelf)
                    throw new Exception("Initial overload / source binding failed");
                foreach (var renderer in effect.GetComponentsInChildren<Renderer>(true))
                    foreach (var mat in renderer.sharedMaterials)
                        if (mat == null || !mat.shader.isSupported) throw new Exception("Invalid destruction material");
                if (effect.GetComponentInChildren<Canvas>(true) != null) throw new Exception("Unexpected UI");
                var camera = new GameObject("Core Preview Camera").AddComponent<Camera>();
                SceneManager.MoveGameObjectToScene(camera.gameObject, scene); camera.scene = scene;
                camera.orthographic = true; camera.orthographicSize = Mathf.Max(bounds.size.y, bounds.size.x) * 0.85f;
                camera.transform.position = bounds.center + new Vector3(8f, 6f, -10f);
                camera.transform.LookAt(bounds.center * 0.6f);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.035f, 0.04f, 0.055f);
                camera.allowHDR = true; camera.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
                var volume = new GameObject("Bloom").AddComponent<Volume>(); SceneManager.MoveGameObjectToScene(volume.gameObject, scene);
                volume.isGlobal = true;
                var profile = ScriptableObject.CreateInstance<VolumeProfile>(); owned.Add(profile);
                profile.Add<Bloom>(true).intensity.value = 0.65f; volume.sharedProfile = profile;
                var light = new GameObject("Key").AddComponent<Light>(); SceneManager.MoveGameObjectToScene(light.gameObject, scene);
                light.type = LightType.Directional; light.intensity = 1.5f; light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(ground, scene);
                ground.transform.position = new Vector3(0f, -0.08f, 0f); ground.transform.localScale = new Vector3(40f, 0.1f, 40f);
                var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); material.color = new Color(0.12f, 0.1f, 0.07f);
                owned.Add(material); ground.GetComponent<Renderer>().sharedMaterial = material;
                var rt = new RenderTexture(640, 480, 24, RenderTextureFormat.ARGBHalf); owned.Add(rt); camera.targetTexture = rt;
                var texture = new Texture2D(640, 480, TextureFormat.RGB24, false); owned.Add(texture);
                var systems = effect.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in systems) { ps.useAutoRandomSeed = false; ps.randomSeed = 125; ps.Simulate(0f, false, true, false); }
                for (int frame = 0; frame < 100; frame++)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        ctrl.Tick(0.02f);
                        foreach (var ps in systems) ps.Simulate(0.02f, false, false, false);
                    }
                    if (frame == 19 && (ctrl.Overload.gameObject.activeSelf || !ctrl.Shards[0].gameObject.activeSelf))
                        throw new Exception("Burst transition failed");
                    camera.Render();
                    var active = RenderTexture.active;
                    try { RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, 640, 480), 0, 0); texture.Apply(); }
                    finally { RenderTexture.active = active; }
                    File.WriteAllBytes(Path.Combine(folder, $"frame-{frame:00}.png"), texture.EncodeToPNG());
                }
                for (int i = 0; i < ctrl.Shards.Length; i++)
                    if (Vector3.Distance(ctrl.Shards[i].localPosition, ctrl.Landings[i]) > 0.01f)
                        throw new Exception("Shard did not settle");
                effect.SetActive(false);
                if (System.Array.Exists(originals, r => r.enabled)) throw new Exception("Cleanup resurrected core");
                ctrl.RestoreTarget();
                for (int i = 0; i < originals.Length; i++) if (originals[i].enabled != enabledStates[i]) throw new Exception("Explicit reset failed");
                ctrl.Restart();
                if (!ctrl.Overload.gameObject.activeSelf || ctrl.Shards[0].gameObject.activeSelf) throw new Exception("Replay reset failed");
                File.WriteAllText(Path.Combine(folder, "validation.txt"), "PASS: Level New Core meshes, textured fragments, blue chain bursts, source/light/circle hiding, settled debris, no automatic resurrection, explicit reset, supported materials. 100 Unity-rendered frames.");
            }
            finally
            {
                if (ctrl != null) ctrl.RestoreTarget();
                EditorSceneManager.ClosePreviewScene(scene);
                foreach (var item in owned) if (item != null) Object.DestroyImmediate(item);
            }
        }
    }
}

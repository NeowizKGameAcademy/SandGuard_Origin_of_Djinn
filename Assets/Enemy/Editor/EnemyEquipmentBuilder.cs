using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SandGuard.Enemy.Editor
{
    /// <summary>Imports only the separately authored equipment; existing enemy prefabs are untouched.</summary>
    public static class EnemyEquipmentBuilder
    {
        private const string Source = "Docs/model-art/enemy-equipment-v1/Exports";
        private const string Output = "Assets/Enemy/Art/Equipment";
        private static readonly string[] Names = { "ShortSword", "AssassinDagger", "ChiefScimitar", "Warhammer", "RoundShield", "TowerShield", "ChiefCape" };

        [MenuItem("SandGuard/Enemy/Build Equipment Assets")]
        public static void Build()
        {
            Directory.CreateDirectory(Output);
            File.Copy(Source + "/EquipmentPalette.png", Output + "/EquipmentPalette.png", true);
            foreach (var name in Names) File.Copy(Source + "/" + name + ".fbx", Output + "/" + name + ".fbx", true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var texturePath = Output + "/EquipmentPalette.png";
            var ti = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            ti.textureType = TextureImporterType.Default;
            ti.filterMode = FilterMode.Point;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.sRGBTexture = true;
            ti.SaveAndReimport();
            var materialPath = Output + "/EquipmentPalette.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable.");
                material = new Material(shader) { name = "EquipmentPalette" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_Smoothness", .22f);
            EditorUtility.SetDirty(material);
            var rows = new System.Collections.Generic.List<string>();
            foreach (var name in Names)
            {
                var path = Output + "/" + name + ".fbx";
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.globalScale = 1;
                importer.useFileScale = true;
                importer.importCameras = false;
                importer.importLights = false;
                importer.importNormals = ModelImporterNormals.Import;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "EquipmentPalette"), material);
                importer.importAnimation = name == "ChiefCape";
                importer.animationType = name == "ChiefCape" ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
                importer.avatarSetup = name == "ChiefCape" ? ModelImporterAvatarSetup.CreateFromThisModel : ModelImporterAvatarSetup.NoAvatar;
                importer.optimizeGameObjects = false;
                importer.isReadable = true; // Also permits the one-time deformation validation below.
                importer.SaveAndReimport();
                if (name == "ChiefCape")
                {
                    var clips = importer.defaultClipAnimations;
                    if (clips.Length == 0) throw new InvalidOperationException("Cape has no imported animation.");
                    foreach (var clip in clips)
                    {
                        clip.name = "Cape_Idle_Sway";
                        clip.loopTime = true;
                        clip.loopPose = false;
                    }
                    importer.clipAnimations = clips;
                    importer.SaveAndReimport();
                }
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                try
                {
                    instance.name = name;
                    foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = material;
                    var skinned = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                    if (skinned != null)
                    {
                        var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
                        var controllerPath = Output + "/ChiefCape.controller";
                        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                        var machine = controller.layers[0].stateMachine;
                        var state = machine.states.FirstOrDefault(s => s.state.name == "Cape_Idle_Sway").state;
                        if (state == null) state = machine.AddState("Cape_Idle_Sway");
                        state.motion = clip;
                        machine.defaultState = state;
                        var animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
                        animator.runtimeAnimatorController = controller;
                        animator.applyRootMotion = false;
                        // Conservative fixed bounds keep the trailing cloth visible during sway.
                        var bounds = skinned.localBounds; bounds.Expand(.35f); skinned.localBounds = bounds;
                        var before = new Mesh(); var after = new Mesh();
                        clip.SampleAnimation(instance, 0); skinned.BakeMesh(before);
                        clip.SampleAnimation(instance, .5f); skinned.BakeMesh(after);
                        var a = before.vertices; var b = after.vertices;
                        float movement = 0;
                        for (int i = 0; i < a.Length; ++i) movement = Mathf.Max(movement, Vector3.Distance(a[i], b[i]));
                        UnityEngine.Object.DestroyImmediate(before); UnityEngine.Object.DestroyImmediate(after);
                        clip.SampleAnimation(instance, 0);
                        if (movement < .005f) throw new InvalidOperationException("Cape animation did not deform in Unity.");
                        if (skinned.bones.Length != 13) throw new InvalidOperationException("Cape bone count changed on import.");
                        rows.Add(name + ": skinned, " + skinned.bones.Length + " bones, sampled displacement=" + movement.ToString("F5") + "m, clip=" + clip.length.ToString("F3") + "s");
                    }
                    else
                    {
                        var filters = instance.GetComponentsInChildren<MeshFilter>();
                        if (filters.Length != 1) throw new InvalidOperationException(name + " should use one mesh renderer.");
                        rows.Add(name + ": " + filters[0].sharedMesh.triangles.Length / 3 + " triangles, material mapped");
                    }
                    PrefabUtility.SaveAsPrefabAsset(instance, Output + "/" + name + ".prefab");
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
            AssetDatabase.SaveAssets();
            File.WriteAllLines("Docs/model-art/enemy-equipment-v1/unity-verification.txt", rows);
            Debug.Log("ENEMY_EQUIPMENT_VERIFIED\n" + string.Join("\n", rows));
        }
    }
}

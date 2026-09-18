using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class MinionVisualsBuilder
{
    const string Root = "Assets/MinionVisuals";

    [MenuItem("Tools/Minion Visuals/Build Visual Prefabs")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        var report = new StringBuilder("Minion visual import validation\n");
        foreach (var name in new[] { "SkeletonMinion", "AnubisMinion" })
        {
            string dir = Root + "/" + name;
            string texturePath = dir + "/Textures/" + name + "_BaseColor.jpg";
            var ti = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.maxTextureSize = 2048;
            ti.SaveAndReimport();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new Exception("URP Lit shader missing");
            string matPath = dir + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, matPath); }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_Smoothness", .25f);
            EditorUtility.SetDirty(material);

            string modelPath = dir + "/Models/" + name + ".fbx";
            Configure(modelPath, material, false, true, null);
            Configure(dir + "/Models/" + name + "_UnriggedSource.fbx", material, false, false, null);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
            if (!avatar || !avatar.isValid) throw new Exception(name + ": invalid Generic avatar");
            var root = new GameObject(name + "_Visual");
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.transform.SetParent(root.transform, false);
                // Keep the FBX transform hierarchy intact for later animation binding.
                PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                foreach (var animator in visual.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(animator);
                foreach (var anim in visual.GetComponentsInChildren<Animation>(true)) Object.DestroyImmediate(anim);
                var renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (renderers.Length == 0) throw new Exception(name + ": skinned mesh missing");
                int triangles = 0;
                foreach (var r in renderers)
                {
                    if (!r.sharedMesh || r.bones.Length == 0 || r.bones.Any(b => !b)) throw new Exception(name + ": broken skin");
                    r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();
                    triangles += r.sharedMesh.triangles.Length / 3;
                }
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                if (bounds.size.y < .1f || bounds.size.y > 5) throw new Exception(name + ": suspicious scale " + bounds.size);
                visual.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
                foreach (var c in root.GetComponentsInChildren<Component>(true))
                    if (!(c is Transform) && !(c is SkinnedMeshRenderer) && !(c is MeshRenderer) && !(c is MeshFilter))
                        throw new Exception("Unexpected component: " + c.GetType().Name);
                string prefabPath = dir + "/" + name + "_Visual.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                int clips = 0;
                foreach (string file in Directory.GetFiles(dir + "/Animations", "*.fbx"))
                {
                    string path = file.Replace('\\', '/');
                    Configure(path, material, true, true, avatar);
                    foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")))
                    {
                        var curves = AnimationUtility.GetCurveBindings(clip);
                        if (curves.Length == 0 || clip.length <= 0) throw new Exception("Empty clip: " + path);
                        foreach (var binding in curves.Where(b => b.type == typeof(Transform)))
                            if (!string.IsNullOrEmpty(binding.path) && !visual.transform.Find(binding.path))
                                throw new Exception("Animation binding missing: " + binding.path);
                        clips++;
                    }
                }
                report.AppendLine(name + ": triangles=" + triangles + ", skin renderers=" + renderers.Length + ", bones=" + renderers[0].bones.Length + ", size=" + bounds.size.ToString("F3") + ", animation clips=" + clips + ", visual-only components=PASS");
            }
            finally { Object.DestroyImmediate(root); }
        }
        if (File.Exists(Root + "/AnubisMinion/Animations/AnubisMinion@Unarmed Idle.fbx"))
            BuildAnubisIdle();
        AssetDatabase.SaveAssets();
        File.WriteAllText("Docs/model-art/minion-visuals/unity-validation.txt", report.ToString());
        Debug.Log("MINION_VISUALS_BUILD_OK\n" + report);
    }

    [MenuItem("Tools/Minion Visuals/Connect Anubis Idle")]
    public static void BuildAnubisIdle()
    {
        const string dir = Root + "/AnubisMinion";
        const string path = dir + "/Animations/AnubisMinion@Unarmed Idle.fbx";
        AssetDatabase.Refresh();
        var avatar = AssetDatabase.LoadAllAssetsAtPath(dir + "/Models/AnubisMinion.fbx").OfType<Avatar>().First();
        Configure(path, AssetDatabase.LoadAssetAtPath<Material>(dir + "/Materials/AnubisMinion.mat"), true, true, avatar);
        var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
        if (!clip.isLooping || clip.length <= 0) throw new Exception("Invalid idle clip");
        const string controllerPath = dir + "/Animations/AnubisMinion.controller";
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(controllerPath);
        if (!controller) controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        if (controller.layers.Length == 0) controller.AddLayer("Base Layer");
        var machine = controller.layers[0].stateMachine;
        var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Idle") ?? machine.AddState("Idle");
        state.motion = clip;
        machine.defaultState = state;
        EditorUtility.SetDirty(controller);
        const string prefabPath = dir + "/AnubisMinion_Visual.prefab";
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var model = root.transform.GetChild(0).gameObject;
            foreach (var binding in AnimationUtility.GetCurveBindings(clip).Where(b => b.type == typeof(Transform)))
                if (!string.IsNullOrEmpty(binding.path) && !model.transform.Find(binding.path))
                    throw new Exception("Idle binding missing: " + binding.path);
            var animator = model.GetComponent<Animator>();
            if (!animator) animator = model.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("ANUBIS_IDLE_OK: Unarmed Idle; default=Idle; looping=" + clip.isLooping + "; duration=" + clip.length);
    }

    static void Configure(string path, Material material, bool animation, bool rig, Avatar avatar)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.globalScale = 1;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importAnimation = animation;
        importer.animationType = rig ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
        importer.avatarSetup = !rig ? ModelImporterAvatarSetup.NoAvatar : avatar ? ModelImporterAvatarSetup.CopyFromOther : ModelImporterAvatarSetup.CreateFromThisModel;
        importer.sourceAvatar = avatar;
        importer.optimizeGameObjects = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.SaveAndReimport();
        foreach (var source in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name), material);
        if (animation)
        {
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.name = Path.GetFileNameWithoutExtension(path).Split('@').Last();
                clip.loopTime = clip.name.IndexOf("Idle", StringComparison.OrdinalIgnoreCase) >= 0 || clip.name.IndexOf("Walk", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            importer.clipAnimations = clips;
        }
        importer.SaveAndReimport();
    }
}

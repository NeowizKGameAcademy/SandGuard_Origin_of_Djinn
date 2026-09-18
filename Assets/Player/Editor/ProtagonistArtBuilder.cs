using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SandGuard.Player.Editor
{
    public static class ProtagonistArtBuilder
    {
        public const string Art = "Assets/Player/Art/Protagonist";
        public const string LampArt = "Assets/Player/Art/Lamp";
        public const string VisualPath = Art + "/ProtagonistVisual.prefab";
        public const string PreviewPath = "Assets/Player/Generated/PlayerArtPreview.unity";
        const string PlayerPath = "Assets/Player/Generated/Player.prefab";

        [MenuItem("SandGuard/Player/Connect Protagonist Art")]
        public static void Build()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var textureImporter = (TextureImporter)AssetImporter.GetAtPath(Art + "/Protagonist_BaseColor.jpg");
            textureImporter.textureType = TextureImporterType.Default;
            textureImporter.sRGBTexture = true; textureImporter.maxTextureSize = 2048;
            textureImporter.mipmapEnabled = true; textureImporter.SaveAndReimport();
            var material = MaterialAt(Art + "/Protagonist.mat", Color.white, 0, 0.1f);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/Protagonist_BaseColor.jpg"));
            material.SetColor("_BaseColor", Color.white); EditorUtility.SetDirty(material);

            ConfigureModel(Art + "/Protagonist.fbx", "Idle", material, null);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(Art + "/Protagonist.fbx").OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman) throw new InvalidOperationException("Mixamo humanoid avatar is invalid.");
            // Mixamo With Skin exports can carry different reference poses. Each file
            // builds its own valid Humanoid avatar; Unity retargets their muscle clips.
            ConfigureModel(Art + "/Walk.fbx", "Walk", material, null);
            ConfigureModel(Art + "/Run.fbx", "Run", material, null);
            RuntimeAnimatorController controller = Controller();
            GameObject lampPrefab = BuildLamp();

            var root = new GameObject("ProtagonistVisual");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/Protagonist.fbx"));
                model.name = "Character"; model.transform.SetParent(root.transform, false);
                var animator = model.GetComponent<Animator>();
                animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                { renderer.sharedMaterial = material; renderer.updateWhenOffscreen = true; }
                // Import scale is shared by all three animations. Do not rescale bones independently.
                animator.Rebind(); animator.Update(0f);
                Bounds bounds = BoundsOf(model);
                Debug.Log("PROTAGONIST_BOUNDS " + bounds);
                // Mixamo exports already use a floor-level origin. Skinned renderer
                // bounds include animation margins and must not offset the whole model.
                model.transform.localPosition = Vector3.zero;
                var bindings = root.AddComponent<PlayerVisualBindings>(); bindings.animator = animator;

                var belt = new GameObject("LampBeltSocket").transform;
                belt.position = new Vector3(-0.32f, 1.13f, 0.10f);
                belt.SetParent(animator.GetBoneTransform(HumanBodyBones.Hips), true);
                var hand = new GameObject("LampHandSocket").transform;
                hand.SetParent(animator.GetBoneTransform(HumanBodyBones.RightHand), true);
                hand.localPosition = Vector3.zero; hand.localRotation = Quaternion.identity;
                var lamp = (GameObject)PrefabUtility.InstantiatePrefab(lampPrefab);
                lamp.transform.SetParent(belt, false);
                var equipment = model.AddComponent<PlayerLampEquipment>();
                equipment.lamp = lamp.transform; equipment.beltSocket = belt; equipment.handSocket = hand;
                equipment.lampRenderer = lamp.GetComponentInChildren<Renderer>();
                equipment.lampLight = lamp.GetComponentInChildren<Light>(true);
                equipment.emissiveMaterialIndex = Array.FindIndex(equipment.lampRenderer.sharedMaterials, m => m.name == "Magic_Inlay_Emission");
                var grip = lamp.GetComponentsInChildren<Transform>().First(t => t.name == "HandGrip");
                equipment.handGripOffset = lamp.transform.InverseTransformPoint(grip.position);
                PlayerLampBuilder.Configure(equipment);
                PrefabUtility.SaveAsPrefabAsset(root, VisualPath);
            }
            finally { Object.DestroyImmediate(root); }

            var player = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                var visuals = player.GetComponent<PlayerVisuals>();
                visuals.visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath);
                visuals.localPosition = Vector3.zero; visuals.localEulerAngles = Vector3.zero; visuals.localScale = Vector3.one;
                visuals.RebuildVisual();
                PrefabUtility.SaveAsPrefabAsset(player, PlayerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }

            if (!File.Exists(PreviewPath))
            {
                AssetDatabase.CopyAsset("Assets/Player/Generated/PlayerTest.unity", PreviewPath);
                var previous = SceneManager.GetActiveScene();
                var scene = EditorSceneManager.OpenScene(PreviewPath, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                new GameObject("Character Preview Controls").AddComponent<PlayerArtPreviewControls>();
                EditorSceneManager.SaveScene(scene); EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
            AssetDatabase.SaveAssets();
            if (File.Exists(Art + "/ManaBoltPose.anim")) PlayerCastingAnimationBuilder.Build();
            Debug.Log("PROTAGONIST_ART_COMPLETE " + PreviewPath);
        }

        static void ConfigureModel(string path, string clipName, Material mat, Avatar source)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = source == null ? ModelImporterAvatarSetup.CreateFromThisModel : ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = source;
            importer.globalScale = 2f;
            importer.importAnimation = true; importer.importCameras = false; importer.importLights = false;
            importer.optimizeGameObjects = false;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
            importer = (ModelImporter)AssetImporter.GetAtPath(path);
            var clips = importer.defaultClipAnimations;
            if (clips.Length == 0) throw new InvalidOperationException("Missing animation: " + path);
            var clip = clips[0]; clip.name = clipName; clip.loopTime = true; clip.loopPose = true;
            clip.lockRootRotation = true; clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = true; clip.keepOriginalPositionY = true;
            clip.lockRootPositionXZ = true; clip.keepOriginalPositionXZ = true;
            importer.clipAnimations = new[] { clip };
            foreach (var material in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(material), mat);
            importer.SaveAndReimport();
        }

        static AnimatorController Controller()
        {
            string path = Art + "/Protagonist.controller";
            var result = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (result != null) return result;
            result = AnimatorController.CreateAnimatorControllerAtPath(path);
            result.AddParameter("Speed", AnimatorControllerParameterType.Float);
            result.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            result.AddParameter("Dashing", AnimatorControllerParameterType.Bool);
            var layers = result.layers; layers[0].iKPass = true; result.layers = layers;
            var state = result.layers[0].stateMachine.AddState("Locomotion");
            var tree = new BlendTree { name = "Idle Walk Run", blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, result);
            tree.AddChild(Clip("Protagonist"), 0f); tree.AddChild(Clip("Walk"), 2f); tree.AddChild(Clip("Run"), 5f);
            state.motion = tree; state.iKOnFeet = true; result.layers[0].stateMachine.defaultState = state;
            EditorUtility.SetDirty(result); return result;
        }

        public static AnimationClip Clip(string file) => AssetDatabase.LoadAllAssetsAtPath(Art + "/" + file + ".fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));

        static GameObject BuildLamp()
        {
            string path = LampArt + "/Lamp.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; importer.SaveAndReimport();
            foreach (var source in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
            {
                var mat = MaterialAt(LampArt + "/" + source.name + ".mat", source.color, source.name.Contains("Recess") ? 0.35f : 0.82f, 0.55f);
                if (source.name == "Magic_Inlay_Emission")
                { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", Color.black); mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
                EditorUtility.SetDirty(mat);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(source), mat);
            }
            importer.SaveAndReimport();
            var root = new GameObject("Lamp");
            try
            {
                // Preserve FBX axis/unit conversion on the nested model. The equipment
                // system rotates only this identity wrapper, never the importer root.
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                model.transform.SetParent(root.transform, false);
                var flame = root.GetComponentsInChildren<Transform>().First(t => t.name == "FlameSocket");
                var light = new GameObject("Lamp Light").AddComponent<Light>();
                light.transform.SetParent(flame, false); light.type = LightType.Point;
                light.color = new Color(0.1f, 0.9f, 1f); light.range = 1.8f; light.intensity = 0.35f; light.enabled = false;
                return PrefabUtility.SaveAsPrefabAsset(root, LampArt + "/Lamp.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        static Material MaterialAt(string path, Color color, float metallic, float smoothness)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.color = color; mat.SetFloat("_Metallic", metallic); mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat); return mat;
        }
        public static Bounds BoundsOf(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            Bounds result = renderers[0].bounds;
            foreach (var r in renderers.Skip(1)) result.Encapsulate(r.bounds);
            return result;
        }
        [MenuItem("SandGuard/Player/Open Character Preview")]
        public static void OpenPreview()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(PreviewPath);
        }
        public static void OpenAndPlay()
        {
            EditorSceneManager.OpenScene(PreviewPath);
            EditorApplication.delayCall += () =>
            {
                EditorApplication.isPlaying = true;
                var gameView = Type.GetType("UnityEditor.GameView,UnityEditor");
                if (gameView != null) EditorWindow.GetWindow(gameView).Focus();
            };
        }
    }
}

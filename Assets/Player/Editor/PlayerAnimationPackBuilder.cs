using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SandGuard.Player.Editor
{
    public static class PlayerAnimationPackBuilder
    {
        public const string Folder = ProtagonistArtBuilder.Art + "/CombatAnimations";
        public static bool Installed => File.Exists(Folder + "/CastMagic.fbx");

        [MenuItem("SandGuard/Player/Apply Animation Packs")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            Import("Pro Magic Pack/Standing 1H Magic Attack 01.fbx", "CastMagic", "Cast Magic", false);
            Import("Great Sword Pack/spell cast.fbx", "CastGreatSword", "Cast Great Sword", false);
            Import("Pro Sword and Shield Pack/sword and shield casting (2).fbx", "CastSwordShield", "Cast Sword Shield", false);
            foreach (string gait in new[] { "Walk", "Run" })
                foreach (string direction in new[] { "Forward", "Back", "Left", "Right" })
                    Import("Pro Magic Pack/Standing " + gait + " " + direction + ".fbx", gait + direction,
                        direction == "Forward" ? gait : gait + " " + direction, true);
            Import("Pro Magic Pack/Standing React Small From Front.fbx", "Hit", "Player Hit", false);
            Import("Pro Magic Pack/Standing React Death Backward.fbx", "Death", "Player Death", false);
            // Refreshes the hand binding and the serialized visual inside Player without changing its tuning.
            // Its final hook applies the pack states after the fallback casting setup.
            PlayerCastingAnimationBuilder.Build();
            Debug.Log("PLAYER_ANIMATION_PACKS_APPLIED");
        }

        static void Import(string source, string file, string name, bool loop)
        {
            string path = Folder + "/" + file + ".fbx";
            if (!File.Exists(path)) File.Copy("Docs/model-art/" + source, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.globalScale = 1f; importer.useFileScale = true;
            importer.importAnimation = true; importer.importCameras = false; importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.SaveAndReimport();
            var clips = importer.defaultClipAnimations;
            if (clips.Length == 0) throw new InvalidOperationException("No animation in " + source);
            var clip = clips[0]; clip.name = name; clip.loopTime = loop; clip.loopPose = loop;
            clip.lockRootRotation = clip.lockRootHeightY = true;
            clip.keepOriginalOrientation = clip.keepOriginalPositionY = true;
            // Extract source translation so the animated hips do not drift away from CharacterController.
            clip.lockRootPositionXZ = false; clip.keepOriginalPositionXZ = true;
            importer.clipAnimations = new[] { clip }; importer.SaveAndReimport();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman || !Clip(file).humanMotion)
                throw new InvalidOperationException("Invalid Humanoid clip: " + source);
            Debug.Log("PLAYER_PACK_CLIP " + source + " length=" + Clip(file).length);
        }

        public static AnimationClip Clip(string name) => AssetDatabase.LoadAllAssetsAtPath(Folder + "/" + name + ".fbx")
            .OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));

        public static void ConfigureController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ProtagonistArtBuilder.Art + "/Protagonist.controller");
            Parameter(controller, "MoveX", AnimatorControllerParameterType.Float);
            Parameter(controller, "MoveZ", AnimatorControllerParameterType.Float);
            Parameter(controller, "CastPhase", AnimatorControllerParameterType.Float);
            Parameter(controller, "CastStyle", AnimatorControllerParameterType.Int);
            Parameter(controller, "Hit", AnimatorControllerParameterType.Trigger);
            Parameter(controller, "Dead", AnimatorControllerParameterType.Bool);
            ConfigureLocomotion(controller);
            ConfigureCasting(controller);
            ConfigureDamage(controller);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        static void ConfigureLocomotion(AnimatorController controller)
        {
            var state = controller.layers[0].stateMachine.states.First(s => s.state.name == "Locomotion").state;
            var speed = state.motion as BlendTree;
            if (speed == null) throw new InvalidOperationException("Locomotion needs its speed Blend Tree.");
            var walk = DirectionTree(controller, "Walk");
            var run = DirectionTree(controller, "Run");
            speed.blendType = BlendTreeType.Simple1D; speed.blendParameter = "Speed"; speed.useAutomaticThresholds = false;
            speed.children = new[]
            {
                new ChildMotion { motion = ProtagonistArtBuilder.Clip("Protagonist"), threshold = 0, timeScale = 1 },
                new ChildMotion { motion = walk, threshold = 2, timeScale = 1 },
                new ChildMotion { motion = run, threshold = 5, timeScale = 1 }
            };
            EditorUtility.SetDirty(speed);
        }

        static BlendTree DirectionTree(AnimatorController controller, string gait)
        {
            string name = "Player " + gait + " Directions";
            var tree = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(controller)).OfType<BlendTree>().FirstOrDefault(t => t.name == name);
            if (tree == null) { tree = new BlendTree { name = name }; AssetDatabase.AddObjectToAsset(tree, controller); }
            tree.blendType = BlendTreeType.SimpleDirectional2D; tree.blendParameter = "MoveX"; tree.blendParameterY = "MoveZ";
            tree.children = new[]
            {
                new ChildMotion { motion = Clip(gait + "Forward"), position = Vector2.up, timeScale = 1 },
                new ChildMotion { motion = Clip(gait + "Back"), position = Vector2.down, timeScale = 1 },
                new ChildMotion { motion = Clip(gait + "Left"), position = Vector2.left, timeScale = 1 },
                new ChildMotion { motion = Clip(gait + "Right"), position = Vector2.right, timeScale = 1 }
            };
            EditorUtility.SetDirty(tree); return tree;
        }

        static void ConfigureCasting(AnimatorController controller)
        {
            var layer = controller.layers.First(l => l.name == "Upper Body Casting");
            var machine = layer.stateMachine;
            foreach (var transition in machine.anyStateTransitions.ToArray())
                if (transition.name.StartsWith("Player Pack: ")) machine.RemoveAnyStateTransition(transition);
            string[] files = { "CastMagic", "CastGreatSword", "CastSwordShield" };
            for (int i = 0; i < files.Length; i++)
            {
                var clip = Clip(files[i]);
                var state = State(machine, clip.name, new Vector3(300, i * 80));
                state.motion = clip; state.mirror = i != 0; state.writeDefaultValues = false;
                state.timeParameter = "CastPhase"; state.timeParameterActive = true;
                if (i == 0) machine.defaultState = state;
                var select = Transition(machine.AddAnyStateTransition(state), "Cast Style " + i, .04f);
                select.AddCondition(AnimatorConditionMode.Equals, i, "CastStyle");
            }
        }

        static void ConfigureDamage(AnimatorController controller)
        {
            var machine = controller.layers[0].stateMachine;
            var death = State(machine, "Death", new Vector3(750, 160));
            death.motion = Clip("Death"); death.speed = 1.25f; death.iKOnFeet = false;
            foreach (var transition in machine.anyStateTransitions.ToArray())
                if (transition.name == "Player Pack: Death") machine.RemoveAnyStateTransition(transition);
            foreach (var transition in machine.anyStateTransitions)
                if (!transition.conditions.Any(c => c.parameter == "Dead")) transition.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            var die = Transition(machine.AddAnyStateTransition(death), "Death", .08f);
            die.AddCondition(AnimatorConditionMode.If, 0, "Dead");
            machine.anyStateTransitions = new[] { die }.Concat(machine.anyStateTransitions.Where(t => t != die)).ToArray();

            string maskPath = Folder + "/HitUpperBody.mask";
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
            if (mask == null) { mask = new AvatarMask { name = "Hit Upper Body" }; AssetDatabase.CreateAsset(mask, maskPath); }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            foreach (var part in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm })
                mask.SetHumanoidBodyPartActive(part, true);
            EditorUtility.SetDirty(mask);
            var layers = controller.layers;
            int index = Array.FindIndex(layers, l => l.name == "Damage Reactions");
            if (index < 0) { controller.AddLayer("Damage Reactions"); layers = controller.layers; index = layers.Length - 1; }
            layers[index].avatarMask = mask; layers[index].defaultWeight = 1f;
            layers[index].blendingMode = AnimatorLayerBlendingMode.Override; layers[index].iKPass = false;
            var reactions = layers[index].stateMachine;
            var empty = State(reactions, "Empty", new Vector3(200, 0)); empty.writeDefaultValues = false;
            var hit = State(reactions, "Hit", new Vector3(440, 0)); hit.motion = Clip("Hit"); hit.speed = 2.5f; hit.writeDefaultValues = false;
            reactions.defaultState = empty;
            foreach (var transition in reactions.anyStateTransitions.ToArray()) reactions.RemoveAnyStateTransition(transition);
            foreach (var transition in hit.transitions.ToArray()) hit.RemoveTransition(transition);
            var start = Transition(reactions.AddAnyStateTransition(hit), "Hit", .035f); start.canTransitionToSelf = true;
            start.AddCondition(AnimatorConditionMode.If, 0, "Hit"); start.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            var end = Transition(hit.AddTransition(empty), "Hit End", .1f); end.hasExitTime = true; end.exitTime = .85f;
            controller.layers = layers;
        }

        static AnimatorState State(AnimatorStateMachine machine, string name, Vector3 position)
        {
            var state = machine.states.FirstOrDefault(s => s.state.name == name).state;
            return state != null ? state : machine.AddState(name, position);
        }

        static void Parameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            if (!controller.parameters.Any(p => p.name == name)) controller.AddParameter(name, type);
        }

        static AnimatorStateTransition Transition(AnimatorStateTransition value, string name, float duration)
        {
            value.name = "Player Pack: " + name; value.hasExitTime = false; value.hasFixedDuration = true;
            value.duration = duration; value.canTransitionToSelf = false;
            value.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
            return value;
        }
    }
}

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SandGuard.Player.Editor
{
    public static class PlayerCastingAnimationBuilder
    {
        const string Art = ProtagonistArtBuilder.Art;
        const string LayerName = "Upper Body Casting";

        [MenuItem("SandGuard/Player/Connect Hand Casting")]
        public static void Build()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Art + "/Protagonist.controller");
            if (controller == null) throw new InvalidOperationException("Connect Protagonist Art first.");
            var pose = AssetDatabase.LoadAssetAtPath<AnimationClip>(Art + "/ManaBoltPose.anim");
            if (pose == null) { pose = new AnimationClip { name = "Mana Bolt Pose", frameRate = 60 }; AssetDatabase.CreateAsset(pose, Art + "/ManaBoltPose.anim"); }
            // Humanoid muscle curves, so locomotion and this pose share the same retargeting system.
            foreach (string muscle in HumanTrait.MuscleName)
            {
                if (!(muscle.StartsWith("Spine") || muscle.StartsWith("Chest") || muscle.StartsWith("UpperChest")
                    || muscle.StartsWith("Right"))) continue;
                float value = 0f;
                if (muscle.Contains("Stretched")) value = .65f;
                if (muscle == "Right Arm Down-Up") value = -.15f;
                if (muscle == "Right Arm Front-Back") value = .65f;
                if (muscle == "Right Forearm Stretch") value = .4f;
                if (muscle == "Chest Front-Back") value = .06f;
                AnimationUtility.SetEditorCurve(pose, EditorCurveBinding.FloatCurve("", typeof(Animator), muscle), AnimationCurve.Constant(0, 1, value));
            }
            var settings = AnimationUtility.GetAnimationClipSettings(pose); settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(pose, settings);
            EditorUtility.SetDirty(pose);

            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(Art + "/CastingUpperBody.mask");
            if (mask == null) { mask = new AvatarMask { name = "Casting Upper Body" }; AssetDatabase.CreateAsset(mask, Art + "/CastingUpperBody.mask"); }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            foreach (var part in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.RightFingers, AvatarMaskBodyPart.RightHandIK })
                mask.SetHumanoidBodyPartActive(part, true);
            EditorUtility.SetDirty(mask);
            if (!controller.parameters.Any(p => p.name == "Casting")) controller.AddParameter("Casting", AnimatorControllerParameterType.Bool);
            var layers = controller.layers;
            int index = Array.FindIndex(layers, l => l.name == LayerName);
            if (index < 0) { controller.AddLayer(LayerName); layers = controller.layers; index = layers.Length - 1; }
            layers[index].avatarMask = mask; layers[index].blendingMode = AnimatorLayerBlendingMode.Override;
            layers[index].defaultWeight = 0f; layers[index].iKPass = true;
            var machine = layers[index].stateMachine;
            var state = machine.states.FirstOrDefault(s => s.state.name == "Mana Bolt Pose").state ?? machine.AddState("Mana Bolt Pose");
            state.motion = pose; state.writeDefaultValues = false; machine.defaultState = state;
            controller.layers = layers;
            ConfigureAirCasting(controller);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            var root = PrefabUtility.LoadPrefabContents(ProtagonistArtBuilder.VisualPath);
            try
            {
                var animator = root.GetComponentInChildren<Animator>();
                var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                var indexFinger = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
                var littleFinger = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
                var middleFinger = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
                Vector3 fingers = hand.InverseTransformDirection(middleFinger.position - hand.position).normalized;
                Vector3 across = hand.InverseTransformDirection(indexFinger.position - littleFinger.position).normalized;
                Vector3 normal = Vector3.Cross(fingers, across).normalized;
                var basis = Quaternion.LookRotation(normal, fingers);
                var muzzle = hand.Find("RightPalmMuzzle");
                if (muzzle == null) { muzzle = new GameObject("RightPalmMuzzle").transform; muzzle.SetParent(hand, false); }
                muzzle.localPosition = fingers * .065f + normal * .035f;
                muzzle.localRotation = basis;
                var casting = animator.GetComponent<PlayerSpellcasting>();
                if (casting == null) casting = animator.gameObject.AddComponent<PlayerSpellcasting>();
                casting.firePoint = muzzle; casting.palmBasis = basis;
                root.GetComponent<PlayerVisualBindings>().firePoint = muzzle;
                var flash = muzzle.Find("Mana Flash");
                if (flash == null)
                {
                    var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    orb.name = "Mana Flash"; Object.DestroyImmediate(orb.GetComponent<Collider>());
                    flash = orb.transform; flash.SetParent(muzzle, false); flash.localScale = Vector3.one * .075f;
                }
                var material = AssetDatabase.LoadAssetAtPath<Material>(Art + "/ManaFlash.mat");
                if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(material, Art + "/ManaFlash.mat"); }
                material.SetColor("_BaseColor", new Color(.15f, 2.5f, 4f)); EditorUtility.SetDirty(material);
                casting.palmFlash = flash.GetComponent<Renderer>(); casting.palmFlash.sharedMaterial = material; casting.palmFlash.enabled = false;
                var light = flash.GetComponent<Light>();
                if (light == null) light = flash.gameObject.AddComponent<Light>();
                light.type = LightType.Point; light.range = 1f; light.intensity = 1.5f; light.color = new Color(.1f, .7f, 1f); light.enabled = false;
                casting.palmLight = light;
                PrefabUtility.SaveAsPrefabAsset(root, ProtagonistArtBuilder.VisualPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var player = PrefabUtility.LoadPrefabContents("Assets/Player/Generated/Player.prefab");
            try
            {
                var visuals = player.GetComponent<PlayerVisuals>();
                visuals.RebuildVisual();
                var anchor = player.transform.Find("CastingEffectAnchor");
                if (anchor == null) { anchor = new GameObject("CastingEffectAnchor").transform; anchor.SetParent(player.transform, false); }
                visuals.fireEffectAnchor = anchor;
                var effect = player.GetComponent<DesertTower.VFX.VfxOneShot>();
                if (effect != null) { effect.Anchor = anchor; effect.SpawnScale = Vector3.one * .35f; }
                PrefabUtility.SaveAsPrefabAsset(player, "Assets/Player/Generated/Player.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            AssetDatabase.SaveAssets();
            if (PlayerAnimationPackBuilder.Installed) PlayerAnimationPackBuilder.ConfigureController();
            Debug.Log("PLAYER_HAND_CASTING_CONNECTED");
        }

        static void ConfigureAirCasting(AnimatorController controller)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Art + "/CastingJump.fbx");
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.globalScale = 2f; importer.importAnimation = true;
            importer.importCameras = false; importer.importLights = false;
            importer.SaveAndReimport();
            var clip = importer.defaultClipAnimations[0]; clip.name = "Casting Jump";
            clip.loopTime = false; clip.loopPose = false;
            clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = true;
            clip.keepOriginalOrientation = clip.keepOriginalPositionY = clip.keepOriginalPositionXZ = true;
            importer.clipAnimations = new[] { clip }; importer.SaveAndReimport();
            var machine = controller.layers[0].stateMachine;
            var air = machine.states.FirstOrDefault(s => s.state.name == "Air Cast").state ?? machine.AddState("Air Cast", new Vector3(720, -80));
            air.motion = ProtagonistArtBuilder.Clip("CastingJump"); air.speed = air.motion.averageDuration / .65f;
            air.iKOnFeet = false;
            foreach (var transition in machine.anyStateTransitions.ToArray())
            {
                if (transition.name.StartsWith("Hand Casting: ")) machine.RemoveAnyStateTransition(transition);
                else if (transition.name == "Player Mobility: Jump" && !transition.conditions.Any(c => c.parameter == "Casting"))
                    transition.AddCondition(AnimatorConditionMode.IfNot, 0, "Casting");
            }
            foreach (var t in air.transitions.ToArray()) if (t.name.StartsWith("Hand Casting: ")) air.RemoveTransition(t);
            var enter = Transition(machine.AddAnyStateTransition(air), "Air Cast");
            enter.AddCondition(AnimatorConditionMode.If, 0, "Casting");
            enter.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
            enter.AddCondition(AnimatorConditionMode.IfNot, 0, "Dashing");
            var jump = Transition(machine.AddAnyStateTransition(air), "Air Jump"); jump.canTransitionToSelf = true;
            jump.AddCondition(AnimatorConditionMode.If, 0, "Casting");
            jump.AddCondition(AnimatorConditionMode.If, 0, "Jump");
            jump.AddCondition(AnimatorConditionMode.IfNot, 0, "Dashing");
            var land = Transition(air.AddTransition(machine.states.First(s => s.state.name == "Locomotion").state), "Land");
            land.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            land.AddCondition(AnimatorConditionMode.IfNot, 0, "Dashing");
        }

        static AnimatorStateTransition Transition(AnimatorStateTransition value, string name)
        {
            value.name = "Hand Casting: " + name; value.hasExitTime = false;
            value.hasFixedDuration = true; value.duration = .06f; value.canTransitionToSelf = false;
            value.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
            return value;
        }
    }
}

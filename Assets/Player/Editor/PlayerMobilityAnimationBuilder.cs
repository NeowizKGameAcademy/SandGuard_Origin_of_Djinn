using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SandGuard.Player.Editor
{
    public static class PlayerMobilityAnimationBuilder
    {
        [MenuItem("SandGuard/Player/Connect Jump and Dash Animations")]
        public static void Build()
        {
            const string art = ProtagonistArtBuilder.Art;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(art + "/Jump.fbx");
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.globalScale = 2f;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.SaveAndReimport();
            var clip = importer.defaultClipAnimations[0];
            clip.name = "Jump";
            clip.loopTime = false;
            clip.loopPose = false;
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
            clip.lockRootPositionXZ = true;
            clip.keepOriginalPositionXZ = true;
            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(art + "/Protagonist.controller");
            if (controller == null) throw new InvalidOperationException("Connect Protagonist Art first.");
            if (!controller.parameters.Any(p => p.name == "Jump"))
                controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            var machine = controller.layers[0].stateMachine;
            var locomotion = machine.states.First(s => s.state.name == "Locomotion").state;
            // Only replace transitions owned by this menu. Preserve authored locomotion and other states.
            var jump = State(machine, "Jump", new Vector3(460, -80));
            var dash = State(machine, "Dash", new Vector3(460, 100));
            jump.motion = ProtagonistArtBuilder.Clip("Jump");
            jump.speed = jump.motion.averageDuration / 0.65f;
            jump.iKOnFeet = false;
            dash.motion = ProtagonistArtBuilder.Clip("Run");
            dash.speed = 1.8f;
            dash.iKOnFeet = false;
            foreach (var transition in machine.anyStateTransitions.ToArray())
                if (transition.name.StartsWith("Player Mobility: ")) machine.RemoveAnyStateTransition(transition);
            foreach (var state in new[] { locomotion, jump, dash })
                foreach (var transition in state.transitions.ToArray())
                    if (transition.name.StartsWith("Player Mobility: ")) state.RemoveTransition(transition);

            var startDash = Configure(machine.AddAnyStateTransition(dash), "Dash", 0.035f);
            startDash.AddCondition(AnimatorConditionMode.If, 0, "Dashing");
            var startJump = Configure(machine.AddAnyStateTransition(jump), "Jump", 0.045f);
            startJump.canTransitionToSelf = true; // Restart on the extra air jump.
            startJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");
            startJump.AddCondition(AnimatorConditionMode.IfNot, 0, "Dashing");
            var airborne = Configure(locomotion.AddTransition(jump), "Airborne", 0.08f);
            airborne.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
            airborne.AddCondition(AnimatorConditionMode.IfNot, 0, "Dashing");
            var land = Configure(jump.AddTransition(locomotion), "Land", 0.08f);
            land.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            land.AddCondition(AnimatorConditionMode.IfNot, 0, "Dashing");
            var dashGround = Configure(dash.AddTransition(locomotion), "Dash Ground", 0.06f);
            dashGround.AddCondition(AnimatorConditionMode.IfNot, 0, "Dashing");
            dashGround.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            var dashAir = Configure(dash.AddTransition(jump), "Dash Air", 0.06f);
            dashAir.AddCondition(AnimatorConditionMode.IfNot, 0, "Dashing");
            dashAir.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            if (System.IO.File.Exists(art + "/ManaBoltPose.anim")) PlayerCastingAnimationBuilder.Build();
            Debug.Log("PLAYER_MOBILITY_ANIMATIONS_CONNECTED jump=" + jump.motion.averageDuration + "s");
        }

        static AnimatorState State(AnimatorStateMachine machine, string name, Vector3 position)
        {
            var existing = machine.states.FirstOrDefault(s => s.state.name == name).state;
            return existing != null ? existing : machine.AddState(name, position);
        }

        static AnimatorStateTransition Configure(AnimatorStateTransition transition, string name, float duration)
        {
            transition.name = "Player Mobility: " + name;
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = duration;
            transition.canTransitionToSelf = false;
            transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
            return transition;
        }
    }
}

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SandGuard.Player.Editor
{
    /// <summary>
    /// 점프 / 2단 점프(플립) / 낙하 / 착지 / 강한 착지 / 대시 상태를 Protagonist 컨트롤러의 Base Layer에 만든다.
    /// 원본 Mixamo 파일은 Docs/model-art/protagonist 에서 복사한다. 이 메뉴가 만든 전환은 모두 "Player Mobility: " 접두어를 가지며
    /// 다시 실행하면 그 전환만 갈아 끼우고 Locomotion Blend Tree, 시전·피격 레이어는 건드리지 않는다.
    /// </summary>
    public static class PlayerMobilityAnimationBuilder
    {
        public const string Folder = ProtagonistArtBuilder.Art + "/MobilityAnimations";
        const string Source = "Docs/model-art/protagonist/";
        const string Prefix = "Player Mobility: ";
        // Mixamo 30fps 원본을 Blender로 분석한 프레임(0 기준). 도약·접지 직전부터 재생해 모터의 즉시 점프/착지와 타이밍을 맞춘다.
        const int JumpUpTakeoffFrame = 14;      // 웅크림 1~13, 발이 바닥을 떠나는 프레임 17
        const int FlipTakeoffFrame = 8;         // 도움닫기 0~8, 착지 27, 이후 회복
        const int FlipLandingFrame = 27;
        const int LandingContactFrame = 7;      // 발이 닿는 프레임 8, 최저 웅크림 14, 회복 완료 26
        const int HardLandingContactFrame = 1;  // 발이 닿는 프레임 2, 깊은 웅크림 ~44, 회복 완료 56
        const float FallSpeedThreshold = PlayerVisuals.FallingSpeedThreshold;

        [MenuItem("SandGuard/Player/Connect Jump and Dash Animations")]
        public static void Build()
        {
            const string art = ProtagonistArtBuilder.Art;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Directory.CreateDirectory(Folder);
            var jumpUp = Import("character-protagonist-mixamo@Jumping Up.fbx", "JumpUp", "Jump Up", false, false, JumpUpTakeoffFrame, -1);
            var flip = Import("character-protagonist-mixamo@Running Forward Flip.fbx", "Flip", "Flip", false, false, FlipTakeoffFrame, FlipLandingFrame);
            var fallingClip = Import("character-protagonist-mixamo@Falling Idle.fbx", "Falling", "Falling", true, true, -1, -1);
            var landingClip = Import("character-protagonist-mixamo@Falling To Landing.fbx", "Landing", "Landing", false, true, LandingContactFrame, -1);
            var hardLandingClip = Import("character-protagonist-mixamo@Hard Landing.fbx", "HardLanding", "Hard Landing", false, true, HardLandingContactFrame, -1);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(art + "/Protagonist.controller");
            if (controller == null) throw new InvalidOperationException("Connect Protagonist Art first.");
            Parameter(controller, "Jump", AnimatorControllerParameterType.Trigger);
            Parameter(controller, "DoubleJump", AnimatorControllerParameterType.Trigger);
            Parameter(controller, "HardLand", AnimatorControllerParameterType.Bool);
            Parameter(controller, "VerticalSpeed", AnimatorControllerParameterType.Float);
            var machine = controller.layers[0].stateMachine;
            var locomotion = machine.states.First(s => s.state.name == "Locomotion").state;
            var jump = State(machine, "Jump", new Vector3(460, -200));
            var doubleJump = State(machine, "Double Jump", new Vector3(460, -120));
            var falling = State(machine, "Falling", new Vector3(720, -160));
            var landing = State(machine, "Landing", new Vector3(720, 0));
            var hardLanding = State(machine, "Hard Landing", new Vector3(720, 80));
            var dash = State(machine, "Dash", new Vector3(460, 100));
            jump.motion = jumpUp; jump.speed = 1f; jump.iKOnFeet = false;
            doubleJump.motion = flip; doubleJump.speed = flip.averageDuration / 0.6f; doubleJump.iKOnFeet = false;
            falling.motion = fallingClip; falling.speed = 1f; falling.iKOnFeet = false;
            landing.motion = landingClip; landing.speed = 1.4f; landing.iKOnFeet = true;
            hardLanding.motion = hardLandingClip; hardLanding.speed = 1.3f; hardLanding.iKOnFeet = true;
            dash.motion = ProtagonistArtBuilder.Clip("Run"); dash.speed = 1.8f; dash.iKOnFeet = false;

            foreach (var transition in machine.anyStateTransitions.ToArray())
                if (transition.name.StartsWith(Prefix)) machine.RemoveAnyStateTransition(transition);
            foreach (var state in new[] { locomotion, jump, doubleJump, falling, landing, hardLanding, dash })
                foreach (var transition in state.transitions.ToArray())
                    if (transition.name.StartsWith(Prefix)) state.RemoveTransition(transition);

            var startDash = Configure(machine.AddAnyStateTransition(dash), "Dash", 0.035f);
            startDash.AddCondition(AnimatorConditionMode.If, 0, "Dashing");
            var startJump = Configure(machine.AddAnyStateTransition(jump), "Jump", 0.05f);
            startJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");
            startJump.AddCondition(AnimatorConditionMode.IfNot, 0, "Dashing");
            var startDoubleJump = Configure(machine.AddAnyStateTransition(doubleJump), "Double Jump", 0.05f);
            startDoubleJump.canTransitionToSelf = true; // extraAirJumps가 2 이상이면 플립을 다시 시작한다
            startDoubleJump.AddCondition(AnimatorConditionMode.If, 0, "DoubleJump");
            startDoubleJump.AddCondition(AnimatorConditionMode.IfNot, 0, "Dashing");

            // 절벽에서 걸어 떨어질 때: 점프 없이 바로 낙하
            var airborne = Configure(locomotion.AddTransition(falling), "Airborne", 0.15f);
            airborne.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
            airborne.AddCondition(AnimatorConditionMode.IfNot, 0, "Dashing");
            airborne.AddCondition(AnimatorConditionMode.Less, -FallSpeedThreshold, "VerticalSpeed");

            // 점프: 정점을 지나면(또는 클립이 끝나면) 낙하, 닿으면 착지. 강한 착지를 먼저 검사한다.
            var apex = Configure(jump.AddTransition(falling), "Apex", 0.2f);
            apex.AddCondition(AnimatorConditionMode.Less, 0f, "VerticalSpeed");
            apex.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
            var jumpEnd = Configure(jump.AddTransition(falling), "Jump End", 0.2f, 0.95f);
            jumpEnd.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
            AddLandings(jump, landing, hardLanding);

            var flipEnd = Configure(doubleJump.AddTransition(falling), "Flip End", 0.15f, 0.8f);
            flipEnd.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
            AddLandings(doubleJump, landing, hardLanding);
            AddLandings(falling, landing, hardLanding);

            // 착지: 회복이 끝나면 복귀. 이동 입력이 있으면 충격 구간만 보여 주고 일찍 끊는다. 착지 중 다시 떨어지면 낙하.
            AddRecovery(landing, locomotion, falling, 0.7f, 0.3f, 0.15f);
            AddRecovery(hardLanding, locomotion, falling, 0.85f, 0.45f, 0.2f);

            var dashGround = Configure(dash.AddTransition(locomotion), "Dash Ground", 0.06f);
            dashGround.AddCondition(AnimatorConditionMode.IfNot, 0, "Dashing");
            dashGround.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            var dashAir = Configure(dash.AddTransition(falling), "Dash Air", 0.06f);
            dashAir.AddCondition(AnimatorConditionMode.IfNot, 0, "Dashing");
            dashAir.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            // 예전 점프 클립(Running Forward Flip 전체)은 Flip.fbx가 대신한다.
            if (File.Exists(art + "/Jump.fbx")) AssetDatabase.DeleteAsset(art + "/Jump.fbx");
            if (File.Exists(art + "/ManaBoltPose.anim")) PlayerCastingAnimationBuilder.Build();
            Debug.Log("PLAYER_MOBILITY_ANIMATIONS_CONNECTED jumpUp=" + jumpUp.length + "s flip=" + flip.length + "s falling=" + fallingClip.length
                + "s landing=" + landingClip.length + "s hardLanding=" + hardLandingClip.length + "s");
        }

        static void AddLandings(AnimatorState from, AnimatorState landing, AnimatorState hardLanding)
        {
            var hard = Configure(from.AddTransition(hardLanding), "Hard Land", 0.05f);
            hard.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            hard.AddCondition(AnimatorConditionMode.If, 0, "HardLand");
            var land = Configure(from.AddTransition(landing), "Land", 0.05f);
            land.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
        }

        static void AddRecovery(AnimatorState state, AnimatorState locomotion, AnimatorState falling, float recoveredAt, float moveCancelAt, float blend)
        {
            Configure(state.AddTransition(locomotion), "Recovered", blend, recoveredAt);
            var move = Configure(state.AddTransition(locomotion), "Move", blend, moveCancelAt);
            move.AddCondition(AnimatorConditionMode.Greater, 1f, "Speed");
            var airborne = Configure(state.AddTransition(falling), "Airborne", 0.15f);
            airborne.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
            airborne.AddCondition(AnimatorConditionMode.Less, -FallSpeedThreshold, "VerticalSpeed");
        }

        /// <summary>원본을 복사해 Humanoid 클립으로 가져온다. bakeHeight가 false면 몸의 상하 이동을 루트 모션으로 빼내 캐릭터 컨트롤러의 점프와 겹치지 않게 한다.</summary>
        static AnimationClip Import(string source, string file, string clipName, bool loop, bool bakeHeight, int firstFrame, int lastFrame)
        {
            string path = Folder + "/" + file + ".fbx";
            if (!File.Exists(Source + source)) throw new FileNotFoundException("Missing Mixamo source: " + Source + source);
            File.Copy(Source + source, path, true);
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
            var clip = clips[0]; clip.name = clipName; clip.loopTime = loop; clip.loopPose = loop;
            if (firstFrame >= 0) clip.firstFrame = firstFrame;
            if (lastFrame >= 0) clip.lastFrame = lastFrame;
            clip.lockRootRotation = true; clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = bakeHeight; clip.keepOriginalPositionY = true;
            clip.lockRootPositionXZ = false; clip.keepOriginalPositionXZ = true; // 전진 이동은 루트 모션으로 빼내 몸이 컨트롤러에서 벗어나지 않게 한다
            importer.clipAnimations = new[] { clip }; importer.SaveAndReimport();
            var result = Clip(file);
            if (!result.humanMotion) throw new InvalidOperationException("Invalid Humanoid clip: " + source);
            return result;
        }

        public static AnimationClip Clip(string file) => AssetDatabase.LoadAllAssetsAtPath(Folder + "/" + file + ".fbx")
            .OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));

        static void Parameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            var existing = controller.parameters.FirstOrDefault(p => p.name == name);
            if (existing != null && existing.type == type) return;
            if (existing != null) controller.RemoveParameter(existing); // 종류가 바뀐 파라미터(예: 트리거→Bool)는 다시 만든다
            controller.AddParameter(name, type);
        }

        static AnimatorState State(AnimatorStateMachine machine, string name, Vector3 position)
        {
            var existing = machine.states.FirstOrDefault(s => s.state.name == name).state;
            return existing != null ? existing : machine.AddState(name, position);
        }

        static AnimatorStateTransition Configure(AnimatorStateTransition transition, string name, float duration, float exitTime = -1f)
        {
            transition.name = Prefix + name;
            transition.hasExitTime = exitTime >= 0f;
            transition.exitTime = exitTime >= 0f ? exitTime : 1f;
            transition.hasFixedDuration = true;
            transition.duration = duration;
            transition.canTransitionToSelf = false;
            transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
            return transition;
        }
    }
}

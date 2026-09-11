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
        const int DashFirstFrame = 38;          // Pushing에서 오른발이 들려 앞으로 내딛는 구간(38~52). 낮은 자세로 한 발 크게 밀어내는 모양
        const int DashLastFrame = 52;
        public const string DashLayerName = "Dash Legs";
        public const string DashMaskPath = ProtagonistArtBuilder.Art + "/DashLowerBody.mask";
        const int FlyPoseFirstFrame = 10;       // Jumping Up 웅크림 최저점(10)부터 도약·공중 자세(25)까지: 발사 순간 몸이 펴지며 마지막 자세를 유지한다
        const int FlyPoseLastFrame = 25;
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
            var crouchClip = Import("character-protagonist-mixamo@Male Crouch Pose.fbx", "CrouchPose", "Crouch Pose", true, true, -1, -1);
            // 대시: Pushing의 한 발 내딛는 구간을 하체 전용 레이어에서 재생한다. 낮은 자세가 보이도록 높이는 자세에 굽고, 이동은 루트 모션으로 빼낸다.
            var dashClip = ImportDash();
            if (File.Exists(Folder + "/AeroDash.fbx")) AssetDatabase.DeleteAsset(Folder + "/AeroDash.fbx"); // 이전 대시 클립
            // 임시 발사 동작: Mixamo Flying은 수평 비행 자세라 수직 발사에 어색하다. Jumping Up의 웅크림 최저점→공중 자세 구간을 한 번 재생하고
            // 마지막 자세(팔 위, 다리 뻗음)를 유지한다(반복 없음). 전용 클립을 구하면 여기서 원본 파일과 프레임만 바꾸면 된다.
            var flyClip = Import("character-protagonist-mixamo@Jumping Up.fbx", "Fly", "Fly", false, false, FlyPoseFirstFrame, FlyPoseLastFrame);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(art + "/Protagonist.controller");
            if (controller == null) throw new InvalidOperationException("Connect Protagonist Art first.");
            Parameter(controller, "Jump", AnimatorControllerParameterType.Trigger);
            Parameter(controller, "DoubleJump", AnimatorControllerParameterType.Trigger);
            Parameter(controller, "HardLand", AnimatorControllerParameterType.Bool);
            Parameter(controller, "VerticalSpeed", AnimatorControllerParameterType.Float);
            Parameter(controller, "Charging", AnimatorControllerParameterType.Bool);
            Parameter(controller, "Charge", AnimatorControllerParameterType.Float);
            Parameter(controller, "Fly", AnimatorControllerParameterType.Trigger);
            var machine = controller.layers[0].stateMachine;
            var locomotion = machine.states.First(s => s.state.name == "Locomotion").state;
            var jump = State(machine, "Jump", new Vector3(460, -200));
            var doubleJump = State(machine, "Double Jump", new Vector3(460, -120));
            var falling = State(machine, "Falling", new Vector3(720, -160));
            var landing = State(machine, "Landing", new Vector3(720, 0));
            var hardLanding = State(machine, "Hard Landing", new Vector3(720, 80));
            var oldDash = machine.states.FirstOrDefault(s => s.state.name == "Dash").state; // 대시는 하체 레이어로 옮겼다
            if (oldDash != null) machine.RemoveState(oldDash);
            var charge = State(machine, "Charge", new Vector3(200, 200));
            var fly = State(machine, "Fly", new Vector3(460, -280));
            jump.motion = jumpUp; jump.speed = 1f; jump.iKOnFeet = false;
            doubleJump.motion = flip; doubleJump.speed = flip.averageDuration / 0.6f; doubleJump.iKOnFeet = false;
            falling.motion = fallingClip; falling.speed = 1f; falling.iKOnFeet = false;
            landing.motion = landingClip; landing.speed = 1.4f; landing.iKOnFeet = true;
            hardLanding.motion = hardLandingClip; hardLanding.speed = 1.3f; hardLanding.iKOnFeet = true;
            // 충전: Charge 0(선 자세)→1(웅크림)로 서서히 낮아진다.
            charge.motion = ChargeTree(controller, ProtagonistArtBuilder.Clip("Protagonist"), crouchClip); charge.speed = 1f; charge.iKOnFeet = true;
            fly.motion = flyClip; fly.speed = 1f; fly.iKOnFeet = false;

            foreach (var transition in machine.anyStateTransitions.ToArray())
                if (transition.name.StartsWith(Prefix)) machine.RemoveAnyStateTransition(transition);
            foreach (var state in new[] { locomotion, jump, doubleJump, falling, landing, hardLanding, charge, fly })
                foreach (var transition in state.transitions.ToArray())
                    if (transition.name.StartsWith(Prefix)) state.RemoveTransition(transition);

            ConfigureDashLayer(controller, dashClip);
            var startJump = Configure(machine.AddAnyStateTransition(jump), "Jump", 0.05f);
            startJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");
            startJump.AddCondition(AnimatorConditionMode.IfNot, 0, "Dashing");
            var startDoubleJump = Configure(machine.AddAnyStateTransition(doubleJump), "Double Jump", 0.05f);
            startDoubleJump.canTransitionToSelf = true; // extraAirJumps가 2 이상이면 플립을 다시 시작한다
            startDoubleJump.AddCondition(AnimatorConditionMode.If, 0, "DoubleJump");
            startDoubleJump.AddCondition(AnimatorConditionMode.IfNot, 0, "Dashing");

            // 상승 기류: 충전 중 웅크림, 발사하면 Fly, 정점을 지나면 낙하, 닿으면 착지.
            var startCharge = Configure(machine.AddAnyStateTransition(charge), "Charge", 0.12f);
            startCharge.AddCondition(AnimatorConditionMode.If, 0, "Charging");
            startCharge.AddCondition(AnimatorConditionMode.IfNot, 0, "Dashing");
            var chargeEnd = Configure(charge.AddTransition(locomotion), "Charge End", 0.15f);
            chargeEnd.AddCondition(AnimatorConditionMode.IfNot, 0, "Charging");
            chargeEnd.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            var startFly = Configure(machine.AddAnyStateTransition(fly), "Fly", 0.12f); // 충전 웅크림 → 도약 웅크림으로 부드럽게 이어진 뒤 클립이 몸을 편다
            startFly.AddCondition(AnimatorConditionMode.If, 0, "Fly");
            startFly.AddCondition(AnimatorConditionMode.IfNot, 0, "Dashing");
            var flyApex = Configure(fly.AddTransition(falling), "Fly Apex", 0.25f);
            flyApex.AddCondition(AnimatorConditionMode.Less, 0f, "VerticalSpeed");
            flyApex.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
            AddLandings(fly, landing, hardLanding);
            var chargeAirborne = Configure(charge.AddTransition(falling), "Airborne", 0.15f);
            chargeAirborne.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
            chargeAirborne.AddCondition(AnimatorConditionMode.Less, -FallSpeedThreshold, "VerticalSpeed");

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
            AddRecovery(hardLanding, locomotion, falling, 1f, -1f, 0.15f);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            // 예전 점프 클립(Running Forward Flip 전체)은 Flip.fbx가 대신한다.
            if (File.Exists(art + "/Jump.fbx")) AssetDatabase.DeleteAsset(art + "/Jump.fbx");
            if (File.Exists(art + "/ManaBoltPose.anim")) PlayerCastingAnimationBuilder.Build();
            Debug.Log("PLAYER_MOBILITY_ANIMATIONS_CONNECTED jumpUp=" + jumpUp.length + "s flip=" + flip.length + "s falling=" + fallingClip.length
                + "s landing=" + landingClip.length + "s hardLanding=" + hardLandingClip.length + "s");
        }

        /// <summary>
        /// 대시 레이어. 마스크는 루트(골반)·척추·양다리라 밀기 자세의 기울임까지 나오고, 팔·머리는 Base Layer의 이동·조준·시전을 그대로 따른다.
        /// Empty 상태는 모션이 없어 가중치 1이면 다리 근육을 고정해 버리므로(피격 레이어와 같은 문제) PlayerVisuals가 대시 중에만 가중치를 올린다.
        /// </summary>
        static void ConfigureDashLayer(AnimatorController controller, AnimationClip dashClip)
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(DashMaskPath);
            if (mask == null) { mask = new AvatarMask { name = "Dash Lower Body" }; AssetDatabase.CreateAsset(mask, DashMaskPath); }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            // 루트·척추·양다리: 낮게 깔린 밀기 자세가 상체 기울임까지 나온다. 팔·머리는 Base Layer(이동·조준·시전)가 맡는다.
            foreach (var part in new[] { AvatarMaskBodyPart.Root, AvatarMaskBodyPart.Body, AvatarMaskBodyPart.LeftLeg, AvatarMaskBodyPart.RightLeg })
                mask.SetHumanoidBodyPartActive(part, true);
            if (dashClip.name == "Sand Dash")
                foreach (var part in new[] { AvatarMaskBodyPart.Head, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers })
                    mask.SetHumanoidBodyPartActive(part, true);
            EditorUtility.SetDirty(mask);

            var layers = controller.layers;
            int index = Array.FindIndex(layers, l => l.name == DashLayerName);
            if (index < 0) { controller.AddLayer(DashLayerName); layers = controller.layers; index = layers.Length - 1; }
            layers[index].avatarMask = mask; layers[index].defaultWeight = 0f;
            layers[index].blendingMode = AnimatorLayerBlendingMode.Override; layers[index].iKPass = false;
            var machine = layers[index].stateMachine;
            var empty = State(machine, "Empty", new Vector3(200, 0)); empty.writeDefaultValues = false; empty.motion = null;
            var dash = State(machine, "Dash", new Vector3(440, 0));
            dash.motion = dashClip; dash.speed = Mathf.Max(0.1f, dashClip.length) / 0.3f; dash.writeDefaultValues = false; dash.iKOnFeet = false;
            machine.defaultState = empty;
            foreach (var transition in machine.anyStateTransitions.ToArray()) if (transition.name.StartsWith(Prefix)) machine.RemoveAnyStateTransition(transition);
            foreach (var transition in dash.transitions.ToArray()) if (transition.name.StartsWith(Prefix)) dash.RemoveTransition(transition);
            var start = Configure(machine.AddAnyStateTransition(dash), "Dash", 0.05f);
            start.AddCondition(AnimatorConditionMode.If, 0, "Dashing");
            if (controller.parameters.Any(p => p.name == "Dead")) start.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            var end = Configure(dash.AddTransition(empty), "Dash End", 0.15f); // 낮은 자세를 풀며 달리기 다리로
            end.AddCondition(AnimatorConditionMode.IfNot, 0, "Dashing");
            // The authored dash controls the whole pose; casting/hit layers retain priority above it.
            if (dashClip.name == "Sand Dash" && index > 1)
            {
                var ordered = layers.ToList(); var dashLayer = ordered[index]; ordered.RemoveAt(index); ordered.Insert(1, dashLayer);
                layers = ordered.ToArray();
            }
            controller.layers = layers;
        }

        static AnimationClip ImportDash() => File.Exists(Source + "character-protagonist-sand-dash.fbx")
            ? Import("character-protagonist-sand-dash.fbx", "SandDash", "Sand Dash", false, true, -1, -1)
            : Import("character-protagonist-mixamo@Pushing.fbx", "DashLegs", "Push Legs", false, true, DashFirstFrame, DashLastFrame);

        [MenuItem("SandGuard/Player/Connect Authored Sand Dash")]
        public static void BuildDashOnly()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ProtagonistArtBuilder.Art + "/Protagonist.controller");
            ConfigureDashLayer(controller, ImportDash());
            EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
        }

        [MenuItem("SandGuard/Player/Apply Hard Landing Recovery")]
        public static void ApplyHardLandingRecovery()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ProtagonistArtBuilder.Art + "/Protagonist.controller");
            var machine = controller.layers[0].stateMachine;
            var hard = machine.states.First(s => s.state.name == "Hard Landing").state;
            foreach (var transition in hard.transitions.ToArray())
                if (transition.name.StartsWith(Prefix)) hard.RemoveTransition(transition);
            AddRecovery(hard, machine.states.First(s => s.state.name == "Locomotion").state,
                machine.states.First(s => s.state.name == "Falling").state, 1f, -1f, .15f);
            EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
        }

        /// <summary>Charge 0→1로 선 자세에서 웅크림으로 섞이는 1D 블렌드 트리. 컨트롤러 안에 한 번만 만든다.</summary>
        static BlendTree ChargeTree(AnimatorController controller, AnimationClip idle, AnimationClip crouch)
        {
            const string name = "Player Charge Crouch";
            var tree = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(controller)).OfType<BlendTree>().FirstOrDefault(t => t.name == name);
            if (tree == null) { tree = new BlendTree { name = name }; AssetDatabase.AddObjectToAsset(tree, controller); }
            tree.blendType = BlendTreeType.Simple1D; tree.blendParameter = "Charge"; tree.useAutomaticThresholds = false;
            tree.children = new[]
            {
                new ChildMotion { motion = idle, threshold = 0f, timeScale = 1f },
                new ChildMotion { motion = crouch, threshold = 1f, timeScale = 1f }
            };
            EditorUtility.SetDirty(tree);
            return tree;
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
            if (moveCancelAt >= 0f)
            {
                var move = Configure(state.AddTransition(locomotion), "Move", blend, moveCancelAt);
                move.AddCondition(AnimatorConditionMode.Greater, 1f, "Speed");
            }
            var airborne = Configure(state.AddTransition(falling), "Airborne", 0.15f);
            airborne.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
            airborne.AddCondition(AnimatorConditionMode.Less, -FallSpeedThreshold, "VerticalSpeed");
        }

        /// <summary>원본을 복사해 Humanoid 클립으로 가져온다. bakeHeight가 false면 몸의 상하 이동을 루트 모션으로 빼내 캐릭터 컨트롤러의 점프와 겹치지 않게 한다.</summary>
        static AnimationClip Import(string source, string file, string clipName, bool loop, bool bakeHeight, int firstFrame, int lastFrame)
        {
            string path = Folder + "/" + file + ".fbx";
            if (!File.Exists(Source + source)) throw new FileNotFoundException("Missing Mixamo source: " + Source + source);
            if (!PlayerAnimationPackBuilder.SameFile(Source + source, path)) File.Copy(Source + source, path, true); // 같은 파일이면 매핑된 FBX를 덮어쓰지 않는다
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

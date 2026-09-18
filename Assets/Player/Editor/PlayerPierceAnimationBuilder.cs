using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SandGuard.Player.Editor
{
    /// <summary>
    /// 관통탄 충전·발사 동작. Mixamo "Standing 2H Magic Attack 04"(30fps, 101프레임: 1~22 두 손을 모아 가슴으로 끌어올림, 23~30 앞으로 내지름, 31~78 뻗은 채 유지, 79~ 복귀)를
    /// 두 클립으로 자른다: <b>Pierce Charge</b> = 4~22(비루프, 마지막 프레임 = 가슴 앞에서 구슬을 감싼 자세로 멈춤), <b>Pierce Fire</b> = 22~42(내지르기, 1.6배속).
    /// 양팔만 덮는 오버라이드 레이어 "Pierce Casting"(기본 가중치 0)에 Empty / Pierce Charge / Pierce Fire 상태를 두고
    /// PlayerVisuals가 PierceCharging(bool)·PierceFire(trigger)와 레이어 가중치를 몬다. 다리는 Base Layer 그대로라 충전 중 이동이 자연스럽다.
    /// 메뉴: SandGuard > Player > Connect Pierce Charge Animation. 배치: -executeMethod SandGuard.Player.Editor.PlayerPierceAnimationBuilder.Build
    /// </summary>
    public static class PlayerPierceAnimationBuilder
    {
        public const string LayerName = "Pierce Casting";
        public const string ChargeState = "Pierce Charge", FireState = "Pierce Fire", EmptyState = "Empty";
        public const string ChargingParameter = "PierceCharging", FireTrigger = "PierceFire";
        const string Source = "Docs/model-art/protagonist/character-protagonist-mixamo@Standing 2H Magic Attack 04.fbx";
        const string Folder = ProtagonistArtBuilder.Art + "/CombatAnimations";
        const string File = "PierceCast";
        const int ChargeFirst = 4, ChargeLast = 22, FireFirst = 22, FireLast = 42;
        const float ChargeSpeed = 1.2f, FireSpeed = 1.6f;

        [MenuItem("SandGuard/Player/Connect Pierce Charge Animation")]
        public static void Build()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ProtagonistArtBuilder.Art + "/Protagonist.controller");
            if (controller == null) throw new InvalidOperationException("Connect Protagonist Art first.");
            ImportClips(out var charge, out var fire);

            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(ProtagonistArtBuilder.Art + "/PierceArms.mask");
            if (mask == null) { mask = new AvatarMask { name = "Pierce Arms" }; AssetDatabase.CreateAsset(mask, ProtagonistArtBuilder.Art + "/PierceArms.mask"); }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            // 양팔·손가락만. 척추는 넣지 않는다(넣으면 충전 중 걷는 상체가 굳는다). IK 골은 끈다: 이 레이어가 켜진 동안 PlayerSpellcasting의 조준 IK는 쉰다.
            foreach (var part in new[] { AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers })
                mask.SetHumanoidBodyPartActive(part, true);
            EditorUtility.SetDirty(mask);

            if (!controller.parameters.Any(p => p.name == ChargingParameter)) controller.AddParameter(ChargingParameter, AnimatorControllerParameterType.Bool);
            if (!controller.parameters.Any(p => p.name == FireTrigger)) controller.AddParameter(FireTrigger, AnimatorControllerParameterType.Trigger);
            var layers = controller.layers;
            int index = Array.FindIndex(layers, l => l.name == LayerName);
            if (index < 0) { controller.AddLayer(LayerName); layers = controller.layers; index = layers.Length - 1; }
            layers[index].avatarMask = mask; layers[index].blendingMode = AnimatorLayerBlendingMode.Override;
            layers[index].defaultWeight = 0f; layers[index].iKPass = false;
            var machine = layers[index].stateMachine;
            var empty = State(machine, EmptyState, new Vector3(40, 40)); empty.motion = null; machine.defaultState = empty;
            var chargeState = State(machine, ChargeState, new Vector3(320, -40)); chargeState.motion = charge; chargeState.speed = ChargeSpeed;
            var fireState = State(machine, FireState, new Vector3(320, 120)); fireState.motion = fire; fireState.speed = FireSpeed;
            foreach (var state in new[] { empty, chargeState, fireState }) foreach (var t in state.transitions.ToArray()) state.RemoveTransition(t);

            // 순서가 우선순위다: 충전 중 발사(PierceFire)가 충전 해제(PierceCharging=false)보다 먼저 평가돼야 한다(둘은 같은 프레임에 바뀐다).
            var begin = Transition(empty.AddTransition(chargeState), "Begin", 0.12f); begin.AddCondition(AnimatorConditionMode.If, 0, ChargingParameter);
            var tap = Transition(empty.AddTransition(fireState), "Tap Fire", 0.08f); tap.AddCondition(AnimatorConditionMode.If, 0, FireTrigger);
            var release = Transition(chargeState.AddTransition(fireState), "Release", 0.05f); release.AddCondition(AnimatorConditionMode.If, 0, FireTrigger);
            var cancel = Transition(chargeState.AddTransition(empty), "Cancel", 0.25f); cancel.AddCondition(AnimatorConditionMode.IfNot, 0, ChargingParameter);
            var done = Transition(fireState.AddTransition(empty), "Done", 0.2f); done.hasExitTime = true; done.exitTime = 0.85f;
            var again = Transition(fireState.AddTransition(chargeState), "Charge Again", 0.15f); again.hasExitTime = true; again.exitTime = 0.6f; again.AddCondition(AnimatorConditionMode.If, 0, ChargingParameter);
            controller.layers = layers;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("PLAYER_PIERCE_ANIMATION_CONNECTED charge=" + charge.length.ToString("0.00") + "s fire=" + fire.length.ToString("0.00") + "s");
        }

        static AnimatorState State(AnimatorStateMachine machine, string name, Vector3 position)
        {
            var existing = machine.states.FirstOrDefault(s => s.state.name == name).state;
            return existing != null ? existing : machine.AddState(name, position);
        }

        static AnimatorStateTransition Transition(AnimatorStateTransition value, string name, float duration)
        {
            value.name = "Pierce: " + name; value.hasExitTime = false; value.exitTime = 1f;
            value.hasFixedDuration = true; value.duration = duration; value.canTransitionToSelf = false;
            value.interruptionSource = TransitionInterruptionSource.None;
            return value;
        }

        /// <summary>원본을 복사해 Humanoid로 가져오고 한 FBX에서 충전·발사 두 클립을 자른다. 루트 이동·회전은 잠근다(제자리 상체 동작).</summary>
        static void ImportClips(out AnimationClip charge, out AnimationClip fire)
        {
            string path = Folder + "/" + File + ".fbx";
            if (!System.IO.File.Exists(Source)) throw new FileNotFoundException("Missing Mixamo source: " + Source);
            if (!PlayerAnimationPackBuilder.SameFile(Source, path)) System.IO.File.Copy(Source, path, true); // 같은 파일이면 매핑된 FBX를 덮어쓰지 않는다
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.globalScale = 1f; importer.useFileScale = true;
            importer.importAnimation = true; importer.importCameras = false; importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.SaveAndReimport();
            var defaults = importer.defaultClipAnimations;
            if (defaults.Length == 0) throw new InvalidOperationException("No animation in " + Source);
            importer.clipAnimations = new[]
            {
                Segment(defaults[0], ChargeState, ChargeFirst, ChargeLast),
                Segment(defaults[0], FireState, FireFirst, FireLast),
            };
            importer.SaveAndReimport();
            var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
            charge = clips.First(c => c.name == ChargeState); fire = clips.First(c => c.name == FireState);
            if (!charge.humanMotion || !fire.humanMotion) throw new InvalidOperationException("Invalid Humanoid clip: " + Source);
        }

        static ModelImporterClipAnimation Segment(ModelImporterClipAnimation template, string name, int first, int last)
        {
            var clip = new ModelImporterClipAnimation
            {
                name = name, takeName = template.takeName, firstFrame = first, lastFrame = last,
                loopTime = false, loopPose = false,
                lockRootRotation = true, keepOriginalOrientation = true,
                lockRootHeightY = true, keepOriginalPositionY = true,
                lockRootPositionXZ = true, keepOriginalPositionXZ = true,
            };
            return clip;
        }
    }
}

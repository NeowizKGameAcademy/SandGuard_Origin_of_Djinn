using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SandGuard.Enemy.Editor
{
    public static class ChiefBombSkillSetup
    {
        const string ClipPath = "Assets/Enemy/Art/Characters/Chief/Chief_Throw.fbx";
        const string ControllerPath = "Assets/Enemy/Art/Characters/Chief/Chief.controller";
        const string VisualPath = "Assets/Enemy/Art/Characters/Chief/Chief_CombatVisual.prefab";
        public static void InspectMotion()
        {
            var go = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath));
            try
            {
                var animator = go.GetComponentInChildren<Animator>();
                var clip = EnemyCombatArtBuilder.Clip(ClipPath);
                AnimationMode.StartAnimationMode();
                for (int i = 0; i <= 20; i++)
                {
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(animator.gameObject, clip, clip.length * i / 20f);
                    AnimationMode.EndSampling();
                    Debug.Log($"THROW_POSE {i / 20f:F2} right={go.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.RightHand).position):F3} left={go.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.LeftHand).position):F3}");
                }
            }
            finally { AnimationMode.StopAnimationMode(); UnityEngine.Object.DestroyImmediate(go); }
        }
        [MenuItem("SandGuard/Enemy/Connect Chief Bomb Throw Skill")]
        public static void Build()
        {
            string source = Directory.GetFiles("Docs/model-art/bandit-leader", "*@Throw.fbx").Single();
            File.Copy(source, ClipPath, true); AssetDatabase.ImportAsset(ClipPath);
            var importer = (ModelImporter)AssetImporter.GetAtPath(ClipPath);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true; importer.importCameras = false; importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.SaveAndReimport();
            var clip = importer.defaultClipAnimations[0];
            clip.name = "Throw"; clip.loopTime = false; clip.loopPose = false;
            clip.lockRootRotation = true; clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = true; clip.keepOriginalPositionY = true;
            clip.lockRootPositionXZ = true; clip.keepOriginalPositionXZ = true;
            // 오른손이 머리 위에서 앞으로 뻗는 구간(정규화 0.35~0.40)에 놓는다.
            clip.events = new[] { new AnimationEvent { functionName = "ReleaseChiefBomb", time = .38f } };
            importer.clipAnimations = new[] { clip }; importer.SaveAndReimport();
            EnsureAnimation();
            var root = PrefabUtility.LoadPrefabContents(ChiefSkillSetup.PrefabPath);
            try { Ensure(root); PrefabUtility.SaveAsPrefabAsset(root, ChiefSkillSetup.PrefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("CHIEF_BOMB_SKILL_READY clip seconds=" + EnemyCombatArtBuilder.Clip(ClipPath).length);
        }
        public static void Ensure(GameObject root)
        {
            if (!File.Exists(ClipPath)) return;
            EnsureAnimation();
            var skill = root.GetComponent<ChiefBombThrowSkill>() ?? root.AddComponent<ChiefBombThrowSkill>();
            skill.bombPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Generated/Chief_Bomb.prefab").GetComponent<ChiefBombProp>();
            skill.targetFacilityId = "tower.cobra"; // 철거 폭탄은 코브라 타워에만 던진다
        }
        static void EnsureAnimation()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.Select(x => x.state).FirstOrDefault(x => x.name == "Throw") ?? machine.AddState("Throw");
            state.motion = EnemyCombatArtBuilder.Clip(ClipPath);
            if (state.transitions.Length == 0)
            {
                var back = state.AddTransition(machine.states.First(x => x.state.name == "Locomotion").state);
                back.hasExitTime = true; back.exitTime = 1; back.duration = .1f;
            }
            EditorUtility.SetDirty(controller);
            var visual = PrefabUtility.LoadPrefabContents(VisualPath);
            try
            {
                var animator = visual.GetComponentInChildren<Animator>();
                if (!animator.GetComponent<ChiefThrowAnimationEvents>()) animator.gameObject.AddComponent<ChiefThrowAnimationEvents>();
                PrefabUtility.SaveAsPrefabAsset(visual, VisualPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(visual); }
        }
    }
}

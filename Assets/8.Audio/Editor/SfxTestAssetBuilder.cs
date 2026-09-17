using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SandGuard.Audio.Editor
{
    /// <summary>테스트용 애니메이터 컨트롤러·클립을 만든다. 이미 있으면 건드리지 않는다.</summary>
    public static class SfxTestAssetBuilder
    {
        const string Dir = "Assets/8.Audio/Tests/Resources";
        const string AssetPath = Dir + "/SfxTestAssets.asset";
        const string ControllerPath = Dir + "/SfxTestAnimator.controller";

        [MenuItem("SandGuard/Audio/Create Test Animator Assets")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<SfxTestAssets>(AssetPath) != null) return;
            Directory.CreateDirectory(Dir);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var sm = controller.layers[0].stateMachine;
            AddState(controller, sm, "Ground", 0.2f, true, null);
            AddState(controller, sm, "Idle", 0.1f, true, null);
            AddState(controller, sm, "Falling", 1f, true, null);
            AddState(controller, sm, "Attack", 0.3f, false, new AnimationEvent { time = 0.1f, functionName = "Swing" });
            var assets = ScriptableObject.CreateInstance<SfxTestAssets>();
            assets.controller = controller;
            AssetDatabase.CreateAsset(assets, AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Audio] 테스트 애니메이터 생성: {AssetPath}");
        }

        static void AddState(AnimatorController controller, AnimatorStateMachine sm, string name, float length, bool loop, AnimationEvent evt)
        {
            var clip = new AnimationClip { name = name };
            clip.SetCurve("", typeof(Transform), "localPosition.y", AnimationCurve.Linear(0f, 0f, length, 1f));
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, settings);
            if (evt != null) AnimationUtility.SetAnimationEvents(clip, new[] { evt });
            AssetDatabase.AddObjectToAsset(clip, controller);
            var state = sm.AddState(name);
            state.motion = clip;
        }
    }
}

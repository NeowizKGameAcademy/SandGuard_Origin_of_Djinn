using DesertTower.VFX;
using DesertTower.VFX.Editor;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using SandGuard.Audio;

namespace DesertTower.LevelIntegration.Editor
{
    public static class BlueDestructionSetup
    {
        [MenuItem("DesertTower/VFX/Build Blue Cobra and Level Core + Preview")]
        public static void BuildAndPreview()
        {
            CobraDestructionBuilder.Build();
            CoreDestructionBuilder.Build();
            WireSound(CobraDestructionBuilder.PrefabPath, "Facility/Facility_Cobra_Destroy", "Facility/Facility_Cobra_Destroy_Debris");
            WireSound(CoreDestructionBuilder.PrefabPath, "Core/Core_Destroy", "Core/Core_Destroy_Debris", "Core/Core_Destroy_Charge");
            WirePrefab(CobraDestructionBuilder.ReferencePath, CobraDestructionBuilder.PrefabPath, true);
            WirePrefab(CoreDestructionBuilder.ReferencePath, CoreDestructionBuilder.PrefabPath, false);
            WireLevelCore();
            CobraDestructionPreview.Render();
            CoreDestructionPreview.Render();
            Debug.Log("BLUE_DESTRUCTION_READY: Cobra + Level New Core wired and rendered.");
        }

        static void WireSound(string path, string burst, string debris, string charge = null)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var child = new GameObject("Sfx"); child.transform.SetParent(root.transform, false);
                var timeline = child.AddComponent<SfxTimeline>();
                if (charge != null) timeline.Entries.Add(new SfxTimeline.Entry { Delay = 0f,
                    Cue = AssetDatabase.LoadAssetAtPath<SfxCue>("Assets/8.Audio/Cues/" + charge + ".asset") });
                timeline.Entries.Add(new SfxTimeline.Entry { Delay = BlueDestructionBursts.BreakTime,
                    Cue = AssetDatabase.LoadAssetAtPath<SfxCue>("Assets/8.Audio/Cues/" + burst + ".asset") });
                timeline.Entries.Add(new SfxTimeline.Entry { Delay = 1.45f,
                    Cue = AssetDatabase.LoadAssetAtPath<SfxCue>("Assets/8.Audio/Cues/" + debris + ".asset") });
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void WirePrefab(string path, string effectPath, bool tower)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var target = tower ? ObeliskDestructionBuilder.FindHealth(root).gameObject : root;
                var death = target.GetComponent<VfxDestructionOnDeath>();
                if (!death) death = target.AddComponent<VfxDestructionOnDeath>();
                death.Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(effectPath);
                death.Target = target.transform;
                death.Lifetime = 4f;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static void WireLevelCore()
        {
            const string path = "Assets/1.Scene/Level.unity";
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                int count = 0;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var core in root.GetComponentsInChildren<CoreReceiver>(true))
                    {
                        var source = PrefabUtility.GetCorrespondingObjectFromSource(core.gameObject);
                        if (!source || AssetDatabase.GetAssetPath(source) != CoreDestructionBuilder.ReferencePath)
                            throw new System.InvalidOperationException("Level core model no longer matches New Core.prefab");
                        var death = core.GetComponent<VfxDestructionOnDeath>();
                        if (!death || !death.Prefab) throw new System.InvalidOperationException("Core destruction prefab is not wired");
                        bool wired = false;
                        for (int i = 0; i < core.onDefeated.GetPersistentEventCount(); i++)
                            if (core.onDefeated.GetPersistentTarget(i) == death && core.onDefeated.GetPersistentMethodName(i) == "Fire") wired = true;
                        if (!wired) UnityEventTools.AddPersistentListener(core.onDefeated, new UnityAction(death.Fire));
                        PrefabUtility.RecordPrefabInstancePropertyModifications(core);
                        count++;
                    }
                if (count == 0) throw new System.InvalidOperationException("Level CoreReceiver missing");
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}

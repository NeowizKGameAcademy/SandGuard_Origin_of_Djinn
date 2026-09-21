#if UNITY_EDITOR
using SandGuard.Cutscenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SandGuard.Cutscenes.Editor
{
    public static class StoryMenuTools
    {
        const string MainScenePath = "Assets/1.Scene/MainScene.unity";

        [MenuItem("SandGuard/Story/Reconnect Story Button")]
        public static void ReconnectStoryButton()
        {
            Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            var controller = Object.FindFirstObjectByType<StoryMenuController>(FindObjectsInactive.Include);
            var storyObject = GameObject.Find("Story");
            var button = storyObject ? storyObject.GetComponent<Button>() : null;
            if (!controller || !button)
                throw new System.InvalidOperationException("StoryMenuController 또는 Story 버튼을 찾을 수 없습니다.");

            var serialized = new SerializedObject(controller);
            serialized.FindProperty("storyButton").objectReferenceValue = button;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
            UnityEventTools.AddPersistentListener(button.onClick, controller.Open);
            EditorUtility.SetDirty(button);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("현재 Story 버튼을 기존 스토리 팝업에 다시 연결했습니다.");
        }

        [MenuItem("SandGuard/Story/Reset All Viewed Stories")]
        public static void ResetAllViewedStories()
        {
            StoryProgress.ResetAllSeen();
            Debug.Log("Prologue, Intro, Ending 시청 기록을 초기화했습니다.");
        }

        [MenuItem("SandGuard/Story/Reset Prologue Only")]
        public static void ResetPrologueOnly()
        {
            StoryProgress.ResetSeen("Prologue");
            Debug.Log("Prologue 시청 기록을 초기화했습니다.");
        }
    }
}
#endif

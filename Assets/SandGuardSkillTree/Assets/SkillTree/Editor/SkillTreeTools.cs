using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SandGuard.Skills.Unity;

namespace SandGuard.Skills.Editor
{
    public static class SkillTreeTools
    {
        [MenuItem("SandGuard/Skills/Create Standalone Test Scene")]
        public static void CreateScene()
        {
            SkillUIPrefabBuilder.CreatePreview();
        }
        [MenuItem("SandGuard/Skills/Create Demo Tree Asset")]
        public static void CreateAsset()
        {
            string path=EditorUtility.SaveFilePanelInProject("스킬 트리 저장","DemoSkillTree","asset","샘플 트리를 저장할 위치");
            if(string.IsNullOrEmpty(path))return;
            var asset=SkillTreeAsset.CreateDemo();AssetDatabase.CreateAsset(asset,path);AssetDatabase.SaveAssets();Selection.activeObject=asset;
        }
        [CustomEditor(typeof(SkillTreeAsset))]
        public sealed class TreeInspector : UnityEditor.Editor
        {
            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();
                if(GUILayout.Button("선행 관계 / ID / 슬롯 검사"))
                    try{((SkillTreeAsset)target).Build();Debug.Log("스킬 트리 데이터 검사 통과",target);}
                    catch(System.Exception e){Debug.LogError(e.Message,target);}
            }
        }
    }
}

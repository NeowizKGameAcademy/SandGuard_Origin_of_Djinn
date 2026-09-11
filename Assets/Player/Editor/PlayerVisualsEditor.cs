using UnityEditor;
using UnityEngine;

namespace SandGuard.Player.Editor
{
    [CustomEditor(typeof(PlayerVisuals))]
    public sealed class PlayerVisualsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Visual Prefab에 모델 프리팹을 넣고 아래 버튼을 누르세요. 모델은 +Z를 정면으로 맞추세요. 손/지팡이 발사 위치는 모델의 PlayerVisualBindings에서 지정할 수 있습니다.", MessageType.Info);
            if (GUILayout.Button("외형 적용 / Rebuild Visual"))
            {
                var value = (PlayerVisuals)target;
                Undo.RegisterFullObjectHierarchyUndo(value.gameObject, "Replace Player Visual");
                value.RebuildVisual();
                EditorUtility.SetDirty(value);
                PrefabUtility.RecordPrefabInstancePropertyModifications(value);
            }
        }
    }
}

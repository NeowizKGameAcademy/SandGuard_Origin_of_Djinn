using UnityEditor;
using UnityEngine;

namespace SandGuard.Enemy.Editor
{
    [CustomEditor(typeof(EnemyVisuals))]
    public sealed class EnemyVisualsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Visual Prefab에 모델 프리팹을 넣고 아래 버튼을 누르세요. 모델은 +Z를 정면으로 맞추세요. 애니메이터, 무기 축, 공격 기준점은 모델의 EnemyVisualBindings에서 지정할 수 있습니다.", MessageType.Info);
            if (GUILayout.Button("외형 적용 / Rebuild Visual"))
            {
                var value = (EnemyVisuals)target;
                Undo.RegisterFullObjectHierarchyUndo(value.gameObject, "Replace Enemy Visual");
                value.RebuildVisual();
                EditorUtility.SetDirty(value);
                PrefabUtility.RecordPrefabInstancePropertyModifications(value);
            }
        }
    }
}

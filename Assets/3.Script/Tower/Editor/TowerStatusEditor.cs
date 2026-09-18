using UnityEditor;
using UnityEngine;
using Tower;

[CustomEditor(typeof(Tower.TowerStatus))]
public class TowerStatusEditor : Editor
{
    private SerializedProperty towerType;

    private SerializedProperty maxHP;
    private SerializedProperty detectRange;

    private SerializedProperty cobraConfig;
    private SerializedProperty obeliskConfig;
    private SerializedProperty coffinConfig;
    private SerializedProperty anubisConfig;

    private void OnEnable()
    {
        towerType = serializedObject.FindProperty("Tower");

        maxHP = serializedObject.FindProperty("MaxHP");
        detectRange = serializedObject.FindProperty("DetectRange");

        cobraConfig = serializedObject.FindProperty("cobraConfig");
        obeliskConfig = serializedObject.FindProperty("obeliskConfig");
        coffinConfig = serializedObject.FindProperty("coffinConfig");
        anubisConfig = serializedObject.FindProperty("anubisConfig");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 타워 종류
        EditorGUILayout.PropertyField(towerType);

        EditorGUILayout.Space(10);

        // 공통 스탯
        EditorGUILayout.LabelField("Basic Status", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(maxHP);
        EditorGUILayout.PropertyField(detectRange);

        EditorGUILayout.Space(10);

        // 선택한 타워의 Config만 표시
        TowerType type = (TowerType)towerType.enumValueIndex;

        switch (type)
        {
            case TowerType.Cobra:
                EditorGUILayout.PropertyField(
                    cobraConfig,
                    new GUIContent("Cobra Config"),
                    true
                );
                break;

            case TowerType.Obelisk:
                EditorGUILayout.PropertyField(
                    obeliskConfig,
                    new GUIContent("Obelisk Config"),
                    true
                );
                break;

            case TowerType.Coffin:
                EditorGUILayout.PropertyField(
                    coffinConfig,
                    new GUIContent("Coffin Config"),
                    true
                );
                break;

            case TowerType.Anubis:
                EditorGUILayout.PropertyField(
                    anubisConfig,
                    new GUIContent("Anubis Config"),
                    true
                );
                break;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
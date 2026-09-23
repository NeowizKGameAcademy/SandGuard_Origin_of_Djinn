#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SandGuard.UI.HUD.Editor
{
    /// <summary>
    /// 스킬 칸이 늘어난 만큼 이미 만들어 둔 HUD 프리팹을 고친다(여러 번 실행해도 안전).
    /// 이동 스킬 줄에 우클릭(흔적 귀환) 칸을 더해 세 칸으로 넓히고, 늘어난 너비를 HUD.unity와 GameHUD.prefab 인스턴스에 반영한다.
    /// 공격 F 칸은 이미 CombatSkillHUD.prefab에 있으므로 자리 표시 아이콘만 비운다(장착한 스킬 아이콘은 런타임에 들어간다).
    /// Batch: <c>-executeMethod SandGuard.UI.HUD.Editor.HUDSkillSlotExpansion.Apply</c>
    /// </summary>
    public static class HUDSkillSlotExpansion
    {
        const string PrefabRoot = "Assets/2.Model/Prefabs/HUD";
        const string MovementPath = PrefabRoot + "/MovementSkillHUD.prefab";
        const string ScenePath = "Assets/1.Scene/HUD.unity";
        /// <summary>스킬트리 창이 쓰는 흔적 귀환 아이콘. 에디터 미리보기용이며 런타임에는 테마가 다시 넣는다.</summary>
        public const string RecallIcon = "Assets/SandGuardSkillTree/Assets/SkillTree/Art/move.recall.png";

        // 슬롯 하나가 112 너비라 세 칸이면 336 + 여백.
        static readonly Vector2 MovementSize = new Vector2(360, 130);
        static readonly float[] SlotX = { -116f, 0f, 116f };

        [MenuItem("SandGuard/HUD/Add Recall And F Skill Slots")]
        public static void Apply()
        {
            PatchMovementPrefab();
            ResizeMovementInstanceInScene();
            ResizeMovementInstanceInGameHUD();
            AssetDatabase.SaveAssets();
            Debug.Log("[HUD] 이동 스킬 줄에 우클릭(흔적 귀환) 칸을 추가했습니다.");
        }

        public static void ApplyBatch() { Apply(); EditorApplication.Exit(0); }

        static void PatchMovementPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(MovementPath);
            try
            {
                var hud = root.GetComponent<MovementSkillHUD>();
                var dash = root.transform.Find("DashSlot");
                var jump = root.transform.Find("DoubleJumpSlot");
                if (dash == null || jump == null) throw new System.InvalidOperationException("DashSlot / DoubleJumpSlot을 찾지 못했습니다: " + MovementPath);

                var recall = root.transform.Find("RecallSlot");
                if (recall == null)
                {
                    var copy = Object.Instantiate(dash.gameObject, root.transform);
                    copy.name = "RecallSlot";
                    recall = copy.transform;
                }
                ((RectTransform)root.transform).sizeDelta = MovementSize;
                Place((RectTransform)dash, SlotX[0]);
                Place((RectTransform)jump, SlotX[1]);
                Place((RectTransform)recall, SlotX[2]);

                // 위쪽 판자는 80px밖에 안 돼 이름만 들어간다. 키(Shift·Space·우클릭)는 고리 아래 작은 글자로 따로 붙인다.
                foreach (var each in root.GetComponentsInChildren<MovementSkillSlotHUD>(true))
                {
                    var eachSo = new SerializedObject(each);
                    var text = eachSo.FindProperty("titleText").objectReferenceValue as TMP_Text;
                    if (text != null && !text.enableAutoSizing)
                    {
                        float authored = text.fontSize;
                        text.enableAutoSizing = true;
                        text.fontSizeMin = 8f;
                        text.fontSizeMax = authored;
                        EditorUtility.SetDirty(text);
                    }
                    AddKeyLabel(each, eachSo, text);
                }

                var slot = recall.GetComponent<MovementSkillSlotHUD>();
                var so = new SerializedObject(slot);
                var title = so.FindProperty("titleText").objectReferenceValue as TMP_Text;
                if (title != null) title.text = "흔적 귀환";
                var recallKey = so.FindProperty("keyText").objectReferenceValue as TMP_Text;
                if (recallKey != null) recallKey.text = "우클릭";
                var icon = so.FindProperty("icon").objectReferenceValue as Image;
                if (icon != null)
                {
                    icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RecallIcon);
                    icon.enabled = icon.sprite != null;
                    icon.preserveAspect = true;
                }
                var ring = so.FindProperty("cooldownRing").objectReferenceValue as Image;
                if (ring != null) ring.fillAmount = 0f;

                var hudSo = new SerializedObject(hud);
                hudSo.FindProperty("recall").objectReferenceValue = slot;
                hudSo.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, MovementPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>이름 글자를 본떠 고리 아래에 키 이름 칸을 만든다(이미 있으면 그대로).</summary>
        static void AddKeyLabel(MovementSkillSlotHUD slot, SerializedObject so, TMP_Text template)
        {
            var field = so.FindProperty("keyText");
            if (field.objectReferenceValue != null || template == null) return;
            var existing = slot.transform.Find("KeyText_TMP");
            TMP_Text key;
            if (existing != null) key = existing.GetComponent<TMP_Text>();
            else
            {
                var copy = Object.Instantiate(template.gameObject, slot.transform);
                copy.name = "KeyText_TMP";
                key = copy.GetComponent<TMP_Text>();
            }
            key.text = slot.name == "DashSlot" ? "Shift" : slot.name == "DoubleJumpSlot" ? "Space" : "우클릭";
            key.fontSize = 11f;
            key.enableAutoSizing = true;
            key.fontSizeMin = 8f;
            key.fontSizeMax = 11f;
            var rect = (RectTransform)key.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(0f, -56f);
            rect.sizeDelta = new Vector2(96f, 16f);
            field.objectReferenceValue = key;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Place(RectTransform rect, float x)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(112, 130);
        }

        // HUD.unity가 배치의 기준이라 여기 값이 HUDLayoutSync로 GameHUD.prefab에 다시 복사된다.
        static void ResizeMovementInstanceInScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            bool changed = false;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var hud in root.GetComponentsInChildren<MovementSkillHUD>(true))
                    changed |= Widen((RectTransform)hud.transform);
            if (!changed) return;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void ResizeMovementInstanceInGameHUD()
        {
            var root = PrefabUtility.LoadPrefabContents(HUDGameWiring.GameHUDPath);
            try
            {
                bool changed = root.GetComponentsInChildren<MovementSkillHUD>(true)
                    .Aggregate(false, (any, hud) => Widen((RectTransform)hud.transform) || any);
                // F는 더 이상 타워 전용 자리가 아니다. 자리 표시 아이콘을 비워 장착한 스킬만 보이게 한다.
                foreach (var combat in root.GetComponentsInChildren<CombatSkillHUD>(true))
                {
                    if (combat.F == null) continue;
                    var icon = new SerializedObject(combat.F).FindProperty("icon").objectReferenceValue as Image;
                    if (icon == null || icon.sprite == null) continue;
                    icon.sprite = null;
                    icon.enabled = false;
                    EditorUtility.SetDirty(icon);
                    changed = true;
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, HUDGameWiring.GameHUDPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static bool Widen(RectTransform rect)
        {
            if (rect.sizeDelta == MovementSize) return false;
            rect.sizeDelta = MovementSize;
            EditorUtility.SetDirty(rect);
            if (PrefabUtility.IsPartOfPrefabInstance(rect)) PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            return true;
        }
    }
}
#endif

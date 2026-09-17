#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SandGuard.UI.HUD.Editor
{
    public static class WaveWarningHUDBuilder
    {
        const string WavePrefabPath = "Assets/2.Model/Prefabs/HUD/WaveHUD.prefab";
        const string GameHUDPrefabPath = "Assets/2.Model/Prefabs/HUD/GameHUD.prefab";
        const string FontPath = "Assets/9.Font/Pretendard-Bold SDF.asset";
        static readonly Color Black = new(.018f, .012f, .014f, .94f);
        static readonly Color Crimson = new(.48f, .018f, .025f, 1f);
        static readonly Color Red = new(.92f, .075f, .06f, 1f);
        static readonly Color Gold = new(1f, .72f, .22f, 1f);
        static readonly Color Ivory = new(1f, .94f, .79f, 1f);
        static TMP_FontAsset font;

        [MenuItem("SandGuard/HUD/Build Wave Warning UI")]
        public static void Build()
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            BuildWaveCounter();
            BuildWaveAlert();
            AssetDatabase.SaveAssets();
            Debug.Log("[HUD] Black/crimson wave warning and remaining enemy counter built.");
        }

        static void BuildWaveCounter()
        {
            var root = PrefabUtility.LoadPrefabContents(WavePrefabPath);
            try
            {
                var old = root.transform.Find("RemainingEnemyPanel");
                if (old) Object.DestroyImmediate(old.gameObject);

                var panel = Rect("RemainingEnemyPanel", root.transform, new Vector2(0, -57), new Vector2(250, 42));
                Image("Shadow", panel, new Color(0, 0, 0, .45f), new Vector2(3, -3), new Vector2(250, 42));
                Image("Background", panel, Black, Vector2.zero, new Vector2(250, 42));
                Image("TopRed", panel, Crimson, new Vector2(0, 19), new Vector2(238, 3));
                Image("BottomRed", panel, Crimson, new Vector2(0, -19), new Vector2(238, 3));
                Diamond("RubyLeft", panel, new Vector2(-119, 0), new Vector2(15, 15), Red);
                Diamond("RubyRight", panel, new Vector2(119, 0), new Vector2(15, 15), Red);
                Text("EnemyIcon_TMP", panel, "☠", 24, Red, new Vector2(-77, 0), new Vector2(34, 32));
                Text("RemainingLabel_TMP", panel, "남은 적", 18, Ivory, new Vector2(-25, 0), new Vector2(80, 28));
                var value = Text("RemainingValue_TMP", panel, "23", 27, Gold, new Vector2(72, 0), new Vector2(70, 34));

                var wave = root.GetComponent<WaveHUD>();
                Set(wave, "remainingEnemyText", value);
                PrefabUtility.SaveAsPrefabAsset(root, WavePrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void BuildWaveAlert()
        {
            var root = PrefabUtility.LoadPrefabContents(GameHUDPrefabPath);
            try
            {
                var old = root.transform.Find("WaveAlertHUD");
                if (old) Object.DestroyImmediate(old.gameObject);

                var alert = Rect("WaveAlertHUD", root.transform, new Vector2(0, 175), new Vector2(1120, 190));
                var group = alert.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;

                Image("OuterGlow", alert, new Color(.5f, 0f, 0f, .16f), Vector2.zero, new Vector2(1120, 190));
                Image("Banner", alert, new Color(.012f, .006f, .008f, .88f), Vector2.zero, new Vector2(1050, 154));
                Image("CrimsonWash", alert, new Color(.35f, .005f, .01f, .22f), Vector2.zero, new Vector2(820, 145));
                Image("TopLine", alert, Crimson, new Vector2(0, 76), new Vector2(1090, 3));
                Image("BottomLine", alert, Crimson, new Vector2(0, -76), new Vector2(1090, 3));
                Image("GoldTop", alert, Gold, new Vector2(0, 80), new Vector2(820, 2));
                Image("GoldBottom", alert, Gold, new Vector2(0, -80), new Vector2(820, 2));
                Diamond("RubyTop", alert, new Vector2(0, 79), new Vector2(31, 31), Red);
                Diamond("RubyBottom", alert, new Vector2(0, -79), new Vector2(22, 22), Red);
                var title = Text("WaveTitle_TMP", alert, "WAVE 1", 64, Ivory, new Vector2(0, 20), new Vector2(600, 78));
                title.fontStyle = FontStyles.Bold;
                title.outlineColor = new Color(.45f, 0f, 0f, .9f);
                title.outlineWidth = .2f;
                Text("Subtitle_TMP", alert, "적의 공세가 시작됩니다", 25, Ivory, new Vector2(0, -43), new Vector2(520, 42));

                var view = alert.gameObject.AddComponent<WaveAlertHUD>();
                Set(view, "canvasGroup", group);
                Set(view, "waveText", title);

                var controller = root.GetComponent<GameHUDController>();
                Set(controller, "waveAlert", view);
                alert.gameObject.SetActive(false);
                alert.SetAsLastSibling();
                PrefabUtility.SaveAsPrefabAsset(root, GameHUDPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static Image Image(string name, Transform parent, Color color, Vector2 position, Vector2 size)
        {
            var rect = Rect(name, parent, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static void Diamond(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var image = Image(name, parent, color, position, size);
            image.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            var inner = Image("Inner", image.transform, Black, Vector2.zero, size * .55f);
            inner.rectTransform.localRotation = Quaternion.identity;
        }

        static TMP_Text Text(string name, Transform parent, string content, float size, Color color, Vector2 position, Vector2 area)
        {
            var rect = Rect(name, parent, position, area);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        static void Set(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif

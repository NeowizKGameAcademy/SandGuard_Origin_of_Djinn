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
        const string WaveBannerSpritePath = "Assets/4.Sprite/UI/HUD/Wave/WaveStartBanner.png";
        const string EnemyFrameSpritePath = "Assets/4.Sprite/UI/HUD/Wave/RemainingEnemyFrame.png";
        static readonly Color Black = new(.018f, .012f, .014f, .94f);
        static readonly Color Crimson = new(.48f, .018f, .025f, 1f);
        static readonly Color Red = new(.92f, .075f, .06f, 1f);
        static readonly Color Gold = new(1f, .72f, .22f, 1f);
        static readonly Color Ivory = new(1f, .94f, .79f, 1f);
        static TMP_FontAsset font;

        [MenuItem("SandGuard/HUD/Build Wave Warning UI")]
        public static void Build()
        {
            ConfigureSprite(WaveBannerSpritePath);
            ConfigureSprite(EnemyFrameSpritePath);
            AssetDatabase.Refresh();
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

                var panel = Rect("RemainingEnemyPanel", root.transform, new Vector2(0, -58), new Vector2(258, 58));
                SpriteImage("FrameSprite", panel, EnemyFrameSpritePath, Vector2.zero, new Vector2(258, 58));
                Text("RemainingLabel_TMP", panel, "남은 적", 17, Ivory, new Vector2(-16, 0), new Vector2(82, 30));
                var value = Text("RemainingValue_TMP", panel, "23", 25, Gold, new Vector2(72, 0), new Vector2(55, 34));

                var wave = root.GetComponent<WaveHUD>();
                Set(wave, "remainingEnemyPanel", panel.gameObject);
                Set(wave, "remainingEnemyText", value);
                panel.gameObject.SetActive(false);
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

                var alert = Rect("WaveAlertHUD", root.transform, new Vector2(0, 190), new Vector2(1120, 230));
                var group = alert.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;

                SpriteImage("BannerSprite", alert, WaveBannerSpritePath, Vector2.zero, new Vector2(1120, 230));
                var title = Text("WaveTitle_TMP", alert, "WAVE 1", 60, Ivory, new Vector2(0, 18), new Vector2(560, 70));
                title.fontStyle = FontStyles.Bold;
                title.outlineColor = new Color(.45f, 0f, 0f, .9f);
                title.outlineWidth = .2f;
                Text("Subtitle_TMP", alert, "적의 공세가 시작됩니다", 22, Ivory, new Vector2(0, -39), new Vector2(500, 36));

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

        static Image SpriteImage(string name, Transform parent, string path, Vector2 position, Vector2 size)
        {
            var image = Image(name, parent, Color.white, position, size);
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            image.preserveAspect = true;
            return image;
        }

        static void ConfigureSprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        static void AddCorner(string name, Transform parent, float x)
        {
            Image(name + "Red", parent, Crimson, new Vector2(x, 0), new Vector2(3, 112));
            Image(name + "GoldTop", parent, Gold, new Vector2(x + Mathf.Sign(x) * 18, 57), new Vector2(38, 2));
            Image(name + "GoldBottom", parent, Gold, new Vector2(x + Mathf.Sign(x) * 18, -57), new Vector2(38, 2));
            Diamond(name + "Ruby", parent, new Vector2(x, 0), new Vector2(17, 17), Red);
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

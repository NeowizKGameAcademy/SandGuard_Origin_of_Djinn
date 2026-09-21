#if UNITY_EDITOR
using System.Linq;
using SandGuard.Cutscenes;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SandGuard.Cutscenes.Editor
{
    public static class StoryMenuBuilder
    {
        const string MainScenePath = "Assets/1.Scene/MainScene.unity";
        const string PrologueSceneName = "PrologueCutscene";
        const string PrologueImagePath = "Assets/Cutscenes/Art/Prologue7.png";
        const string IntroImagePath = "Assets/Cutscenes/Art/Intro7.png";
        const string EndingImagePath = "Assets/Cutscenes/Art/Ending9.png";
        const string StoryButtonPath = "Assets/4.Sprite/UI/MainScene/Button_Story.png";
        const string RuntimeCursorPath = "Assets/4.Sprite/UI/MainScene/Cursor_Runtime.png";

        [MenuItem("SandGuard/Cutscenes/Build Story Menu")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            PrepareSprite(StoryButtonPath);
            PrepareCursor(RuntimeCursorPath);
            var canvas = Object.FindFirstObjectByType<Canvas>();
            var start = Find("GameStart");
            if (!canvas || !start) throw new System.InvalidOperationException("MainScene의 Canvas 또는 GameStart 버튼을 찾지 못했습니다.");
            AssignRuntimeCursor();

            DeleteExisting(canvas.transform, "StoryMenuSystem");
            DeleteExisting(start.transform.parent, "Story");

            TMP_FontAsset font = FindFont();
            Button storyButton = CreateStoryButton(start.transform.parent, font);
            var system = new GameObject("StoryMenuSystem", typeof(RectTransform), typeof(StoryMenuController));
            system.transform.SetParent(canvas.transform, false);
            Stretch(system.GetComponent<RectTransform>());

            var overlay = CreateImage("StoryPopup", system.transform, new Color(0.015f, 0.02f, 0.035f, 0.96f));
            Stretch(overlay.rectTransform);
            var popup = overlay.gameObject;

            var frame = CreateImage("Frame", popup.transform, new Color(0.035f, 0.045f, 0.065f, 1f));
            SetRect(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1500f, 820f), Vector2.zero);
            var outline = frame.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.92f, 0.66f, 0.2f, 1f);
            outline.effectDistance = new Vector2(3f, -3f);

            CreateText("Title", frame.transform, font, "STORY ARCHIVE", 44f, FontStyles.Bold,
                new Color(1f, 0.83f, 0.46f), new Vector2(0f, 348f), new Vector2(700f, 70f), TextAlignmentOptions.Center);
            CreateText("Guide", frame.transform, font, "지나온 이야기를 선택해 다시 감상할 수 있습니다.", 24f, FontStyles.Normal,
                new Color(0.82f, 0.84f, 0.88f), new Vector2(0f, 300f), new Vector2(850f, 42f), TextAlignmentOptions.Center);

            Button close = CreateSimpleButton("Close", frame.transform, font, "×", new Vector2(695f, 362f), new Vector2(58f, 58f));
            Sprite prologue = AssetDatabase.LoadAssetAtPath<Sprite>(PrologueImagePath);
            Sprite intro = AssetDatabase.LoadAssetAtPath<Sprite>(IntroImagePath);
            Sprite ending = AssetDatabase.LoadAssetAtPath<Sprite>(EndingImagePath);
            Button prologueButton = CreateCard(frame.transform, font, "PrologueCard", new Vector2(-455f, -30f), prologue,
                new Color(1f, 1f, 1f), "01", "PROLOGUE", "모래가 가리킨 곳", out TMP_Text prologueState);
            Button introButton = CreateCard(frame.transform, font, "IntroCard", new Vector2(0f, -30f), intro,
                new Color(0.9f, 0.9f, 0.9f), "02", "INTRO", "깨어난 마석", out TMP_Text introState);
            Button endingButton = CreateCard(frame.transform, font, "EndingCard", new Vector2(455f, -30f), ending,
                Color.white, "03", "ENDING", "첫 번째 지니", out TMP_Text endingState);

            var controller = system.GetComponent<StoryMenuController>();
            var so = new SerializedObject(controller);
            so.FindProperty("storyButton").objectReferenceValue = storyButton;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.FindProperty("popup").objectReferenceValue = popup;
            var entries = so.FindProperty("stories");
            entries.arraySize = 3;
            SetEntry(entries.GetArrayElementAtIndex(0), "Prologue", PrologueSceneName, true, prologueButton, prologueState);
            SetEntry(entries.GetArrayElementAtIndex(1), "Intro", "IntroCutscene", true, introButton, introState);
            SetEntry(entries.GetArrayElementAtIndex(2), "Ending", "EndingCutscene", true, endingButton, endingState);
            so.ApplyModifiedPropertiesWithoutUndo();

            popup.SetActive(false);
            MarkPrologueSequence();
            ValidateBuiltUi(canvas, storyButton, prologueButton);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Story menu built: Story button, popup, and three story cards are ready.");
        }

        static Button CreateStoryButton(Transform parent, TMP_FontAsset font)
        {
            var image = CreateImage("Story", parent, Color.white);
            image.rectTransform.sizeDelta = new Vector2(529.2f, 124.8f);
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(StoryButtonPath);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.87f, 0.56f);
            colors.pressedColor = new Color(0.82f, 0.64f, 0.32f);
            button.colors = colors;
            int quitIndex = parent.Cast<Transform>().ToList().FindIndex(t => t.name == "Quit");
            if (quitIndex >= 0) image.transform.SetSiblingIndex(quitIndex);
            return button;
        }

        static Button CreateCard(Transform parent, TMP_FontAsset font, string name, Vector2 position, Sprite sprite,
            Color tint, string number, string title, string subtitle, out TMP_Text state)
        {
            var outer = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(SlantedStoryCardGraphic), typeof(Button));
            outer.transform.SetParent(parent, false);
            SetRect(outer.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(365f, 590f), position);
            var gold = outer.GetComponent<SlantedStoryCardGraphic>();
            gold.color = new Color(0.92f, 0.63f, 0.15f, 1f);

            var inner = new GameObject("Artwork", typeof(RectTransform), typeof(CanvasRenderer), typeof(SlantedStoryCardGraphic));
            inner.transform.SetParent(outer.transform, false);
            var innerRect = inner.GetComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero; innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(5f, 5f); innerRect.offsetMax = new Vector2(-5f, -5f);
            var art = inner.GetComponent<SlantedStoryCardGraphic>();
            art.Sprite = sprite;
            art.PreserveAspectFill = true;
            art.color = sprite ? tint : new Color(tint.r, tint.g, tint.b, 1f);
            outer.GetComponent<Button>().targetGraphic = art;

            var shade = CreateImage("CaptionShade", outer.transform, new Color(0.015f, 0.02f, 0.03f, 0.88f));
            var sr = shade.rectTransform;
            sr.anchorMin = new Vector2(0.08f, 0f); sr.anchorMax = new Vector2(0.92f, 0f);
            sr.pivot = new Vector2(0.5f, 0f); sr.anchoredPosition = new Vector2(0f, 20f); sr.sizeDelta = new Vector2(0f, 185f);
            CreateText("Number", shade.transform, font, number, 46f, FontStyles.Bold, new Color(1f, 0.84f, 0.48f),
                new Vector2(0f, 56f), new Vector2(270f, 52f), TextAlignmentOptions.Left);
            CreateText("Title", shade.transform, font, title, 31f, FontStyles.Bold, Color.white,
                new Vector2(0f, 12f), new Vector2(270f, 42f), TextAlignmentOptions.Left);
            CreateText("Subtitle", shade.transform, font, subtitle, 22f, FontStyles.Normal, new Color(0.84f, 0.85f, 0.88f),
                new Vector2(0f, -27f), new Vector2(270f, 36f), TextAlignmentOptions.Left);
            state = CreateText("State", shade.transform, font, "준비 중", 20f, FontStyles.Bold, new Color(0.3f, 0.78f, 1f),
                new Vector2(0f, -62f), new Vector2(270f, 30f), TextAlignmentOptions.Left);
            return outer.GetComponent<Button>();
        }

        static Button CreateSimpleButton(string name, Transform parent, TMP_FontAsset font, string label, Vector2 position, Vector2 size)
        {
            var image = CreateImage(name, parent, new Color(0.12f, 0.09f, 0.055f, 1f));
            SetRect(image.rectTransform, new Vector2(0.5f, 0.5f), size, position);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            CreateText("Label", image.transform, font, label, 42f, FontStyles.Normal, Color.white, Vector2.zero, size, TextAlignmentOptions.Center);
            return button;
        }

        static TMP_Text CreateText(string name, Transform parent, TMP_FontAsset font, string value, float size,
            FontStyles style, Color color, Vector2 position, Vector2 dimensions, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            SetRect(go.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), dimensions, position);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = font; text.text = value; text.fontSize = size; text.fontStyle = style;
            text.color = color; text.alignment = alignment; text.raycastTarget = false;
            text.enableWordWrapping = false;
            return text;
        }

        static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>(); image.color = color;
            return image;
        }

        static void SetEntry(SerializedProperty p, string id, string scene, bool available, Button button, TMP_Text state)
        {
            p.FindPropertyRelative("storyId").stringValue = id;
            p.FindPropertyRelative("sceneName").stringValue = scene;
            p.FindPropertyRelative("available").boolValue = available;
            p.FindPropertyRelative("button").objectReferenceValue = button;
            p.FindPropertyRelative("stateText").objectReferenceValue = state;
        }

        static void MarkPrologueSequence()
        {
            var sequence = AssetDatabase.LoadAssetAtPath<CutSceneSequence>("Assets/Cutscenes/PrologueSequence.asset");
            if (!sequence) return;
            var so = new SerializedObject(sequence);
            so.FindProperty("storyId").stringValue = "Prologue";
            so.FindProperty("playOnceAutomatically").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sequence);
        }

        static TMP_FontAsset FindFont()
        {
            string guid = AssetDatabase.FindAssets("Pretendard-Bold SDF t:TMP_FontAsset").FirstOrDefault();
            return string.IsNullOrEmpty(guid) ? TMP_Settings.defaultFontAsset : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
        }

        static void PrepareSprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
            if (importer.textureType == TextureImporterType.Sprite && importer.alphaIsTransparency) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        static void PrepareCursor(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
            importer.textureType = TextureImporterType.Cursor;
            importer.isReadable = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        static void AssignRuntimeCursor()
        {
            var cursorObject = Find("CursorManager");
            if (!cursorObject || !cursorObject.TryGetComponent<CursorManager>(out var cursorManager)) return;
            var so = new SerializedObject(cursorManager);
            so.FindProperty("cursorTexture").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(RuntimeCursorPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cursorManager);
        }

        static void ValidateBuiltUi(Canvas canvas, Button storyButton, Button prologueButton)
        {
            if (!storyButton || !storyButton.image || !storyButton.image.sprite)
                throw new System.InvalidOperationException("스토리 버튼 스프라이트가 연결되지 않았습니다.");
            var cards = canvas.GetComponentsInChildren<SlantedStoryCardGraphic>(true);
            if (cards.Length != 6 || cards.Any(card => !card.GetComponent<CanvasRenderer>()))
                throw new System.InvalidOperationException("스토리 카드의 CanvasRenderer 구성이 올바르지 않습니다.");
            var artwork = prologueButton.transform.Find("Artwork")?.GetComponent<SlantedStoryCardGraphic>();
            if (!artwork || !artwork.Sprite)
                throw new System.InvalidOperationException("프롤로그 카드 대표 이미지가 연결되지 않았습니다.");
        }

        static GameObject Find(string name) => GameObject.Find(name);
        static void DeleteExisting(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child) Object.DestroyImmediate(child.gameObject);
        }
        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        }
        static void SetRect(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
        {
            rect.anchorMin = anchor; rect.anchorMax = anchor; rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size; rect.anchoredPosition = position;
        }
    }
}
#endif

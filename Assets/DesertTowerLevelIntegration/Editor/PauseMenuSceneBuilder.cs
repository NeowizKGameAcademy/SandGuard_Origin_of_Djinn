using DesertTower.LevelIntegration;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DesertTower.LevelIntegration.Editor
{
    public static class PauseMenuSceneBuilder
    {
        const string ScenePath = "Assets/1.Scene/Level.unity";
        const string Art = "Assets/4.Sprite/UI/GameScene/Pause/";
        const string FontPath = "Assets/9.Font/Pretendard-Bold SDF.asset";

        [MenuItem("SandGuard/Build Level Pause Menu")]
        public static void Build()
        {
            ConfigureSprite(Art + "PausePanel.png", new Vector4(92, 92, 92, 92));
            ConfigureSprite(Art + "PauseButtonFrame.png", new Vector4(70, 55, 70, 55));
            ConfigureSprite(Art + "ResumeButton.png", Vector4.zero);
            ConfigureSprite(Art + "MainButton.png", Vector4.zero);
            ConfigureSprite(Art + "PauseHudButton.png", Vector4.zero);
            ConfigureSprite(Art + "ResumeArrow.png", Vector4.zero);
            ConfigureSprite(Art + "PauseDivider.png", Vector4.zero);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EnsureEventSystem();
            var old = GameObject.Find("PauseMenuCanvas");
            if (old) Object.DestroyImmediate(old);

            var canvasGo = new GameObject("PauseMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(PauseMenuController));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 450;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;

            var popup = new GameObject("PausePopup", typeof(RectTransform), typeof(Image));
            popup.transform.SetParent(canvasGo.transform, false);
            var popupRect = popup.GetComponent<RectTransform>();
            popupRect.anchorMin = Vector2.zero; popupRect.anchorMax = Vector2.one; popupRect.offsetMin = popupRect.offsetMax = Vector2.zero;
            popup.GetComponent<Image>().color = new Color(0.015f, 0.008f, 0.004f, .68f);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(popup.transform, false);
            SetRect(panel.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(610, 700));
            var panelImage = panel.GetComponent<Image>(); panelImage.sprite = Sprite("PausePanel.png"); panelImage.type = Image.Type.Sliced;

            Text("Title", panel.transform, "일시정지", 52, new Vector2(0, 225), new Vector2(430, 80), new Color(1f, .79f, .34f));
            var divider = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            divider.transform.SetParent(panel.transform, false);
            SetRect(divider.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 160), new Vector2(390, 58));
            divider.GetComponent<Image>().sprite = Sprite("PauseDivider.png"); divider.GetComponent<Image>().preserveAspect = true;

            var resume = ImageButton("Resume", panel.transform, Sprite("ResumeButton.png"));
            SetRect(resume.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 35), new Vector2(360, 120));

            var main = ImageButton("Main", panel.transform, Sprite("MainButton.png"));
            SetRect(main.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -115), new Vector2(360, 120));

            var note = Text("Hint", panel.transform, "ESC 키로 게임을 계속할 수 있습니다", 21, new Vector2(0, -235), new Vector2(440, 45), new Color(.76f, .69f, .57f));
            note.fontStyle = FontStyles.Normal;

            var controller = canvasGo.GetComponent<PauseMenuController>();
            UnityEventTools.AddPersistentListener(resume.onClick, controller.Resume);
            UnityEventTools.AddPersistentListener(main.onClick, controller.ReturnToMain);

            var so = new SerializedObject(controller);
            so.FindProperty("popupRoot").objectReferenceValue = popup;
            so.FindProperty("resumeButton").objectReferenceValue = resume;
            so.FindProperty("mainButton").objectReferenceValue = main;
            so.ApplyModifiedPropertiesWithoutUndo();

            popup.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("PauseMenuSceneBuilder: Level scene pause menu built.");
        }

        [MenuItem("SandGuard/Validate Level Pause Menu")]
        public static void Validate()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var root = GameObject.Find("PauseMenuCanvas");
            if (!root || !root.GetComponent<PauseMenuController>()) throw new System.Exception("PauseMenuCanvas/controller missing");
            var eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (!eventSystem || !eventSystem.GetComponent<InputSystemUIInputModule>())
                throw new System.Exception("Input System EventSystem missing");
            var buttons = root.GetComponentsInChildren<Button>(true);
            if (buttons.Length != 2) throw new System.Exception($"Expected 2 pause buttons, found {buttons.Length}");
            if (buttons[0].onClick.GetPersistentEventCount() == 0 || buttons[1].onClick.GetPersistentEventCount() == 0)
                throw new System.Exception("Pause button actions are not serialized");
            var popup = root.transform.Find("PausePopup");
            if (!popup || popup.gameObject.activeSelf) throw new System.Exception("PausePopup initial state invalid");
            Debug.Log("PAUSE_MENU_VALIDATION_PASS");
        }

        static Button ImageButton(string name, Transform parent, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>(); image.sprite = sprite; image.type = Image.Type.Simple;
            var button = go.GetComponent<Button>();
            var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1f, .93f, .72f); colors.pressedColor = new Color(.72f, .58f, .35f); colors.fadeDuration = .08f; button.colors = colors;
            return button;
        }

        static TextMeshProUGUI Text(string name, Transform parent, string value, float size, Vector2 pos, Vector2 box, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            SetRect(go.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f), pos, box);
            var text = go.GetComponent<TextMeshProUGUI>(); text.text = value; text.fontSize = size; text.alignment = TextAlignmentOptions.Center; text.color = color; text.fontStyle = FontStyles.Bold; text.raycastTarget = false;
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            return text;
        }

        static void SetRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 pos, Vector2 size)
        { rect.anchorMin = min; rect.anchorMax = max; rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = pos; rect.sizeDelta = size; }
        static Sprite Sprite(string file) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + file);
        static void EnsureEventSystem()
        {
            var systems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            EventSystem eventSystem = systems.Length > 0 ? systems[0] : null;
            if (!eventSystem)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem));
                eventSystem = go.GetComponent<EventSystem>();
            }

            var legacy = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacy) Object.DestroyImmediate(legacy);
            var input = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (!input) input = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            input.AssignDefaultActions();
            eventSystem.firstSelectedGameObject = null;
        }

        static void ConfigureSprite(string path, Vector4 border)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.spritePixelsPerUnit = 100; importer.spriteBorder = border; importer.SaveAndReimport();
        }
    }
}

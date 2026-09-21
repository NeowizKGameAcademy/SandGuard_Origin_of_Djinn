#if UNITY_EDITOR
using System;
using System.Linq;
using SandGuard.Cutscenes;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SandGuard.Cutscenes.Editor
{
    public static class EndingSceneBuilder
    {
        const string Root = "Assets/Cutscenes";
        const string Art = Root + "/Art/";
        const string ScenePath = "Assets/1.Scene/EndingCutscene.unity";
        const string SequencePath = Root + "/EndingSequence.asset";
        const string MainScenePath = "Assets/1.Scene/MainScene.unity";

        [MenuItem("SandGuard/Cutscenes/Build Ending Scene")]
        public static void Build()
        {
            for (int i = 1; i <= 9; i++) ImportSprite(Art + $"Ending{i}.png");
            ImportSprite(Art + "CutScene_Pannel.png");

            var sequence = AssetDatabase.LoadAssetAtPath<CutSceneSequence>(SequencePath);
            if (!sequence)
            {
                sequence = ScriptableObject.CreateInstance<CutSceneSequence>();
                AssetDatabase.CreateAsset(sequence, SequencePath);
            }
            sequence.storyId = "Ending";
            sequence.playOnceAutomatically = true;
            sequence.nextScene = "MainScene";
            sequence.fadeDuration = 0.7f;
            sequence.cuts = MakeCuts();
            EditorUtility.SetDirty(sequence);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;

            var canvasObject = new GameObject("Cutscene Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var visual = Rect("Visual Sequence", canvasObject.transform);
            Stretch(visual);
            var group = visual.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            var background = Image("Cut Image", visual, sequence.cuts[0].image);
            Stretch(background.rectTransform);
            background.preserveAspect = true;
            background.raycastTarget = false;

            var panel = Image("Story Panel", visual, AssetDatabase.LoadAssetAtPath<Sprite>(Art + "CutScene_Pannel.png"));
            Place(panel.rectTransform, new Vector2(0.77f, 0.5f), new Vector2(850, 850));
            panel.color = new Color(1f, 1f, 1f, 200f / 255f);
            panel.preserveAspect = true;
            panel.raycastTarget = false;

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/9.Font/Pretendard-Bold SDF.asset");
            if (!font) throw new InvalidOperationException("Pretendard-Bold SDF font is missing.");

            var title = Text("Cut Title", panel.transform, font, sequence.cuts[0].title, 44, new Color(1f, 0.82f, 0.48f));
            Box(title.rectTransform, new Vector2(0, 292), new Vector2(620, 56));
            title.alignment = TextAlignmentOptions.Center;
            var body = Text("Story Text", panel.transform, font, sequence.cuts[0].body, 31, new Color(0.96f, 0.89f, 0.74f));
            Box(body.rectTransform, new Vector2(0, 0), new Vector2(680, 540));
            body.alignment = TextAlignmentOptions.TopLeft;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.enableAutoSizing = true;
            body.fontSizeMin = 20;
            body.fontSizeMax = 31;
            body.lineSpacing = 5;
            var progress = Text("Cut Number", panel.transform, font, "01 / 09", 26, new Color(0.47f, 0.75f, 0.97f));
            Box(progress.rectTransform, new Vector2(0, -293), new Vector2(140, 35));
            progress.alignment = TextAlignmentOptions.Center;
            foreach (var text in new TMP_Text[] { title, body, progress }) text.fontStyle |= FontStyles.Bold;

            var next = Button("Next Button", visual, font, "다음  ▶", new Vector2(0.9f, 0.055f), new Vector2(170, 50));
            var skip = Button("Skip Button", visual, font, "건너뛰기", new Vector2(0.93f, 0.95f), new Vector2(150, 48));

            var manager = new GameObject("CutSceneManager", typeof(CutSceneManager)).GetComponent<CutSceneManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("sequence").objectReferenceValue = sequence;
            so.FindProperty("background").objectReferenceValue = background;
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("bodyText").objectReferenceValue = body;
            so.FindProperty("progressText").objectReferenceValue = progress;
            so.FindProperty("visibleGroup").objectReferenceValue = group;
            so.FindProperty("nextButton").objectReferenceValue = next;
            so.FindProperty("skipButton").objectReferenceValue = skip;
            so.ApplyModifiedPropertiesWithoutUndo();

            new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            UpdateStoryMenu();
            AssetDatabase.SaveAssets();
            Debug.Log("Ending cutscene scene and nine-cut sequence built: " + ScenePath);
        }

        [MenuItem("SandGuard/Cutscenes/Validate Ending Scene")]
        public static void Validate()
        {
            var sequence = AssetDatabase.LoadAssetAtPath<CutSceneSequence>(SequencePath);
            if (!sequence || sequence.storyId != "Ending" || sequence.cuts == null || sequence.cuts.Length != 9)
                throw new InvalidOperationException("Ending sequence must contain exactly nine cuts.");
            foreach (var cut in sequence.cuts)
                if (!cut.image || string.IsNullOrWhiteSpace(cut.title) || string.IsNullOrWhiteSpace(cut.body))
                    throw new InvalidOperationException("Ending cut is missing an image, title, or body.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var manager = UnityEngine.Object.FindFirstObjectByType<CutSceneManager>();
            var body = UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None).FirstOrDefault(x => x.name == "Story Text");
            if (!scene.IsValid() || !manager || !body || !body.font || body.font.name.IndexOf("Pretendard", StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidOperationException("Ending scene UI or Pretendard font is incomplete.");
            var area = body.rectTransform.rect;
            foreach (var cut in sequence.cuts)
            {
                body.enableAutoSizing = false;
                body.fontSize = body.fontSizeMin;
                body.text = cut.body;
                body.ForceMeshUpdate();
                var required = body.GetPreferredValues(cut.body, area.width, Mathf.Infinity);
                if (required.y > area.height) throw new InvalidOperationException($"Text overflow in {cut.title}: {required.y:0} > {area.height:0}");
            }
            if (!EditorBuildSettings.scenes.Any(s => s.enabled && s.path == ScenePath))
                throw new InvalidOperationException("EndingCutscene is absent from Build Settings.");
            var main = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            var controller = UnityEngine.Object.FindFirstObjectByType<StoryMenuController>(FindObjectsInactive.Include);
            var controllerSo = new SerializedObject(controller);
            var stories = controllerSo.FindProperty("stories");
            bool endingLinked = false;
            for (int i = 0; i < stories.arraySize; i++)
            {
                var entry = stories.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("storyId").stringValue != "Ending") continue;
                endingLinked = entry.FindPropertyRelative("available").boolValue &&
                               entry.FindPropertyRelative("sceneName").stringValue == "EndingCutscene" &&
                               entry.FindPropertyRelative("button").objectReferenceValue;
            }
            var art = UnityEngine.Object.FindObjectsByType<SlantedStoryCardGraphic>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(x => x.name == "Artwork" && x.transform.parent && x.transform.parent.name == "EndingCard");
            if (!main.IsValid() || !endingLinked || !art || !art.Sprite) throw new InvalidOperationException("Ending story card is not fully linked.");
            Debug.Log("ENDING_VALIDATED: 9 images, 9 texts, panel, Pretendard font, manager, build scene, story card.");
        }

        static CutSceneSequence.Cut[] MakeCuts()
        {
            string[] titles =
            {
                "최후의 공방", "끝났다고 믿었던 싸움", "통제를 잃은 마석", "사막을 뒤덮은 오염", "봉인의 대가",
                "마지막으로 바라본 사막", "자유를 위한 선택", "영원한 봉인", "첫 번째 지니"
            };
            string[] bodies =
            {
                "끝없이 몰려들던 도적들과의 싸움은 어느새 신전 중심부까지 번져 있었다.\n\n수호탑에서는 불길과 번개가 쏟아졌고, 깨어난 아누비스와 해골 병사들은 무너진 통로와 계단을 지키며 침입자들을 막아섰다. 도적들도 물러서지 않았다. 사방에서 신전의 벽을 기어오르고, 수호자들을 밀어내며 마석을 향해 전진했다.\n\n나는 램프에서 끌어낸 마력으로 전장을 오가며 무너지는 방어선을 지켜냈다. 처음에는 다루는 것조차 어려웠던 힘이 이제는 내 손짓을 따라 적들을 향해 뻗어 나갔다.",
                "결국, 마지막 남은 도적이 쓰러지자 신전을 뒤덮었던 함성이 멎었다.\n\n최종 보스가 힘을 잃고 무너졌고, 살아남은 도적들은 무기를 버린 채 사막으로 달아났다. 곳곳에 부서진 수호탑과 무너진 벽이 남았지만, 신전 중앙의 마석은 여전히 자리를 지키고 있었다.\n\n온몸에 힘이 남아 있지 않았다. 그래도 끝까지 마석을 지켜냈다는 사실에 안도하며 거친 숨을 내쉬었다.\n\n이제 모든 것이 끝났다고 생각했다.\n\n그러나 잠시 후, 신전 아래에서 불길한 진동이 울려 퍼졌다.",
                "마석의 표면에 검은 균열이 번지기 시작했다.\n\n푸른빛 사이로 탁하고 붉은 마력이 새어 나왔고, 공중을 맴돌던 파편들이 거칠게 흔들렸다. 나는 램프의 힘으로 마석을 진정시키려 했지만, 이미 내부에 쌓인 마력은 통제할 수 있는 수준을 넘어선 뒤였다.\n\n도적들이 마석을 차지하기 위해 가한 공격과 내가 끌어낸 힘이 서로 뒤엉켜 있었다. 오랫동안 잠들어 있던 마석은 그 모든 힘을 견디지 못하고 무너지고 있었다.\n\n균열이 마석 전체를 뒤덮은 순간, 거대한 폭발과 함께 오염된 마력이 신전 밖으로 쏟아져 나갔다.",
                "검붉은 마력은 폭풍이 되어 사막 전역으로 퍼져 나갔다.\n\n신전 주변의 모래는 검게 물들었고, 마력에 휩쓸린 생명들은 이성을 잃은 채 서로를 공격하기 시작했다. 메마른 땅에서 살아가던 짐승들마저 흉포한 괴물로 변해 갔다.\n\n멀리 바라본 하늘 아래에는 내가 살던 마을과 피라미드 발굴 현장이 있었다.\n\n오염된 폭풍은 그곳까지 집어삼키고 있었다. 함께 돌을 나르던 사람들과 매일 인사를 나누던 이웃들이 검은 마력 속에서 고통받는 모습이 보였다.\n\n마석을 지키면 모두를 구할 수 있을 거라 믿었다. 그러나 내가 깨운 힘은 이제 내가 사랑했던 모든 것을 파괴하고 있었다.",
                "급박한 상황, 어떻게 해야할까 고민하던 와중, 램프가 떠올랐다. 그래, 마력을 담아둘수있는 이 램프에 모든 흘러나오는 모든 마력을 담아넣자. 하지만 램프에 마력을 가두는 것만으로는 부족했다.\n\n폭주하는 힘이 다시 밖으로 흘러나오지 않게 하려면, 램프 안에서 그 힘을 붙들어 둘 영혼이 필요했다. 영원히 떠날 수 없는 닻이 되어 마력을 억누를 존재가 있어야 했다.",
                "나는 신전 가장 높은 곳에 올라 사막을 바라봤다.\n\n어느새 해가 저물고, 밤하늘에는 수없이 많은 별과 은하수가 펼쳐져 있었다. 달빛을 받은 모래언덕은 바람을 따라 끝없이 이어졌고, 그 너머에는 내가 평생 바라보며 동경했던 자유로운 세상이 있었다.\n\n언젠가는 저 모래언덕 너머까지 가 보고 싶었다. 누구의 명령도 받지 않고, 발길이 닿는 곳이라면 어디든 떠나는 삶을 꿈꿨다.\n\n그것이 내가 바라던 자유였다.\n\n나는 눈을 감고 사막의 바람을 마지막으로 느꼈다. 그리고 다시 눈을 떴을 때, 무엇을 해야 하는지 이미 알고 있었다.",
                "내가 자유를 포기하면, 다른 이들의 자유를 지킬 수 있다.\n\n마을의 사람들이 다시 평범한 하루를 살아가고, 아이들이 두려움 없이 사막을 달릴 수 있다. 오염된 마력에 휩쓸린 생명들도 본래의 모습을 되찾을 것이다.\n\n두렵지 않다면 거짓말이었다.\n\n한 번 램프 안으로 들어가면 다시는 이 하늘을 볼 수 없다. 그토록 사랑했던 사막을 내 발로 걸을 수도 없고, 누구와도 같은 시간을 살아갈 수 없게 된다.\n\n하지만 내가 시작한 일이었다. 이제는 내가 끝내야 했다.\n\n나는 램프를 가슴 가까이 끌어안고 소원을 빌었다.\n\n“이 사막에 살아가는 모두에게 자유를 돌려줘.”",
                "램프에서 눈부신 푸른빛이 터져 나왔다.\n\n사막을 뒤덮었던 오염된 마력이 흐름을 바꾸어 신전으로 돌아오기 시작했다. 마을과 발굴 현장, 검게 물든 모래와 생명들에게서 빠져나온 마력이 거대한 소용돌이를 이루며 램프 안으로 빨려 들어갔다.\n\n내 몸도 서서히 빛으로 변해 갔다.\n\n손끝이 사라지고, 발밑의 감각이 희미해졌다. 마지막 순간까지 눈앞의 사막을 바라보려 했지만, 세상은 점차 푸른빛 속에 잠겼다.\n\n마침내 마석의 폭주는 멈췄고, 오염된 마력은 나의 영혼과 함께 램프 안에 봉인되었다.\n\n사막에는 다시 고요한 바람이 불기 시작했다.",
                "폭풍이 사라진 모래밭 위에 낡은 램프 하나만이 남았다.\n\n사람들은 평화를 되찾았고, 검게 물들었던 모래도 서서히 본래의 빛을 되찾았다. 누구도 그날 신전에서 무슨 일이 있었는지 모두 알지는 못했다.\n\n시간이 흐르며 신전은 다시 모래에 묻혔고, 램프에 관한 이야기도 전설이 되어 잊혀 갔다.\n\n하지만 램프 안에서 나의 의식은 사라지지 않았다.\n\n좁고 끝없는 어둠 속에서도 밤하늘의 은하수와 바람이 지나가던 모래언덕을 떠올렸다. 다시는 닿을 수 없는 사막의 자유를 그리워하며, 오염된 마력이 깨어나지 않도록 붙들었다.\n\n얼마나 긴 시간이 흘렀는지는 알 수 없다.\n\n다만 언젠가 누군가 이 램프를 발견하고, 다시 한번 나를 세상 밖으로 불러주기를 기다릴 뿐이다.\n\n그렇게 한 인간의 마지막 소원과 희생으로, 사막의 수호하는 최초의 지니가 탄생했다."
            };
            float[] durations = { 17f, 18f, 18f, 19f, 16f, 18f, 20f, 19f, 21f };
            float[] panelXs = { .77f, .77f, .77f, .77f, .77f, .77f, .77f, .77f, .77f };
            var cuts = new CutSceneSequence.Cut[9];
            for (int i = 0; i < cuts.Length; i++) cuts[i] = new CutSceneSequence.Cut
            {
                title = titles[i], body = bodies[i], image = AssetDatabase.LoadAssetAtPath<Sprite>(Art + $"Ending{i + 1}.png"),
                duration = durations[i], panelX = panelXs[i], panelY = .5f
            };
            return cuts;
        }

        static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(s => s.path != ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            else scenes.First(s => s.path == ScenePath).enabled = true;
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void UpdateStoryMenu()
        {
            var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            var controller = UnityEngine.Object.FindFirstObjectByType<StoryMenuController>(FindObjectsInactive.Include);
            if (!controller) throw new InvalidOperationException("MainScene StoryMenuController is missing.");
            var so = new SerializedObject(controller);
            var stories = so.FindProperty("stories");
            for (int i = 0; i < stories.arraySize; i++)
            {
                var entry = stories.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("storyId").stringValue != "Ending") continue;
                entry.FindPropertyRelative("sceneName").stringValue = "EndingCutscene";
                entry.FindPropertyRelative("available").boolValue = true;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
            var artwork = UnityEngine.Object.FindObjectsByType<SlantedStoryCardGraphic>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(x => x.name == "Artwork" && x.transform.parent && x.transform.parent.name == "EndingCard");
            if (!artwork) throw new InvalidOperationException("MainScene EndingCard artwork is missing.");
            artwork.Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "Ending9.png");
            artwork.color = Color.white;
            EditorUtility.SetDirty(artwork);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void ImportSprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) throw new InvalidOperationException("Missing image: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        static RectTransform Rect(string name, Transform parent) { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return (RectTransform)go.transform; }
        static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; }
        static void Place(RectTransform rect, Vector2 anchor, Vector2 size) { rect.anchorMin = anchor; rect.anchorMax = anchor; rect.sizeDelta = size; rect.anchoredPosition = Vector2.zero; }
        static void Box(RectTransform rect, Vector2 position, Vector2 size) { Place(rect, new Vector2(.5f, .5f), size); rect.anchoredPosition = position; }
        static Image Image(string name, RectTransform parent, Sprite sprite) { var rect = Rect(name, parent); var image = rect.gameObject.AddComponent<Image>(); image.sprite = sprite; return image; }
        static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, string content, float size, Color color) { var rect = Rect(name, parent); var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = font; text.text = content; text.fontSize = size; text.color = color; text.raycastTarget = false; return text; }
        static Button Button(string name, RectTransform parent, TMP_FontAsset font, string caption, Vector2 anchor, Vector2 size)
        {
            var rect = Rect(name, parent); Place(rect, anchor, size);
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.035f, .025f, .02f, .85f);
            var button = rect.gameObject.AddComponent<Button>();
            var label = Text("Label", rect, font, caption, 28, new Color(1f, .82f, .48f));
            label.fontStyle |= FontStyles.Bold;
            Stretch(label.rectTransform); label.alignment = TextAlignmentOptions.Center;
            return button;
        }
    }
}
#endif

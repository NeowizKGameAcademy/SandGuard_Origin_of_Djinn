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
    public static class IntroSceneBuilder
    {
        const string Root = "Assets/Cutscenes";
        const string Art = Root + "/Art/";
        const string ScenePath = "Assets/1.Scene/IntroCutscene.unity";
        const string SequencePath = Root + "/IntroSequence.asset";
        const string MainScenePath = "Assets/1.Scene/MainScene.unity";

        [MenuItem("SandGuard/Cutscenes/Build Intro Scene")]
        public static void Build()
        {
            for (int i = 1; i <= 7; i++) ImportSprite(Art + $"Intro{i}.png");
            ImportSprite(Art + "CutScene_Pannel.png");

            var sequence = AssetDatabase.LoadAssetAtPath<CutSceneSequence>(SequencePath);
            if (!sequence)
            {
                sequence = ScriptableObject.CreateInstance<CutSceneSequence>();
                AssetDatabase.CreateAsset(sequence, SequencePath);
            }
            sequence.storyId = "Intro";
            sequence.playOnceAutomatically = true;
            sequence.nextScene = "Level";
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
            Place(panel.rectTransform, new Vector2(0.78f, 0.5f), new Vector2(760, 760));
            panel.preserveAspect = true;
            panel.raycastTarget = false;

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/9.Font/Pretendard-Bold SDF.asset");
            Material material = null;
            if (!font) throw new InvalidOperationException("Pretendard-Bold SDF font is missing.");

            var title = Text("Cut Title", panel.transform, font, sequence.cuts[0].title, 44, new Color(1f, 0.82f, 0.48f));
            Box(title.rectTransform, new Vector2(0, 253), new Vector2(540, 56));
            title.alignment = TextAlignmentOptions.Center;
            var body = Text("Story Text", panel.transform, font, sequence.cuts[0].body, 31, new Color(0.96f, 0.89f, 0.74f));
            Box(body.rectTransform, new Vector2(0, 3), new Vector2(590, 450));
            body.alignment = TextAlignmentOptions.TopLeft;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.enableAutoSizing = true;
            body.fontSizeMin = 20;
            body.fontSizeMax = 31;
            body.lineSpacing = 6;
            var progress = Text("Cut Number", panel.transform, font, "01 / 07", 26, new Color(0.47f, 0.75f, 0.97f));
            Box(progress.rectTransform, new Vector2(0, -254), new Vector2(140, 35));
            progress.alignment = TextAlignmentOptions.Center;
            foreach (var text in new TMP_Text[] { title, body, progress })
            {
                text.fontStyle |= FontStyles.Bold;
                if (material) text.fontSharedMaterial = material;
            }

            var next = Button("Next Button", visual, font, "다음  ▶", new Vector2(0.9f, 0.055f), new Vector2(170, 50), material);
            var skip = Button("Skip Button", visual, font, "건너뛰기", new Vector2(0.93f, 0.95f), new Vector2(150, 48), material);

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
            Debug.Log("Intro cutscene scene and seven-cut sequence built: " + ScenePath);
        }

        [MenuItem("SandGuard/Cutscenes/Validate Intro Scene")]
        public static void Validate()
        {
            var sequence = AssetDatabase.LoadAssetAtPath<CutSceneSequence>(SequencePath);
            if (!sequence || sequence.storyId != "Intro" || sequence.cuts == null || sequence.cuts.Length != 7)
                throw new InvalidOperationException("Intro sequence must contain exactly seven cuts.");
            foreach (var cut in sequence.cuts)
                if (!cut.image || string.IsNullOrWhiteSpace(cut.title) || string.IsNullOrWhiteSpace(cut.body))
                    throw new InvalidOperationException("Intro cut is missing an image, title, or body.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var manager = UnityEngine.Object.FindFirstObjectByType<CutSceneManager>();
            var body = UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None).FirstOrDefault(x => x.name == "Story Text");
            if (!scene.IsValid() || !manager || !body || !body.font) throw new InvalidOperationException("Intro scene UI is incomplete.");
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
                throw new InvalidOperationException("IntroCutscene is absent from Build Settings.");
            Debug.Log("INTRO_VALIDATED: 7 images, 7 texts, panel, font, manager, build scene.");
        }

        static CutSceneSequence.Cut[] MakeCuts()
        {
            string[] titles = { "신전으로 향하는 계단", "신전 정상의 마석", "램프와 마석의 공명", "흘러들어온 마력", "사막 전역으로 퍼진 파동", "빛을 쫓는 도적들", "다가오는 전투" };
            string[] bodies =
            {
                "모래폭풍이 걷힌 뒤에도 신전 정상의 푸른빛은 사라지지 않았다. 손에 쥔 램프 역시 그 빛에 반응하듯 희미하게 떨리고 있었다.\n\n나는 램프가 가리키는 방향을 따라 신전의 계단에 발을 올렸다. 가까이 다가갈수록 신전은 생각했던 것보다 훨씬 거대했고, 모래에 파묻혀 있던 곳이라고는 믿기 어려울 만큼 온전한 모습을 간직하고 있었다.\n\n계단 양옆에는 오래된 석탑과 기둥이 줄지어 서 있었다. 인기척은 없었지만, 누군가 뒤에서 나를 지켜보는 듯한 기분이 들었다.\n\n그래도 여기까지 온 이상, 정상에 무엇이 있는지 직접 확인하고 싶었다.",
                "긴 계단을 오르자 신전 중앙에 거대한 푸른 마석이 떠 있는 광장이 나타났다.\n\n마석 주위에는 부서진 파편들이 공중에 멈춘 채 천천히 돌고 있었다. 표면 안쪽에서는 푸른빛과 보랏빛이 뒤섞이며 맥박처럼 일정하게 깜빡였다.\n\n나는 그 압도적인 모습에 한동안 움직이지 못했다. 발굴 현장에서 수많은 유물과 석조물을 보았지만, 눈앞의 마석과 비교할 수 있는 것은 아무것도 없었다.\n\n그때 허리춤에 매달아 둔 램프가 더욱 거세게 진동하기 시작했다.",
                "램프를 꺼내 드는 순간, 표면에 새겨진 문양이 푸른빛으로 물들었다.\n\n곧 램프의 주둥이에서 가느다란 마력이 흘러나와 마석을 향해 뻗었다. 마석에서도 빛줄기가 내려와 램프와 맞닿았고, 두 물체 사이로 거대한 힘이 오가기 시작했다.\n\n나는 놀라 램프를 내려놓으려 했지만 손이 움직이지 않았다. 마치 보이지 않는 힘이 램프를 붙잡고 있는 것 같았다.\n\n모래폭풍 속에서 우연히 주운 물건이 이 신전의 마석과 연결되어 있으리라고는 상상조차 하지 못했다.",
                "마석에서 쏟아진 빛이 램프를 지나 내 몸으로 흘러들었다.\n\n뜨거운 기운이 팔을 타고 퍼졌고, 손바닥 앞에는 복잡한 문양으로 이루어진 마법진이 펼쳐졌다. 처음 접하는 힘이었지만, 손을 움직일 때마다 마법진도 내 의지를 따라 반응했다.\n\n당황한 나는 본능적으로 손을 뻗었다. 그러자 손안의 마법진이 강하게 공명하며 작은 빛의 구체를 만들어냈다.\n\n이 힘을 어떻게 멈춰야 하는지 알 수 없었다. 램프와 마석의 연결은 더욱 강해졌고, 신전 전체가 푸른빛에 잠기기 시작했다.\n\n그리고 그 순간, 오랫동안 잠들어 있던 마석이 완전히 깨어났다.",
                "마석에 쌓여 있던 마력이 거대한 파동이 되어 하늘로 솟구쳤다.\n\n푸른빛은 신전을 중심으로 원을 그리며 사막 전역으로 뻗어 나갔다. 신전 주변의 도시와 발굴 현장에서도 사람들이 하던 일을 멈추고 하늘을 올려다봤다.\n\n그 빛은 잠들어 있던 거대한 힘이 깨어났음을 알리는 신호였다.\n\n그러나 너무 늦게 깨달았다. 이 빛은 신전의 존재와 그 위치를 사막 전역에 드러내고 있었다.",
                "마력의 파동을 목격한 것은 도시의 사람들뿐만이 아니었다.\n\n사막 곳곳에 흩어져 있던 도적들도 하늘을 뒤덮은 푸른빛을 발견했다. 신전에 잠든 보물과 힘을 노리는 자들은 붉은 깃발을 들고 빛이 솟아난 곳을 향해 움직이기 시작했다.\n\n이동하는 동안 다른 무리들이 계속 합류했고, 작은 도적단은 어느새 군대에 가까운 규모로 불어났다.\n\n그들이 향하는 곳은 단 하나였다.\n\n내가 서 있는 신전이었다.",
                "멀리서 모래를 일으키며 다가오는 붉은 깃발들을 바라보는 순간, 내 직감이 위험을 경고했다. 저들에게 이 마석을 빼앗겨서는 안 된다고.\n\n저들이 노리는 것은 마석과 그 안에 담긴 힘이다. 그렇다면 그 힘을 사용하게 된 나 또한 어떻게 될지 모르는 일이다.\n\n내가 램프를 가져오지 않았다면 마석도 깨어나지 않았을지 모른다. 하지만 이미 벌어진 일을 되돌릴 방법은 없었다.\n\n손안에는 여전히 낯선 마력이 흐르고 있었다. 제대로 다룰 수 있을지는 알 수 없지만, 이 힘으로 마석과 나 자신을 지켜야 한다는 것만은 분명했다.\n\n램프를 단단히 움켜쥐고, 신전 아래로 다가오는 적들을 바라봤다.\n\n이제 이곳을 지키기 위한 싸움이 시작된다."
            };
            float[] durations = { 18f, 17f, 18f, 20f, 16f, 17f, 20f };
            float[] panelXs = { .78f, .78f, .78f, .22f, .78f, .76f, .78f };
            var cuts = new CutSceneSequence.Cut[7];
            for (int i = 0; i < cuts.Length; i++) cuts[i] = new CutSceneSequence.Cut
            {
                title = titles[i], body = bodies[i], image = AssetDatabase.LoadAssetAtPath<Sprite>(Art + $"Intro{i + 1}.png"),
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
                if (entry.FindPropertyRelative("storyId").stringValue != "Intro") continue;
                entry.FindPropertyRelative("sceneName").stringValue = "IntroCutscene";
                entry.FindPropertyRelative("available").boolValue = true;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
            var artwork = UnityEngine.Object.FindObjectsByType<SlantedStoryCardGraphic>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(x => x.name == "Artwork" && x.transform.parent && x.transform.parent.name == "IntroCard");
            if (artwork)
            {
                artwork.Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "Intro7.png");
                artwork.color = Color.white;
                EditorUtility.SetDirty(artwork);
            }
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
        static Button Button(string name, RectTransform parent, TMP_FontAsset font, string caption, Vector2 anchor, Vector2 size, Material material)
        {
            var rect = Rect(name, parent); Place(rect, anchor, size);
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.035f, .025f, .02f, .85f);
            var button = rect.gameObject.AddComponent<Button>();
            var label = Text("Label", rect, font, caption, 28, new Color(1f, .82f, .48f));
            label.fontStyle |= FontStyles.Bold; if (material) label.fontSharedMaterial = material;
            Stretch(label.rectTransform); label.alignment = TextAlignmentOptions.Center;
            return button;
        }
    }
}
#endif

using System;
using System.Linq;
using SandGuard.Cutscenes;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SandGuard.Cutscenes.Editor
{
    public static class PrologueSceneBuilder
    {
        const string Root = "Assets/Cutscenes";
        const string ScenePath = "Assets/1.Scene/PrologueCutscene.unity";
        const string SequencePath = Root + "/PrologueSequence.asset";
        const string Art = Root + "/Art/";

        [MenuItem("SandGuard/Cutscenes/Build Prologue Scene")]
        public static void Build()
        {
            for (int i = 1; i <= 7; i++) ImportSprite(Art + $"Prologue{i}.png");
            ImportSprite(Art + "CutScene_Pannel.png");
            var sequence = AssetDatabase.LoadAssetAtPath<CutSceneSequence>(SequencePath);
            if (!sequence)
            {
                sequence = ScriptableObject.CreateInstance<CutSceneSequence>();
                AssetDatabase.CreateAsset(sequence, SequencePath);
            }
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
            Place(panel.rectTransform, new Vector2(0.76f, 0.5f), new Vector2(760, 760));
            panel.preserveAspect = true;
            panel.raycastTarget = false;

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/9.Font/Cafe24Shiningstar-v2.0 SDF.asset");
            var cutsceneMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/9.Font/Cafe24Shiningstar-v2.0 Cutscene Outline.mat");
            if (!font) Debug.LogWarning("Prologue: Cafe24 Shiningstar TMP 폰트를 찾지 못했습니다. SandGuard/UI/Apply Cafe24 Shiningstar Font를 먼저 실행하세요.");
            var title = Text("Cut Title", panel.transform, font, sequence.cuts[0].title, 44, new Color(1f, 0.82f, 0.48f));
            Box(title.rectTransform, new Vector2(0, 253), new Vector2(540, 56));
            title.alignment = TextAlignmentOptions.Center;
            var body = Text("Story Text", panel.transform, font, sequence.cuts[0].body, 31, new Color(0.96f, 0.89f, 0.74f));
            Box(body.rectTransform, new Vector2(0, 15), new Vector2(540, 428));
            body.alignment = TextAlignmentOptions.TopLeft;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.enableAutoSizing = true;
            body.fontSizeMin = 24;
            body.fontSizeMax = 31;
            body.lineSpacing = 6;
            var progress = Text("Cut Number", panel.transform, font, "01 / 07", 26, new Color(0.47f, 0.75f, 0.97f));
            Box(progress.rectTransform, new Vector2(0, -254), new Vector2(140, 35));
            progress.alignment = TextAlignmentOptions.Center;

            foreach (var text in new TMP_Text[] { title, body, progress })
            {
                text.fontStyle |= FontStyles.Bold;
                if (cutsceneMaterial) text.fontSharedMaterial = cutsceneMaterial;
            }
            var next = Button("Next Button", visual, font, "다음  ▶", new Vector2(0.9f, 0.055f), new Vector2(170, 50), cutsceneMaterial);
            var skip = Button("Skip Button", visual, font, "건너뛰기", new Vector2(0.93f, 0.95f), new Vector2(150, 48), cutsceneMaterial);

            var managerObject = new GameObject("CutSceneManager", typeof(CutSceneManager));
            var manager = managerObject.GetComponent<CutSceneManager>();
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
            var existing = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToArray();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(existing).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Prologue cutscene scene and seven-cut sequence built: " + ScenePath);
        }

        [MenuItem("SandGuard/Cutscenes/Validate Prologue Scene")]
        public static void Validate()
        {
            var sequence = AssetDatabase.LoadAssetAtPath<CutSceneSequence>(SequencePath);
            if (!sequence || sequence.cuts == null || sequence.cuts.Length != 7)
                throw new InvalidOperationException("Prologue must contain exactly seven cuts.");
            for (int i = 0; i < sequence.cuts.Length; i++)
                if (!sequence.cuts[i].image || string.IsNullOrWhiteSpace(sequence.cuts[i].body))
                    throw new InvalidOperationException($"Cut {i + 1} is missing an image or text.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var manager = UnityEngine.Object.FindFirstObjectByType<CutSceneManager>();
            var body = UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None).FirstOrDefault(x => x.name == "Story Text");
            if (!scene.IsValid() || !manager || !body || !body.font)
                throw new InvalidOperationException("Cutscene scene, manager, body text or font is missing.");
            var area = body.rectTransform.rect;
            foreach (var cut in sequence.cuts)
            {
                body.text = cut.body;
                body.enableAutoSizing = false;
                body.fontSize = body.fontSizeMin;
                body.ForceMeshUpdate();
                if (body.textInfo.characterCount == 0)
                    throw new InvalidOperationException("One cut did not generate text.");
                var required = body.GetPreferredValues(cut.body, area.width, Mathf.Infinity);
                if (required.y > area.height)
                    throw new InvalidOperationException($"Text overflow in {cut.title}: {required.y:0} > {area.height:0}");
                Debug.Log($"Prologue layout: {cut.title}, {cut.body.Length} chars, {required.y:0}/{area.height:0} px high");
            }
            var first = EditorBuildSettings.scenes.FirstOrDefault();
            if (first == null || first.path != ScenePath || !first.enabled)
                throw new InvalidOperationException("Prologue is not the first enabled build scene.");
            Debug.Log("PROLOGUE_VALIDATED: 7 images, 7 texts, font, manager, boot scene.");
        }

        static CutSceneSequence.Cut[] MakeCuts()
        {
            var titles = new[] { "평범한 하루", "사막을 향한 마음", "다시 일터로", "갑작스러운 모래폭풍", "모래 속에서 발견한 램프", "폭풍 속의 이정표", "드러난 신전" };
            var bodies = new[]
            {
                "발굴 현장의 하루는 해가 뜨기 전에 시작해서, 해가 진 뒤에야 끝난다. 온종일 돌을 나른 탓에 팔이 묵직했다.\n\n나는 잠시 돌더미에 앉아 숨을 고르며 붉게 물든 하늘을 바라봤다. 아래에서는 작업자들이 마지막 돌을 옮기느라 분주하다.\n\n“조금만 더 하면 끝나겠네.” 혼잣말을 하고는, 해가 사막 너머로 내려가는 모습을 잠시 더 지켜봤다.",
                "시선을 돌리자 현장 너머로 모래 언덕이 끝없이 펼쳐져 있었다. 매일 보는 풍경인데도, 저 너머에 무엇이 있을지 궁금해질 때가 있다.\n\n이곳에서 발견한 것들도 한때는 모래 아래 잠들어 있었으니까. 아직 아무도 찾지 못한 무언가가 저 멀리에 있을지도 모른다.\n\n그때 아래에서 내 이름을 부르는 소리가 들렸다. 상상은 여기까지. 나는 옷에 묻은 먼지를 털고 일어났다.",
                "작업자들이 건네는 돌을 받아 계단 쪽으로 옮겼다. 몇 번을 오르내렸는지 모를 만큼 익숙한 길이었다.\n\n곧 일이 끝나면 쉬러 갈 수 있겠지. 그런 생각으로 다음 돌을 집어 들었을 때, 바람이 갑자기 모래를 실어 왔다.\n\n처음에는 평소와 다르지 않은 돌풍인 줄 알았다. 하지만 누군가 하늘을 가리켰고, 모두의 손길이 멈췄다.",
                "멀리 있던 모래바람이 믿을 수 없는 속도로 다가왔다. 사람들이 피하라고 외치는 순간, 바람이 현장을 덮쳤다.\n\n나는 돌을 내려놓고 몸을 낮췄다. 모래가 얼굴을 때리고, 조금 전까지 보이던 계단도 순식간에 사라졌다.\n\n다른 사람들의 목소리는 들리는데 어느 쪽인지 알 수가 없다. 일단 바람을 피해야 해. 손으로 앞을 더듬으며 가까운 돌벽 쪽으로 몸을 옮겼다.",
                "벽을 찾으려 뻗은 손끝에 돌이 아닌 물건이 걸렸다. 모래에 반쯤 묻혀 있던 작은 램프였다.\n\n왜 이런 곳에 있는지 의아했지만, 다시 날아가지 않도록 두 손으로 감싸 쥐었다. 오래된 표면을 훑던 손가락 아래로 희미한 푸른빛이 비쳤다.\n\n착각인가 싶어 눈을 가까이 가져갔다. 램프에 새겨진 문양이, 모래바람 속에서도 분명하게 빛나고 있었다.",
                "빛은 조금씩 밝아지더니 램프의 주둥이로 모여들었다. 다음 순간, 실처럼 가느다란 빛줄기가 폭풍 속으로 뻗어 나갔다.\n\n바람은 여전히 거센데 빛은 흩어지지 않는다. 어디를 가리키는 걸까?\n\n나는 램프를 든 채 그 방향을 바라봤다. 휘몰아치는 모래 너머로 거대한 윤곽이 언뜻 보였다가 다시 사라졌다.",
                "바람이 잦아들면서 시야가 조금씩 트였다. 희미한 윤곽은 계단이었고, 그 위로 거대한 피라미드 신전이 모습을 드러냈다.\n\n나는 믿기지 않아 주변을 돌아봤다. 조금 전까지 이곳에는 모래바람밖에 보이지 않았는데.\n\n신전 정상에서 푸른빛이 반짝이자 손안의 램프가 다시 빛났다. 우연히 발견한 이 램프와 저 신전은 무슨 관계일까?\n\n나는 한동안 그 자리에 서서, 빛이 가리킨 곳을 올려다봤다."
            };
            var durations = new[] { 14f, 16f, 15f, 18f, 17f, 15f, 20f };
            var result = new CutSceneSequence.Cut[7];
            for (int i = 0; i < 7; i++) result[i] = new CutSceneSequence.Cut
            {
                title = titles[i], body = bodies[i], image = AssetDatabase.LoadAssetAtPath<Sprite>(Art + $"Prologue{i + 1}.png"),
                duration = durations[i], panelX = i == 6 ? 0.24f : 0.76f, panelY = 0.5f
            };
            return result;
        }

        static void ImportSprite(string path)
        {
            AssetDatabase.ImportAsset(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing image: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        }
        static void Place(RectTransform rect, Vector2 anchor, Vector2 size)
        {
            rect.anchorMin = anchor; rect.anchorMax = anchor;
            rect.sizeDelta = size; rect.anchoredPosition = Vector2.zero;
        }
        static void Box(RectTransform rect, Vector2 position, Vector2 size)
        {
            Place(rect, new Vector2(.5f, .5f), size); rect.anchoredPosition = position;
        }
        static Image Image(string name, RectTransform parent, Sprite sprite)
        {
            var rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>(); image.sprite = sprite;
            return image;
        }
        static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, string content, float size, Color color)
        {
            var rect = Rect(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.text = content; text.fontSize = size; text.color = color;
            text.raycastTarget = false; return text;
        }
        static Button Button(string name, RectTransform parent, TMP_FontAsset font, string caption, Vector2 anchor, Vector2 size, Material material)
        {
            var rect = Rect(name, parent); Place(rect, anchor, size);
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.035f, .025f, .02f, .85f);
            var button = rect.gameObject.AddComponent<Button>();
            var label = Text("Label", rect, font, caption, 28, new Color(1f, .82f, .48f));
            label.fontStyle |= FontStyles.Bold;
            if (material) label.fontSharedMaterial = material;
            Stretch(label.rectTransform); label.alignment = TextAlignmentOptions.Center;
            return button;
        }
    }
}

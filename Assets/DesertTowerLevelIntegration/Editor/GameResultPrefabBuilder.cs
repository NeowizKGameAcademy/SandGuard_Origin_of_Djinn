#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DesertTower.LevelIntegration.Editor
{
    public static class GameResultPrefabBuilder
    {
        const string PrefabPath = "Assets/2.Model/Prefabs/HUD/GameResultCanvas.prefab";
        const string ArtPath = "Assets/4.Sprite/UI/GameScene/Result/ResultCards.png";
        const string LevelPath = "Assets/1.Scene/Level.unity";
        const string PreviewPath = "Assets/1.Scene/GameResultPreview.unity";

        [MenuItem("Tools/SandGuard/Adjust Game Result Layout")]
        public static void AdjustLayout()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var card = root.transform.Find("Overlay/Result Card");
                var subtitle = card.Find("Subtitle").GetComponent<TMP_Text>();
                subtitle.fontSize = 16;
                subtitle.color = new Color(.13f,.09f,.05f);
                subtitle.rectTransform.anchoredPosition = new Vector2(0,84);
                for (int i=0;i<3;i++)
                {
                    var names = new[] { "클리어 시간", "방어한 웨이브", "처치한 적" };
                    var stat = card.Find("Stat " + names[i]).GetComponent<RectTransform>();
                    stat.anchoredPosition = new Vector2(-45,25-i*91);
                    stat.sizeDelta = new Vector2(200,44);
                    var value = card.Find("Value " + names[i]).GetComponent<RectTransform>();
                    value.anchoredPosition = new Vector2(120,25-i*91);
                    value.sizeDelta = new Vector2(110,44);
                }
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
            Debug.Log("[GameResult] Result text layout adjusted.");
        }

        [MenuItem("Tools/SandGuard/Build Game Result Preview Scene")]
        public static void BuildPreviewScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!prefab) throw new System.InvalidOperationException("Build Game Result UI prefab first: " + PrefabPath);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var cameraObject = new GameObject("Preview Camera", typeof(Camera), typeof(AudioListener));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.055f,.04f,.035f);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "GameResultCanvas — Inspector: Show Failure";
            var screen = instance.GetComponent<GameResultScreen>();
            var serialized = new SerializedObject(screen);
            serialized.FindProperty("previewOnly").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var preview = instance.AddComponent<GameResultPreview>();
            var previewData = new SerializedObject(preview);
            Set(previewData, "screen", screen);
            previewData.ApplyModifiedPropertiesWithoutUndo();
            screen.ShowPreview(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, PreviewPath);
            EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.SaveAssets();
            Debug.Log("[GameResult] Preview scene saved: " + PreviewPath);
        }

        [MenuItem("Tools/SandGuard/Build Game Result UI")]
        public static void Build()
        {
            var art = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtPath);
            if (!art) throw new System.InvalidOperationException("Missing result artwork: " + ArtPath);
            var root = new GameObject("GameResultCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 500;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = .5f;
            var controller = root.AddComponent<GameResultScreen>();
            var overlay = Rect("Overlay", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var dim = overlay.gameObject.AddComponent<Image>(); dim.color = new Color(0, 0, 0, .86f);
            var card = Rect("Result Card", overlay.transform, new Vector2(.5f,.5f), new Vector2(.5f,.5f), Vector2.zero, new Vector2(660,880));
            var raw = card.gameObject.AddComponent<RawImage>(); raw.texture = art; raw.uvRect = new Rect(0,0,.5f,1); raw.raycastTarget = false;
            var gold = new Color(1f,.88f,.66f); var dark = new Color(.13f,.09f,.05f);
            var title = Label("Title",card.transform,"CLEAR",72,gold,new Vector2(0,163),new Vector2(490,96),false);
            var subtitle = Label("Subtitle",card.transform,"사막의 평화가 다시 찾아왔습니다!",16,dark,new Vector2(0,84),new Vector2(540,44),false);
            string[] names = { "클리어 시간", "방어한 웨이브", "처치한 적" };
            string[] placeholders = { "00:00", "0 / 0", "0" };
            TMP_Text[] statNames = new TMP_Text[3];
            TMP_Text[] values = new TMP_Text[3];
            for (int i=0;i<3;i++)
            {
                float y = 25 - i*91;
                statNames[i] = Label("Stat " + names[i],card.transform,names[i],26,dark,new Vector2(-45,y),new Vector2(200,44),true);
                values[i] = Label("Value " + names[i],card.transform,placeholders[i],29,dark,new Vector2(120,y),new Vector2(110,44),false);
            }
            var retry = HitButton("Retry",card.transform,new Vector2(-151,-303),new Vector2(255,74));
            var menu = HitButton("Main Menu",card.transform,new Vector2(151,-303),new Vector2(255,74));
            Label("Retry Text",retry.transform,"다시하기",28,gold,Vector2.zero,new Vector2(245,65),false);
            Label("Menu Text",menu.transform,"메인으로",28,gold,Vector2.zero,new Vector2(245,65),false);
            var so = new SerializedObject(controller);
            Set(so,"overlay",overlay.gameObject); Set(so,"cardArtwork",raw); Set(so,"title",title); Set(so,"subtitle",subtitle);
            Set(so,"timeLabel",values[0]); Set(so,"waveLabel",values[1]); Set(so,"killsLabel",values[2]);
            var statArray=so.FindProperty("statNames"); statArray.arraySize=statNames.Length;
            for (int i=0;i<statNames.Length;i++) statArray.GetArrayElementAtIndex(i).objectReferenceValue=statNames[i];
            Set(so,"retryButton",retry); Set(so,"mainMenuButton",menu); so.ApplyModifiedPropertiesWithoutUndo();
            overlay.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            Object.DestroyImmediate(root);

            var scene = EditorSceneManager.OpenScene(LevelPath,OpenSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == "GameResultCanvas") Object.DestroyImmediate(go);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
            var view = instance.GetComponent<GameResultScreen>();
            var director = Object.FindFirstObjectByType<WaveDirector>();
            if (director) { var binding = new SerializedObject(view); Set(binding,"director",director); binding.ApplyModifiedPropertiesWithoutUndo(); }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[GameResult] Prefab created and connected in Level scene.");
        }

        static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max,Vector2 pos,Vector2 size)
        {
            var go = new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false);
            var r = go.GetComponent<RectTransform>(); r.anchorMin=min; r.anchorMax=max; r.pivot=new Vector2(.5f,.5f);
            r.anchoredPosition=pos; r.sizeDelta=size; return r;
        }
        static TMP_Text Label(string name,Transform parent,string text,int size,Color color,Vector2 pos,Vector2 area,bool left)
        {
            var go = Rect(name,parent,new Vector2(.5f,.5f),new Vector2(.5f,.5f),pos,area).gameObject;
            var label=go.AddComponent<TextMeshProUGUI>(); label.text=text; label.fontSize=size; label.color=color;
            label.alignment=left?TextAlignmentOptions.MidlineLeft:TextAlignmentOptions.Center;
            label.textWrappingMode=TextWrappingModes.NoWrap; label.overflowMode=TextOverflowModes.Ellipsis; label.raycastTarget=false;
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/9.Font/Pretendard-Bold SDF.asset");
            if (font) label.font=font;
            return label;
        }
        static Button HitButton(string name,Transform parent,Vector2 pos,Vector2 size)
        {
            var go=Rect(name,parent,new Vector2(.5f,.5f),new Vector2(.5f,.5f),pos,size).gameObject;
            var hit=go.AddComponent<Image>(); hit.color=new Color(1,1,1,0); hit.raycastTarget=true;
            var button=go.AddComponent<Button>(); button.targetGraphic=hit; return button;
        }
        static void Set(SerializedObject so,string field,Object value) => so.FindProperty(field).objectReferenceValue=value;
    }
}
#endif

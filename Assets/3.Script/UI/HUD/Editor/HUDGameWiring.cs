#if UNITY_EDITOR
using SandGuard.Enemy;
using TMPro;
using UnityEngine.TextCore.LowLevel;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SandGuard.UI.HUD.Editor
{
    /// <summary>
    /// GameHUD를 실제 게임에 연결한다. 여러 번 실행해도 안전하다.
    /// 임시 스킬 아이콘 → GameHUD 프리팹(GameHUDPresenter, 슬롯 아이콘) → GameHUDCanvas 프리팹 → 게임 씬 3곳에 배치, Enemy_Chief에 보스 표식.
    /// Batch: <c>-executeMethod SandGuard.UI.HUD.Editor.HUDGameWiring.WireAll</c>
    /// </summary>
    public static class HUDGameWiring
    {
        const string PrefabRoot = "Assets/2.Model/Prefabs/HUD";
        public const string GameHUDPath = PrefabRoot + "/GameHUD.prefab";
        public const string CanvasPath = PrefabRoot + "/GameHUDCanvas.prefab";
        const string ChiefPath = "Assets/Enemy/Generated/Enemy_Chief.prefab";
        const string FontSourcePath = "Assets/9.Font/public/static/alternative/Pretendard-Bold.ttf";
        public const string FontAssetPath = "Assets/9.Font/Pretendard-Bold SDF.asset";
        public static readonly string[] Scenes =
        {
            "Assets/1.Scene/Level.unity",
            "Assets/PlayerAndEnemy/Generated/PlayerAndEnemyTest.unity",
            "Assets/Player/Generated/PlayerTest.unity",
        };

        [MenuItem("SandGuard/HUD/Connect HUD Into Game Scenes")]
        public static void WireAll()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(HUDSkillIconGenerator.Burst) == null) HUDSkillIconGenerator.Generate();
            ApplyKoreanFont();
            WireGameHUDPrefab();
            HUDLayoutSync.SyncFromHUDScene(); // HUD.unity에서 맞춘 배치를 GameHUD 프리팹에 옮긴다
            BuildCanvasPrefab();
            WireChiefBoss();
            foreach (var scene in Scenes) WireScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[HUD] GameHUD connected to Level, PlayerAndEnemyTest and PlayerTest.");
        }

        // TMP 기본 폰트(LiberationSans)에는 한글이 없어 "체력", "대시"가 □로 나온다. Pretendard로 동적 SDF 폰트를 만들어 HUD 조각 프리팹 글자에 건다.
        static void ApplyKoreanFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (font == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>(FontSourcePath);
                font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                font.name = "Pretendard-Bold SDF";
                AssetDatabase.CreateAsset(font, FontAssetPath);
                font.atlasTextures[0].name = font.name + " Atlas";
                font.material.name = font.name + " Material";
                AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
                AssetDatabase.AddObjectToAsset(font.material, font);
                AssetDatabase.SaveAssets();
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path == GameHUDPath || path == CanvasPath) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed = false;
                    foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                        if (text.font != font) { text.font = font; changed = true; }
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        static void WireGameHUDPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(GameHUDPath);
            try
            {
                var controller = root.GetComponent<GameHUDController>();
                var presenter = root.GetComponent<GameHUDPresenter>();
                if (presenter == null) presenter = root.AddComponent<GameHUDPresenter>();
                Set(presenter, "hud", controller);
                // GameHUD는 조각 프리팹 글자에 LiberationSans 재정의를 들고 있어 여기서도 덮는다.
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) if (text.font != font) text.font = font;
                SetIcon(controller.CombatSkills.Q, HUDSkillIconGenerator.Burst);
                SetIcon(controller.CombatSkills.E, HUDSkillIconGenerator.Vortex);
                SetIcon(controller.CombatSkills.R, HUDSkillIconGenerator.Storm);
                SetIcon(controller.CombatSkills.F, HUDSkillIconGenerator.Tower);
                SetIcon(controller.MovementSkills.Dash, HUDSkillIconGenerator.Dash);
                SetIcon(controller.MovementSkills.DoubleJump, HUDSkillIconGenerator.Jump);
                PrefabUtility.SaveAsPrefabAsset(root, GameHUDPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // HUDSceneBuilder와 같은 캔버스(1920×1080 기준, 가로·세로 0.5 매칭). 시설 메뉴(40)·조준점(50) 아래에 그린다.
        static void BuildCanvasPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPath) != null) return; // 다시 만들면 씬 인스턴스의 내부 참조가 끊긴다
            var canvasGo = new GameObject("GameHUDCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            try
            {
                var canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10;
                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = .5f;
                var safe = new GameObject("SafeArea", typeof(RectTransform));
                safe.transform.SetParent(canvasGo.transform, false);
                Stretch(safe.GetComponent<RectTransform>());
                var hud = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GameHUDPath), safe.transform);
                hud.name = "GameHUD";
                Stretch(hud.GetComponent<RectTransform>());
                PrefabUtility.SaveAsPrefabAsset(canvasGo, CanvasPath);
            }
            finally { Object.DestroyImmediate(canvasGo); }
        }

        static void WireChiefBoss()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ChiefPath) == null) return;
            var root = PrefabUtility.LoadPrefabContents(ChiefPath);
            try
            {
                if (root.GetComponent<EnemyBossInfo>() != null) return;
                root.AddComponent<EnemyBossInfo>().displayName = "우두머리 자히르"; // EnemyCatalog의 이름
                PrefabUtility.SaveAsPrefabAsset(root, ChiefPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void WireScene(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) { Debug.LogWarning("[HUD] Scene missing: " + path); return; }
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "GameHUDCanvas") return; // 이미 있다. 캔버스 프리팹이 바뀌면 인스턴스가 따라온다
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPath), scene);
            instance.name = "GameHUDCanvas";
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void SetIcon(Component slot, string spritePath)
        {
            var image = new SerializedObject(slot).FindProperty("icon").objectReferenceValue as Image;
            if (image == null) return;
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            image.enabled = image.sprite != null;
            image.preserveAspect = true;
            EditorUtility.SetDirty(image);
        }

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        }
    }
}
#endif

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SandGuard.UI.HUD.Editor
{
    /// <summary>
    /// HUD.unity(디자인 기준 씬, Docs/ui/ingame-hud.png)에서 손으로 맞춘 조각 프리팹 인스턴스의 배치·이미지 값을
    /// GameHUD.prefab의 같은 조각 인스턴스에 복사한다. 게임 씬은 GameHUDCanvas → GameHUD 프리팹을 쓰므로 그대로 따라온다.
    /// 복사 대상: RectTransform(앵커·피벗·위치·크기), Image(스프라이트·색·타입·채움 방식·비율 유지). 런타임 값(채움량·글자·활성)과 스킬·보스 아이콘은 건드리지 않는다.
    /// Batch: <c>-executeMethod SandGuard.UI.HUD.Editor.HUDLayoutSync.SyncFromHUDScene</c>
    /// </summary>
    public static class HUDLayoutSync
    {
        const string ScenePath = "Assets/1.Scene/HUD.unity";
        const string PrefabRoot = "Assets/2.Model/Prefabs/HUD";

        struct RectValues { public Vector2 anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta; }
        struct ImageValues { public Sprite sprite; public Color color; public Image.Type type; public Image.FillMethod fillMethod; public int fillOrigin; public bool preserveAspect; }

        [MenuItem("SandGuard/HUD/Sync GameHUD Layout From HUD Scene")]
        public static void SyncFromHUDScene()
        {
            var rects = new Dictionary<Object, RectValues>();
            var images = new Dictionary<Object, ImageValues>();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
                {
                    if (!IsPieceInstance(rect)) continue;
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(rect);
                    if (source != null) rects[source] = new RectValues { anchorMin = rect.anchorMin, anchorMax = rect.anchorMax, pivot = rect.pivot, anchoredPosition = rect.anchoredPosition, sizeDelta = rect.sizeDelta };
                }
                foreach (var image in root.GetComponentsInChildren<Image>(true))
                {
                    if (!IsPieceInstance(image) || IsRuntimeIcon(image)) continue;
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(image);
                    if (source != null) images[source] = new ImageValues { sprite = image.sprite, color = image.color, type = image.type, fillMethod = image.fillMethod, fillOrigin = image.fillOrigin, preserveAspect = image.preserveAspect };
                }
            }
            if (rects.Count == 0) { Debug.LogWarning("[HUD] No HUD piece instances found in " + ScenePath); return; }

            int changed = 0;
            var contents = PrefabUtility.LoadPrefabContents(HUDGameWiring.GameHUDPath);
            try
            {
                foreach (var rect in contents.GetComponentsInChildren<RectTransform>(true))
                {
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(rect);
                    if (source == null || !rects.TryGetValue(source, out var v)) continue;
                    if (rect.anchorMin == v.anchorMin && rect.anchorMax == v.anchorMax && rect.pivot == v.pivot && rect.anchoredPosition == v.anchoredPosition && rect.sizeDelta == v.sizeDelta) continue;
                    rect.anchorMin = v.anchorMin; rect.anchorMax = v.anchorMax; rect.pivot = v.pivot; rect.anchoredPosition = v.anchoredPosition; rect.sizeDelta = v.sizeDelta;
                    changed++;
                }
                foreach (var image in contents.GetComponentsInChildren<Image>(true))
                {
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(image);
                    if (source == null || !images.TryGetValue(source, out var v)) continue;
                    if (image.sprite == v.sprite && image.color == v.color && image.type == v.type && image.fillMethod == v.fillMethod && image.fillOrigin == v.fillOrigin && image.preserveAspect == v.preserveAspect) continue;
                    image.sprite = v.sprite; image.color = v.color; image.type = v.type; image.fillMethod = v.fillMethod; image.fillOrigin = v.fillOrigin; image.preserveAspect = v.preserveAspect;
                    changed++;
                }
                if (changed > 0) PrefabUtility.SaveAsPrefabAsset(contents, HUDGameWiring.GameHUDPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            AssetDatabase.SaveAssets();
            Debug.Log($"[HUD] GameHUD layout synced from HUD.unity: {changed} component(s) updated.");
        }

        public static void SyncFromHUDSceneBatch() { SyncFromHUDScene(); EditorApplication.Exit(0); }

        // 스킬·보스 아이콘은 HUDGameWiring/런타임이 넣는다. HUD.unity의 빈 자리(회색 사각형)를 복사하면 안 된다.
        static bool IsRuntimeIcon(Image image) => image.gameObject.name == "Icon" || image.gameObject.name == "BossIcon";

        // HUD 조각 프리팹(PlayerStatusHUD 등)의 인스턴스에 속한 컴포넌트만 본다. 씬에서 직접 만든 GameHUD/TopHUD 등은 건너뛴다.
        static bool IsPieceInstance(Component component)
        {
            var root = PrefabUtility.GetNearestPrefabInstanceRoot(component.gameObject);
            if (root == null) return false;
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
            return !string.IsNullOrEmpty(path) && path.StartsWith(PrefabRoot) && path != HUDGameWiring.GameHUDPath && path != HUDGameWiring.CanvasPath;
        }
    }
}
#endif

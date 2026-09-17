#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SandGuard.UI.HUD.Editor
{
    public static class MinimapHUDBuilder
    {
        const string MinimapPrefabPath = "Assets/2.Model/Prefabs/HUD/MinimapHUD.prefab";
        const string GameHUDPrefabPath = "Assets/2.Model/Prefabs/HUD/GameHUD.prefab";

        [MenuItem("SandGuard/HUD/Resize Fixed Minimap")]
        public static void Build()
        {
            ResizeMinimapPrefab();
            ResizeGameHUDInstance();
            AssetDatabase.SaveAssets();
            Debug.Log("[HUD] Minimap enlarged and fixed full-map camera configured.");
        }

        static void ResizeMinimapPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(MinimapPrefabPath);
            try
            {
                var rootRect = root.GetComponent<RectTransform>();
                rootRect.sizeDelta = new Vector2(320f, 320f);

                var viewport = root.transform.Find("MapViewport") as RectTransform;
                if (viewport != null)
                {
                    viewport.anchoredPosition = new Vector2(.5f, 1.5f);
                    viewport.sizeDelta = new Vector2(244f, 232f);
                }

                var frame = root.transform.Find("Frame") as RectTransform;
                if (frame != null) frame.sizeDelta = new Vector2(320f, 320f);

                PrefabUtility.SaveAsPrefabAsset(root, MinimapPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void ResizeGameHUDInstance()
        {
            var root = PrefabUtility.LoadPrefabContents(GameHUDPrefabPath);
            try
            {
                var minimap = FindChild(root.transform, "MinimapHUD") as RectTransform;
                if (minimap != null)
                {
                    minimap.anchorMin = minimap.anchorMax = minimap.pivot = Vector2.one;
                    minimap.anchoredPosition = new Vector2(-24f, -126f);
                    minimap.sizeDelta = new Vector2(320f, 320f);
                }

                var controller = root.GetComponent<MinimapController>();
                if (controller != null)
                {
                    controller.viewRadius = 75f;
                    controller.cameraHeight = 120f;
                    controller.textureSize = 512;
                }

                PrefabUtility.SaveAsPrefabAsset(root, GameHUDPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static Transform FindChild(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }
    }
}
#endif

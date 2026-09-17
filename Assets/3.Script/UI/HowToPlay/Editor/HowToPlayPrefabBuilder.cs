#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class HowToPlayPrefabBuilder
{
    private const string ScenePath = "Assets/1.Scene/MainScene.unity";
    private const string PrefabPath = "Assets/2.Model/Prefabs/UI/HowToPlayPopup.prefab";
    private const string PagesPath = "Assets/4.Sprite/UI/MainScene/HowToPlay/Pages/";
    private const float ArtWidth = 1672f;
    private const float ArtHeight = 939f;

    private static readonly string[] PageFiles =
    {
        "Page01_Basics.png",
        "Page02_Preparation.png",
        "Page03_Movement.png",
        "Page04_Attack.png",
        "Page05_Towers.png"
    };

    [MenuItem("SandGuard/UI/Build How To Play Popup")]
    public static void Build()
    {
        Directory.CreateDirectory("Assets/2.Model/Prefabs/UI");
        ImportPages();

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var canvas = GameObject.Find("Canvas").GetComponent<Canvas>();
        var menuButton = canvas.transform.Find("Menu/HowToPlay").GetComponent<Button>();

        // Replace either the original hand-made panel or an earlier generated popup.
        var oldPanel = canvas.transform.Find("HowToPlay");
        if (oldPanel) UnityEngine.Object.DestroyImmediate(oldPanel.gameObject);
        var oldPopup = canvas.transform.Find("HowToPlayPopup");
        if (oldPopup) UnityEngine.Object.DestroyImmediate(oldPopup.gameObject);

        var root = Rect("HowToPlayPopup", null);
        Stretch(root);
        var blocker = root.gameObject.AddComponent<Image>();
        blocker.color = Color.black;
        blocker.raycastTarget = true;

        // Keep the page art and its transparent click targets aligned on every aspect ratio.
        var viewport = Rect("PageViewport", root);
        Stretch(viewport);
        var aspect = viewport.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        aspect.aspectRatio = ArtWidth / ArtHeight;

        var pages = new GameObject[PageFiles.Length];
        for (int i = 0; i < PageFiles.Length; i++)
        {
            var page = Rect($"Page_{i + 1}", viewport);
            Stretch(page);
            var image = page.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PagesPath + PageFiles[i]);
            if (!image.sprite) throw new InvalidOperationException("Missing HowToPlay page: " + PageFiles[i]);
            image.raycastTarget = false;
            pages[i] = page.gameObject;
            page.gameObject.SetActive(i == 0);
        }

        var controls = Rect("HitAreas", viewport);
        Stretch(controls);
        var previous = HitArea("Previous", controls, 70, 402, 202, 548);
        var next = HitArea("Next", controls, 1471, 402, 1608, 548);
        var close = HitArea("Close", controls, 1350, 105, 1440, 199);

        var popup = root.gameObject.AddComponent<HowToPlayPopup>();
        var serialized = new SerializedObject(popup);
        var pageArray = serialized.FindProperty("pages");
        pageArray.arraySize = pages.Length;
        for (int i = 0; i < pages.Length; i++)
            pageArray.GetArrayElementAtIndex(i).objectReferenceValue = pages[i];
        serialized.FindProperty("previousButton").objectReferenceValue = previous;
        serialized.FindProperty("nextButton").objectReferenceValue = next;
        serialized.FindProperty("closeButton").objectReferenceValue = close;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root.gameObject);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.transform.SetParent(canvas.transform, false);
        instance.transform.SetAsLastSibling();
        instance.SetActive(false);

        var link = menuButton.GetComponent<HowToPlayMenuLink>();
        if (!link) link = menuButton.gameObject.AddComponent<HowToPlayMenuLink>();
        var linkSerialized = new SerializedObject(link);
        linkSerialized.FindProperty("popup").objectReferenceValue = instance.GetComponent<HowToPlayPopup>();
        linkSerialized.ApplyModifiedPropertiesWithoutUndo();

        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"HowToPlay: saved {pages.Length} page prefab and MainScene button link.");
    }

    private static void ImportPages()
    {
        foreach (var file in PageFiles)
        {
            var importer = AssetImporter.GetAtPath(PagesPath + file) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing page image: " + file);
            if (importer.textureType == TextureImporterType.Sprite &&
                importer.spriteImportMode == SpriteImportMode.Single &&
                importer.filterMode == FilterMode.Bilinear &&
                importer.textureCompression == TextureImporterCompression.Uncompressed &&
                importer.maxTextureSize >= 2048) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }
    }

    private static Button HitArea(string name, Transform parent, float left, float top, float right, float bottom)
    {
        var rect = Rect(name, parent);
        rect.anchorMin = new Vector2(left / ArtWidth, 1f - bottom / ArtHeight);
        rect.anchorMax = new Vector2(right / ArtWidth, 1f - top / ArtHeight);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        return button;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        var rect = go.GetComponent<RectTransform>();
        if (parent) rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
#endif

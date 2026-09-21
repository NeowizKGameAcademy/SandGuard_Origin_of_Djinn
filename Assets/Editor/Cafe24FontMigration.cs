#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

public static class Cafe24FontMigration
{
    public const string SourcePath = "Assets/9.Font/Cafe24Shiningstar-v2.0.ttf";
    public const string TargetPath = "Assets/9.Font/Cafe24Shiningstar-v2.0 SDF.asset";
    public const string PretendardPath = "Assets/9.Font/Pretendard-Bold SDF.asset";
    public const string CutsceneMaterialPath = "Assets/9.Font/Cafe24Shiningstar-v2.0 Cutscene Outline.mat";
    const string PrologueScene = "Assets/1.Scene/PrologueCutscene.unity";
    static readonly string[] Prefabs =
    {
        "Assets/2.Model/Prefabs/HUD/BossStatusHUD.prefab",
        "Assets/2.Model/Prefabs/HUD/CombatSkillHUD.prefab",
        "Assets/2.Model/Prefabs/HUD/GameHUDCanvas.prefab",
        "Assets/2.Model/Prefabs/HUD/GameHUD.prefab",
        "Assets/2.Model/Prefabs/HUD/CoreStatusHUD.prefab",
        "Assets/2.Model/Prefabs/HUD/MovementSkillHUD.prefab",
        "Assets/2.Model/Prefabs/HUD/GameResultCanvas.prefab",
        "Assets/2.Model/Prefabs/HUD/PlayerStatusHUD.prefab",
        "Assets/2.Model/Prefabs/HUD/WaveHUD.prefab",
    };

    [MenuItem("SandGuard/UI/Apply Requested Font Split")]
    public static void ApplyRequestedSplit()
    {
        var cafe24 = GetOrCreateFontAsset();
        var pretendard = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PretendardPath);
        if (!pretendard) throw new InvalidOperationException("Pretendard TMP font asset is missing.");
        var outline = GetOrCreateCutsceneMaterial(cafe24);
        int hudCount = 0;
        foreach (var path in Prefabs)
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(path)) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int count = Assign(root.GetComponentsInChildren<TMP_Text>(true), pretendard);
                if (count > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                hudCount += count;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        var scene = EditorSceneManager.OpenScene(PrologueScene, OpenSceneMode.Single);
        int cutsceneCount = 0;
        foreach (var text in scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<TMP_Text>(true)))
        {
            text.font = cafe24;
            text.fontSharedMaterial = outline;
            text.fontStyle |= FontStyles.Bold;
            switch (text.name)
            {
                case "Cut Title": text.fontSize = 44; break;
                case "Story Text":
                    text.enableAutoSizing = true; text.fontSizeMin = 24; text.fontSizeMax = 31; text.fontSize = 31;
                    text.lineSpacing = 6; break;
                case "Cut Number": text.fontSize = 26; break;
                case "Label": text.fontSize = 28; break;
            }
            EditorUtility.SetDirty(text); cutsceneCount++;
        }
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"FONT_SPLIT_APPLIED: {hudCount} HUD texts restored to Pretendard; {cutsceneCount} cutscene texts styled with Cafe24.");
    }

    [MenuItem("SandGuard/UI/Validate Requested Font Split")]
    public static void ValidateRequestedSplit()
    {
        var cafe24 = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TargetPath);
        var pretendard = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PretendardPath);
        var outline = AssetDatabase.LoadAssetAtPath<Material>(CutsceneMaterialPath);
        if (!cafe24 || !pretendard || !outline) throw new InvalidOperationException("Required font assets are missing.");
        int hudTotal = 0;
        foreach (var path in Prefabs)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) { hudTotal++; if (text.font != pretendard) throw new InvalidOperationException(path + " still has a non-Pretendard text."); } }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        var scene = EditorSceneManager.OpenScene(PrologueScene, OpenSceneMode.Single);
        var cutsceneTexts = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<TMP_Text>(true)).ToArray();
        if (cutsceneTexts.Length != 5 || cutsceneTexts.Any(x => x.font != cafe24 || x.fontSharedMaterial != outline || (x.fontStyle & FontStyles.Bold) == 0))
            throw new InvalidOperationException("Cutscene font, outline, or bold styling is incomplete.");
        Debug.Log($"FONT_SPLIT_VALIDATED: {hudTotal} HUD texts use Pretendard; {cutsceneTexts.Length} cutscene texts use bold outlined Cafe24.");
    }

    [MenuItem("SandGuard/UI/Apply Cafe24 Shiningstar Font")]
    public static void Apply()
    {
        var font = GetOrCreateFontAsset();
        int changedTexts = 0;
        foreach (var path in Prefabs)
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(path)) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int count = Assign(root.GetComponentsInChildren<TMP_Text>(true), font);
                if (count > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                changedTexts += count;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PrologueScene))
        {
            Scene scene = SceneManager.GetSceneByPath(PrologueScene);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(PrologueScene, OpenSceneMode.Additive);
            var texts = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<TMP_Text>(true));
            int count = Assign(texts, font);
            if (count > 0) EditorSceneManager.SaveScene(scene);
            changedTexts += count;
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"CAFE24_FONT_APPLIED: {changedTexts} TMP text components updated. Legacy Text conversion was unnecessary.");
    }

    [MenuItem("SandGuard/UI/Validate Cafe24 Shiningstar Font")]
    public static void Validate()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TargetPath);
        if (!font) throw new InvalidOperationException("Cafe24 TMP font asset is missing.");
        int wrong = 0, total = 0;
        foreach (var path in Prefabs)
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(path)) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                { total++; if (text.font != font) wrong++; }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        if (wrong > 0) throw new InvalidOperationException($"{wrong}/{total} HUD texts do not use Cafe24.");
        Debug.Log($"CAFE24_FONT_VALIDATED: {total} HUD TMP texts use Cafe24 Shiningstar.");
    }

    static TMP_FontAsset GetOrCreateFontAsset()
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TargetPath);
        if (existing) return existing;
        var source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
        if (!source) throw new InvalidOperationException("Font source is missing: " + SourcePath);
        var font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
        font.name = "Cafe24Shiningstar-v2.0 SDF";
        AssetDatabase.CreateAsset(font, TargetPath);
        font.atlasTextures[0].name = font.name + " Atlas";
        font.material.name = font.name + " Material";
        AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
        AssetDatabase.AddObjectToAsset(font.material, font);
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();
        return font;
    }

    static Material GetOrCreateCutsceneMaterial(TMP_FontAsset font)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(CutsceneMaterialPath);
        if (!material)
        {
            material = new Material(font.material) { name = "Cafe24Shiningstar-v2.0 Cutscene Outline" };
            AssetDatabase.CreateAsset(material, CutsceneMaterialPath);
        }
        material.EnableKeyword("OUTLINE_ON");
        material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.055f, 0.025f, 0.012f, 0.98f));
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.14f);
        material.SetFloat(ShaderUtilities.ID_FaceDilate, 0.05f);
        EditorUtility.SetDirty(material);
        return material;
    }

    static int Assign(System.Collections.Generic.IEnumerable<TMP_Text> texts, TMP_FontAsset font)
    {
        int count = 0;
        foreach (var text in texts)
        {
            bool changed = false;
            if (text.font != font) { text.font = font; changed = true; }
            if (text.fontSharedMaterial != font.material) { text.fontSharedMaterial = font.material; changed = true; }
            if (!changed) continue;
            EditorUtility.SetDirty(text); count++;
        }
        return count;
    }
}
#endif

#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace SandGuard.Skills.Editor
{
    public static class SkillPromptSpriteSetup
    {
        const string Path = "Assets/SandGuardSkillTree/Assets/SkillTree/Resources/SandGuardLampSkin/InteractionPromptRounded.png";

        [MenuItem("SandGuard/Skills/Configure Rounded Altar Prompt")]
        public static void Configure()
        {
            AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(Path) is not TextureImporter importer)
                throw new InvalidOperationException("Rounded altar prompt texture is missing.");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.spriteBorder = new Vector4(145f, 110f, 145f, 110f);
            importer.SaveAndReimport();
            AssetDatabase.SaveAssets();
            Debug.Log("SKILL_ALTAR_PROMPT_CONFIGURED: rounded sprite, preserved live text and colors.");
        }

        [MenuItem("SandGuard/Skills/Validate Rounded Altar Prompt")]
        public static void Validate()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Path);
            if (!sprite) throw new InvalidOperationException("Rounded altar prompt is not imported as a Sprite.");
            if (sprite.border.x < 1f || sprite.border.y < 1f)
                throw new InvalidOperationException("Rounded altar prompt has no 9-slice border.");
            if (sprite.texture.width < sprite.texture.height * 4)
                throw new InvalidOperationException("Rounded altar prompt aspect ratio is invalid.");
            Debug.Log($"SKILL_ALTAR_PROMPT_VALIDATED: {sprite.texture.width}x{sprite.texture.height}, border={sprite.border}.");
        }
    }
}
#endif

using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DesertTowerVFX
{
    public sealed class DesertMenuVFXSetup : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/DesertTowerVFX/Textures/")) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Default;
            t.alphaSource = TextureImporterAlphaSource.FromInput;
            t.alphaIsTransparency = true;
            t.mipmapEnabled = false;
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.npotScale = TextureImporterNPOTScale.None;
            t.maxTextureSize = 1024;
            t.textureCompression = TextureImporterCompression.Uncompressed;
        }

        [MenuItem("Tools/Desert Tower/Add VFX To Selected Background")]
        static void Create()
        {
            var target = Selection.activeTransform as RectTransform;
            if (target == null || target.GetComponentInParent<Canvas>() == null)
            {
                EditorUtility.DisplayDialog("Desert Tower VFX", "Canvas 안의 배경 Image 또는 RawImage 오브젝트를 선택해주세요.", "확인");
                return;
            }
            const string path = "Assets/DesertTowerVFX/Textures/";
            var sand = AssetDatabase.LoadAssetAtPath<Texture2D>(path + "SandPuff.png");
            var rock = AssetDatabase.LoadAssetAtPath<Texture2D>(path + "SandstoneShard.png");
            var light = AssetDatabase.LoadAssetAtPath<Texture2D>(path + "CrystalMote.png");
            if (sand == null || rock == null || light == null)
            {
                EditorUtility.DisplayDialog("Desert Tower VFX", "DesertTowerVFX 폴더를 Assets 바로 아래에 넣어주세요. Textures 폴더의 PNG 3개가 필요합니다.", "확인");
                return;
            }
            var go = new GameObject("DesertMenuVFX", typeof(RectTransform), typeof(RectMask2D), typeof(DesertMenuVFX));
            Undo.RegisterCreatedObjectUndo(go, "Create Desert Menu VFX");
            go.transform.SetParent(target, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            // Place particles before any button children already under the background.
            rt.SetAsFirstSibling();
            var fx = go.GetComponent<DesertMenuVFX>();
            fx.sand = sand; fx.rock = rock; fx.lightMote = light;
            Selection.activeGameObject = go;
            EditorUtility.SetDirty(go);
        }
    }
}

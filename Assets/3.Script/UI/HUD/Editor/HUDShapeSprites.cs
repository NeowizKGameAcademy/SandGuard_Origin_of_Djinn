#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace SandGuard.UI.HUD.Editor
{
    /// <summary>
    /// HUD가 쓰는 단색 도형 스프라이트(64×64, 흰색, 가장자리 4× 슈퍼샘플링). 없으면 만들고 있으면 그대로 돌려준다.
    /// Image는 스프라이트가 없으면 Filled 타입이어도 사각형 전체를 그리므로, 쿨타임 오버레이·링·미니맵 마커는 이 스프라이트를 쓴다.
    /// </summary>
    public static class HUDShapeSprites
    {
        public const string Folder = "Assets/4.Sprite/UI/GameScene/HUD";
        public const string Circle = Folder + "/Overlay_Circle.png", Diamond = Folder + "/Overlay_Diamond.png", Arrow = Folder + "/Marker_Arrow.png";

        public static Sprite CircleSprite() => Ensure(Circle, (x, y) => x * x + y * y <= 1f);
        public static Sprite DiamondSprite() => Ensure(Diamond, (x, y) => Mathf.Abs(x) + Mathf.Abs(y) <= 1f);
        /// <summary>위를 향한 화살표(꼭짓점 (0,1), 밑변 y=-0.75, 가운데 홈).</summary>
        public static Sprite ArrowSprite() => Ensure(Arrow, (x, y) =>
        {
            bool triangle = y >= -.75f && Mathf.Abs(x) <= .8f * (1f - y) / 1.75f;
            bool notch = y < -.3f && Mathf.Abs(x) <= .28f * (-.3f - y) / .45f;
            return triangle && !notch;
        });

        public const string MinimapFrame = Folder + "/MinimapContainer.png", MinimapFrameCutout = Folder + "/MinimapContainer_Cutout.png";

        /// <summary>
        /// 미니맵 틀의 안쪽(가죽 무늬)을 투명하게 뚫은 사본. 원본은 안쪽이 불투명해서 위에 그리면 지도를 가린다.
        /// 중심에서 밝은(금색) 픽셀을 경계로 채워 나가며 안쪽을 찾는다. 테두리·모서리 장식·N/E/S/W 글자는 밝아서 남는다.
        /// </summary>
        public static Sprite MinimapFrameCutoutSprite()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MinimapFrameCutout);
            if (sprite != null) return sprite;
            var bytes = System.IO.File.ReadAllBytes(MinimapFrame);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(bytes);
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32();
            var inside = new bool[w * h];
            var stack = new System.Collections.Generic.Stack<int>();
            int start = (h / 2) * w + w / 2; stack.Push(start); inside[start] = true;
            bool Bright(Color32 c) => c.r * .3f + c.g * .6f + c.b * .1f > 110f;
            while (stack.Count > 0)
            {
                int i = stack.Pop(); int x = i % w, y = i / w;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy; if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int j = ny * w + nx; if (inside[j] || Bright(px[j]) || px[j].a < 40) continue;
                    inside[j] = true; stack.Push(j);
                }
            }
            for (int i = 0; i < px.Length; i++) if (inside[i]) px[i] = new Color32(px[i].r, px[i].g, px[i].b, 0);
            tex.SetPixels32(px); tex.Apply();
            System.IO.File.WriteAllBytes(MinimapFrameCutout, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(MinimapFrameCutout);
            var importer = (TextureImporter)AssetImporter.GetAtPath(MinimapFrameCutout);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(MinimapFrameCutout);
        }

        // inside(x, y): -1..1 정규화 좌표(위가 +y)에서 채울지.
        public static Sprite Ensure(string path, Func<float, float, bool> inside)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;
            const int size = 64, ss = 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < ss; sy++)
                        for (int sx = 0; sx < ss; sx++)
                        {
                            float nx = ((x + (sx + .5f) / ss) / size) * 2f - 1f, ny = ((y + (sy + .5f) / ss) / size) * 2f - 1f;
                            if (inside(nx, ny)) hits++;
                        }
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * hits / (ss * ss)));
                }
            tex.SetPixels32(px); tex.Apply();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
#endif

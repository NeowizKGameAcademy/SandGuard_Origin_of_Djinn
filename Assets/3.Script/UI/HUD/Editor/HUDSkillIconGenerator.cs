#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SandGuard.UI.HUD.Editor
{
    /// <summary>
    /// 스킬 슬롯용 임시 아이콘. 선분·원으로 된 금빛 글리프를 거리장으로 그려 PNG로 저장한다(부드러운 가장자리 + 은은한 후광).
    /// 진짜 아이콘이 오면 같은 파일을 덮어쓰거나 슬롯 Icon 이미지의 스프라이트를 바꾸면 된다.
    /// </summary>
    public static class HUDSkillIconGenerator
    {
        public const string Folder = "Assets/4.Sprite/UI/GameScene/HUD/SkillIcons";
        public const string Burst = Folder + "/Icon_SandBurst.png", Vortex = Folder + "/Icon_SandVortex.png", Storm = Folder + "/Icon_SandStorm.png",
            Tower = Folder + "/Icon_Tower.png", Dash = Folder + "/Icon_Dash.png", Jump = Folder + "/Icon_DoubleJump.png";
        const int Size = 256;
        static readonly Color Core = new Color(1f, .93f, .72f), Glow = new Color(1f, .7f, .22f);

        struct Stroke { public Vector2 a, b; public float width; }

        [MenuItem("SandGuard/HUD/Generate Placeholder Skill Icons")]
        public static void Generate()
        {
            Directory.CreateDirectory(Folder);
            Write(Burst, BurstShape());
            Write(Vortex, VortexShape());
            Write(Storm, StormShape());
            Write(Tower, TowerShape());
            Write(Dash, DashShape());
            Write(Jump, JumpShape());
            AssetDatabase.Refresh();
            foreach (var path in new[] { Burst, Vortex, Storm, Tower, Dash, Jump })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
        }

        // 좌표는 -1..1, 위가 +y.
        static List<Stroke> BurstShape()
        {
            var s = new List<Stroke>();
            Circle(s, Vector2.zero, .22f, .09f, 20);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4f + Mathf.PI / 8f;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float length = i % 2 == 0 ? .82f : .62f;
                s.Add(new Stroke { a = dir * .4f, b = dir * length, width = i % 2 == 0 ? .085f : .06f });
            }
            return s;
        }

        static List<Stroke> VortexShape()
        {
            var s = new List<Stroke>();
            Vector2 previous = Vector2.zero;
            const int steps = 90;
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps, angle = t * Mathf.PI * 4.2f, radius = .06f + t * .74f;
                var point = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                s.Add(new Stroke { a = previous, b = point, width = Mathf.Lerp(.04f, .09f, t) });
                previous = point;
            }
            return s;
        }

        static List<Stroke> StormShape()
        {
            var s = new List<Stroke>();
            float[] rows = { .45f, 0f, -.45f };
            for (int r = 0; r < rows.Length; r++)
            {
                Vector2 previous = default;
                float start = r == 1 ? -.85f : -.7f, end = r == 1 ? .7f : .85f;
                for (int i = 0; i <= 40; i++)
                {
                    float x = Mathf.Lerp(start, end, i / 40f);
                    var point = new Vector2(x, rows[r] + Mathf.Sin(x * 7f + r) * .1f);
                    if (i > 0) s.Add(new Stroke { a = previous, b = point, width = .075f });
                    previous = point;
                }
            }
            return s;
        }

        static List<Stroke> TowerShape()
        {
            var s = new List<Stroke>();
            const float w = .08f;
            Line(s, w, new Vector2(-.45f, -.75f), new Vector2(.45f, -.75f));
            Line(s, w, new Vector2(-.3f, -.75f), new Vector2(-.3f, .3f), new Vector2(.3f, .3f), new Vector2(.3f, -.75f));
            Line(s, w, new Vector2(-.45f, .3f), new Vector2(-.45f, .7f), new Vector2(-.25f, .7f), new Vector2(-.25f, .52f),
                new Vector2(-.08f, .52f), new Vector2(-.08f, .7f), new Vector2(.08f, .7f), new Vector2(.08f, .52f),
                new Vector2(.25f, .52f), new Vector2(.25f, .7f), new Vector2(.45f, .7f), new Vector2(.45f, .3f), new Vector2(-.45f, .3f));
            Line(s, w, new Vector2(0f, -.75f), new Vector2(0f, -.35f));
            return s;
        }

        static List<Stroke> DashShape()
        {
            var s = new List<Stroke>();
            Line(s, .1f, new Vector2(-.35f, .5f), new Vector2(.05f, 0f), new Vector2(-.35f, -.5f));
            Line(s, .1f, new Vector2(.15f, .5f), new Vector2(.55f, 0f), new Vector2(.15f, -.5f));
            Line(s, .05f, new Vector2(-.85f, .22f), new Vector2(-.5f, .22f));
            Line(s, .05f, new Vector2(-.95f, 0f), new Vector2(-.5f, 0f));
            Line(s, .05f, new Vector2(-.85f, -.22f), new Vector2(-.5f, -.22f));
            return s;
        }

        static List<Stroke> JumpShape()
        {
            var s = new List<Stroke>();
            Line(s, .1f, new Vector2(-.5f, .15f), new Vector2(0f, .62f), new Vector2(.5f, .15f));
            Line(s, .1f, new Vector2(-.5f, -.3f), new Vector2(0f, .17f), new Vector2(.5f, -.3f));
            Line(s, .06f, new Vector2(-.3f, -.72f), new Vector2(.3f, -.72f));
            return s;
        }

        static void Line(List<Stroke> s, float width, params Vector2[] points)
        {
            for (int i = 1; i < points.Length; i++) s.Add(new Stroke { a = points[i - 1], b = points[i], width = width });
        }

        static void Circle(List<Stroke> s, Vector2 center, float radius, float width, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                s.Add(new Stroke { a = center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius, b = center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius, width = width });
            }
        }

        static void Write(string path, List<Stroke> strokes)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var pixels = new Color[Size * Size];
            float pixel = 2f / Size;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                var p = new Vector2((x + .5f) / Size * 2f - 1f, (y + .5f) / Size * 2f - 1f);
                float distance = float.MaxValue;
                foreach (var stroke in strokes) distance = Mathf.Min(distance, SegmentDistance(p, stroke.a, stroke.b) - stroke.width * .5f);
                float body = Mathf.Clamp01(.5f - distance / (pixel * 1.5f));
                float halo = Mathf.Exp(-Mathf.Max(0f, distance) / .07f) * .45f;
                float alpha = Mathf.Max(body, halo);
                Color color = Color.Lerp(Glow, Core, body);
                color.a = alpha;
                pixels[y * Size + x] = color;
            }
            texture.SetPixels(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude < 1e-8f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
#endif

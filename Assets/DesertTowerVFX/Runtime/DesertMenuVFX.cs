using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DesertTowerVFX
{
    // UI particles deliberately use the Canvas renderer so Overlay menus work too.
    [RequireComponent(typeof(RectTransform))]
    public sealed class DesertMenuVFX : MonoBehaviour
    {
        public Texture2D sand;
        public Texture2D rock;
        public Texture2D lightMote;
        [Range(0f, 1f)] public float intensity = 0.65f;
        [Range(0f, 2f)] public float speed = 1f;
        public Vector2 crystalPosition = new Vector2(0.695f, 0.75f);
        [Range(0, 24)] public int sandCount = 12;
        [Range(0, 16)] public int rockCount = 7;
        [Range(0, 20)] public int lightCount = 9;

        sealed class Mote
        {
            public RawImage image;
            public int kind;
            public float age, life, size, angle, spin, alpha;
            public Vector2 start, travel;
        }
        readonly List<Mote> motes = new List<Mote>();
        RectTransform area;
        System.Random random;
        float R(float a, float b) { return Mathf.Lerp(a, b, (float)random.NextDouble()); }

        void Start()
        {
            area = (RectTransform)transform;
            random = new System.Random(48271);
            Add(sand, 0, Mathf.Clamp(sandCount, 0, 24));
            Add(rock, 1, Mathf.Clamp(rockCount, 0, 16));
            Add(lightMote, 2, Mathf.Clamp(lightCount, 0, 20));
            Draw(0f);
        }

        void Add(Texture2D texture, int kind, int count)
        {
            if (texture == null) return;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("VFX_" + kind + "_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                go.transform.SetParent(transform, false);
                var img = go.GetComponent<RawImage>();
                img.texture = texture;
                img.raycastTarget = false;
                img.rectTransform.anchorMin = img.rectTransform.anchorMax = Vector2.zero;
                var m = new Mote { image = img, kind = kind };
                Reset(m);
                m.age = R(0f, m.life);
                motes.Add(m);
            }
        }

        void Reset(Mote m)
        {
            m.age = 0;
            if (m.kind == 0)
            {
                m.life = R(9f, 16f); m.size = R(0.19f, 0.34f);
                m.start = new Vector2(R(-0.25f, 0.85f), R(0.04f, 0.29f));
                m.travel = new Vector2(R(0.22f, 0.45f), R(0.015f, 0.06f));
                m.angle = R(-8f, 8f); m.spin = R(-1f, 1f); m.alpha = 0.22f;
            }
            else if (m.kind == 1)
            {
                m.life = R(7f, 12f); m.size = R(0.008f, 0.022f);
                bool left = random.NextDouble() < 0.65;
                m.start = new Vector2(left ? R(0.02f, 0.29f) : R(0.91f, 0.98f), R(0.25f, 0.8f));
                m.travel = new Vector2(R(0.025f, 0.075f), R(0.07f, 0.18f));
                m.angle = R(0f, 360f); m.spin = R(-14f, 14f); m.alpha = 0.7f;
            }
            else
            {
                m.life = R(2.5f, 5f); m.size = R(0.008f, 0.019f);
                m.start = new Vector2(R(-0.034f, 0.034f), R(-0.025f, 0.025f));
                m.travel = new Vector2(R(-0.012f, 0.012f), R(0.025f, 0.07f));
                m.angle = R(-20f, 20f); m.spin = R(-8f, 8f); m.alpha = 0.9f;
            }
        }

        void Update() { Draw(Time.unscaledDeltaTime * speed); }

        void Draw(float dt)
        {
            if (area == null) return;
            Vector2 dimensions = area.rect.size;
            foreach (Mote m in motes)
            {
                m.age += dt;
                if (m.age >= m.life) Reset(m);
                float t = m.age / m.life;
                Vector2 p = m.start + m.travel * t;
                if (m.kind == 2) p += crystalPosition;
                p.y += Mathf.Sin(t * Mathf.PI * 2f + m.angle) * (m.kind == 0 ? 0.004f : 0.007f);
                var rt = m.image.rectTransform;
                rt.anchoredPosition = Vector2.Scale(p, dimensions);
                float size = dimensions.x * m.size * (m.kind == 0 ? Mathf.Lerp(0.85f, 1.2f, t) : 1f);
                rt.sizeDelta = new Vector2(size, size);
                rt.localRotation = Quaternion.Euler(0f, 0f, m.angle + m.spin * m.age);
                float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.2f)) *
                             Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - t) / 0.25f));
                m.image.color = new Color(1f, 1f, 1f, fade * m.alpha * intensity);
            }
        }
    }
}
